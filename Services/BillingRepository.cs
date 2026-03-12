using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Label_CRM_demo.Models;

namespace Label_CRM_demo.Services;

public sealed class BillingRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim gate = new(1, 1);
    private BillingStoreDocument? cachedStore;
    private bool isInitialized;

    public BillingRepository(string? storagePath = null)
    {
        StoragePath = storagePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LabelCrmDemo",
            "billing",
            "billing-data.json");
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

    public BillingSnapshot LoadForUser(string username)
        => LoadForUserAsync(username).GetAwaiter().GetResult();

    public async Task<BillingSnapshot> LoadForUserAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        var normalizedUsername = NormalizeUserKey(username);

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var store = await LoadStoreCoreAsync(cancellationToken).ConfigureAwait(false);
            var profile = store.Profiles
                .FirstOrDefault(item => string.Equals(item.OwnerUsername, normalizedUsername, StringComparison.OrdinalIgnoreCase));

            var invoices = store.Invoices
                .Where(item => string.Equals(item.OwnerUsername, normalizedUsername, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.PaidOn ?? item.DueDate ?? item.IssuedOn)
                .ThenByDescending(item => item.UpdatedUtc)
                .ToList();

            return new BillingSnapshot(profile, invoices);
        }
        finally
        {
            gate.Release();
        }
    }

    public void SaveForUser(
        string username,
        BillingProfileRecord? profile,
        IEnumerable<PaymentInvoiceRecord> invoices)
        => SaveForUserAsync(username, profile, invoices).GetAwaiter().GetResult();

    public async Task SaveForUserAsync(
        string username,
        BillingProfileRecord? profile,
        IEnumerable<PaymentInvoiceRecord> invoices,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoices);

        var normalizedUsername = NormalizeUserKey(username);
        var invoiceList = invoices.ToList();

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var store = await LoadStoreCoreAsync(cancellationToken).ConfigureAwait(false);

            store.Profiles.RemoveAll(item => string.Equals(item.OwnerUsername, normalizedUsername, StringComparison.OrdinalIgnoreCase));
            if (profile is not null)
            {
                store.Profiles.Add(NormalizeProfile(profile, normalizedUsername));
            }

            store.Invoices.RemoveAll(item => string.Equals(item.OwnerUsername, normalizedUsername, StringComparison.OrdinalIgnoreCase));
            store.Invoices.AddRange(invoiceList.Select(invoice => NormalizeInvoice(invoice, normalizedUsername)));

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
            ?? throw new InvalidOperationException("Billing store path is invalid.");

        Directory.CreateDirectory(directory);

        if (!File.Exists(StoragePath))
        {
            await SaveStoreCoreAsync(new BillingStoreDocument(), cancellationToken).ConfigureAwait(false);
            return;
        }

        isInitialized = true;
    }

    private async Task<BillingStoreDocument> LoadStoreCoreAsync(CancellationToken cancellationToken)
    {
        await EnsureInitializedCoreAsync(cancellationToken).ConfigureAwait(false);

        if (cachedStore is not null)
        {
            return cachedStore;
        }

        try
        {
            cachedStore = await RepositoryFileStore.ReadJsonAsync<BillingStoreDocument>(StoragePath, SerializerOptions, cancellationToken).ConfigureAwait(false)
                ?? new BillingStoreDocument();

            isInitialized = true;
            return cachedStore;
        }
        catch
        {
            await BackupCorruptStoreCoreAsync(cancellationToken).ConfigureAwait(false);
            var resetStore = new BillingStoreDocument();
            await SaveStoreCoreAsync(resetStore, cancellationToken).ConfigureAwait(false);
            return cachedStore!;
        }
    }

    private async Task SaveStoreCoreAsync(BillingStoreDocument store, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(StoragePath)
            ?? throw new InvalidOperationException("Billing store path is invalid.");

        Directory.CreateDirectory(directory);

        store.Profiles = store.Profiles
            .Select(profile => NormalizeProfile(profile, NormalizeUserKey(profile.OwnerUsername)))
            .OrderBy(profile => profile.OwnerUsername)
            .ToList();

        store.Invoices = store.Invoices
            .Select(invoice => NormalizeInvoice(invoice, NormalizeUserKey(invoice.OwnerUsername)))
            .OrderBy(invoice => invoice.OwnerUsername)
            .ThenByDescending(invoice => invoice.PaidOn ?? invoice.DueDate ?? invoice.IssuedOn)
            .ThenByDescending(invoice => invoice.UpdatedUtc)
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

    private static BillingProfileRecord NormalizeProfile(BillingProfileRecord profile, string ownerUsername) => new BillingProfileRecord
    {
        OwnerUsername = ownerUsername,
        ProviderName = profile.ProviderName.Trim(),
        ProviderCustomerId = profile.ProviderCustomerId.Trim(),
        PlanName = profile.PlanName.Trim(),
        BillingCycle = profile.BillingCycle.Trim(),
        DefaultPaymentMethod = profile.DefaultPaymentMethod.Trim(),
        NextChargeAmount = profile.NextChargeAmount,
        NextChargeDate = profile.NextChargeDate?.Date,
        CurrencyCode = NormalizeCurrency(profile.CurrencyCode),
        UpdatedUtc = NormalizeUtc(profile.UpdatedUtc)
    };

    private static PaymentInvoiceRecord NormalizeInvoice(PaymentInvoiceRecord invoice, string ownerUsername)
    {
        var issuedOn = invoice.IssuedOn == default
            ? (invoice.PaidOn ?? invoice.DueDate ?? DateTime.Today).Date
            : invoice.IssuedOn.Date;

        return new PaymentInvoiceRecord
        {
            Id = string.IsNullOrWhiteSpace(invoice.Id) ? Guid.NewGuid().ToString("N") : invoice.Id,
            OwnerUsername = ownerUsername,
            InvoiceNumber = invoice.InvoiceNumber.Trim(),
            Amount = invoice.Amount,
            CurrencyCode = NormalizeCurrency(invoice.CurrencyCode),
            PaymentMethod = invoice.PaymentMethod.Trim(),
            Status = NormalizeStatus(invoice),
            IssuedOn = issuedOn,
            DueDate = invoice.DueDate?.Date,
            PaidOn = invoice.PaidOn?.Date,
            ProviderName = invoice.ProviderName.Trim(),
            ProviderInvoiceId = invoice.ProviderInvoiceId.Trim(),
            HostedInvoiceUrl = invoice.HostedInvoiceUrl.Trim(),
            UpdatedUtc = NormalizeUtc(invoice.UpdatedUtc)
        };
    }

    private static string NormalizeStatus(PaymentInvoiceRecord invoice)
    {
        var status = invoice.Status.Trim();
        if (!string.IsNullOrWhiteSpace(status))
        {
            return status;
        }

        if (invoice.PaidOn.HasValue)
        {
            return "Paid";
        }

        return invoice.DueDate.HasValue ? "Open" : "Pending";
    }

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

    private static string NormalizeCurrency(string? currencyCode)
        => string.IsNullOrWhiteSpace(currencyCode) ? "USD" : currencyCode.Trim().ToUpperInvariant();

    private static string NormalizeUserKey(string username) => username.Trim();

    private sealed class BillingStoreDocument
    {
        public List<BillingProfileRecord> Profiles { get; set; } = new();
        public List<PaymentInvoiceRecord> Invoices { get; set; } = new();
    }
}
