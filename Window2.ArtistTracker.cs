using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Label_CRM_demo.Models;

namespace Label_CRM_demo;

public partial class Window2
{
    private readonly ObservableCollection<ContactRecord> artistTrackerRows = new ObservableCollection<ContactRecord>();

    private void InitializeArtistTrackerUi()
    {
        DataWatchGrid.ItemsSource = artistTrackerRows;
        SetArtistTrackerStatus("No contacts saved yet. Add an artist in Contacts to search them here.", SupportNeutralBrush);
        ApplySelectedArtist(null);

        UiAnimator.AttachHoverLift(new FrameworkElement[]
        {
            OpenSelectedArtistButton,
            OpenArtistWorkspaceButton
        }, -4, 1.01);
    }

    private void DataWatchSearch_TextChanged(object sender, TextChangedEventArgs e)
        => RefreshArtistTracker();

    private void DataWatchGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => ApplySelectedArtist(DataWatchGrid.SelectedItem as ContactRecord);

    private void OpenSelectedArtistContact_Click(object sender, RoutedEventArgs e)
    {
        if (DataWatchGrid.SelectedItem is not ContactRecord selectedContact)
        {
            SetArtistTrackerStatus("Pick an artist before opening the contact editor.", SupportUrgentBrush);
            return;
        }

        var targetContact = contacts.FirstOrDefault(contact =>
            string.Equals(contact.Id, selectedContact.Id, StringComparison.OrdinalIgnoreCase));

        if (targetContact is null)
        {
            SetArtistTrackerStatus("That contact is no longer in the local store. Refreshing the tracker now.", SupportUrgentBrush);
            RefreshArtistTracker();
            return;
        }

        ContactsGrid.SelectedItem = targetContact;
        ContactsGrid.ScrollIntoView(targetContact);
        FocusSection(ContactsSection);
        SetArtistTrackerStatus($"{targetContact.FullName} is loaded into the contact editor.", SupportSuccessBrush);
    }

    private void OpenArtistWorkspace_Click(object sender, RoutedEventArgs e)
        => OpenDataWorkspace();

    private void RefreshArtistTracker()
    {
        var searchText = DataWatchSearchBox?.Text?.Trim() ?? string.Empty;
        var selectedId = (DataWatchGrid.SelectedItem as ContactRecord)?.Id;

        var matches = contacts
            .Where(contact => MatchesArtistTrackerQuery(contact, searchText))
            .OrderBy(contact => GetArtistTrackerMatchRank(contact, searchText))
            .ThenBy(contact => contact.FullName)
            .ThenBy(contact => contact.Company)
            .ToList();

        ReplaceCollection(artistTrackerRows, matches);

        if (artistTrackerRows.Count == 0)
        {
            DataWatchGrid.SelectedItem = null;
            ApplySelectedArtist(null);
        }
        else
        {
            var selectedContact = artistTrackerRows.FirstOrDefault(contact =>
                string.Equals(contact.Id, selectedId, StringComparison.OrdinalIgnoreCase))
                ?? artistTrackerRows[0];

            DataWatchGrid.SelectedItem = selectedContact;
            ApplySelectedArtist(selectedContact);
        }

        var statusBrush = artistTrackerRows.Count == 0 && !string.IsNullOrWhiteSpace(searchText)
            ? SupportUrgentBrush
            : SupportNeutralBrush;
        SetArtistTrackerStatus(BuildArtistTrackerStatusMessage(searchText, artistTrackerRows.Count, contacts.Count), statusBrush);
    }

    private void ApplySelectedArtist(ContactRecord? contact)
    {
        if (contact is null)
        {
            DataWatchSelectedNameText.Text = "No artist selected";
            DataWatchSelectedMetaText.Text = "Pick a saved contact to review artist details.";
            DataWatchSelectedContactText.Text = "Contact methods will appear here.";
            DataWatchSelectedFollowUpText.Text = "Follow-up timing will appear here.";
            DataWatchSelectedNotesText.Text = "Notes: Save notes on a contact and they will show up here.";
            OpenSelectedArtistButton.IsEnabled = false;
            return;
        }

        DataWatchSelectedNameText.Text = string.IsNullOrWhiteSpace(contact.FullName)
            ? "Unnamed contact"
            : contact.FullName;

        var metaParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(contact.Company))
        {
            metaParts.Add(contact.Company.Trim());
        }

