using System;

namespace Label_CRM_demo.Models;

public sealed class ContractRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string OwnerUsername { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string ContractType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartDate { get; set; } = DateTime.Today;
    public DateTime? ReminderDate { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
