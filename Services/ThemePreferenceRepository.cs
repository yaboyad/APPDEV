using System;
using System.IO;
using System.Text.Json;
using Label_CRM_demo.Models;

namespace Label_CRM_demo.Services;

public sealed class ThemePreferenceRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public ThemePreferenceRepository(string? storagePath = null)
    {
        StoragePath = storagePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LabelCrmDemo",
            "theme-preferences.json");
    }

    public string StoragePath { get; }

    public AppThemeMode Load()
    {
        if (!File.Exists(StoragePath))
        {
            return AppThemeMode.Dark;
        }

        try
        {
            var json = File.ReadAllText(StoragePath);
            var preference = JsonSerializer.Deserialize<AppThemePreference>(json, SerializerOptions);
            return preference?.Mode ?? AppThemeMode.Dark;
        }
        catch
        {
            BackupCorruptStore();
            File.Delete(StoragePath);
            return AppThemeMode.Dark;
        }
    }

    public void Save(AppThemeMode mode)
    {
        var directory = Path.GetDirectoryName(StoragePath)
            ?? throw new InvalidOperationException("Theme preference storage path is invalid.");

        Directory.CreateDirectory(directory);

        var preference = new AppThemePreference
        {
            Mode = mode
        };

        var json = JsonSerializer.Serialize(preference, SerializerOptions);
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
}
