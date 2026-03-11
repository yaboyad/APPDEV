using System;
using System.Collections.Generic;

namespace Label_CRM_demo.Models;

public sealed class ModuleWindowState
{
    public string WindowKey { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string Highlight { get; init; } = string.Empty;
    public string Footer { get; init; } = string.Empty;
    public string Column1Header { get; init; } = "Column 1";
    public string Column2Header { get; init; } = "Column 2";
    public string Column3Header { get; init; } = "Column 3";
    public string Column4Header { get; init; } = "Column 4";
    public IReadOnlyList<ModuleMetric> Metrics { get; init; } = Array.Empty<ModuleMetric>();
    public IReadOnlyList<ModuleRow> Rows { get; init; } = Array.Empty<ModuleRow>();

    public static ModuleWindowState CreateAccount(AuthenticatedUser user) => new()
    {
        WindowKey = "account",
        Title = "Account Workspace",
        Subtitle = $"Signed in as {user.DisplayName}",
        Highlight = "Your local admin profile is active and backed by the encrypted credential store on this machine.",
        Footer = "Use this space to surface local profile settings, role controls, device trust details, and SQL sync state next.",
        Column1Header = "Field",
        Column2Header = "Value",
        Column3Header = "Status",
        Column4Header = "Notes",
        Metrics = new[]
        {
            new ModuleMetric("Role", "Administrator", "Full desktop access"),
            new ModuleMetric("Storage", "Encrypted", "DPAPI + local file"),
            new ModuleMetric("Session", "Active", DateTime.Now.ToString("MMM dd, yyyy"))
        },
        Rows = CreateAccountRows(user)
    };

    public static ModuleWindowState CreatePayments() => new()
    {
        WindowKey = "payments",
        Title = "Payments Workspace",
        Subtitle = "Track recurring billing and recent charges",
        Highlight = "This window stays separate from the dashboard so payment work does not interrupt the main overview.",
        Footer = "Next step: connect a real payment source or local database when you are ready.",
        Column1Header = "Invoice",
        Column2Header = "Date",
        Column3Header = "Amount",
        Column4Header = "Status",
        Metrics = new[]
        {
            new ModuleMetric("Current Plan", "Creator Pro", "Monthly cycle"),
            new ModuleMetric("Next Charge", "$49.00", "Due Mar 18"),
            new ModuleMetric("Method", "Card", "Ending in 4242")
        },
        Rows = new[]
        {
            new ModuleRow("INV-1001", "Feb 18", "$49.00", "Paid"),
            new ModuleRow("INV-1000", "Jan 18", "$49.00", "Paid"),
            new ModuleRow("INV-0999", "Dec 18", "$49.00", "Paid")
        }
    };

    public static ModuleWindowState CreateContracts() => new()
    {
        WindowKey = "contracts",
        Title = "Contracts Workspace",
        Subtitle = "Monitor active agreements and launch prep",
        Highlight = "Keep contracts in their own window so the dashboard can stay focused on status and next actions.",
        Footer = "Next step: wire these rows to a local database or document directory.",
        Column1Header = "Client",
        Column2Header = "Type",
        Column3Header = "Start",
        Column4Header = "Status",
        Metrics = new[]
        {
            new ModuleMetric("Active", "2", "Management + split"),
            new ModuleMetric("Pending", "1", "Awaiting signature"),
            new ModuleMetric("Templates", "3", "Ready to use")
        },
        Rows = new[]
        {
            new ModuleRow("Demo Artist", "Management", "Feb 01", "Active"),
            new ModuleRow("Demo Producer", "Split", "Jan 10", "Active"),
            new ModuleRow("Feature Release", "Single", "Mar 14", "Draft")
        }
    };

    public static ModuleWindowState CreateCalendar() => new()
    {
        WindowKey = "calendar",
        Title = "Calendar Workspace",
        Subtitle = "Upcoming calls, tasks, and release checkpoints",
        Highlight = "This pop-out format works well for keeping scheduling visible while your dashboard remains open underneath.",
        Footer = "Next step: sync this with Google Calendar, Outlook, or a local SQLite store.",
        Column1Header = "Date",
        Column2Header = "Event",
        Column3Header = "Type",
        Column4Header = "Owner",
        Metrics = new[]
        {
            new ModuleMetric("This Week", "4", "Scheduled items"),
            new ModuleMetric("Priority", "2", "Need follow-up"),
            new ModuleMetric("View", "Timeline", "Desktop ready")
        },
        Rows = new[]
        {
            new ModuleRow("Mar 12", "Drop Prep Check-in", "Task", "Admin"),
            new ModuleRow("Mar 14", "Artist Outreach", "Call", "Admin"),
            new ModuleRow("Mar 18", "Payment Due", "Billing", "System")
        }
    };

    public static ModuleWindowState CreateSmsManager() => new()
    {
        WindowKey = "sms",
        Title = "SMS Manager",
        Subtitle = "Templates and outbound touch points",
        Highlight = "A dedicated SMS window gives you room to manage templates without crowding the main shell.",
        Footer = "Next step: connect this to Twilio or your preferred local messaging workflow.",
        Column1Header = "Template",
        Column2Header = "Audience",
        Column3Header = "Last Used",
        Column4Header = "Status",
        Metrics = new[]
        {
            new ModuleMetric("Templates", "5", "Ready to send"),
            new ModuleMetric("Queued", "0", "No pending sends"),
            new ModuleMetric("Delivery", "Stable", "Demo mode")
        },
        Rows = new[]
        {
            new ModuleRow("Launch Reminder", "Artists", "Mar 05", "Ready"),
            new ModuleRow("Payment Follow-up", "Clients", "Mar 03", "Ready"),
            new ModuleRow("Check-in", "Warm Leads", "Feb 26", "Draft")
        }
    };

    public static ModuleWindowState CreateEmailManager() => new()
    {
        WindowKey = "email",
        Title = "Email Manager",
        Subtitle = "Templates, scheduling, and send history",
        Highlight = "Email lives in its own workspace so you can build campaigns while the main dashboard remains anchored.",
        Footer = "Next step: add SMTP or API delivery and persist send history locally.",
        Column1Header = "Template",
        Column2Header = "Subject",
        Column3Header = "Last Updated",
        Column4Header = "Status",
        Metrics = new[]
        {
            new ModuleMetric("Drafts", "4", "Editable now"),
            new ModuleMetric("Scheduled", "1", "Next send tomorrow"),
            new ModuleMetric("History", "12", "Tracked locally")
        },
        Rows = new[]
        {
            new ModuleRow("Welcome", "Welcome to Titan", "Mar 08", "Active"),
            new ModuleRow("Release Push", "Your release checklist", "Mar 06", "Scheduled"),
            new ModuleRow("Invoice Notice", "Payment due reminder", "Mar 02", "Active")
        }
    };

    private static IReadOnlyList<ModuleRow> CreateAccountRows(AuthenticatedUser user)
    {
        var rows = new List<ModuleRow>
        {
            new ModuleRow("Username", user.Username, "Active", "Primary local login"),
            new ModuleRow("Display Name", user.DisplayName, "Synced", "Shown across the dashboard")
        };

        if (!string.IsNullOrWhiteSpace(user.FirstName))
        {
            rows.Add(new ModuleRow("First Name", user.FirstName, "Captured", "Stored for signup testing"));
        }

        if (!string.IsNullOrWhiteSpace(user.LastName))
        {
            rows.Add(new ModuleRow("Last Name", user.LastName, "Captured", "Stored for signup testing"));
        }

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            rows.Add(new ModuleRow("Email", user.Email, "Primary", "You can sign in with this"));
        }

        if (!string.IsNullOrWhiteSpace(user.PhoneNumber))
        {
            rows.Add(new ModuleRow("Phone Number", user.PhoneNumber, "Captured", "Ready for SQL mapping"));
        }

        rows.Add(new ModuleRow("Credential Store", "Current User", "Protected", "Readable only for this Windows user"));
        return rows;
    }
}

public sealed record ModuleMetric(string Label, string Value, string Detail);

public sealed record ModuleRow(string Column1, string Column2, string Column3, string Column4);
