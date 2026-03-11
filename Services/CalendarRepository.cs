using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Label_CRM_demo.Models;

namespace Label_CRM_demo.Services;

public sealed class CalendarRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim gate = new(1, 1);
    private CalendarStoreDocument? cachedStore;
    private bool isInitialized;

    public CalendarRepository(string? storagePath = null)
    {
        StoragePath = storagePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LabelCrmDemo",
            "calendar",
            "events.json");
    }

    public string StoragePath { get; }

    public event EventHandler? EventsChanged;

    public void EnsureSeeded()
        => EnsureSeededAsync().GetAwaiter().GetResult();

    public async Task EnsureSeededAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureSeededCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public IReadOnlyList<CalendarEventRecord> GetEvents()
        => GetEventsAsync().GetAwaiter().GetResult();

    public async Task<IReadOnlyList<CalendarEventRecord>> GetEventsAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return (await LoadStoreCoreAsync(cancellationToken).ConfigureAwait(false)).Events
                .OrderBy(item => item.Start)
                .ToList();
        }
        finally
        {
            gate.Release();
        }
    }

    public IReadOnlyList<CalendarEventRecord> GetUpcomingEvents(int count)
        => GetUpcomingEventsAsync(count).GetAwaiter().GetResult();

    public async Task<IReadOnlyList<CalendarEventRecord>> GetUpcomingEventsAsync(
        int count,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.Now.AddMinutes(-1);

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return (await LoadStoreCoreAsync(cancellationToken).ConfigureAwait(false)).Events
                .Where(item => item.End >= now)
                .OrderBy(item => item.Start)
                .Take(count)
                .ToList();
        }
        finally
        {
            gate.Release();
        }
    }

    public CalendarEventRecord Save(CalendarEventRecord item)
        => SaveAsync(item).GetAwaiter().GetResult();

    public async Task<CalendarEventRecord> SaveAsync(
        CalendarEventRecord item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        CalendarEventRecord normalized;

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var store = await LoadStoreCoreAsync(cancellationToken).ConfigureAwait(false);
            normalized = Normalize(item);
            var existing = store.Events.FindIndex(candidate => string.Equals(candidate.Id, normalized.Id, StringComparison.OrdinalIgnoreCase));

            if (existing >= 0)
            {
                store.Events[existing] = normalized;
            }
            else
            {
                store.Events.Add(normalized);
            }

            await SaveStoreCoreAsync(store, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }

        OnEventsChanged();
        return normalized;
    }

    public void SaveMany(IEnumerable<CalendarEventRecord> items)
        => SaveManyAsync(items).GetAwaiter().GetResult();

    public async Task SaveManyAsync(
        IEnumerable<CalendarEventRecord> items,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        var normalizedItems = items
            .Select(Normalize)
            .ToList();

        if (normalizedItems.Count == 0)
        {
            return;
        }

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var store = await LoadStoreCoreAsync(cancellationToken).ConfigureAwait(false);

            foreach (var normalized in normalizedItems)
            {
                var existing = store.Events.FindIndex(candidate => string.Equals(candidate.Id, normalized.Id, StringComparison.OrdinalIgnoreCase));

                if (existing >= 0)
                {
                    store.Events[existing] = normalized;
                }
                else
                {
                    store.Events.Add(normalized);
                }
            }

            await SaveStoreCoreAsync(store, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }

        OnEventsChanged();
    }

    public void Delete(string id)
        => DeleteAsync(id).GetAwaiter().GetResult();

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var changed = false;

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var store = await LoadStoreCoreAsync(cancellationToken).ConfigureAwait(false);
            changed = store.Events.RemoveAll(candidate => string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase)) > 0;

            if (changed)
            {
                await SaveStoreCoreAsync(store, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            gate.Release();
        }

        if (changed)
        {
            OnEventsChanged();
        }
    }

    public void DeleteMany(IEnumerable<string> ids)
        => DeleteManyAsync(ids).GetAwaiter().GetResult();

    public async Task DeleteManyAsync(
        IEnumerable<string> ids,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var idSet = ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (idSet.Count == 0)
        {
            return;
        }

        var changed = false;

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var store = await LoadStoreCoreAsync(cancellationToken).ConfigureAwait(false);
            changed = store.Events.RemoveAll(candidate => idSet.Contains(candidate.Id)) > 0;

            if (changed)
            {
                await SaveStoreCoreAsync(store, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            gate.Release();
        }

        if (changed)
        {
            OnEventsChanged();
        }
    }

    public void MergeRemoteEvents(string providerName, IEnumerable<CalendarEventRecord> incoming)
        => MergeRemoteEventsAsync(providerName, incoming).GetAwaiter().GetResult();

    public async Task MergeRemoteEventsAsync(
        string providerName,
        IEnumerable<CalendarEventRecord> incoming,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentNullException.ThrowIfNull(incoming);

        var changed = false;
        var incomingEvents = incoming.ToList();

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var store = await LoadStoreCoreAsync(cancellationToken).ConfigureAwait(false);

            foreach (var candidate in incomingEvents)
            {
                var normalized = Normalize(candidate);
                var existing = FindExistingMatch(store.Events, providerName, normalized);

                if (existing is null)
                {
                    normalized.Source = ComputeSource(normalized);
                    store.Events.Add(normalized);
                    changed = true;
                    continue;
                }

                existing.ExternalUid = Choose(normalized.ExternalUid, existing.ExternalUid);
                existing.Title = Choose(normalized.Title, existing.Title);
                existing.Category = Choose(normalized.Category, existing.Category);
                existing.Description = Choose(normalized.Description, existing.Description);
                existing.Location = Choose(normalized.Location, existing.Location);
                existing.Start = normalized.Start;
                existing.End = normalized.End;
                existing.GoogleEventId = Choose(normalized.GoogleEventId, existing.GoogleEventId);
                existing.AppleEventHref = Choose(normalized.AppleEventHref, existing.AppleEventHref);
                existing.Source = ComputeSource(existing);
                existing.LastModifiedUtc = DateTimeOffset.UtcNow;
                changed = true;
            }

            if (changed)
            {
                await SaveStoreCoreAsync(store, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            gate.Release();
        }

        if (changed)
        {
            OnEventsChanged();
        }
    }

    private async Task EnsureSeededCoreAsync(CancellationToken cancellationToken)
    {
        if (isInitialized)
        {
            return;
        }

        var directory = Path.GetDirectoryName(StoragePath)
            ?? throw new InvalidOperationException("Calendar store path is invalid.");

        Directory.CreateDirectory(directory);

        if (!File.Exists(StoragePath))
        {
            await SaveStoreCoreAsync(new CalendarStoreDocument
            {
                Events = CreateSeedEvents()
            }, cancellationToken).ConfigureAwait(false);
            return;
        }

        isInitialized = true;
    }

    private async Task<CalendarStoreDocument> LoadStoreCoreAsync(CancellationToken cancellationToken)
    {
        await EnsureSeededCoreAsync(cancellationToken).ConfigureAwait(false);

        if (cachedStore is not null)
        {
            return cachedStore;
        }

        try
        {
            cachedStore = await RepositoryFileStore.ReadJsonAsync<CalendarStoreDocument>(StoragePath, SerializerOptions, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("Calendar store is empty.");

            isInitialized = true;
            return cachedStore;
        }
        catch
        {
            await BackupCorruptStoreCoreAsync(cancellationToken).ConfigureAwait(false);

            if (File.Exists(StoragePath))
            {
                File.Delete(StoragePath);
            }

            cachedStore = null;
            isInitialized = false;
            await EnsureSeededCoreAsync(cancellationToken).ConfigureAwait(false);
            return cachedStore ?? throw new InvalidDataException("Unable to rebuild the calendar store.");
        }
    }

    private async Task SaveStoreCoreAsync(CalendarStoreDocument store, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(StoragePath)
            ?? throw new InvalidOperationException("Calendar store path is invalid.");

        Directory.CreateDirectory(directory);

        store.Events = store.Events
            .Select(Normalize)
            .OrderBy(item => item.Start)
            .ToList();

        cachedStore = store;
        await RepositoryFileStore.WriteJsonAtomicAsync(StoragePath, store, SerializerOptions, cancellationToken).ConfigureAwait(false);
        isInitialized = true;
    }

    private async Task BackupCorruptStoreCoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(StoragePath))
        {
            return;
        }

        var backupPath = StoragePath + ".broken-" + DateTime.Now.ToString("yyyyMMddHHmmss");
        await RepositoryFileStore.CopyAsync(StoragePath, backupPath, cancellationToken).ConfigureAwait(false);
    }

    private static CalendarEventRecord Normalize(CalendarEventRecord item)
    {
        var normalizedStart = item.Start == default ? DateTimeOffset.Now : item.Start;
        var normalizedEnd = item.End <= normalizedStart ? normalizedStart.AddHours(1) : item.End;

        return new CalendarEventRecord
        {
            Id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("N") : item.Id,
            ExternalUid = item.ExternalUid?.Trim() ?? string.Empty,
            Title = item.Title?.Trim() ?? string.Empty,
            Category = string.IsNullOrWhiteSpace(item.Category) ? "Task" : item.Category.Trim(),
            Description = item.Description?.Trim() ?? string.Empty,
            Location = item.Location?.Trim() ?? string.Empty,
            Start = normalizedStart,
            End = normalizedEnd,
            Source = string.IsNullOrWhiteSpace(item.Source) ? "Local" : item.Source.Trim(),
            GoogleEventId = item.GoogleEventId?.Trim() ?? string.Empty,
            AppleEventHref = item.AppleEventHref?.Trim() ?? string.Empty,
            LastModifiedUtc = DateTimeOffset.UtcNow
        };
    }

    private static CalendarEventRecord? FindExistingMatch(List<CalendarEventRecord> events, string providerName, CalendarEventRecord incoming)
    {
        if (string.Equals(providerName, "Google", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(incoming.GoogleEventId))
        {
            return events.FirstOrDefault(candidate =>
                string.Equals(candidate.GoogleEventId, incoming.GoogleEventId, StringComparison.OrdinalIgnoreCase));
        }

        if (string.Equals(providerName, "Apple", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(incoming.AppleEventHref))
            {
                var hrefMatch = events.FirstOrDefault(candidate =>
                    string.Equals(candidate.AppleEventHref, incoming.AppleEventHref, StringComparison.OrdinalIgnoreCase));

                if (hrefMatch is not null)
                {
                    return hrefMatch;
                }
            }

            if (!string.IsNullOrWhiteSpace(incoming.ExternalUid))
            {
                var uidMatch = events.FirstOrDefault(candidate =>
                    string.Equals(candidate.ExternalUid, incoming.ExternalUid, StringComparison.OrdinalIgnoreCase));

                if (uidMatch is not null)
                {
                    return uidMatch;
                }
            }
        }

        return events.FirstOrDefault(candidate =>
            string.Equals(candidate.Title, incoming.Title, StringComparison.OrdinalIgnoreCase) &&
            candidate.Start == incoming.Start &&
            candidate.End == incoming.End);
    }

    private static string Choose(string primary, string fallback)
        => string.IsNullOrWhiteSpace(primary) ? fallback : primary.Trim();

    private static string ComputeSource(CalendarEventRecord item)
    {
        var hasGoogle = !string.IsNullOrWhiteSpace(item.GoogleEventId);
        var hasApple = !string.IsNullOrWhiteSpace(item.AppleEventHref);

        return (hasGoogle, hasApple) switch
        {
            (true, true) => "Google + Apple",
            (true, false) => "Google",
            (false, true) => "Apple",
            _ => "Local"
        };
    }

    private void OnEventsChanged()
    {
        EventsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static List<CalendarEventRecord> CreateSeedEvents()
    {
        return new List<CalendarEventRecord>
        {
            CreateSeedEvent("Drop Prep Check-in", "Task", 1, 10, 0, 11, 0, "Review the release checklist and artwork deadlines.", "Studio A"),
            CreateSeedEvent("Artist Outreach", "Call", 3, 14, 0, 14, 45, "Follow up on campaign timing and deliverables.", "Phone"),
            CreateSeedEvent("Payment Due", "Billing", 7, 9, 0, 9, 30, "Recurring creator plan charge.", "Accounts")
        };
    }

    private static CalendarEventRecord CreateSeedEvent(
        string title,
        string category,
        int dayOffset,
        int startHour,
        int startMinute,
        int endHour,
        int endMinute,
        string description,
        string location)
    {
        var startLocal = DateTime.Today.AddDays(dayOffset).AddHours(startHour).AddMinutes(startMinute);
        var endLocal = DateTime.Today.AddDays(dayOffset).AddHours(endHour).AddMinutes(endMinute);
        var offset = TimeZoneInfo.Local.GetUtcOffset(startLocal);

        return new CalendarEventRecord
        {
            Title = title,
            Category = category,
            Description = description,
            Location = location,
            Start = new DateTimeOffset(startLocal, offset),
            End = new DateTimeOffset(endLocal, offset),
            Source = "Local"
        };
    }

    private sealed class CalendarStoreDocument
    {
        public List<CalendarEventRecord> Events { get; set; } = new();
    }
}
