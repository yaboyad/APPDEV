using System;

namespace Label_CRM_demo.Models;

public sealed class SupportMessageRow
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public bool IsFromUser { get; init; }

    public string SenderName { get; init; } = string.Empty;

    public string Body { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public string Channel { get; init; } = "General";

    public bool IsUrgent { get; init; }
}
