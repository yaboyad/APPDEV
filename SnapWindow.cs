using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Label_CRM_demo;

public class SnapWindow : Window
{
    private readonly ScaleTransform scale = new ScaleTransform(0.96, 0.96);
    private readonly TranslateTransform translate = new TranslateTransform(0, 20);
    private FrameworkElement? animatedSurface;
    private bool playedOpen;
    private bool closingAnimated;

    public int OpenDurationMs { get; set; } = 300;
    public int CloseDurationMs { get; set; } = 170;
    public double StartScale { get; set; } = 0.9;
    public double StartOffsetY { get; set; } = 28;

    public SnapWindow()
    {
        ContentRendered += (_, _) =>
        {
            if (playedOpen)
            {
                return;
            }

            playedOpen = true;
            PlaySnapOpen();
        };

        Closing += SnapWindow_Closing;
    }

    private void PlaySnapOpen()
    {
        var surface = EnsureAnimatedSurface();

        BeginAnimation(OpacityProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        translate.BeginAnimation(TranslateTransform.YProperty, null);

        Opacity = 0;
        scale.ScaleX = StartScale;
        scale.ScaleY = StartScale;
        translate.Y = StartOffsetY;

        if (surface is not null)
        {
            surface.Opacity = 1;
        }

        var ease = new BackEase { Amplitude = 0.35, EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(OpenDurationMs);

        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(StartScale, 1, duration) { EasingFunction = ease });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(StartScale, 1, duration) { EasingFunction = ease });
        translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(StartOffsetY, 0, duration) { EasingFunction = ease });
    }

    private void SnapWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (closingAnimated || !IsLoaded)
        {
            return;
        }

        var surface = EnsureAnimatedSurface();
        if (surface is null)
        {
            return;
        }

        closingAnimated = true;
        e.Cancel = true;

        BeginAnimation(OpacityProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        translate.BeginAnimation(TranslateTransform.YProperty, null);

        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        var duration = TimeSpan.FromMilliseconds(CloseDurationMs);

        var fade = new DoubleAnimation { To = 0, Duration = duration, EasingFunction = ease };
        fade.Completed += (_, _) =>
        {
            Closing -= SnapWindow_Closing;
            Close();
        };

        BeginAnimation(OpacityProperty, fade);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation { To = StartScale, Duration = duration, EasingFunction = ease });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation { To = StartScale, Duration = duration, EasingFunction = ease });
        translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation { To = StartOffsetY, Duration = duration, EasingFunction = ease });
    }

    private FrameworkElement? EnsureAnimatedSurface()
    {
        if (animatedSurface is not null)
        {
            return animatedSurface;
        }

        animatedSurface = Content as FrameworkElement;
        if (animatedSurface is null)
        {
            return null;
        }

        var transforms = new TransformGroup();
        if (!animatedSurface.RenderTransform.Value.IsIdentity)
        {
            transforms.Children.Add(animatedSurface.RenderTransform);
        }

        transforms.Children.Add(scale);
        transforms.Children.Add(translate);

        animatedSurface.RenderTransformOrigin = new Point(0.5, 0.5);
        animatedSurface.RenderTransform = transforms;

        return animatedSurface;
    }
}
