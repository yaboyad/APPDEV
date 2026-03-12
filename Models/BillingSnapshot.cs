using System;
using System.Collections.Generic;

namespace Label_CRM_demo.Models;

public sealed record BillingSnapshot(
    BillingProfileRecord? Profile,
    IReadOnlyList<PaymentInvoiceRecord> Invoices)
{
    public static BillingSnapshot Empty { get; } = new(null, Array.Empty<PaymentInvoiceRecord>());

    public bool HasData => Profile is not null || Invoices.Count > 0;
}
