using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Label_CRM_demo;

public static class WindowManager
{
    private static readonly Dictionary<object, Window> OpenWindows = new Dictionary<object, Window>();

    public static void ShowOrFocus<T>(Window? owner = null) where T : Window, new()
        => ShowOrFocus(typeof(T), () => new T(), owner);

    public static void ShowOrFocus(object key, Func<Window> factory, Window? owner = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(factory);

        if (OpenWindows.TryGetValue(key, out var existing) && existing is not null)
        {
            FocusWindow(existing);
            return;
        }

        var window = factory();
        ConfigureOwner(window, owner);

        window.Closed += (_, _) => OpenWindows.Remove(key);

        OpenWindows[key] = window;
        window.Show();
        FocusWindow(window);
    }

    public static void CloseAll()
    {
        foreach (var window in OpenWindows.Values.ToList())
        {
            try
            {
                window.Close();
            }
            catch
            {
                // Keep shutdown resilient even if one child window fails.
            }
        }

        OpenWindows.Clear();
    }

    private static void ConfigureOwner(Window window, Window? owner)
    {
        if (owner is null)
        {
            if (window.WindowStartupLocation == WindowStartupLocation.Manual)
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            return;
        }

        window.Owner = owner;
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
    }

    private static void FocusWindow(Window window)
    {
        if (!window.IsVisible)
        {
            window.Show();
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
        window.Topmost = true;
        window.Topmost = false;
        window.Focus();
    }
}
