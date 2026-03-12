using System;

namespace Label_CRM_demo.Models;

public sealed record BillingProfileRecord
{
    public string OwnerUsername { get; init; } = string.Empty;
    public string ProviderName { get; init; } = string.Empty;
    public string ProviderCustomerId { get; init; } = string.Empty;
    public string PlanName { get; init; } = string.Empty;
    public string BillingCycle { get; init; } = string.Empty;
    public string DefaultPaymentMethod { get; init; } = string.Empty;
    public decimal? NextChargeAmount { get; init; }
    public DateTime? NextChargeDate { get; init; }
    public string CurrencyCode { get; init; } = "USD";
    public DateTime UpdatedUtc { get; init; }
}
