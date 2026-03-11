using System;

namespace Label_CRM_demo.Models;

public sealed class CalendarSyncSettings
{
    public GoogleCalendarConnection Google { get; set; } = new();

    public AppleCalendarConnection Apple { get; set; } = new();
}

public sealed class GoogleCalendarConnection
{
    public string ClientId { get; set; } = string.Empty;

    public string CalendarId { get; set; } = "primary";

    public string AccessToken { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public DateTimeOffset AccessTokenExpiresUtc { get; set; }

    public string AccountEmail { get; set; } = string.Empty;

    public DateTimeOffset? LastPullUtc { get; set; }

    public DateTimeOffset? LastPushUtc { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId);

    public bool IsConnected => !string.IsNullOrWhiteSpace(RefreshToken);
}

public sealed class AppleCalendarConnection
{
    public string AppleId { get; set; } = string.Empty;

    public string AppSpecificPassword { get; set; } = string.Empty;

    public string CalendarHref { get; set; } = string.Empty;

    public string CalendarName { get; set; } = string.Empty;

    public DateTimeOffset? LastPullUtc { get; set; }

    public DateTimeOffset? LastPushUtc { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AppleId) &&
        !string.IsNullOrWhiteSpace(AppSpecificPassword);

    public bool IsConnected => IsConfigured && !string.IsNullOrWhiteSpace(CalendarHref);
}