        metaParts.Add($"Updated {contact.UpdatedUtc.ToLocalTime().ToString("MMM dd, h:mm tt", CultureInfo.CurrentCulture)}");
        DataWatchSelectedMetaText.Text = string.Join(" | ", metaParts);
        DataWatchSelectedContactText.Text = BuildArtistContactSummary(contact);
        DataWatchSelectedFollowUpText.Text = contact.FollowUpDate.HasValue
            ? $"Next follow-up: {contact.FollowUpDate.Value.ToString("dddd, MMM dd, yyyy", CultureInfo.CurrentCulture)}"
            : "Next follow-up: Not scheduled";
        DataWatchSelectedNotesText.Text = string.IsNullOrWhiteSpace(contact.Notes)
            ? "Notes: No notes saved for this artist yet."
            : $"Notes: {contact.Notes.Trim()}";
        OpenSelectedArtistButton.IsEnabled = true;
    }

    private static string BuildArtistTrackerStatusMessage(string searchText, int matchCount, int totalContacts)
    {
        if (totalContacts == 0)
        {
            return "No contacts saved yet. Add an artist in Contacts to search them here.";
        }

        if (string.IsNullOrWhiteSpace(searchText))
        {
            return $"Showing {matchCount} saved contact(s). Search by artist name, company, notes, phone, or email.";
        }

        return matchCount == 0
            ? $"No saved contacts match \"{searchText}\" yet."
            : $"Found {matchCount} contact match(es) for \"{searchText}\".";
    }

    private static string BuildArtistContactSummary(ContactRecord contact)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(contact.Email))
        {
            parts.Add(contact.Email.Trim());
        }

        if (!string.IsNullOrWhiteSpace(contact.PhoneNumber))
        {
            parts.Add(contact.PhoneNumber.Trim());
        }

        return parts.Count == 0
            ? "Contact methods: None saved yet"
            : $"Contact methods: {string.Join(" | ", parts)}";
    }

    private static bool MatchesArtistTrackerQuery(ContactRecord contact, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        return ContainsIgnoreCase(contact.FullName, searchText)
            || ContainsIgnoreCase(contact.Company, searchText)
            || ContainsIgnoreCase(contact.Email, searchText)
            || ContainsIgnoreCase(contact.PhoneNumber, searchText)
            || ContainsIgnoreCase(contact.Notes, searchText);
    }

    private static int GetArtistTrackerMatchRank(ContactRecord contact, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return 0;
        }

        if (StartsWithIgnoreCase(contact.FullName, searchText))
        {
            return 0;
        }

        if (StartsWithIgnoreCase(contact.Company, searchText))
        {
            return 1;
        }

        if (ContainsIgnoreCase(contact.FullName, searchText))
        {
            return 2;
        }

        if (ContainsIgnoreCase(contact.Company, searchText))
        {
            return 3;
        }

        if (ContainsIgnoreCase(contact.Notes, searchText))
        {
            return 4;
        }

        if (ContainsIgnoreCase(contact.Email, searchText))
        {
            return 5;
        }

        if (ContainsIgnoreCase(contact.PhoneNumber, searchText))
        {
            return 6;
        }

        return 7;
    }

    private static bool ContainsIgnoreCase(string value, string searchText)
        => !string.IsNullOrWhiteSpace(value)
            && value.Contains(searchText, StringComparison.OrdinalIgnoreCase);

    private static bool StartsWithIgnoreCase(string value, string searchText)
        => !string.IsNullOrWhiteSpace(value)
            && value.StartsWith(searchText, StringComparison.OrdinalIgnoreCase);

    private void SetArtistTrackerStatus(string message, Brush brush)
    {
        ArtistTrackerStatusText.Text = message;
        ArtistTrackerStatusText.Foreground = brush;
    }
}
