using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Label_CRM_demo.Models;

namespace Label_CRM_demo.Services;

public sealed class WorkspaceRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim gate = new(1, 1);
    private WorkspaceStoreDocument? cachedStore;
    private bool isInitialized;

    public WorkspaceRepository(string? storagePath = null)
    {
        StoragePath = storagePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LabelCrmDemo",
            "workspace",
            "workspace-data.json");
    }

    public string StoragePath { get; }

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

    public WorkspaceSnapshot LoadForUser(string username)
        => LoadForUserAsync(username).GetAwaiter().GetResult();

    public async Task<WorkspaceSnapshot> LoadForUserAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        var normalizedUsername = NormalizeUserKey(username);

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var store = await LoadStoreCoreAsync(cancellationToken).ConfigureAwait(false);

            var contacts = store.Contacts
                .Where(contact => string.Equals(contact.OwnerUsername, normalizedUsername, StringComparison.OrdinalIgnoreCase))
                .OrderBy(contact => contact.FullName)
                .ThenBy(contact => contact.Company)
                .ToList();

            var contracts = store.Contracts
                .Where(contract => string.Equals(contract.OwnerUsername, normalizedUsername, StringComparison.OrdinalIgnoreCase))
                .OrderBy(contract => contract.ReminderDate ?? contract.StartDate)
                .ThenBy(contract => contract.Title)
                .ToList();

            return new WorkspaceSnapshot(contacts, contracts);
        }
        finally
        {
            gate.Release();
        }
    }

    public void SaveForUser(
        string username,
        IEnumerable<ContactRecord> contacts,
        IEnumerable<ContractRecord> contracts)
        => SaveForUserAsync(username, contacts, contracts).GetAwaiter().GetResult();

    public async Task SaveForUserAsync(
        string username,
        IEnumerable<ContactRecord> contacts,
        IEnumerable<ContractRecord> contracts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contacts);
        ArgumentNullException.ThrowIfNull(contracts);

        var normalizedUsername = NormalizeUserKey(username);
        var contactList = contacts.ToList();
        var contractList = contracts.ToList();

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var store = await LoadStoreCoreAsync(cancellationToken).ConfigureAwait(false);

            store.Contacts.RemoveAll(contact => string.Equals(contact.OwnerUsername, normalizedUsername, StringComparison.OrdinalIgnoreCase));
            store.Contracts.RemoveAll(contract => string.Equals(contract.OwnerUsername, normalizedUsername, StringComparison.OrdinalIgnoreCase));

            store.Contacts.AddRange(contactList.Select(contact => NormalizeContact(contact, normalizedUsername)));
            store.Contracts.AddRange(contractList.Select(contract => NormalizeContract(contract, normalizedUsername)));

            await SaveStoreCoreAsync(store, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task EnsureInitializedCoreAsync(CancellationToken cancellationToken)
    {
        if (isInitialized)
        {
            return;
        }

        var directory = Path.GetDirectoryName(StoragePath)
            ?? throw new InvalidOperationException("Workspace store path is invalid.");

        Directory.CreateDirectory(directory);

        if (!File.Exists(StoragePath))
        {
            await SaveStoreCoreAsync(new WorkspaceStoreDocument(), cancellationToken).ConfigureAwait(false);
            return;
        }

        isInitialized = true;
    }

    private async Task<WorkspaceStoreDocument> LoadStoreCoreAsync(CancellationToken cancellationToken)
    {
        await EnsureInitializedCoreAsync(cancellationToken).ConfigureAwait(false);

        if (cachedStore is not null)
        {
            return cachedStore;
        }

        try
        {
            cachedStore = await RepositoryFileStore.ReadJsonAsync<WorkspaceStoreDocument>(StoragePath, SerializerOptions, cancellationToken).ConfigureAwait(false)
                ?? new WorkspaceStoreDocument();

            isInitialized = true;
            return cachedStore;
        }
        catch
        {
            await BackupCorruptStoreCoreAsync(cancellationToken).ConfigureAwait(false);
            var resetStore = new WorkspaceStoreDocument();
            await SaveStoreCoreAsync(resetStore, cancellationToken).ConfigureAwait(false);
            return cachedStore!;
        }
    }

    private async Task SaveStoreCoreAsync(WorkspaceStoreDocument store, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(StoragePath)
            ?? throw new InvalidOperationException("Workspace store path is invalid.");

        Directory.CreateDirectory(directory);

        store.Contacts = store.Contacts
            .Select(contact => NormalizeContact(contact, NormalizeUserKey(contact.OwnerUsername)))
            .OrderBy(contact => contact.OwnerUsername)
            .ThenBy(contact => contact.FullName)
            .ToList();

        store.Contracts = store.Contracts
            .Select(contract => NormalizeContract(contract, NormalizeUserKey(contract.OwnerUsername)))
            .OrderBy(contract => contract.OwnerUsername)
            .ThenBy(contract => contract.ReminderDate ?? contract.StartDate)
            .ThenBy(contract => contract.Title)
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

    private static ContactRecord NormalizeContact(ContactRecord contact, string ownerUsername) => new ContactRecord
    {
        Id = string.IsNullOrWhiteSpace(contact.Id) ? Guid.NewGuid().ToString("N") : contact.Id,
        OwnerUsername = ownerUsername,
        FullName = contact.FullName.Trim(),
        Company = contact.Company.Trim(),
        PhoneNumber = contact.PhoneNumber.Trim(),
        Email = contact.Email.Trim(),
        FollowUpDate = contact.FollowUpDate?.Date,
        Notes = contact.Notes.Trim(),
        UpdatedUtc = NormalizeUtc(contact.UpdatedUtc)
    };

    private static ContractRecord NormalizeContract(ContractRecord contract, string ownerUsername) => new ContractRecord
    {
        Id = string.IsNullOrWhiteSpace(contract.Id) ? Guid.NewGuid().ToString("N") : contract.Id,
        OwnerUsername = ownerUsername,
        Title = contract.Title.Trim(),
        ClientName = contract.ClientName.Trim(),
        ContractType = contract.ContractType.Trim(),
        Status = contract.Status.Trim(),
        StartDate = contract.StartDate.Date,
        ReminderDate = contract.ReminderDate?.Date,
        Notes = contract.Notes.Trim(),
        UpdatedUtc = NormalizeUtc(contract.UpdatedUtc)
    };

    private static DateTime NormalizeUtc(DateTime value)
    {
        if (value == default)
        {
            return DateTime.UtcNow;
        }

        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static string NormalizeUserKey(string username) => username.Trim();

    private sealed class WorkspaceStoreDocument
    {
        public List<ContactRecord> Contacts { get; set; } = new();
        public List<ContractRecord> Contracts { get; set; } = new();
    }
}
