using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Label_CRM_demo.Models;

namespace Label_CRM_demo.Services;

public sealed class CredentialRepository
{
    private const string MasterUsername = "Admin";
    private const string MasterEmail = "admin@local.test";
    private const string MasterPassword = "Dink1";
    private const string TestUsername = "Testuser";
    private const string TestEmail = "testuser@local.test";
    private const string TestPassword = "Password1";
    private static readonly byte[] Entropy = System.Text.Encoding.UTF8.GetBytes("Label CRM demo local credential store");
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim gate = new(1, 1);
    private CredentialStore? cachedStore;
    private bool isInitialized;

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

    public bool TryAuthenticate(string usernameOrEmail, string password, out AuthenticatedUser? user)
    {
        user = AuthenticateAsync(usernameOrEmail, password).GetAwaiter().GetResult();
        return user is not null;
    }

    public async Task<AuthenticatedUser?> AuthenticateAsync(
        string usernameOrEmail,
        string password,
        CancellationToken cancellationToken = default)
    {
        var result = await AuthenticateWithStatusAsync(usernameOrEmail, password, cancellationToken).ConfigureAwait(false);
        return result.User;
    }

    public async Task<(AuthenticatedUser? User, string ErrorMessage)> AuthenticateWithStatusAsync(
        string usernameOrEmail,
        string password,
        CancellationToken cancellationToken = default)
    {
        var identifier = NormalizeIdentifier(usernameOrEmail);
        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(password))
        {
            return (null, "Enter your email or username and password.");
        }

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var store = await LoadStoreCoreAsync(cancellationToken).ConfigureAwait(false);
            var record = FindStoredUser(store, identifier);

            if (record is null)
            {
                return (null, "Incorrect email, username, or password.");
            }

            bool passwordVerified;
            try
            {
                passwordVerified = await Task.Run(
                    () => VerifyPassword(password, record.PasswordSalt, record.PasswordHash),
                    cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                return (null, "Incorrect email, username, or password.");
            }

            if (!passwordVerified)
            {
                return (null, "Incorrect email, username, or password.");
            }

            if (record.IsBanned)
            {
                return (null, "This account has been banned. Sign in with the master account to restore access.");
            }

            return (ToAuthenticatedUser(record), string.Empty);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<ManagedAccountRecord>> GetManagedAccountsAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var store = await LoadStoreCoreAsync(cancellationToken).ConfigureAwait(false);
            return store.Users
                .OrderByDescending(user => AccountTiers.IsMaster(user.AccountTier))
                .ThenBy(user => user.IsBanned)
                .ThenBy(user => string.IsNullOrWhiteSpace(user.DisplayName) ? user.Username : user.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .Select(ToManagedAccount)
                .ToList();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<(bool Success, string ErrorMessage)> SetBanStateAsync(
        string usernameOrEmail,
        bool isBanned,
        CancellationToken cancellationToken = default)
    {
        var identifier = NormalizeIdentifier(usernameOrEmail);
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return (false, "Select an account before changing access.");
        }

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var store = await LoadStoreCoreAsync(cancellationToken).ConfigureAwait(false);
            var record = FindStoredUser(store, identifier);
            if (record is null)
            {
                return (false, "That account could not be found.");
            }

            if (AccountTiers.IsMaster(record.AccountTier) || IsMasterIdentity(record.Username, record.Email))
            {
                return (false, "Master accounts are protected and cannot be banned.");
            }

            if (record.IsBanned == isBanned)
            {
                return (true, isBanned
                    ? "That account is already banned."
                    : "That account already has access.");
            }

            record.IsBanned = isBanned;
            await SaveStoreCoreAsync(store, cancellationToken).ConfigureAwait(false);
            return (true, string.Empty);
        }
        finally
        {
            gate.Release();
        }
    }

