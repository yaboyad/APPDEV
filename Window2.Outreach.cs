using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Label_CRM_demo.Models;
using Label_CRM_demo.Services;

namespace Label_CRM_demo;

public partial class Window2
{
    private readonly ObservableCollection<OutreachCampaignRecord> textCampaigns = new ObservableCollection<OutreachCampaignRecord>();
    private readonly ObservableCollection<OutreachCampaignRecord> emailCampaigns = new ObservableCollection<OutreachCampaignRecord>();
    private readonly ObservableCollection<OutreachTrackingRow> outreachTrackingRows = new ObservableCollection<OutreachTrackingRow>();
    private string? editingTextCampaignId;
    private string? editingEmailCampaignId;

    private void InitializeOutreachUi()
    {
        TextCampaignsGrid.ItemsSource = textCampaigns;
        EmailCampaignsGrid.ItemsSource = emailCampaigns;
        OutreachGrid.ItemsSource = outreachTrackingRows;

        ResetTextCampaignEditor();
        ResetEmailCampaignEditor();
        RefreshOutreachSummary();

        UiAnimator.AttachHoverLift(new FrameworkElement[]
        {
            TextCampaignsSection,
            EmailCampaignsSection,
            OutreachTrackerSection
        }, -7, 1.012);

        UiAnimator.AttachHoverLift(new FrameworkElement[]
        {
            ContactsButton,
            SmsButton,
            EmailButton,
            OutreachButton,
            OpenContactsButton,
            OpenSmsButton,
            OpenEmailButton,
            SaveTextCampaignButton,
            ResetTextCampaignButton,
            SaveEmailCampaignButton,
            ResetEmailCampaignButton
        }, -4, 1.01);

        Loaded += (_, _) => UiAnimator.PlayEntrance(new FrameworkElement[]
        {
            TextCampaignsSection,
            EmailCampaignsSection,
            OutreachTrackerSection
        }, 24, 105);
    }

    private void ApplyOutreachCampaigns(IEnumerable<OutreachCampaignRecord> campaigns)
    {
        var campaignList = campaigns.ToList();

        ReplaceCollection(textCampaigns, campaignList
            .Where(campaign => string.Equals(campaign.Channel, "Text", StringComparison.OrdinalIgnoreCase))
            .OrderBy(campaign => campaign.ScheduledDate)
            .ThenBy(campaign => campaign.CampaignName));

        ReplaceCollection(emailCampaigns, campaignList
            .Where(campaign => string.Equals(campaign.Channel, "Email", StringComparison.OrdinalIgnoreCase))
            .OrderBy(campaign => campaign.ScheduledDate)
            .ThenBy(campaign => campaign.CampaignName));

        RefreshOutreachSummary();
    }

    private List<OutreachCampaignRecord> GetAllOutreachCampaigns()
        => textCampaigns
            .Concat(emailCampaigns)
            .OrderBy(campaign => campaign.ScheduledDate)
            .ThenBy(campaign => campaign.Channel)
            .ThenBy(campaign => campaign.CampaignName)
            .ToList();

    private int GetOutreachCampaignCount()
        => textCampaigns.Count + emailCampaigns.Count;

    private int CountUpcomingOutreachCampaigns()
        => GetAllOutreachCampaigns().Count(campaign =>
            !IsSentCampaignStatus(campaign.Status)
            && campaign.ScheduledDate.Date >= DateTime.Today
            && campaign.ScheduledDate.Date <= DateTime.Today.AddDays(7));

    private IEnumerable<ActivityRow> BuildOutreachActivityRows()
    {
        var latestTextCampaign = textCampaigns.OrderByDescending(campaign => campaign.UpdatedUtc).FirstOrDefault();
        var latestEmailCampaign = emailCampaigns.OrderByDescending(campaign => campaign.UpdatedUtc).FirstOrDefault();
        var nextCampaign = GetAllOutreachCampaigns()
            .Where(campaign => !IsSentCampaignStatus(campaign.Status))
            .OrderBy(campaign => campaign.ScheduledDate)
            .ThenBy(campaign => campaign.Channel)
            .FirstOrDefault();

        return new[]
        {
            new ActivityRow(
                "Texts",
                latestTextCampaign is null ? "No text campaigns saved yet" : $"{latestTextCampaign.CampaignName} queued for {latestTextCampaign.Audience}",
                currentUser.DisplayName,
                latestTextCampaign is null ? "Ready" : latestTextCampaign.Status),
            new ActivityRow(
                "Emails",
                latestEmailCampaign is null ? "No email campaigns saved yet" : $"{latestEmailCampaign.CampaignName} queued for {latestEmailCampaign.Audience}",
                currentUser.DisplayName,
                latestEmailCampaign is null ? "Ready" : latestEmailCampaign.Status),
            new ActivityRow(
                "Outreach",
                nextCampaign is null ? "No outreach campaign scheduled" : $"Next send is {nextCampaign.CampaignName}",
                currentUser.DisplayName,
                nextCampaign is null ? "Open" : nextCampaign.ScheduledDate.ToString("MMM dd", CultureInfo.CurrentCulture))
        };
    }

