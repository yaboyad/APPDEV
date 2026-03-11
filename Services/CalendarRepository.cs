using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Label_CRM_demo.Models;

namespace Label_CRM_demo.Services;

public sealed class CalendarRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly object gate = new();

    public CalendarRepository(string? storagePath = null)
    {
        StoragePath = storagePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LabelCrmDemo",
            "calendar",
            "events.json");
    }

    public string StoragePath { get; }

    public void EnsureSeeded()
    {
        lock (gate)
        {
            if (File.Exists(StoragePath))
            {
                return;
            }

            SaveStore(new CalendarStoreDocument
            {
                Events = CreateSeedEvents()
            });
        }
    }

    public IReadOnlyList<CalendarEventRecord> GetEvents()
    {
        lock (gate)
        {
            return LoadStore().Events
                .OrderBy(item => item.Start)
                .ToList();
        }
    }

    public IReadOnlyList<CalendarEventRecord> GetUpcomingEvents(int count)
    {
        var now = DateTimeOffset.Now.AddMinutes(-1);

        lock (gate)
        {
            return LoadStore().Events
                .Where(item => item.End >= now)
                .OrderBy(item => item.Start)
                .Take(count)
                .ToList();
        }
    }

    public CalendarEventRecord Save(CalendarEventRecord item)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (gate)
        {
            var store = LoadStore();
            var normalized = Normalize(item);
            var existing = store.Events.FindIndex(candidate => string.Equals(candidate.Id, normalized.Id, StringComparison.OrdinalIgnoreCase));

            if (existing >= 0)
            {
                store.Events[existing] = normalized;
            }
            else
            {
                store.Events.Add(normalized);
            }

            SaveStore(store);
            return normalized;
        }
    }

    public void SaveMany(IEnumerable<CalendarEventRecord> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        lock (gate)
        {
            var store = LoadStore();

            foreach (var item in items)
            {
                var normalized = Normalize(item);
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

            SaveStore(store);
        }
    }

    public void Delete(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        lock (gate)
        {
            var store = LoadStore();
            store.Events.RemoveAll(candidate => string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase));
            SaveStore(store);
        }
    }

    public void MergeRemoteEvents(string providerName, IEnumerable<CalendarEventRecord> incoming)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentNullException.ThrowIfNull(incoming);

        lock (gate)
        {
            var store = LoadStore();

            foreach (var candidate in incoming)
            {
                var normalized = Normalize(candidate);
                var existing = FindExistingMatch(store.Events, providerName, normalized);

                if (existing is null)
                {
                    normalized.Source = ComputeSource(normalized);
                    store.Events.Add(normalized);
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
            }

            SaveStore(store);
        }
    }

    private CalendarStoreDocument LoadStore()
    {
        EnsureSeeded();

        try
        {
            var json = File.ReadAllText(StoragePath);
            var store = JsonSerializer.Deserialize<CalendarStoreDocument>(json, SerializerOptions);

            if (store is null)
            {
                throw new InvalidDataException("Calendar store is empty.");
            }

            return store;
        }
        catch
        {
            BackupCorruptStore();
            File.Delete(StoragePath);
            EnsureSeeded();
            var json = File.ReadAllText(StoragePath);
            return JsonSerializer.Deserialize<CalendarStoreDocument>(json, SerializerOptions)
                ?? throw new InvalidDataException("Unable to rebuild the calendar store.");
        }
    }

    private void BackupCorruptStore()
    {
        if (!File.Exists(StoragePath))
        {
            return;
        }

        var backupPath = StoragePath + ".broken-" + DateTime.Now.ToString("yyyyMMddHHmmss");
        File.Copy(StoragePath, backupPath, overwrite: true);
    }

    private void SaveStore(CalendarStoreDocument store)
    {
        var directory = Path.GetDirectoryName(StoragePath)
            ?? throw new InvalidOperationException("Calendar store path is invalid.");

        Directory.CreateDirectory(directory);

        store.Events = store.Events
            .Select(Normalize)
            .OrderBy(item => item.Start)
            .ToList();

        var json = JsonSerializer.Serialize(store, SerializerOptions);
        File.WriteAllText(StoragePath, json);
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
