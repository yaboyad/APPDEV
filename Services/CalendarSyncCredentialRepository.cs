using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Label_CRM_demo.Models;

namespace Label_CRM_demo.Services;

public sealed class CalendarSyncCredentialRepository
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Label CRM demo calendar sync settings");
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public CalendarSyncCredentialRepository(string? storagePath = null)
    {
        StoragePath = storagePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LabelCrmDemo",
            "calendar",
            "sync.dat");
    }

    public string StoragePath { get; }

    public CalendarSyncSettings Load()
    {
        if (!File.Exists(StoragePath))
        {
            return new CalendarSyncSettings();
        }

        try
        {
            var encryptedBytes = File.ReadAllBytes(StoragePath);
            var clearBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(clearBytes);

            return JsonSerializer.Deserialize<CalendarSyncSettings>(json, SerializerOptions)
                ?? new CalendarSyncSettings();
        }
        catch
        {
            BackupCorruptStore();
            File.Delete(StoragePath);
            return new CalendarSyncSettings();
        }
    }

    public void Save(CalendarSyncSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var directory = Path.GetDirectoryName(StoragePath)
            ?? throw new InvalidOperationException("Calendar sync settings path is invalid.");

        Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(settings, SerializerOptions);
        var clearBytes = Encoding.UTF8.GetBytes(json);
        var encryptedBytes = ProtectedData.Protect(clearBytes, Entropy, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(StoragePath, encryptedBytes);
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
}
