using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Label_CRM_demo;

public static class UiAnimator
{
    private static readonly HashSet<FrameworkElement> HoverBoundElements = new();

    public static void PlayEntrance(IEnumerable<FrameworkElement> elements, double offsetY = 24, int staggerMs = 85, double startScale = 0.98)
    {
        var index = 0;

        foreach (var element in elements)
        {
            if (element is null)
            {
                continue;
            }

            var transforms = EnsureTransforms(element);
            var scale = (ScaleTransform)transforms.Children[0];
            var translate = (TranslateTransform)transforms.Children[1];

            element.Opacity = 0;
            scale.ScaleX = startScale;
            scale.ScaleY = startScale;
            translate.Y = offsetY;

            var beginTime = TimeSpan.FromMilliseconds(index * staggerMs);
            var duration = TimeSpan.FromMilliseconds(520);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, duration)
            {
                BeginTime = beginTime,
                EasingFunction = ease
            });

            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(offsetY, 0, duration)
            {
                BeginTime = beginTime,
                EasingFunction = ease
            });

            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(startScale, 1, duration)
            {
                BeginTime = beginTime,
                EasingFunction = ease
            });

            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(startScale, 1, duration)
            {
                BeginTime = beginTime,
                EasingFunction = ease
            });

            index++;
        }
    }

    public static void AttachHoverLift(IEnumerable<FrameworkElement> elements, double hoverOffsetY = -6, double hoverScale = 1.01)
    {
        foreach (var element in elements)
        {
            AttachHoverLift(element, hoverOffsetY, hoverScale);
        }
    }

    public static void AttachHoverLift(FrameworkElement? element, double hoverOffsetY = -6, double hoverScale = 1.01)
    {
        if (element is null || !HoverBoundElements.Add(element))
        {
            return;
        }

        element.MouseEnter += (_, _) => AnimateHoverState(element, hoverOffsetY, hoverScale);
        element.MouseLeave += (_, _) => AnimateHoverState(element, 0, 1);
    }

    public static void PlayLogoReveal(FrameworkElement element)
    {
        var transforms = EnsureLogoTransforms(element);
        var scale = (ScaleTransform)transforms.Children[0];
        var rotate = (RotateTransform)transforms.Children[1];
        var translate = (TranslateTransform)transforms.Children[2];

        element.Opacity = 0;
        scale.ScaleX = 0.72;
        scale.ScaleY = 0.72;
        rotate.Angle = -14;
        translate.Y = 28;

        var settleEase = new BackEase { Amplitude = 0.45, EasingMode = EasingMode.EaseOut };
        var spinEase = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(720);

        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(360))
        {
            EasingFunction = spinEase
        });

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.72, 1, duration)
        {
            EasingFunction = settleEase
        });

        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.72, 1, duration)
        {
            EasingFunction = settleEase
        });

        rotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(-14, 0, TimeSpan.FromMilliseconds(640))
        {
            EasingFunction = spinEase
        });

        var translateAnimation = new DoubleAnimation(28, 0, duration)
        {
            EasingFunction = settleEase
        };

        translateAnimation.Completed += (_, _) => StartLogoFloat(scale, translate);
        translate.BeginAnimation(TranslateTransform.YProperty, translateAnimation);
    }

    public static void Shake(FrameworkElement element)
    {
        var transforms = EnsureTransforms(element);
        var translate = (TranslateTransform)transforms.Children[1];

        var animation = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(360)
        };

        animation.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(0.0)));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(-10, KeyTime.FromPercent(0.16)));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(9, KeyTime.FromPercent(0.32)));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(-6, KeyTime.FromPercent(0.48)));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(4, KeyTime.FromPercent(0.64)));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(-2, KeyTime.FromPercent(0.8)));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1.0)));

        translate.BeginAnimation(TranslateTransform.XProperty, animation);
    }

    private static void AnimateHoverState(FrameworkElement element, double translateY, double scaleTo)
    {
        var transforms = EnsureTransforms(element);
        var scale = (ScaleTransform)transforms.Children[0];
        var translate = (TranslateTransform)transforms.Children[1];
        var duration = TimeSpan.FromMilliseconds(180);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(translateY, duration)
        {
            EasingFunction = ease
        });

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(scaleTo, duration)
        {
            EasingFunction = ease
        });

        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(scaleTo, duration)
        {
            EasingFunction = ease
        });
    }

    private static void StartLogoFloat(ScaleTransform scale, TranslateTransform translate)
    {
        var loopDuration = TimeSpan.FromSeconds(3.2);
        var ease = new SineEase { EasingMode = EasingMode.EaseInOut };

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, 1.025, loopDuration)
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = ease
        });

        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, 1.025, loopDuration)
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = ease
        });

        translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -6, loopDuration)
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = ease
        });
    }

    private static TransformGroup EnsureTransforms(FrameworkElement element)
    {
        if (element.RenderTransform is TransformGroup existingGroup
            && existingGroup.Children.Count == 2
            && existingGroup.Children[0] is ScaleTransform
            && existingGroup.Children[1] is TranslateTransform)
        {
            return existingGroup;
        }

        var group = new TransformGroup();
        group.Children.Add(new ScaleTransform(1, 1));
        group.Children.Add(new TranslateTransform());

        element.RenderTransformOrigin = new Point(0.5, 0.5);
        element.RenderTransform = group;

        return group;
    }

    private static TransformGroup EnsureLogoTransforms(FrameworkElement element)
    {
        if (element.RenderTransform is TransformGroup existingGroup
            && existingGroup.Children.Count == 3
            && existingGroup.Children[0] is ScaleTransform
            && existingGroup.Children[1] is RotateTransform
            && existingGroup.Children[2] is TranslateTransform)
        {
            return existingGroup;
        }

        var group = new TransformGroup();
        group.Children.Add(new ScaleTransform(1, 1));
        group.Children.Add(new RotateTransform(0));
        group.Children.Add(new TranslateTransform());

        element.RenderTransformOrigin = new Point(0.5, 0.5);
        element.RenderTransform = group;

        return group;
    }
}
