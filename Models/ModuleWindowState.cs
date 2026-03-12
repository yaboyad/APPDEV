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

    public static ModuleWindowState CreateAccountsManager(IEnumerable<ManagedAccountRecord>? accounts)
    {
        var accountList = accounts?
            .OrderByDescending(account => account.IsMaster)
            .ThenBy(account => account.IsBanned)
            .ThenBy(account => account.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList()
            ?? new List<ManagedAccountRecord>();

        var blockedCount = accountList.Count(account => account.IsBanned);
        var activeCount = accountList.Count(account => !account.IsBanned);
        var protectedCount = accountList.Count(account => account.IsMaster);

        return new ModuleWindowState
        {
            WindowKey = "all-accounts",
            Title = "All Accounts Manager",
            Subtitle = accountList.Count == 0
                ? "No saved accounts found in the local credential store"
                : $"{accountList.Count} account(s) available in the local credential store",
            Highlight = accountList.Count == 0
                ? "This workspace is ready, but the local store does not have any saved accounts to manage yet."
                : blockedCount == 0
                    ? "Every saved account can currently sign in. Master accounts remain protected from accidental lockouts."
                    : $"{blockedCount} account(s) are blocked right now. Restore access from the dashboard when they are ready to sign in again.",
            Footer = "Use the dashboard actions to ban or restore access. This focused window keeps the full account list visible beside the main CRM overview.",
            Column1Header = "Account",
            Column2Header = "Username",
            Column3Header = "Tier",
            Column4Header = "Access",
            Metrics = new[]
            {
                new ModuleMetric("Accounts", accountList.Count.ToString(CultureInfo.InvariantCulture), blockedCount == 0 ? "No blocked users" : $"{blockedCount} blocked account(s)"),
                new ModuleMetric("Active", activeCount.ToString(CultureInfo.InvariantCulture), activeCount == 0 ? "No active sign-ins yet" : "Can sign in now"),
                new ModuleMetric("Protected", protectedCount.ToString(CultureInfo.InvariantCulture), protectedCount == 0 ? "No master accounts detected" : "Master access stays protected")
            },
            Rows = accountList
                .Take(10)
                .Select(account => new ModuleRow(
                    OrFallback(account.DisplayName, account.Username),
                    account.Username,
                    account.TierLabel,
                    account.AccessStatus))
                .ToArray()
        };
    }

    public static ModuleWindowState CreatePayments() => CreatePayments(DateTime.Today);

    public static ModuleWindowState CreatePayments(DateTime today)
    {
        var nextChargeDate = GetNextPaymentDueDate(today);
        var recentChargeDates = Enumerable.Range(1, 3)
            .Select(monthOffset => nextChargeDate.AddMonths(-monthOffset))
            .ToList();

        return new ModuleWindowState
        {
            WindowKey = "payments",
            Title = "Payments Workspace",
            Subtitle = $"Recurring billing summary through {nextChargeDate:MMMM yyyy}",
            Highlight = "This workspace now opens with the same billing cadence shown on the dashboard so payment review stays in sync with the rest of the CRM.",
            Footer = "Use this focused window to review recent charges while the dashboard stays anchored on contacts, campaigns, data watch, and support.",
            Column1Header = "Date",
            Column2Header = "Amount",
            Column3Header = "Method",
            Column4Header = "Status",
            Metrics = new[]
            {
                new ModuleMetric("Current Plan", "Creator Pro", "Monthly cycle"),
                new ModuleMetric("Next Charge", "$49.00", $"Due {nextChargeDate:MMM dd, yyyy}"),
                new ModuleMetric("Recent Charges", recentChargeDates.Count.ToString(CultureInfo.InvariantCulture), $"Latest {recentChargeDates.FirstOrDefault():MMM dd}")
            },
            Rows = recentChargeDates
                .Select(chargeDate => new ModuleRow(
                    chargeDate.ToString("MMM dd, yyyy", CultureInfo.CurrentCulture),
                    "$49.00",
                    "Card",
                    "Paid"))
                .ToArray()
        };
    }

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

    public static ModuleWindowState CreateSmsManager() => CreateSmsManager(null);

    public static ModuleWindowState CreateSmsManager(IEnumerable<OutreachCampaignRecord>? campaigns)
        => CreateCampaignWorkspace(
            windowKey: "text-campaigns",
            title: "Text Campaigns Workspace",
            channel: "Text",
            campaigns: campaigns,
            includeSubjectLine: false);

    public static ModuleWindowState CreateEmailManager() => CreateEmailManager(null);

    public static ModuleWindowState CreateEmailManager(IEnumerable<OutreachCampaignRecord>? campaigns)
        => CreateCampaignWorkspace(
            windowKey: "email-campaigns",
            title: "Email Campaigns Workspace",
            channel: "Email",
            campaigns: campaigns,
            includeSubjectLine: true);

    public static ModuleWindowState CreateDataWatch()
    {
        var sources = CreateDataWatchRows();
        var readyCount = sources.Count(source => source.Status.StartsWith("Ready", StringComparison.OrdinalIgnoreCase));

        return new ModuleWindowState
        {
            WindowKey = "data-watch",
            Title = "Data Workspace",
            Subtitle = "Track platform APIs and signal checks from one focused window",
            Highlight = "Keep Instagram, YouTube, X, and the rest of the watchlist together so launch checks stay fast without cluttering the main dashboard.",
            Footer = "Next step: connect the APIs you care about most and swap these staged rows for live pulls from your real sources.",
            Column1Header = "API",
            Column2Header = "Signals",
            Column3Header = "Cadence",
            Column4Header = "Status",
            Metrics = new[]
            {
                new ModuleMetric("Sources", sources.Count.ToString(CultureInfo.InvariantCulture), "Instagram, YouTube, X, and more"),
                new ModuleMetric("Ready", readyCount.ToString(CultureInfo.InvariantCulture), readyCount == sources.Count ? "Every source is staged" : "A few credentials still need setup"),
                new ModuleMetric("Focus", "Audience + reach", "Momentum, mentions, and retention")
            },
            Rows = sources
                .Select(source => new ModuleRow(
                    source.Platform,
                    $"{source.Audience} | {source.Reach}",
                    source.LastUpdate,
                    source.Status))
                .ToArray()
        };
    }

    public static ModuleWindowState CreateDataWatch(IEnumerable<ContactRecord>? contacts)
    {
        var contactList = contacts?
            .OrderBy(contact => contact.FollowUpDate ?? DateTime.MaxValue)
            .ThenBy(contact => contact.FullName)
            .ThenBy(contact => contact.Company)
            .ToList()
            ?? new List<ContactRecord>();

        var followUpCount = contactList.Count(contact => contact.FollowUpDate.HasValue && IsWithinNextDays(contact.FollowUpDate.Value, 7));
        var reachableCount = contactList.Count(HasContactMethod);
        var notedCount = contactList.Count(contact => !string.IsNullOrWhiteSpace(contact.Notes));

        return new ModuleWindowState
        {
            WindowKey = "data-watch",
            Title = "Artist Tracker",
            Subtitle = contactList.Count == 0
                ? "No saved artists in contacts yet"
                : $"{contactList.Count} contact record(s) ready for artist lookup",
            Highlight = contactList.Count == 0
                ? "Save an artist in Contacts and the tracker will surface them here for quick review."
                : "This focused tracker mirrors your saved contacts so you can scan artist names, follow-ups, and reachability without hunting through the full dashboard.",
            Footer = "Use the dashboard artist tracker or contacts editor to update records. This workspace reflects the same local contact store.",
            Column1Header = "Artist",
            Column2Header = "Company",
            Column3Header = "Follow-up",
            Column4Header = "Reach",
            Metrics = new[]
            {
                new ModuleMetric("Tracked", contactList.Count.ToString(CultureInfo.InvariantCulture), notedCount == 0 ? "No notes saved yet" : $"{notedCount} record(s) with notes"),
                new ModuleMetric("Due soon", followUpCount.ToString(CultureInfo.InvariantCulture), followUpCount == 0 ? "Nothing due this week" : "Follow-up within 7 days"),
                new ModuleMetric("Reachable", reachableCount.ToString(CultureInfo.InvariantCulture), "Phone or email on file")
            },
            Rows = contactList
                .Take(10)
                .Select(contact => new ModuleRow(
                    OrFallback(contact.FullName, "Unnamed contact"),
                    OrFallback(contact.Company, "Independent"),
                    contact.FollowUpDate?.ToString("MMM dd", CultureInfo.CurrentCulture) ?? "Not scheduled",
                    GetReachLabel(contact)))
                .ToArray()
        };
    }
    public static IReadOnlyList<SocialPlatformRow> CreateDataWatchRows() => new[]
    {
        new SocialPlatformRow("Instagram Graph API", "Follower growth + saves", "Reels reach + profile taps", "15 min cadence", "Ready to connect"),
        new SocialPlatformRow("YouTube Data API v3", "Subscribers + returning viewers", "Shorts lift + top video velocity", "30 min cadence", "Ready to connect"),
        new SocialPlatformRow("X API v2", "Mentions + reposts", "Conversation spikes + link clicks", "15 min cadence", "Ready to connect"),
        new SocialPlatformRow("TikTok Business API", "Follower growth + watch rate", "Video completion + shares", "Hourly review", "Credential check"),
        new SocialPlatformRow("Spotify Web API", "Follower movement + catalog attention", "Top-track momentum", "Hourly review", "Ready to connect"),
        new SocialPlatformRow("SoundCloud API", "Plays + reposts", "Comment bursts + likes", "Hourly review", "Credential check"),
        new SocialPlatformRow("Bandsintown API", "Event follows + RSVPs", "City-by-city demand", "Daily sweep", "Ready to connect")
    };

    public static ModuleWindowState CreateSupport(AuthenticatedUser user) => CreateSupport(user, null, null);

    public static ModuleWindowState CreateSupport(AuthenticatedUser user, IEnumerable<SupportSubmissionRecord>? submissions)
        => CreateSupport(user, submissions, null);

    public static ModuleWindowState CreateSupport(
        AuthenticatedUser user,
        IEnumerable<SupportSubmissionRecord>? submissions,
        IEnumerable<SupportMessageRow>? conversation)
    {
        if (!user.IsMaster)
        {
            var conversationList = conversation?
                .OrderByDescending(message => message.CreatedAt)
                .ToList()
                ?? new List<SupportMessageRow>();

            var urgentCount = conversationList.Count(message => message.IsUrgent && message.IsFromUser);
            var latestMessage = conversationList.FirstOrDefault();

            return new ModuleWindowState
            {
                WindowKey = "support",
                Title = "Support Workspace",
                Subtitle = conversationList.Count == 0
                    ? "Start a support conversation from the dashboard"
                    : $"{conversationList.Count} saved message(s) in your Titan thread",
                Highlight = latestMessage is null
                    ? "Open the dashboard support center to start a threaded support conversation. Messages and Titan replies will persist locally for this account."
                    : $"Latest thread update from {latestMessage.SenderName}: {CreateSupportPreview(latestMessage.Body)}",
                Footer = "This workspace mirrors the same local support transcript shown on the dashboard while each user message still routes to the master inbox.",
                Column1Header = "Sender",
                Column2Header = "Lane",
                Column3Header = "Received",
                Column4Header = "Preview",
                Metrics = new[]
                {
                    new ModuleMetric("Access", "Threaded", "Titan replies stay in one transcript"),
                    new ModuleMetric("Saved", conversationList.Count.ToString(CultureInfo.InvariantCulture), conversationList.Count == 0 ? "Ready for the first message" : "Stored locally"),
                    new ModuleMetric("Urgent", urgentCount.ToString(CultureInfo.InvariantCulture), urgentCount == 0 ? "No priority items flagged" : "Priority support triggered")
                },
                Rows = conversationList
                    .Take(8)
                    .Select(message => new ModuleRow(
                        OrFallback(message.SenderName, message.IsFromUser ? "You" : "Titan Support"),
                        OrFallback(message.Channel, "General"),
                        message.CreatedAt.ToString("MMM dd, h:mm tt", CultureInfo.CurrentCulture),
                        CreateSupportPreview(message.Body)))
                    .ToArray()
            };
        }

        var submissionList = submissions?
            .OrderByDescending(submission => submission.CreatedAt)
            .ToList()
            ?? new List<SupportSubmissionRecord>();

        var urgentSubmissionCount = submissionList.Count(submission => submission.IsUrgent);
        var latestSubmission = submissionList.FirstOrDefault();

        return new ModuleWindowState
        {
            WindowKey = "support",
            Title = "Support Workspace",
            Subtitle = "Review saved support requests across every account",
            Highlight = latestSubmission is null
                ? "The master inbox is clear right now. This workspace will populate as soon as users submit new requests."
                : $"Latest request: {latestSubmission.Channel} from {latestSubmission.SubmittedByLabel} at {latestSubmission.CreatedAt:MMM dd, h:mm tt}.",
            Footer = "Master accounts can review every saved submission here while users continue sending requests from their dashboard.",
            Column1Header = "Account",
            Column2Header = "Lane",
            Column3Header = "Received",
            Column4Header = "Priority",
            Metrics = new[]
            {
                new ModuleMetric("Access", "Master inbox", "All accounts visible"),
                new ModuleMetric("Saved", submissionList.Count.ToString(CultureInfo.InvariantCulture), submissionList.Count == 0 ? "Inbox clear" : "Stored locally"),
                new ModuleMetric("Urgent", urgentSubmissionCount.ToString(CultureInfo.InvariantCulture), urgentSubmissionCount == 0 ? "No urgent requests" : "Needs quick attention")
            },
            Rows = submissionList
                .Take(8)
                .Select(submission => new ModuleRow(
                    submission.SubmittedByLabel,
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

    private static ModuleWindowState CreateCampaignWorkspace(
        string windowKey,
        string title,
        string channel,
        IEnumerable<OutreachCampaignRecord>? campaigns,
        bool includeSubjectLine)
    {
        var campaignList = campaigns?
            .Where(campaign => string.Equals(campaign.Channel, channel, StringComparison.OrdinalIgnoreCase))
            .OrderBy(campaign => campaign.ScheduledDate)
            .ThenBy(campaign => campaign.CampaignName)
            .ToList()
            ?? new List<OutreachCampaignRecord>();

        var scheduledCount = campaignList.Count(campaign => !IsSentCampaignStatus(campaign.Status));
        var automatedCount = campaignList.Count(campaign => campaign.AutomationEnabled);
        var nextCampaign = campaignList
            .Where(campaign => !IsSentCampaignStatus(campaign.Status))
            .OrderBy(campaign => campaign.ScheduledDate)
            .ThenBy(campaign => campaign.CampaignName)
            .FirstOrDefault();
        var channelLabel = channel.ToLowerInvariant();

        return new ModuleWindowState
        {
            WindowKey = windowKey,
            Title = title,
            Subtitle = campaignList.Count == 0
                ? $"No {channelLabel} campaigns saved yet"
                : $"{campaignList.Count} {channelLabel} campaign(s) saved in the local workspace",
            Highlight = campaignList.Count == 0
                ? $"This workspace now opens correctly. Save a {channelLabel} campaign on the dashboard and it will appear here with its send window and automation state."
                : nextCampaign is null
                    ? $"Every saved {channelLabel} campaign is already marked Sent, so this workspace is acting as the recent history view."
                    : $"The next {channelLabel} send is {nextCampaign.CampaignName} on {nextCampaign.ScheduledDate:MMM dd} during the {nextCampaign.SendWindow.ToLowerInvariant()} window.",
            Footer = $"Use the dashboard editor to create or update {channelLabel} campaigns. This focused window now launches reliably and reflects the current local send plan.",
            Column1Header = "Campaign",
            Column2Header = includeSubjectLine ? "Audience / Subject" : "Audience",
            Column3Header = "Scheduled",
            Column4Header = "Status",
            Metrics = new[]
            {
                new ModuleMetric("Saved", campaignList.Count.ToString(CultureInfo.InvariantCulture), campaignList.Count == 0 ? "No campaigns yet" : "Stored locally"),
                new ModuleMetric("Scheduled", scheduledCount.ToString(CultureInfo.InvariantCulture), scheduledCount == 0 ? "Nothing queued" : "Not marked Sent"),
                new ModuleMetric("Automated", automatedCount.ToString(CultureInfo.InvariantCulture), automatedCount == 0 ? "Manual sends only" : "Automation enabled")
            },
            Rows = campaignList
                .Take(8)
                .Select(campaign => new ModuleRow(
                    OrFallback(campaign.CampaignName, $"{channel} campaign"),
                    BuildCampaignContext(campaign, includeSubjectLine),
                    $"{campaign.ScheduledDate:MMM dd, yyyy} | {OrFallback(campaign.SendWindow, "Morning")}",
                    BuildCampaignStatus(campaign)))
                .ToArray()
        };
    }

    private static string BuildCampaignContext(OutreachCampaignRecord campaign, bool includeSubjectLine)
    {
        var audience = OrFallback(campaign.Audience, "Open audience");
        if (!includeSubjectLine)
        {
            return audience;
        }

        var subject = OrFallback(campaign.SubjectLine, "No subject");
        return $"{audience} | {subject}";
    }

    private static string BuildCampaignStatus(OutreachCampaignRecord campaign)
        => $"{OrFallback(campaign.Status, "Draft")} | {(campaign.AutomationEnabled ? "Auto" : "Manual")}";

    private static bool IsSentCampaignStatus(string status)
        => string.Equals(status, "Sent", StringComparison.OrdinalIgnoreCase);

    private static DateTime GetNextPaymentDueDate(DateTime anchor)
    {
        var dueDate = new DateTime(anchor.Year, anchor.Month, Math.Min(18, DateTime.DaysInMonth(anchor.Year, anchor.Month)));
        if (anchor.Date > dueDate.Date)
        {
            var nextMonth = anchor.AddMonths(1);
            dueDate = new DateTime(nextMonth.Year, nextMonth.Month, Math.Min(18, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month)));
        }

        return dueDate;
    }
    private static bool IsWithinNextDays(DateTime value, int days)
        => value.Date >= DateTime.Today && value.Date <= DateTime.Today.AddDays(days);

    private static string CreateSupportPreview(string body)
    {
        var flattened = string.Join(" ", OrFallback(body, "No message")
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));

        return flattened.Length <= 58
            ? flattened
            : flattened[..55] + "...";
    }

    private static string OrFallback(string value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}

public sealed record ModuleMetric(string Label, string Value, string Detail);

public sealed record ModuleRow(string Column1, string Column2, string Column3, string Column4);