    public bool TryRegister(SignupRequest request, out AuthenticatedUser? user, out string errorMessage)
    {
        var result = RegisterAsync(request).GetAwaiter().GetResult();
        user = result.User;
        errorMessage = result.ErrorMessage;
        return user is not null;
    }

    public async Task<(AuthenticatedUser? User, string ErrorMessage)> RegisterAsync(
        SignupRequest request,
        CancellationToken cancellationToken = default)
    {
        var firstName = NormalizeName(request.FirstName);
        var lastName = NormalizeName(request.LastName);
        var phoneNumber = NormalizePhoneNumber(request.PhoneNumber);
        var email = NormalizeEmail(request.Email);
        var password = request.Password;

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
        {
            return (null, "First name and last name are required.");
        }

        if (!IsValidPhoneNumber(phoneNumber))
        {
            return (null, "Enter a valid phone number for testing.");
        }

        if (!IsValidEmail(email))
        {
            return (null, "Enter a valid email address.");
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            return (null, "Use a password with at least 6 characters.");
        }

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var store = await LoadStoreCoreAsync(cancellationToken).ConfigureAwait(false);
            var duplicateUser = store.Users.Any(candidate =>
                string.Equals(candidate.Email, email, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate.Username, email, StringComparison.OrdinalIgnoreCase));

            if (duplicateUser)
            {
                return (null, "An account with that email already exists.");
            }

            var storedUser = await Task.Run(
                () => CreateStoredUser(email, firstName, lastName, phoneNumber, email, password, AccountTiers.User),
                cancellationToken).ConfigureAwait(false);

            store.Users.Add(storedUser);
            await SaveStoreCoreAsync(store, cancellationToken).ConfigureAwait(false);
            return (ToAuthenticatedUser(storedUser), string.Empty);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task EnsureSeededCoreAsync(CancellationToken cancellationToken)
    {
        if (isInitialized)
        {
            return;
        }

        var directory = Path.GetDirectoryName(StoragePath)
            ?? throw new InvalidOperationException("Credential store path is invalid.");

        Directory.CreateDirectory(directory);

        CredentialStore store;
        var shouldSave = false;

        if (!File.Exists(StoragePath))
        {
            store = new CredentialStore();
            shouldSave = true;
        }
        else
        {
            try
            {
                store = await LoadStoreWithoutRecoveryCoreAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                await BackupCorruptStoreCoreAsync(cancellationToken).ConfigureAwait(false);
                File.Delete(StoragePath);
                store = new CredentialStore();
                shouldSave = true;
            }
        }

        if (EnsureRequiredUsers(store))
        {
            shouldSave = true;
        }

        cachedStore = store;

        if (shouldSave)
        {
            await SaveStoreCoreAsync(store, cancellationToken).ConfigureAwait(false);
            return;
        }

        isInitialized = true;
    }

    private async Task<CredentialStore> LoadStoreCoreAsync(CancellationToken cancellationToken)
    {
        await EnsureSeededCoreAsync(cancellationToken).ConfigureAwait(false);

        if (cachedStore is not null)
        {
            return cachedStore;
        }

        try
        {
            cachedStore = await LoadStoreWithoutRecoveryCoreAsync(cancellationToken).ConfigureAwait(false);

            if (cachedStore.Users.Count == 0)
            {
                throw new InvalidDataException("Credential store is empty.");
            }

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
            return cachedStore ?? throw new InvalidDataException("Unable to rebuild the credential store.");
        }
    }

    private async Task<CredentialStore> LoadStoreWithoutRecoveryCoreAsync(CancellationToken cancellationToken)
    {
        var encryptedBytes = await RepositoryFileStore.ReadAllBytesAsync(StoragePath, cancellationToken).ConfigureAwait(false);

        return await Task.Run(() =>
        {
            var clearBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<CredentialStore>(clearBytes, SerializerOptions)
                ?? throw new InvalidDataException("Unable to load credential store.");
        }, cancellationToken).ConfigureAwait(false);
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

    private async Task SaveStoreCoreAsync(CredentialStore store, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(StoragePath)
            ?? throw new InvalidOperationException("Credential store path is invalid.");

        Directory.CreateDirectory(directory);

        var encryptedBytes = await Task.Run(() =>
        {
            var clearBytes = JsonSerializer.SerializeToUtf8Bytes(store, SerializerOptions);
            return ProtectedData.Protect(clearBytes, Entropy, DataProtectionScope.CurrentUser);
        }, cancellationToken).ConfigureAwait(false);

        cachedStore = store;
        await RepositoryFileStore.WriteAllBytesAtomicAsync(StoragePath, encryptedBytes, cancellationToken).ConfigureAwait(false);
        isInitialized = true;
    }

    private static StoredUser? FindStoredUser(CredentialStore store, string identifier)
        => store.Users.FirstOrDefault(candidate =>
            string.Equals(candidate.Username, identifier, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate.Email, identifier, StringComparison.OrdinalIgnoreCase));

    private static StoredUser CreateStoredUser(
        string username,
        string firstName,
        string lastName,
        string phoneNumber,
        string email,
        string password,
        string accountTier)
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
            AccountTier = AccountTiers.Normalize(accountTier),
            PasswordSalt = salt,
            PasswordHash = ComputeHash(password, salt),
            IsBanned = false
        };
    }

