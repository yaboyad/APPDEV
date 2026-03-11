using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Label_CRM_demo.Models;

namespace Label_CRM_demo.Services;

public sealed class SupportSubmissionRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim gate = new(1, 1);
    private SupportSubmissionStore? cachedStore;
    private bool isInitialized;

    public SupportSubmissionRepository(string? storagePath = null)
    {
        StoragePath = storagePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LabelCrmDemo",
            "support",
            "submissions.json");
    }

    public string StoragePath { get; }

    public IReadOnlyList<SupportSubmissionRecord> LoadAll()
        => LoadAllAsync().GetAwaiter().GetResult();

    public async Task<IReadOnlyList<SupportSubmissionRecord>> LoadAllAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return (await LoadStoreCoreAsync(cancellationToken).ConfigureAwait(false)).Submissions
                .OrderByDescending(submission => submission.CreatedAt)
                .ToList();
        }
        finally
        {
            gate.Release();
        }
    }

    public IReadOnlyList<SupportSubmissionRecord> LoadForUser(AuthenticatedUser user)
        => LoadForUserAsync(user).GetAwaiter().GetResult();

    public async Task<IReadOnlyList<SupportSubmissionRecord>> LoadForUserAsync(
        AuthenticatedUser user,
        CancellationToken cancellationToken = default)
    {
        var username = NormalizeIdentifier(user.Username);
        var email = NormalizeEmail(user.Email);

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return (await LoadStoreCoreAsync(cancellationToken).ConfigureAwait(false)).Submissions
                .Where(submission =>
                    string.Equals(submission.SubmittedByUsername, username, StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrWhiteSpace(email)
                        && string.Equals(submission.SubmittedByEmail, email, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(submission => submission.CreatedAt)
                .ToList();
        }
        finally
        {
            gate.Release();
        }
    }

    public SupportSubmissionRecord Submit(AuthenticatedUser user, string message)
        => SubmitAsync(user, message).GetAwaiter().GetResult();

    public async Task<SupportSubmissionRecord> SubmitAsync(
        AuthenticatedUser user,
        string message,
        CancellationToken cancellationToken = default)
    {
        var submission = Normalize(CreateSubmission(user, message));

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var store = await LoadStoreCoreAsync(cancellationToken).ConfigureAwait(false);
            store.Submissions.Add(submission);
            await SaveStoreCoreAsync(store, cancellationToken).ConfigureAwait(false);
            return submission;
        }
        finally
        {
            gate.Release();
        }
    }

    public SupportSubmissionRecord CreateSubmission(AuthenticatedUser user, string message)
        => new()
        {
            SubmittedByUsername = NormalizeIdentifier(user.Username),
            SubmittedByDisplayName = user.DisplayName.Trim(),
            SubmittedByEmail = NormalizeEmail(user.Email),
            SubmittedByTier = user.TierLabel,
            Body = message.Trim(),
            CreatedAt = DateTime.Now,
            Channel = DetectChannel(message),
            IsUrgent = IsUrgent(message),
            Status = "New"
        };

    public void EnsureInitialized()
        => EnsureInitializedAsync().GetAwaiter().GetResult();

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureInitializedCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<SupportSubmissionStore> LoadStoreCoreAsync(CancellationToken cancellationToken)
    {
        await EnsureInitializedCoreAsync(cancellationToken).ConfigureAwait(false);

        if (cachedStore is not null)
        {
            return cachedStore;
        }

        try
        {
            cachedStore = await RepositoryFileStore.ReadJsonAsync<SupportSubmissionStore>(StoragePath, SerializerOptions, cancellationToken).ConfigureAwait(false)
                ?? new SupportSubmissionStore();

            isInitialized = true;
            return cachedStore;
        }
        catch
        {
            await BackupCorruptStoreCoreAsync(cancellationToken).ConfigureAwait(false);
            var resetStore = new SupportSubmissionStore();
            await SaveStoreCoreAsync(resetStore, cancellationToken).ConfigureAwait(false);
            return cachedStore!;
        }
    }

    private async Task EnsureInitializedCoreAsync(CancellationToken cancellationToken)
    {
        if (isInitialized)
        {
            return;
        }

        var directory = Path.GetDirectoryName(StoragePath)
            ?? throw new InvalidOperationException("Support submission store path is invalid.");

        Directory.CreateDirectory(directory);

        if (!File.Exists(StoragePath))
        {
            await SaveStoreCoreAsync(new SupportSubmissionStore(), cancellationToken).ConfigureAwait(false);
            return;
        }

        isInitialized = true;
    }

    private async Task SaveStoreCoreAsync(SupportSubmissionStore store, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(StoragePath)
            ?? throw new InvalidOperationException("Support submission store path is invalid.");

        Directory.CreateDirectory(directory);

        store.Submissions = store.Submissions
            .Select(Normalize)
            .OrderByDescending(submission => submission.CreatedAt)
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

    private static SupportSubmissionRecord Normalize(SupportSubmissionRecord submission)
        => new()
        {
            Id = string.IsNullOrWhiteSpace(submission.Id) ? Guid.NewGuid().ToString("N") : submission.Id,
            SubmittedByUsername = NormalizeIdentifier(submission.SubmittedByUsername),
            SubmittedByDisplayName = submission.SubmittedByDisplayName.Trim(),
            SubmittedByEmail = NormalizeEmail(submission.SubmittedByEmail),
            SubmittedByTier = AccountTiers.Normalize(submission.SubmittedByTier),
            Body = submission.Body.Trim(),
            CreatedAt = submission.CreatedAt == default ? DateTime.Now : submission.CreatedAt,
            Channel = string.IsNullOrWhiteSpace(submission.Channel) ? "General" : submission.Channel.Trim(),
            IsUrgent = submission.IsUrgent,
            Status = string.IsNullOrWhiteSpace(submission.Status) ? "New" : submission.Status.Trim()
        };

    private static string DetectChannel(string message)
    {
        var normalized = message.ToLowerInvariant();

        if (ContainsAny(normalized, "password", "login", "sign in", "signin", "access", "locked out"))
        {
            return "Access";
        }

        if (ContainsAny(normalized, "bill", "billing", "payment", "invoice", "charge", "refund"))
        {
            return "Billing";
        }

        if (ContainsAny(normalized, "calendar", "schedule", "meeting", "call", "reminder"))
        {
            return "Calendar";
        }

        if (ContainsAny(normalized, "contract", "agreement", "split", "signature"))
        {
            return "Contracts";
        }

        if (ContainsAny(normalized, "release", "launch", "post", "social", "spotify", "youtube", "facebook"))
        {
            return "Launch";
        }

        return "General";
    }

    private static bool IsUrgent(string message)
    {
        var normalized = message.ToLowerInvariant();
        return ContainsAny(normalized, "urgent", "asap", "immediately", "cant", "can't", "down", "broken", "failed");
    }

    private static bool ContainsAny(string value, params string[] fragments)
        => fragments.Any(value.Contains);

    private static string NormalizeIdentifier(string value) => value.Trim();

    private static string NormalizeEmail(string value) => value.Trim().ToLowerInvariant();

    private sealed class SupportSubmissionStore
    {
        public List<SupportSubmissionRecord> Submissions { get; set; } = new();
    }
}
