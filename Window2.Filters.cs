using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using Label_CRM_demo.Models;

namespace Label_CRM_demo;

public partial class Window2
{
    private ICollectionView? managedAccountsView;
    private ICollectionView? contactsView;
    private ICollectionView? contractsView;

    private void InitializeGridFilters()
    {
        managedAccountsView = CollectionViewSource.GetDefaultView(managedAccounts);
        managedAccountsView.Filter = FilterManagedAccountRecord;
        AccountsGrid.ItemsSource = managedAccountsView;

        contactsView = CollectionViewSource.GetDefaultView(contacts);
        contactsView.Filter = FilterContactRecord;
        ContactsGrid.ItemsSource = contactsView;

        contractsView = CollectionViewSource.GetDefaultView(contracts);
        contractsView.Filter = FilterContractRecord;
        ContractsGrid.ItemsSource = contractsView;
    }

    private void AccountsSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        => RefreshAccountsGridFilter();

    private void AccountsAccessFilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => RefreshAccountsGridFilter();

    private void ContactsSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        => RefreshContactsGridFilter();

    private void ContactsFollowUpFilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => RefreshContactsGridFilter();

    private void ContractsSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        => RefreshContractsGridFilter();

    private void ContractsStatusFilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => RefreshContractsGridFilter();

    private void RefreshAccountsGridFilter()
    {
        if (managedAccountsView is null)
        {
            return;
        }

        managedAccountsView.Refresh();

        if (AccountsGrid.SelectedItem is ManagedAccountRecord selectedAccount
            && IsItemVisible(managedAccountsView, selectedAccount))
        {
            ApplySelectedManagedAccount(selectedAccount);
            return;
        }

        var firstVisibleAccount = GetFirstVisibleItem<ManagedAccountRecord>(managedAccountsView);
        AccountsGrid.SelectedItem = firstVisibleAccount;
        ApplySelectedManagedAccount(firstVisibleAccount);
    }

    private void RefreshContactsGridFilter()
    {
        if (contactsView is null)
        {
            return;
        }

        contactsView.Refresh();

        if (ContactsGrid.SelectedItem is ContactRecord selectedContact
            && !IsItemVisible(contactsView, selectedContact))
        {
            ContactsGrid.SelectedItem = null;
        }
    }

    private void RefreshContractsGridFilter()
    {
        if (contractsView is null)
        {
            return;
        }

        contractsView.Refresh();

        if (ContractsGrid.SelectedItem is ContractRecord selectedContract
            && !IsItemVisible(contractsView, selectedContract))
        {
            ContractsGrid.SelectedItem = null;
        }
    }

    private bool FilterManagedAccountRecord(object item)
    {
        if (item is not ManagedAccountRecord account)
        {
            return false;
        }

        return MatchesSearch(
                AccountsSearchBox?.Text,
                account.DisplayName,
                account.Username,
                account.FirstName,
                account.LastName,
                account.Email,
                account.PhoneNumber,
                account.TierLabel,
                account.ContactLabel,
                account.AccessStatus)
            && MatchesManagedAccountAccessFilter(account, GetFilterComboBoxValue(AccountsAccessFilterBox, "All accounts"));
    }

    private bool FilterContactRecord(object item)
    {
        if (item is not ContactRecord contact)
        {
            return false;
        }

        return MatchesSearch(
                ContactsSearchBox?.Text,
                contact.FullName,
                contact.Company,
                contact.PhoneNumber,
                contact.Email,
                contact.Notes,
                contact.FollowUpDate?.ToString("MMM dd, yyyy", CultureInfo.CurrentCulture))
            && MatchesContactFollowUpFilter(contact, GetFilterComboBoxValue(ContactsFollowUpFilterBox, "All contacts"));
    }

    private bool FilterContractRecord(object item)
    {
        if (item is not ContractRecord contract)
        {
            return false;
        }

        return MatchesSearch(
                ContractsSearchBox?.Text,
                contract.Title,
                contract.ClientName,
                contract.ContractType,
                contract.Status,
                contract.Notes,
                contract.StartDate.ToString("MMM dd, yyyy", CultureInfo.CurrentCulture),
                contract.ReminderDate?.ToString("MMM dd, yyyy", CultureInfo.CurrentCulture))
            && MatchesContractStatusFilter(contract, GetFilterComboBoxValue(ContractsStatusFilterBox, "All contracts"));
    }

    private static string GetFilterComboBoxValue(ComboBox? comboBox, string fallback)
    {
        if (comboBox is null)
        {
            return fallback;
        }

        var selectedValue = (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString()?.Trim();
        if (!string.IsNullOrWhiteSpace(selectedValue))
        {
            return selectedValue;
        }

        return string.IsNullOrWhiteSpace(comboBox.Text) ? fallback : comboBox.Text.Trim();
    }

    private static bool MatchesSearch(string? query, params string?[] values)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var trimmedQuery = query.Trim();
        return values.Any(value =>
            !string.IsNullOrWhiteSpace(value)
            && value.Contains(trimmedQuery, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesManagedAccountAccessFilter(ManagedAccountRecord account, string filter)
        => filter switch
        {
            "Active" => !account.IsMaster && !account.IsBanned,
            "Banned" => account.IsBanned,
            "Protected" => account.IsMaster,
            _ => true
        };

    private static bool MatchesContactFollowUpFilter(ContactRecord contact, string filter)
        => filter switch
        {
            "Due soon" => IsWithinNextWeek(contact.FollowUpDate),
            "Has follow-up" => contact.FollowUpDate.HasValue,
            "No follow-up" => !contact.FollowUpDate.HasValue,
            _ => true
        };

    private static bool MatchesContractStatusFilter(ContractRecord contract, string filter)
        => filter switch
        {
            "Draft" => string.Equals(contract.Status, "Draft", StringComparison.OrdinalIgnoreCase),
            "Pending" => string.Equals(contract.Status, "Pending", StringComparison.OrdinalIgnoreCase),
            "Active" => string.Equals(contract.Status, "Active", StringComparison.OrdinalIgnoreCase),
            "Expiring" => string.Equals(contract.Status, "Expiring", StringComparison.OrdinalIgnoreCase),
            "Closed" => string.Equals(contract.Status, "Closed", StringComparison.OrdinalIgnoreCase),
            "Reminder due" => IsWithinNextWeek(contract.ReminderDate),
            _ => true
        };

    private static T? GetFirstVisibleItem<T>(ICollectionView? view)
        where T : class
        => view?.Cast<object>().OfType<T>().FirstOrDefault();

    private static bool IsItemVisible(ICollectionView? view, object item)
        => view is not null && view.Cast<object>().Any(entry => ReferenceEquals(entry, item));
}
