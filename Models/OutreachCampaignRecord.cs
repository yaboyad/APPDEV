using System;

namespace Label_CRM_demo.Models;

public sealed class OutreachCampaignRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string OwnerUsername { get; set; } = string.Empty;
    public string Channel { get; set; } = "Text";
    public string CampaignName { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SubjectLine { get; set; } = string.Empty;
    public string MessageBody { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; } = DateTime.Today;
    public string SendWindow { get; set; } = "Morning";
    public string Status { get; set; } = "Draft";
    public bool AutomationEnabled { get; set; } = true;
    public string Notes { get; set; } = string.Empty;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
