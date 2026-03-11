using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Label_CRM_demo.Models;

namespace Label_CRM_demo.Services;

public sealed class WorkspaceRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

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
    {
        var directory = Path.GetDirectoryName(StoragePath)
            ?? throw new InvalidOperationException("Workspace store path is invalid.");

        Directory.CreateDirectory(directory);

        if (!File.Exists(StoragePath))
        {
            SaveStore(new WorkspaceStoreDocument());
        }
    }

    public WorkspaceSnapshot LoadForUser(string username)
    {
        var normalizedUsername = NormalizeUserKey(username);
        var store = LoadStore();

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

    public void SaveForUser(
        string username,
        IEnumerable<ContactRecord> contacts,
        IEnumerable<ContractRecord> contracts)
    {
        var normalizedUsername = NormalizeUserKey(username);
        var store = LoadStore();

        store.Contacts.RemoveAll(contact => string.Equals(contact.OwnerUsername, normalizedUsername, StringComparison.OrdinalIgnoreCase));
        store.Contracts.RemoveAll(contract => string.Equals(contract.OwnerUsername, normalizedUsername, StringComparison.OrdinalIgnoreCase));

        store.Contacts.AddRange(contacts.Select(contact => NormalizeContact(contact, normalizedUsername)));
        store.Contracts.AddRange(contracts.Select(contract => NormalizeContract(contract, normalizedUsername)));

        SaveStore(store);
    }

    private WorkspaceStoreDocument LoadStore()
    {
        EnsureInitialized();

        try
        {
            var json = File.ReadAllText(StoragePath);
            return JsonSerializer.Deserialize<WorkspaceStoreDocument>(json, SerializerOptions) ?? new WorkspaceStoreDocument();
        }
        catch
        {
            BackupCorruptStore();
            SaveStore(new WorkspaceStoreDocument());
            var resetJson = File.ReadAllText(StoragePath);
            return JsonSerializer.Deserialize<WorkspaceStoreDocument>(resetJson, SerializerOptions) ?? new WorkspaceStoreDocument();
        }
    }

    private void SaveStore(WorkspaceStoreDocument store)
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

        var json = JsonSerializer.Serialize(store, SerializerOptions);
        File.WriteAllText(StoragePath, json);
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
