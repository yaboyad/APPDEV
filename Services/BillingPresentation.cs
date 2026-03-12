using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Label_CRM_demo.Models;

namespace Label_CRM_demo.Services;

public static class BillingPresentation
{
    public static BillingChargeSummary? GetNextCharge(BillingSnapshot snapshot, DateTime today)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.Profile?.NextChargeDate is DateTime nextChargeDate)
        {
            return new BillingChargeSummary(
                snapshot.Profile.NextChargeAmount,
                NormalizeCurrency(snapshot.Profile.CurrencyCode),
                nextChargeDate.Date);
        }

        var nextInvoice = snapshot.Invoices
            .Where(invoice => !IsSettledInvoice(invoice))
            .OrderBy(invoice => GetInvoiceDueDate(invoice, today))
            .ThenBy(invoice => GetInvoiceDisplayDate(invoice))
            .FirstOrDefault();

        return nextInvoice is null
            ? null
            : new BillingChargeSummary(
                nextInvoice.Amount,
                NormalizeCurrency(nextInvoice.CurrencyCode),
                GetInvoiceDueDate(nextInvoice, today));
    }

    public static IReadOnlyList<PaymentInvoiceRecord> GetRecentInvoices(BillingSnapshot snapshot, int maxCount)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return snapshot.Invoices
            .OrderByDescending(GetInvoiceDisplayDate)
            .ThenByDescending(invoice => invoice.UpdatedUtc)
            .Take(maxCount)
            .ToArray();
    }

    public static DateTime GetInvoiceDisplayDate(PaymentInvoiceRecord invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        return (invoice.PaidOn ?? invoice.DueDate ?? invoice.IssuedOn).Date;
    }

    public static string GetInvoiceStatusLabel(PaymentInvoiceRecord invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var normalized = invoice.Status.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return invoice.PaidOn.HasValue ? "Paid" : invoice.DueDate.HasValue ? "Open" : "Pending";
        }

        return normalized.ToLowerInvariant() switch
        {
            "succeeded" => "Paid",
            "settled" => "Paid",
            "open" => "Open",
            "unpaid" => "Open",
            "void" => "Voided",
            _ => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(normalized)
        };
    }

    public static string GetInvoiceMethodLabel(PaymentInvoiceRecord invoice, BillingProfileRecord? profile)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        if (!string.IsNullOrWhiteSpace(invoice.PaymentMethod))
        {
            return invoice.PaymentMethod.Trim();
        }

        if (!string.IsNullOrWhiteSpace(profile?.DefaultPaymentMethod))
        {
            return profile.DefaultPaymentMethod.Trim();
        }

        return "Not set";
    }

    public static string GetPlanLabel(BillingSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!string.IsNullOrWhiteSpace(snapshot.Profile?.PlanName))
        {
            return snapshot.Profile.PlanName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(snapshot.Profile?.ProviderName))
        {
            return snapshot.Profile.ProviderName.Trim();
        }

        var providerName = snapshot.Invoices
            .Select(invoice => invoice.ProviderName?.Trim())
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        return string.IsNullOrWhiteSpace(providerName) ? "Not connected" : providerName;
    }

    public static string GetPlanDetail(BillingSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!string.IsNullOrWhiteSpace(snapshot.Profile?.BillingCycle))
        {
            return snapshot.Profile.BillingCycle.Trim();
        }

        if (!string.IsNullOrWhiteSpace(snapshot.Profile?.ProviderName))
        {
            return snapshot.Profile.ProviderName.Trim();
        }

        return snapshot.HasData ? "Persisted locally" : "Local billing store";
    }

    public static string FormatAmount(decimal amount, string? currencyCode)
    {
        var normalizedCurrency = NormalizeCurrency(currencyCode);
        if (string.Equals(normalizedCurrency, "USD", StringComparison.OrdinalIgnoreCase))
        {
            return amount.ToString("C", CultureInfo.CurrentCulture);
        }

        return amount.ToString("0.00", CultureInfo.CurrentCulture) + " " + normalizedCurrency;
    }

    private static DateTime GetInvoiceDueDate(PaymentInvoiceRecord invoice, DateTime today)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        if (invoice.DueDate is DateTime dueDate)
        {
            return dueDate.Date;
        }

        return invoice.IssuedOn == default ? today.Date : invoice.IssuedOn.Date;
    }

    private static bool IsSettledInvoice(PaymentInvoiceRecord invoice)
    {
        if (invoice.PaidOn.HasValue)
        {
            return true;
        }

        var status = invoice.Status.Trim();
        return status.Equals("paid", StringComparison.OrdinalIgnoreCase)
            || status.Equals("succeeded", StringComparison.OrdinalIgnoreCase)
            || status.Equals("settled", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeCurrency(string? currencyCode)
        => string.IsNullOrWhiteSpace(currencyCode) ? "USD" : currencyCode.Trim().ToUpperInvariant();
}
