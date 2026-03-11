using System;

namespace Label_CRM_demo.Models;

public sealed class SupportSubmissionRecord
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string SubmittedByUsername { get; init; } = string.Empty;

    public string SubmittedByDisplayName { get; init; } = string.Empty;

    public string SubmittedByEmail { get; init; } = string.Empty;

    public string SubmittedByTier { get; init; } = AccountTiers.User;

    public string Body { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public string Channel { get; init; } = "General";

    public bool IsUrgent { get; init; }

    public string Status { get; init; } = "New";

    public string SubmittedByLabel => string.IsNullOrWhiteSpace(SubmittedByDisplayName)
        ? SubmittedByUsername
        : $"{SubmittedByDisplayName} ({SubmittedByUsername})";

    public string PriorityLabel => IsUrgent ? "Urgent" : "Standard";

    public string TierLabel => AccountTiers.Normalize(SubmittedByTier);

    public string Preview => Body.Length <= 72
        ? Body
        : Body[..69] + "...";
}