    private static string ComputeHash(string password, string salt)
    {
        var passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);
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
        record.Email,
        AccountTiers.Normalize(record.AccountTier),
        record.IsBanned);

    private static ManagedAccountRecord ToManagedAccount(StoredUser record) => new(
        record.Username,
        string.IsNullOrWhiteSpace(record.DisplayName)
            ? BuildDisplayName(record.FirstName, record.LastName, record.Username)
            : record.DisplayName,
        record.FirstName,
        record.LastName,
        record.PhoneNumber,
        record.Email,
        AccountTiers.Normalize(record.AccountTier),
        record.IsBanned);

    private static bool EnsureRequiredUsers(CredentialStore store)
    {
        var changed = false;

        foreach (var user in store.Users)
        {
            changed = NormalizeStoredUser(user) || changed;
        }

        changed = UpsertRequiredUser(
            store,
            MasterUsername,
            "Admin",
            string.Empty,
            string.Empty,
            MasterEmail,
            MasterPassword,
            AccountTiers.Master,
            resetPassword: false) || changed;

        changed = UpsertRequiredUser(
            store,
            TestUsername,
            "Test",
            "User",
            "317-555-0101",
            TestEmail,
            TestPassword,
            AccountTiers.User,
            resetPassword: true) || changed;

        return changed;
    }

    private static bool UpsertRequiredUser(
        CredentialStore store,
        string username,
        string firstName,
        string lastName,
        string phoneNumber,
        string email,
        string password,
        string accountTier,
        bool resetPassword)
    {
        var index = store.Users.FindIndex(candidate =>
            string.Equals(candidate.Username, username, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate.Email, email, StringComparison.OrdinalIgnoreCase));

        if (index < 0)
        {
            store.Users.Add(CreateStoredUser(username, firstName, lastName, phoneNumber, email, password, accountTier));
            return true;
        }

        var user = store.Users[index];
        var changed = false;
        var normalizedTier = AccountTiers.Normalize(accountTier);
        var normalizedIsBanned = AccountTiers.IsMaster(normalizedTier) ? false : user.IsBanned;

        changed = SetValue(user.Username, NormalizeIdentifier(username), value => user.Username = value) || changed;
        changed = SetValue(user.FirstName, NormalizeName(firstName), value => user.FirstName = value) || changed;
        changed = SetValue(user.LastName, NormalizeName(lastName), value => user.LastName = value) || changed;
        changed = SetValue(user.PhoneNumber, NormalizePhoneNumber(phoneNumber), value => user.PhoneNumber = value) || changed;
        changed = SetValue(user.Email, NormalizeEmail(email), value => user.Email = value) || changed;
        changed = SetValue(user.DisplayName, BuildDisplayName(firstName, lastName, username), value => user.DisplayName = value) || changed;
        changed = SetValue(user.AccountTier, normalizedTier, value => user.AccountTier = value) || changed;
        changed = SetValue(user.IsBanned, normalizedIsBanned, value => user.IsBanned = value) || changed;

        if ((resetPassword && NeedsPasswordReset(user, password))
            || string.IsNullOrWhiteSpace(user.PasswordSalt)
            || string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            SetPassword(user, password);
            changed = true;
        }

        return changed;
    }

    private static bool NormalizeStoredUser(StoredUser user)
    {
        var changed = false;
        var normalizedUsername = NormalizeIdentifier(user.Username);
        var normalizedFirstName = NormalizeName(user.FirstName);
        var normalizedLastName = NormalizeName(user.LastName);
        var normalizedPhoneNumber = NormalizePhoneNumber(user.PhoneNumber);
        var normalizedEmail = NormalizeEmail(user.Email);
        var normalizedDisplayName = string.IsNullOrWhiteSpace(user.DisplayName)
            ? BuildDisplayName(normalizedFirstName, normalizedLastName, normalizedUsername)
            : user.DisplayName.Trim();
        var normalizedTier = IsMasterIdentity(normalizedUsername, normalizedEmail)
            ? AccountTiers.Master
            : AccountTiers.Normalize(user.AccountTier);
        var normalizedIsBanned = AccountTiers.IsMaster(normalizedTier) ? false : user.IsBanned;

        changed = SetValue(user.Username, normalizedUsername, value => user.Username = value) || changed;
        changed = SetValue(user.FirstName, normalizedFirstName, value => user.FirstName = value) || changed;
        changed = SetValue(user.LastName, normalizedLastName, value => user.LastName = value) || changed;
        changed = SetValue(user.PhoneNumber, normalizedPhoneNumber, value => user.PhoneNumber = value) || changed;
        changed = SetValue(user.Email, normalizedEmail, value => user.Email = value) || changed;
        changed = SetValue(user.DisplayName, normalizedDisplayName, value => user.DisplayName = value) || changed;
        changed = SetValue(user.AccountTier, normalizedTier, value => user.AccountTier = value) || changed;
        changed = SetValue(user.IsBanned, normalizedIsBanned, value => user.IsBanned = value) || changed;

        return changed;
    }

    private static bool SetValue<T>(T currentValue, T value, Action<T> setter)
    {
        if (EqualityComparer<T>.Default.Equals(currentValue, value))
        {
            return false;
        }

        setter(value);
        return true;
    }

    private static void SetPassword(StoredUser user, string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(16);
        var salt = Convert.ToBase64String(saltBytes);
        user.PasswordSalt = salt;
        user.PasswordHash = ComputeHash(password, salt);
    }

    private static bool NeedsPasswordReset(StoredUser user, string password)
    {
        if (string.IsNullOrWhiteSpace(user.PasswordSalt) || string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return true;
        }

        try
        {
            return !VerifyPassword(password, user.PasswordSalt, user.PasswordHash);
        }
        catch
        {
            return true;
        }
    }

    private static bool IsMasterIdentity(string username, string email)
        => string.Equals(username, MasterUsername, StringComparison.OrdinalIgnoreCase)
        || string.Equals(email, MasterEmail, StringComparison.OrdinalIgnoreCase);

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
        public List<StoredUser> Users { get; set; } = new();
    }

    private sealed class StoredUser
    {
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string AccountTier { get; set; } = AccountTiers.User;
        public string PasswordHash { get; set; } = string.Empty;
        public string PasswordSalt { get; set; } = string.Empty;
        public bool IsBanned { get; set; }
    }
}