    private void RefreshOutreachSummary()
    {
        var campaigns = GetAllOutreachCampaigns();
        var nextCampaign = campaigns
            .Where(campaign => !IsSentCampaignStatus(campaign.Status))
            .OrderBy(campaign => campaign.ScheduledDate)
            .ThenBy(campaign => campaign.Channel)
            .FirstOrDefault();

        OutreachScheduledCountText.Text = campaigns.Count(campaign => !IsSentCampaignStatus(campaign.Status)).ToString(CultureInfo.InvariantCulture);
        OutreachAutomationCountText.Text = campaigns.Count(campaign => campaign.AutomationEnabled).ToString(CultureInfo.InvariantCulture);
        OutreachSentCountText.Text = campaigns.Count(campaign => IsSentCampaignStatus(campaign.Status)).ToString(CultureInfo.InvariantCulture);
        OutreachNextTouchText.Text = nextCampaign is null
            ? "Nothing queued"
            : $"{nextCampaign.ScheduledDate:MMM dd} · {nextCampaign.Channel}";

        ReplaceCollection(outreachTrackingRows, campaigns
            .OrderBy(campaign => campaign.ScheduledDate)
            .ThenBy(campaign => campaign.Channel)
            .Select(campaign => new OutreachTrackingRow(
                campaign.Channel,
                campaign.CampaignName,
                campaign.Audience,
                campaign.ScheduledDate.ToString("MMM dd, yyyy", CultureInfo.CurrentCulture),
                campaign.AutomationEnabled ? "Automated" : "Manual",
                campaign.Status)));
    }

    private void SetTextCampaignStatus(string message, Brush brush)
    {
        TextCampaignStatusText.Text = message;
        TextCampaignStatusText.Foreground = brush;
    }

    private void SetEmailCampaignStatus(string message, Brush brush)
    {
        EmailCampaignStatusText.Text = message;
        EmailCampaignStatusText.Foreground = brush;
    }

    private void Nav_Contacts_Click(object sender, RoutedEventArgs e)
        => FocusSection(ContactsSection);

    private void Nav_TextCampaigns_Click(object sender, RoutedEventArgs e)
        => FocusSection(TextCampaignsSection);

    private void Nav_EmailCampaigns_Click(object sender, RoutedEventArgs e)
        => FocusSection(EmailCampaignsSection);

    private void Nav_Outreach_Click(object sender, RoutedEventArgs e)
        => FocusSection(OutreachTrackerSection);

    private void OpenContactsDashboard_Click(object sender, RoutedEventArgs e)
        => FocusSection(ContactsSection);

    private void OpenTextCampaigns_Click(object sender, RoutedEventArgs e)
        => FocusSection(TextCampaignsSection);

    private void OpenEmailCampaigns_Click(object sender, RoutedEventArgs e)
        => FocusSection(EmailCampaignsSection);

    private async void SaveTextCampaign_Click(object sender, RoutedEventArgs e)
    {
        var isEditing = !string.IsNullOrWhiteSpace(editingTextCampaignId);
        using var saveTiming = PerformanceInstrumentation.Measure("workspace.save-text-campaign", ("user", currentUser.Username), ("editing", isEditing));

        if (!TryBuildTextCampaign(out var campaign, out var errorMessage))
        {
            saveTiming.Checkpoint("validation-failed", ("reason", errorMessage));
            SetTextCampaignStatus(errorMessage, SupportErrorBrush);
            TextCampaignNameBox.Focus();
            return;
        }

        UpsertCampaign(textCampaigns, campaign);
        await PersistWorkspaceDataAsync();
        saveTiming.Checkpoint("workspace-persisted", ("textCampaigns", textCampaigns.Count), ("emailCampaigns", emailCampaigns.Count));
        ResetTextCampaignEditor(false);
        SetTextCampaignStatus(
            isEditing
                ? $"{campaign.CampaignName} updated and scheduled in the calendar."
                : $"{campaign.CampaignName} saved and scheduled in the calendar.",
            SupportSuccessBrush);
    }

