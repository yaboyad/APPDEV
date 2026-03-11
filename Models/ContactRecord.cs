using System;

namespace Label_CRM_demo.Models;

public sealed class ContactRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string OwnerUsername { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime? FollowUpDate { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
