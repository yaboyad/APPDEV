using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Label_CRM_demo
{
    public class SnapWindow : Window
    {
        private readonly ScaleTransform _scale = new ScaleTransform(0.92, 0.92);
        private bool _playedOpen;
        private bool _closingAnimated;

        public int OpenDurationMs { get; set; } = 180;
        public int CloseDurationMs { get; set; } = 140;
        public double StartScale { get; set; } = 0.92;

        public SnapWindow()
        {
            RenderTransformOrigin = new Point(0.5, 0.5);
            RenderTransform = _scale;

            ContentRendered += (_, __) =>
            {
                if (_playedOpen) return;
                _playedOpen = true;
                PlaySnapOpen();
            };

            Closing += SnapWindow_Closing;
        }

        private void PlaySnapOpen()
        {
            // kill old anims
            BeginAnimation(OpacityProperty, null);
            _scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            _scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

            Opacity = 0;
            _scale.ScaleX = StartScale;
            _scale.ScaleY = StartScale;

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(OpenDurationMs))
            { EasingFunction = ease };

            var scale = new DoubleAnimation(StartScale, 1, TimeSpan.FromMilliseconds(OpenDurationMs))
            { EasingFunction = ease };

            BeginAnimation(OpacityProperty, fade);
            _scale.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
            _scale.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
        }

        private void SnapWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (_closingAnimated) return;
            if (!IsLoaded) return;

            _closingAnimated = true;
            e.Cancel = true;

            BeginAnimation(OpacityProperty, null);
            _scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            _scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

            var ease = new CubicEase { EasingMode = EasingMode.EaseIn };

            var fade = new DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(CloseDurationMs), EasingFunction = ease };
            var scale = new DoubleAnimation { To = StartScale, Duration = TimeSpan.FromMilliseconds(CloseDurationMs), EasingFunction = ease };

            fade.Completed += (_, __) =>
            {
                Closing -= SnapWindow_Closing;
                Close();
            };

            BeginAnimation(OpacityProperty, fade);
            _scale.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
            _scale.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
        }
    }
}