using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

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

    public static ModuleWindowState CreateAccount(AuthenticatedUser user)
    {
        var supportScope = user.IsMaster ? "All submissions" : "Submission form only";
        var roleNotes = user.IsMaster
            ? "Master access can review every saved support request and keep the full desktop workspace unlocked."
            : "User access keeps the CRM workspace available while routing support requests to the master inbox.";

        return new ModuleWindowState
        {
            WindowKey = "account",
            Title = "Account Workspace",
            Subtitle = $"Signed in as {user.DisplayName}",
            Highlight = roleNotes,
            Footer = user.IsMaster
                ? "Use this space to manage local roles, master-only support review, device trust details, and SQL sync state next."
                : "Use this space to confirm your profile details and submit support requests while the master account handles inbox review.",
            Column1Header = "Field",
            Column2Header = "Value",
            Column3Header = "Status",
            Column4Header = "Notes",
            Metrics = new[]
            {
                new ModuleMetric("Tier", user.TierLabel, user.IsMaster ? "Full oversight" : "Standard workspace"),
                new ModuleMetric("Support", supportScope, user.IsMaster ? "Master review enabled" : "Submission access"),
                new ModuleMetric("Storage", "Encrypted", "DPAPI + local file")
            },
            Rows = CreateAccountRows(user, supportScope)
        };
    }

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

    public static ModuleWindowState CreateContracts() => CreateContracts(null);

    public static ModuleWindowState CreateContracts(IEnumerable<ContractRecord>? contracts)
    {
        var contractList = contracts?
            .OrderBy(contract => contract.ReminderDate ?? contract.StartDate)
            .ThenBy(contract => contract.Title)
            .ToList()
            ?? new List<ContractRecord>();

        var activeCount = contractList.Count(contract => IsActiveContractStatus(contract.Status));
        var reminderCount = contractList.Count(contract => contract.ReminderDate.HasValue && IsWithinNextDays(contract.ReminderDate.Value, 14));
        var clientCount = contractList
            .Select(contract => contract.ClientName.Trim())
            .Where(clientName => !string.IsNullOrWhiteSpace(clientName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        return new ModuleWindowState
        {
            WindowKey = "contracts",
            Title = "Contracts Workspace",
            Subtitle = contractList.Count == 0
                ? "No contracts saved yet"
                : $"{contractList.Count} contract(s) saved in the local workspace",
            Highlight = contractList.Count == 0
                ? "This page was wired back up and is ready to open. Save a contract on the dashboard and it will appear here automatically."
                : activeCount == 0
                    ? "Contracts are saved locally and ready for review. Mark one Active or Pending on the dashboard when you want it to surface as live work."
                    : "Active and pending agreements stay visible here so the dashboard can remain focused on the overall CRM pulse.",
            Footer = "Use the dashboard editor to add or update contracts. This focused window now opens correctly and mirrors the current local store.",
            Column1Header = "Client",
            Column2Header = "Type",
            Column3Header = "Start",
            Column4Header = "Status",
            Metrics = new[]
            {
                new ModuleMetric("Saved", contractList.Count.ToString(CultureInfo.InvariantCulture), clientCount == 0 ? "No client records yet" : $"{clientCount} client account(s)"),
                new ModuleMetric("Active", activeCount.ToString(CultureInfo.InvariantCulture), activeCount == 0 ? "Nothing in motion" : "Active or pending now"),
                new ModuleMetric("Reminders", reminderCount.ToString(CultureInfo.InvariantCulture), reminderCount == 0 ? "No near-term reminders" : "Due within 14 days")
            },
            Rows = contractList
                .Take(8)
                .Select(contract => new ModuleRow(
                    OrFallback(contract.ClientName, "Unknown client"),
                    OrFallback(contract.ContractType, "Contract"),
                    contract.StartDate.ToString("MMM dd", CultureInfo.CurrentCulture),
                    OrFallback(contract.Status, "Draft")))
                .ToArray()
        };
    }

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

    public static ModuleWindowState CreateContacts() => CreateContacts(null);

    public static ModuleWindowState CreateContacts(IEnumerable<ContactRecord>? contacts)
    {
        var contactList = contacts?
            .OrderBy(contact => contact.FullName)
            .ThenBy(contact => contact.Company)
            .ToList()
            ?? new List<ContactRecord>();

        var followUpCount = contactList.Count(contact => contact.FollowUpDate.HasValue && IsWithinNextDays(contact.FollowUpDate.Value, 7));
        var companyCount = contactList
            .Select(contact => contact.Company.Trim())
            .Where(company => !string.IsNullOrWhiteSpace(company))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var reachableCount = contactList.Count(HasContactMethod);

        return new ModuleWindowState
        {
            WindowKey = "contacts",
            Title = "Contacts Workspace",
            Subtitle = contactList.Count == 0
                ? "No contacts saved yet"
                : $"{contactList.Count} contact(s) saved in the local workspace",
            Highlight = contactList.Count == 0
                ? "This page now opens correctly. Save a contact on the dashboard and it will show up here with the next follow-up date."
                : "Keep contacts in a focused window when you want quick outreach context without scrolling through the full dashboard.",
            Footer = "Use the dashboard editor to maintain contact details. This window now launches reliably and reflects your current local records.",
            Column1Header = "Contact",
            Column2Header = "Company",
            Column3Header = "Follow-up",
            Column4Header = "Reach",
            Metrics = new[]
            {
                new ModuleMetric("Saved", contactList.Count.ToString(CultureInfo.InvariantCulture), companyCount == 0 ? "Independent contacts only" : $"{companyCount} company record(s)"),
                new ModuleMetric("Follow-ups", followUpCount.ToString(CultureInfo.InvariantCulture), followUpCount == 0 ? "Nothing due this week" : "Due within 7 days"),
                new ModuleMetric("Reachability", reachableCount.ToString(CultureInfo.InvariantCulture), "Phone or email on file")
            },
            Rows = contactList
                .Take(8)
                .Select(contact => new ModuleRow(
                    OrFallback(contact.FullName, "Unnamed contact"),
                    OrFallback(contact.Company, "Independent"),
                    contact.FollowUpDate?.ToString("MMM dd", CultureInfo.CurrentCulture) ?? "Not scheduled",
                    GetReachLabel(contact)))
                .ToArray()
        };
    }

    public static ModuleWindowState CreateSmsManager() => CreateContacts();

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

    public static ModuleWindowState CreateSupport(AuthenticatedUser user) => CreateSupport(user, null);

    public static ModuleWindowState CreateSupport(AuthenticatedUser user, IEnumerable<SupportSubmissionRecord>? submissions)
    {
        var submissionList = submissions?
            .OrderByDescending(submission => submission.CreatedAt)
            .ToList()
            ?? new List<SupportSubmissionRecord>();

        var urgentCount = submissionList.Count(submission => submission.IsUrgent);
        var latestSubmission = submissionList.FirstOrDefault();
        var accessLabel = user.IsMaster ? "Master inbox" : "Submit only";

        return new ModuleWindowState
        {
            WindowKey = "support",
            Title = "Support Workspace",
            Subtitle = user.IsMaster
                ? "Review saved support requests across every account"
                : "Track the support requests saved for this account",
            Highlight = latestSubmission is null
                ? user.IsMaster
                    ? "The master inbox is clear right now. This page now opens correctly and will populate as soon as users submit requests."
                    : "No support requests have been saved for this account yet. This page now opens correctly and is ready for your next submission."
                : user.IsMaster
                    ? $"Latest request: {latestSubmission.Channel} from {latestSubmission.SubmittedByLabel} at {latestSubmission.CreatedAt:MMM dd, h:mm tt}."
                    : $"Latest request saved to the master inbox: {latestSubmission.Channel} at {latestSubmission.CreatedAt:MMM dd, h:mm tt}.",
            Footer = user.IsMaster
                ? "Master accounts can review every saved submission here while users continue sending requests from their dashboard."
                : "Send requests from the dashboard composer. This focused window now opens reliably and summarizes what has already been saved.",
            Column1Header = user.IsMaster ? "Account" : "Sender",
            Column2Header = "Lane",
            Column3Header = "Received",
            Column4Header = "Priority",
            Metrics = new[]
            {
                new ModuleMetric("Access", accessLabel, user.IsMaster ? "All accounts visible" : "Reviewed by master"),
                new ModuleMetric("Saved", submissionList.Count.ToString(CultureInfo.InvariantCulture), submissionList.Count == 0 ? "Inbox clear" : "Stored locally"),
                new ModuleMetric("Urgent", urgentCount.ToString(CultureInfo.InvariantCulture), urgentCount == 0 ? "No urgent requests" : "Needs quick attention")
            },
            Rows = submissionList
                .Take(8)
                .Select(submission => new ModuleRow(
                    user.IsMaster ? submission.SubmittedByLabel : "You",
                    OrFallback(submission.Channel, "General"),
                    submission.CreatedAt.ToString("MMM dd, h:mm tt", CultureInfo.CurrentCulture),
                    submission.PriorityLabel))
                .ToArray()
        };
    }

    private static IReadOnlyList<ModuleRow> CreateAccountRows(AuthenticatedUser user, string supportScope)
    {
        var rows = new List<ModuleRow>
        {
            new ModuleRow("Username", user.Username, "Active", "Primary local login"),
            new ModuleRow("Display Name", user.DisplayName, "Synced", "Shown across the dashboard"),
            new ModuleRow("Account Tier", user.TierLabel, user.IsMaster ? "Master" : "User", supportScope)
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

        rows.Add(new ModuleRow("Support Access", supportScope, user.IsMaster ? "Global inbox" : "Submit only", user.IsMaster ? "You can review every saved request." : "Only the master account can review saved requests."));
        rows.Add(new ModuleRow("Credential Store", "Current User", "Protected", "Readable only for this Windows user"));
        return rows;
    }

    private static bool HasContactMethod(ContactRecord contact)
        => !string.IsNullOrWhiteSpace(contact.Email) || !string.IsNullOrWhiteSpace(contact.PhoneNumber);

    private static string GetReachLabel(ContactRecord contact)
    {
        if (!string.IsNullOrWhiteSpace(contact.Email))
        {
            return contact.Email.Trim();
        }

        if (!string.IsNullOrWhiteSpace(contact.PhoneNumber))
        {
            return contact.PhoneNumber.Trim();
        }

        return "Needs contact info";
    }

    private static bool IsActiveContractStatus(string status)
        => string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "Expiring", StringComparison.OrdinalIgnoreCase);

    private static bool IsWithinNextDays(DateTime value, int days)
        => value.Date >= DateTime.Today && value.Date <= DateTime.Today.AddDays(days);

    private static string OrFallback(string value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}

public sealed record ModuleMetric(string Label, string Value, string Detail);

public sealed record ModuleRow(string Column1, string Column2, string Column3, string Column4);
