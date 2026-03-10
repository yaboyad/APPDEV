using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Label_CRM_demo
{
    public static class WindowManager
    {
        // Keeps one instance per window type
        private static readonly Dictionary<Type, Window> _open = new Dictionary<Type, Window>();

        /// <summary>
        /// Open window T if not open. If already open, bring it to front ("snap").
        /// </summary>
        public static void ShowOrFocus<T>(Window? owner = null) where T : Window, new()
        {
            var type = typeof(T);

            // If already open -> focus it
            if (_open.TryGetValue(type, out var existing) && existing != null)
            {
                if (!existing.IsVisible)
                {
                    existing.Show();
                }

                if (existing.WindowState == WindowState.Minimized)
                    existing.WindowState = WindowState.Normal;

                existing.Activate();

                // "snap" to front
                existing.Topmost = true;
                existing.Topmost = false;

                existing.Focus();
                return;
            }

            // Not open -> create and show
            var win = new T();

            if (owner != null)
            {
                win.Owner = owner;
                win.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                win.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            // When it closes, remove from dictionary
            win.Closed += (_, __) => _open.Remove(type);

            _open[type] = win;
            win.Show();
            win.Activate();
        }

        /// <summary>
        /// Close all tracked windows (optional helper).
        /// </summary>
        public static void CloseAll()
        {
            foreach (var window in _open.Values.ToList())
            {
                try { window?.Close(); }
                catch { /* ignore */ }
            }

            _open.Clear();
        }
    }
}