    private void ResetTextCampaign_Click(object sender, RoutedEventArgs e)
    {
        TextCampaignsGrid.SelectedItem = null;
        ResetTextCampaignEditor();
    }

    private void TextCampaignsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TextCampaignsGrid.SelectedItem is not OutreachCampaignRecord selectedCampaign)
        {
            return;
        }

        editingTextCampaignId = selectedCampaign.Id;
        TextCampaignNameBox.Text = selectedCampaign.CampaignName;
        TextAudienceBox.Text = selectedCampaign.Audience;
        SelectComboBoxValue(TextSendWindowBox, selectedCampaign.SendWindow);
        TextScheduledDatePicker.SelectedDate = selectedCampaign.ScheduledDate;
        SelectComboBoxValue(TextCampaignStateBox, selectedCampaign.Status);
        TextAutomationCheckBox.IsChecked = selectedCampaign.AutomationEnabled;
        TextMessageBox.Text = selectedCampaign.MessageBody;
        TextCampaignNotesBox.Text = selectedCampaign.Notes;
        SetTextCampaignStatus($"Editing {selectedCampaign.CampaignName}. Save to update the send plan.", SupportNeutralBrush);
    }

    private async void SaveEmailCampaign_Click(object sender, RoutedEventArgs e)
    {
        var isEditing = !string.IsNullOrWhiteSpace(editingEmailCampaignId);
        using var saveTiming = PerformanceInstrumentation.Measure("workspace.save-email-campaign", ("user", currentUser.Username), ("editing", isEditing));

        if (!TryBuildEmailCampaign(out var campaign, out var errorMessage))
        {
            saveTiming.Checkpoint("validation-failed", ("reason", errorMessage));
            SetEmailCampaignStatus(errorMessage, SupportErrorBrush);
            EmailCampaignNameBox.Focus();
            return;
        }

        UpsertCampaign(emailCampaigns, campaign);
        await PersistWorkspaceDataAsync();
        saveTiming.Checkpoint("workspace-persisted", ("textCampaigns", textCampaigns.Count), ("emailCampaigns", emailCampaigns.Count));
        ResetEmailCampaignEditor(false);
        SetEmailCampaignStatus(
            isEditing
                ? $"{campaign.CampaignName} updated and scheduled in the calendar."
                : $"{campaign.CampaignName} saved and scheduled in the calendar.",
            SupportSuccessBrush);
    }

    private void ResetEmailCampaign_Click(object sender, RoutedEventArgs e)
    {
        EmailCampaignsGrid.SelectedItem = null;
        ResetEmailCampaignEditor();
    }

    private void EmailCampaignsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EmailCampaignsGrid.SelectedItem is not OutreachCampaignRecord selectedCampaign)
        {
            return;
        }

        editingEmailCampaignId = selectedCampaign.Id;
        EmailCampaignNameBox.Text = selectedCampaign.CampaignName;
        EmailAudienceBox.Text = selectedCampaign.Audience;
        EmailSubjectBox.Text = selectedCampaign.SubjectLine;
        SelectComboBoxValue(EmailSendWindowBox, selectedCampaign.SendWindow);
        EmailScheduledDatePicker.SelectedDate = selectedCampaign.ScheduledDate;
        SelectComboBoxValue(EmailCampaignStateBox, selectedCampaign.Status);
        EmailAutomationCheckBox.IsChecked = selectedCampaign.AutomationEnabled;
        EmailMessageBox.Text = selectedCampaign.MessageBody;
        EmailCampaignNotesBox.Text = selectedCampaign.Notes;
        SetEmailCampaignStatus($"Editing {selectedCampaign.CampaignName}. Save to update the send plan.", SupportNeutralBrush);
    }

    private bool TryBuildTextCampaign(out OutreachCampaignRecord campaign, out string errorMessage)
        => TryBuildCampaign(
            channel: "Text",
            editingId: editingTextCampaignId,
            campaignName: TextCampaignNameBox.Text,
            audience: TextAudienceBox.Text,
            subjectLine: string.Empty,
            messageBody: TextMessageBox.Text,
            scheduledDate: TextScheduledDatePicker.SelectedDate,
            sendWindow: GetComboBoxValue(TextSendWindowBox),
            status: GetComboBoxValue(TextCampaignStateBox),
            automationEnabled: TextAutomationCheckBox.IsChecked == true,
            notes: TextCampaignNotesBox.Text,
            requiresSubject: false,
            campaign: out campaign,
            errorMessage: out errorMessage);

    private bool TryBuildEmailCampaign(out OutreachCampaignRecord campaign, out string errorMessage)
        => TryBuildCampaign(
            channel: "Email",
            editingId: editingEmailCampaignId,
            campaignName: EmailCampaignNameBox.Text,
            audience: EmailAudienceBox.Text,
            subjectLine: EmailSubjectBox.Text,
            messageBody: EmailMessageBox.Text,
            scheduledDate: EmailScheduledDatePicker.SelectedDate,
            sendWindow: GetComboBoxValue(EmailSendWindowBox),
            status: GetComboBoxValue(EmailCampaignStateBox),
            automationEnabled: EmailAutomationCheckBox.IsChecked == true,
            notes: EmailCampaignNotesBox.Text,
            requiresSubject: true,
            campaign: out campaign,
            errorMessage: out errorMessage);

    private bool TryBuildCampaign(
        string channel,
        string? editingId,
        string campaignName,
        string audience,
        string subjectLine,
        string messageBody,
        DateTime? scheduledDate,
        string sendWindow,
        string status,
        bool automationEnabled,
        string notes,
        bool requiresSubject,
        out OutreachCampaignRecord campaign,
        out string errorMessage)
    {
        var normalizedCampaignName = campaignName.Trim();
        var normalizedAudience = audience.Trim();
        var normalizedSubjectLine = subjectLine.Trim();
        var normalizedMessageBody = messageBody.Trim();
        var normalizedNotes = notes.Trim();

        if (string.IsNullOrWhiteSpace(normalizedCampaignName))
        {
            errorMessage = $"Add a {channel.ToLowerInvariant()} campaign name before saving.";
            campaign = new OutreachCampaignRecord();
            return false;
        }

        if (string.IsNullOrWhiteSpace(normalizedAudience))
        {
            errorMessage = $"Add the audience or segment for this {channel.ToLowerInvariant()} campaign.";
            campaign = new OutreachCampaignRecord();
            return false;
        }

        if (requiresSubject && string.IsNullOrWhiteSpace(normalizedSubjectLine))
        {
            errorMessage = "Add an email subject line before saving.";
            campaign = new OutreachCampaignRecord();
            return false;
        }

        if (string.IsNullOrWhiteSpace(normalizedMessageBody))
        {
            errorMessage = $"Write the {channel.ToLowerInvariant()} message before saving.";
            campaign = new OutreachCampaignRecord();
            return false;
        }

        if (!scheduledDate.HasValue)
        {
            errorMessage = $"Pick a scheduled date for this {channel.ToLowerInvariant()} campaign.";
            campaign = new OutreachCampaignRecord();
            return false;
        }

        if (string.IsNullOrWhiteSpace(sendWindow))
        {
            errorMessage = "Choose a send window before saving.";
            campaign = new OutreachCampaignRecord();
            return false;
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            errorMessage = "Choose a campaign status before saving.";
            campaign = new OutreachCampaignRecord();
            return false;
        }

        campaign = new OutreachCampaignRecord
        {
            Id = editingId ?? Guid.NewGuid().ToString("N"),
            OwnerUsername = currentUser.Username,
            Channel = channel,
            CampaignName = normalizedCampaignName,
            Audience = normalizedAudience,
            SubjectLine = normalizedSubjectLine,
            MessageBody = normalizedMessageBody,
            ScheduledDate = scheduledDate.Value.Date,
            SendWindow = sendWindow,
            Status = status,
            AutomationEnabled = automationEnabled,
            Notes = normalizedNotes,
            UpdatedUtc = DateTime.UtcNow
        };

        errorMessage = string.Empty;
        return true;
    }

    private void UpsertCampaign(ObservableCollection<OutreachCampaignRecord> target, OutreachCampaignRecord campaign)
    {
        var existingIndex = target
            .Select((item, index) => new { item, index })
            .FirstOrDefault(entry => string.Equals(entry.item.Id, campaign.Id, StringComparison.OrdinalIgnoreCase))
            ?.index;

        if (existingIndex.HasValue)
        {
            target[existingIndex.Value] = campaign;
            return;
        }

        target.Add(campaign);
    }

    private void ResetTextCampaignEditor(bool resetStatus = true)
    {
        editingTextCampaignId = null;
        TextCampaignNameBox.Clear();
        TextAudienceBox.Clear();
        SelectComboBoxValue(TextSendWindowBox, "Morning");
        TextScheduledDatePicker.SelectedDate = DateTime.Today;
        SelectComboBoxValue(TextCampaignStateBox, "Draft");
        TextAutomationCheckBox.IsChecked = true;
        TextMessageBox.Clear();
        TextCampaignNotesBox.Clear();

        if (resetStatus)
        {
            SetTextCampaignStatus("Build a text campaign, schedule it, and it will appear in the calendar automatically.", SupportNeutralBrush);
        }
    }

    private void ResetEmailCampaignEditor(bool resetStatus = true)
    {
        editingEmailCampaignId = null;
        EmailCampaignNameBox.Clear();
        EmailAudienceBox.Clear();
        EmailSubjectBox.Clear();
        SelectComboBoxValue(EmailSendWindowBox, "Morning");
        EmailScheduledDatePicker.SelectedDate = DateTime.Today;
        SelectComboBoxValue(EmailCampaignStateBox, "Draft");
        EmailAutomationCheckBox.IsChecked = true;
        EmailMessageBox.Clear();
        EmailCampaignNotesBox.Clear();

        if (resetStatus)
        {
            SetEmailCampaignStatus("Build an email campaign, schedule it, and it will appear in the calendar automatically.", SupportNeutralBrush);
        }
    }

    private void AddOutreachCalendarEvents(
        IReadOnlyDictionary<string, CalendarEventRecord> existingManagedEvents,
        ICollection<CalendarEventRecord> desiredEvents)
    {
        foreach (var campaign in GetAllOutreachCampaigns())
        {
            var eventId = ManagedCalendarEventPrefix + campaign.Channel.ToLowerInvariant() + "-campaign-" + campaign.Id;
            existingManagedEvents.TryGetValue(eventId, out var existingEvent);
            var (startHour, startMinute, durationMinutes) = GetSendWindowSlot(campaign.SendWindow);

            desiredEvents.Add(CreateManagedEvent(
                eventId,
                existingEvent,
                $"{campaign.CampaignName} {campaign.Channel.ToLowerInvariant()} campaign",
                campaign.Channel + " campaign",
                BuildCampaignDescription(campaign),
                campaign.Audience,
                campaign.ScheduledDate,
                startHour,
                startMinute,
                durationMinutes));
        }
    }

    private static (int StartHour, int StartMinute, int DurationMinutes) GetSendWindowSlot(string sendWindow)
        => sendWindow.Trim() switch
        {
            "Midday" => (12, 0, 45),
            "Afternoon" => (15, 0, 45),
            "Evening" => (18, 0, 45),
            _ => (9, 30, 45)
        };

    private static string BuildCampaignDescription(OutreachCampaignRecord campaign)
    {
        var parts = new List<string>
        {
            $"Channel: {campaign.Channel}",
            $"Audience: {campaign.Audience}",
            $"Send window: {campaign.SendWindow}",
            $"Status: {campaign.Status}",
            $"Automation: {(campaign.AutomationEnabled ? "Enabled" : "Manual review")}" 
        };

        if (!string.IsNullOrWhiteSpace(campaign.SubjectLine))
        {
            parts.Add($"Subject: {campaign.SubjectLine}");
        }

        parts.Add($"Message: {campaign.MessageBody}");

        if (!string.IsNullOrWhiteSpace(campaign.Notes))
        {
            parts.Add($"Notes: {campaign.Notes}");
        }

        return string.Join(Environment.NewLine, parts);
    }

    private static bool IsSentCampaignStatus(string status)
        => string.Equals(status, "Sent", StringComparison.OrdinalIgnoreCase);
}

public sealed record OutreachTrackingRow(
    string Channel,
    string Campaign,
    string Audience,
    string Scheduled,
    string Automation,
    string Status);
