using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Label_CRM_demo;

public static class WheelScrollAssist
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(WheelScrollAssist),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element)
        => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value)
        => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            element.PreviewMouseWheel += OnPreviewMouseWheel;
            return;
        }

        element.PreviewMouseWheel -= OnPreviewMouseWheel;
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not DependencyObject scope)
        {
            return;
        }

        var scrollViewer = ResolveScrollViewer(scope, e.OriginalSource as DependencyObject);
        if (scrollViewer is null)
        {
            return;
        }

        if (TryScroll(scrollViewer, e.Delta))
        {
            e.Handled = true;
            return;
        }

        var parentScrollViewer = FindParentScrollViewer(scrollViewer);
        while (parentScrollViewer is not null)
        {
            if (TryScroll(parentScrollViewer, e.Delta))
            {
                e.Handled = true;
                return;
            }

            parentScrollViewer = FindParentScrollViewer(parentScrollViewer);
        }
    }

    private static ScrollViewer? ResolveScrollViewer(DependencyObject scope, DependencyObject? originalSource)
    {
        var sourceScrollViewer = FindAncestor<ScrollViewer>(originalSource);
        if (sourceScrollViewer is not null)
        {
            return sourceScrollViewer;
        }

        return scope as ScrollViewer ?? FindDescendant<ScrollViewer>(scope);
    }

    private static bool TryScroll(ScrollViewer scrollViewer, int delta)
    {
        if (scrollViewer.ScrollableHeight <= 0)
        {
            return false;
        }

        var wheelTicks = Math.Max(1d, Math.Abs(delta) / 120d);
        var step = Math.Clamp(scrollViewer.ViewportHeight * 0.16, 20d, 72d) * wheelTicks;
        var requestedOffset = scrollViewer.VerticalOffset - (Math.Sign(delta) * step);
        var clampedOffset = Math.Clamp(requestedOffset, 0d, scrollViewer.ScrollableHeight);

        if (Math.Abs(clampedOffset - scrollViewer.VerticalOffset) < 0.1)
        {
            return false;
        }

        scrollViewer.ScrollToVerticalOffset(clampedOffset);
        return true;
    }

    private static ScrollViewer? FindParentScrollViewer(DependencyObject? child)
    {
        var current = FindParent(child);
        while (current is not null)
        {
            if (current is ScrollViewer scrollViewer)
            {
                return scrollViewer;
            }

            current = FindParent(current);
        }

        return null;
    }

    private static T? FindAncestor<T>(DependencyObject? child)
        where T : DependencyObject
    {
        var current = child;
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = FindParent(current);
        }

        return null;
    }

    private static T? FindDescendant<T>(DependencyObject? parent)
        where T : DependencyObject
    {
        if (parent is null)
        {
            return null;
        }

        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var index = 0; index < childCount; index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
            {
                return match;
            }

            var descendant = FindDescendant<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private static DependencyObject? FindParent(DependencyObject? child)
    {
        if (child is null)
        {
            return null;
        }

        if (child is Visual || child is Visual3D)
        {
            return VisualTreeHelper.GetParent(child);
        }

        if (child is FrameworkContentElement frameworkContent)
        {
            return frameworkContent.Parent;
        }

        if (child is FrameworkElement frameworkElement)
        {
            return frameworkElement.Parent;
        }

        return null;
    }
}
