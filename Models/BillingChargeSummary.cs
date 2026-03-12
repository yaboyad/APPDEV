using System;

namespace Label_CRM_demo.Models;

public sealed record BillingChargeSummary(decimal? Amount, string CurrencyCode, DateTime DueDate);
