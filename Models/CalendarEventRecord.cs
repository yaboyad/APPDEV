using System;
using System.Text.Json.Serialization;

namespace Label_CRM_demo.Models;

public sealed class CalendarEventRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string ExternalUid { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Category { get; set; } = "Task";

    public string Description { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public DateTimeOffset Start { get; set; }

    public DateTimeOffset End { get; set; }

    public string Source { get; set; } = "Local";

    public string GoogleEventId { get; set; } = string.Empty;

    public string AppleEventHref { get; set; } = string.Empty;

    public DateTimeOffset LastModifiedUtc { get; set; } = DateTimeOffset.UtcNow;

    [JsonIgnore]
    public string StartDisplay => Start.ToLocalTime().ToString("MMM dd, yyyy h:mm tt");

    [JsonIgnore]
    public string EndDisplay => End.ToLocalTime().ToString("MMM dd, yyyy h:mm tt");

    [JsonIgnore]
    public string SyncTargetsDisplay
    {
        get
        {
            var hasGoogle = !string.IsNullOrWhiteSpace(GoogleEventId);
            var hasApple = !string.IsNullOrWhiteSpace(AppleEventHref);

            return (hasGoogle, hasApple) switch
            {
                (true, true) => "Google + Apple",
                (true, false) => "Google",
                (false, true) => "Apple",
                _ => "Local only"
            };
        }
    }
}
