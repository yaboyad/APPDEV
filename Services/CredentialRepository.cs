using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Label_CRM_demo.Models;

namespace Label_CRM_demo.Services;

public sealed class CredentialRepository
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Label CRM demo local credential store");
    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
    {
        WriteIndented = true
    };

    public CredentialRepository(string? storagePath = null)
    {
        StoragePath = storagePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LabelCrmDemo",
            "security",
            "users.dat");
    }

    public string StoragePath { get; }

    public void EnsureSeeded()
    {
        if (File.Exists(StoragePath))
        {
            return;
        }

        SaveStore(new CredentialStore
        {
            Users = new List<StoredUser>
            {
                CreateStoredUser("Admin", "Admin", string.Empty, string.Empty, "admin@local.test", "Dink1")
            }
        });
    }

    public bool TryAuthenticate(string usernameOrEmail, string password, out AuthenticatedUser? user)
    {
        user = null;

        var identifier = NormalizeIdentifier(usernameOrEmail);
        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        var store = LoadStore();
        var record = store.Users.FirstOrDefault(candidate =>
            string.Equals(candidate.Username, identifier, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate.Email, identifier, StringComparison.OrdinalIgnoreCase));

        if (record is null)
        {
            return false;
        }

        if (!VerifyPassword(password, record.PasswordSalt, record.PasswordHash))
        {
            return false;
        }

        user = ToAuthenticatedUser(record);
        return true;
    }

    public bool TryRegister(SignupRequest request, out AuthenticatedUser? user, out string errorMessage)
    {
        user = null;
        errorMessage = string.Empty;

        var firstName = NormalizeName(request.FirstName);
        var lastName = NormalizeName(request.LastName);
        var phoneNumber = NormalizePhoneNumber(request.PhoneNumber);
        var email = NormalizeEmail(request.Email);
        var password = request.Password;

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
        {
            errorMessage = "First name and last name are required.";
            return false;
        }

        if (!IsValidPhoneNumber(phoneNumber))
        {
            errorMessage = "Enter a valid phone number for testing.";
            return false;
        }

        if (!IsValidEmail(email))
        {
            errorMessage = "Enter a valid email address.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            errorMessage = "Use a password with at least 6 characters.";
            return false;
        }

        var store = LoadStore();
        var duplicateUser = store.Users.Any(candidate =>
            string.Equals(candidate.Email, email, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate.Username, email, StringComparison.OrdinalIgnoreCase));

        if (duplicateUser)
        {
            errorMessage = "An account with that email already exists.";
            return false;
        }

        var storedUser = CreateStoredUser(email, firstName, lastName, phoneNumber, email, password);
        store.Users.Add(storedUser);
        SaveStore(store);

        user = ToAuthenticatedUser(storedUser);
        return true;
    }

    private CredentialStore LoadStore()
    {
        EnsureSeeded();

        try
        {
            var encryptedBytes = File.ReadAllBytes(StoragePath);
            var clearBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(clearBytes);
            var store = JsonSerializer.Deserialize<CredentialStore>(json, SerializerOptions);

            if (store is null || store.Users.Count == 0)
            {
                throw new InvalidDataException("Credential store is empty.");
            }

            return store;
        }
        catch
        {
            BackupCorruptStore();
            File.Delete(StoragePath);
            EnsureSeeded();
            return LoadStoreWithoutRecovery();
        }
    }

    private CredentialStore LoadStoreWithoutRecovery()
    {
        var encryptedBytes = File.ReadAllBytes(StoragePath);
        var clearBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);
        var json = Encoding.UTF8.GetString(clearBytes);

        return JsonSerializer.Deserialize<CredentialStore>(json, SerializerOptions)
            ?? throw new InvalidDataException("Unable to load credential store.");
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

    private void SaveStore(CredentialStore store)
    {
        var directory = Path.GetDirectoryName(StoragePath)
            ?? throw new InvalidOperationException("Credential store path is invalid.");

        Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(store, SerializerOptions);
        var clearBytes = Encoding.UTF8.GetBytes(json);
        var encryptedBytes = ProtectedData.Protect(clearBytes, Entropy, DataProtectionScope.CurrentUser);

        File.WriteAllBytes(StoragePath, encryptedBytes);
    }

    private static StoredUser CreateStoredUser(
        string username,
        string firstName,
        string lastName,
        string phoneNumber,
        string email,
        string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(16);
        var salt = Convert.ToBase64String(saltBytes);

        return new StoredUser
        {
            Username = NormalizeIdentifier(username),
            DisplayName = BuildDisplayName(firstName, lastName, username),
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = phoneNumber,
            Email = NormalizeEmail(email),
            PasswordSalt = salt,
            PasswordHash = ComputeHash(password, salt)
        };
    }

    private static string ComputeHash(string password, string salt)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var saltBytes = Convert.FromBase64String(salt);
        var combinedBytes = new byte[saltBytes.Length + passwordBytes.Length];

        Buffer.BlockCopy(saltBytes, 0, combinedBytes, 0, saltBytes.Length);
        Buffer.BlockCopy(passwordBytes, 0, combinedBytes, saltBytes.Length, passwordBytes.Length);

        var hashBytes = SHA256.HashData(combinedBytes);
        return Convert.ToBase64String(hashBytes);
    }

    private static bool VerifyPassword(string password, string salt, string expectedHash)
    {
        var candidateHash = Convert.FromBase64String(ComputeHash(password, salt));
        var expectedHashBytes = Convert.FromBase64String(expectedHash);
        return CryptographicOperations.FixedTimeEquals(candidateHash, expectedHashBytes);
    }

    private static AuthenticatedUser ToAuthenticatedUser(StoredUser record) => new(
        record.Username,
        string.IsNullOrWhiteSpace(record.DisplayName)
            ? BuildDisplayName(record.FirstName, record.LastName, record.Username)
            : record.DisplayName,
        record.FirstName,
        record.LastName,
        record.PhoneNumber,
        record.Email);

    private static string BuildDisplayName(string firstName, string lastName, string fallback)
    {
        var displayName = $"{firstName} {lastName}".Trim();
        return string.IsNullOrWhiteSpace(displayName) ? fallback.Trim() : displayName;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new MailAddress(email);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsValidPhoneNumber(string phoneNumber)
        => phoneNumber.Count(char.IsDigit) >= 7;

    private static string NormalizeIdentifier(string value) => value.Trim();

    private static string NormalizeName(string value) => value.Trim();

    private static string NormalizeEmail(string value) => value.Trim().ToLowerInvariant();

    private static string NormalizePhoneNumber(string value) => value.Trim();

    private sealed class CredentialStore
    {
        public List<StoredUser> Users { get; set; } = new List<StoredUser>();
    }

    private sealed class StoredUser
    {
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string PasswordSalt { get; set; } = string.Empty;
    }
}
