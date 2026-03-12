using System;

namespace Label_CRM_demo.Models;

public sealed record PaymentInvoiceRecord
{
    public string Id { get; init; } = string.Empty;
    public string OwnerUsername { get; init; } = string.Empty;
    public string InvoiceNumber { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string CurrencyCode { get; init; } = "USD";
    public string PaymentMethod { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime IssuedOn { get; init; }
    public DateTime? DueDate { get; init; }
    public DateTime? PaidOn { get; init; }
    public string ProviderName { get; init; } = string.Empty;
    public string ProviderInvoiceId { get; init; } = string.Empty;
    public string HostedInvoiceUrl { get; init; } = string.Empty;
    public DateTime UpdatedUtc { get; init; }
}
