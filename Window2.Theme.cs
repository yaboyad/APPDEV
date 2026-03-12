using System;
using System.Windows;

namespace Label_CRM_demo;

public partial class Window2
{
    private void InitializeThemeState()
    {
        UpdateThemeToggleButton();
        App.Theme.ThemeChanged += OnThemeChanged;
        Closed += OnDashboardClosed;
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        UpdateThemeToggleButton();
    }

    private void UpdateThemeToggleButton()
    {
        ThemeToggleButton.Content = App.Theme.GetToggleLabel();
    }

    private void ThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        App.Theme.Toggle();
    }

    private void OnDashboardClosed(object? sender, EventArgs e)
    {
        App.Theme.ThemeChanged -= OnThemeChanged;
        Closed -= OnDashboardClosed;
    }
}
