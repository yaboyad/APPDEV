using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Label_CRM_demo.Models;
using Label_CRM_demo.Services;

namespace Label_CRM_demo;

public partial class MainWindow : SnapWindow
{
    private const double CompactLayoutBreakpoint = 1020;
    private const double ShortHeightBreakpoint = 720;
    private static readonly Brush ActiveTabBackground = CreateBrush("#F0B862");
    private static readonly Brush ActiveTabBorder = CreateBrush("#FFD166");
    private static readonly Brush InactiveTabBackground = CreateBrush("#101928");
    private static readonly Brush InactiveTabBorder = CreateBrush("#2A3A66");
    private static readonly Brush ActiveTabForeground = CreateBrush("#07131F");
    private static readonly Brush InactiveTabForeground = CreateBrush("#CBD5E1");
    private readonly CredentialRepository credentials;
    private bool isSignUpMode;

    public MainWindow()
        : this(App.Credentials)
    {
    }

    internal MainWindow(CredentialRepository credentials)
    {
        this.credentials = credentials;
        OpenDurationMs = 460;
        StartScale = 0.82;
        StartOffsetY = 48;
        InitializeComponent();
        InitializeInteractiveStates();

        StoragePathText.Text = credentials.StoragePath;
        StatusText.Text = string.Empty;
        SignUpStatusText.Text = string.Empty;
        SetAuthMode(false);

        SizeChanged += (_, _) => UpdateResponsiveLayout();
        StateChanged += (_, _) => EnsureFullscreenLock();
        Loaded += (_, _) =>
        {
            EnsureFullscreenLock();
            UpdateResponsiveLayout();
            BeginStartupSequence();
        };

        EnsureFullscreenLock();
        UpdateResponsiveLayout();
    }

    private static SolidColorBrush CreateBrush(string hex)
        => (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;

    private void InitializeInteractiveStates()
    {
        UiAnimator.AttachHoverLift(new FrameworkElement[]
        {
            NotesCard,
            LoginCard,
            SignInTabButton,
            SignUpTabButton
        }, -5, 1.008);
    }

    private void BeginStartupSequence()
    {
        UiAnimator.PlayEntrance(new FrameworkElement[] { StartupPanel }, 18, 0, 0.9);
        UiAnimator.PlayLogoReveal(StartupLogo);

        if (StartupPulse.RenderTransform is ScaleTransform pulseScale)
        {
            var pulseEase = new CubicEase { EasingMode = EasingMode.EaseOut };
            pulseScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.12, 1, TimeSpan.FromMilliseconds(650))
            {
                BeginTime = TimeSpan.FromMilliseconds(180),
                EasingFunction = pulseEase
            });
        }

        var overlayFade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(360))
        {
            BeginTime = TimeSpan.FromMilliseconds(1050),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };

        overlayFade.Completed += (_, _) =>
        {
            StartupOverlay.Visibility = Visibility.Collapsed;
            UiAnimator.PlayLogoReveal(LogoMark);
            UiAnimator.PlayEntrance(new FrameworkElement[]
            {
                HeroCopyPanel,
                NotesCard,
                LoginCard
            }, 36, 120, 0.92);
            FocusActiveInput();
        };

        StartupOverlay.BeginAnimation(UIElement.OpacityProperty, overlayFade);
    }

    private void EnsureFullscreenLock()
    {
        if (WindowState == WindowState.Normal)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void UpdateResponsiveLayout()
    {
        var viewportWidth = ShellScrollViewer.ViewportWidth;
        if (viewportWidth <= 0)
        {
            viewportWidth = Math.Max(0, ActualWidth - 160);
        }

        if (viewportWidth <= 0)
        {
            return;
        }

        var isCompactLayout = viewportWidth < CompactLayoutBreakpoint;
        var isShortLayout = ActualHeight > 0 && ActualHeight < ShortHeightBreakpoint;

        ContentBottomRow.Height = isCompactLayout ? GridLength.Auto : new GridLength(0);
        HeroColumn.Width = isCompactLayout ? new GridLength(1, GridUnitType.Star) : new GridLength(1.18, GridUnitType.Star);
        LoginColumn.Width = isCompactLayout ? new GridLength(0) : new GridLength(0.92, GridUnitType.Star);

        Grid.SetRow(HeroSection, 0);
        Grid.SetColumn(HeroSection, 0);
        Grid.SetColumnSpan(HeroSection, isCompactLayout ? 2 : 1);
        HeroSection.Margin = isCompactLayout ? new Thickness(0) : new Thickness(10, 0, 36, 0);

        Grid.SetRow(LoginSection, isCompactLayout ? 1 : 0);
        Grid.SetColumn(LoginSection, isCompactLayout ? 0 : 1);
        Grid.SetColumnSpan(LoginSection, isCompactLayout ? 2 : 1);
        LoginSection.Margin = isCompactLayout ? new Thickness(0, 28, 0, 0) : new Thickness(0);

        HeroPanel.VerticalAlignment = isCompactLayout ? VerticalAlignment.Top : VerticalAlignment.Center;
        LogoMark.HorizontalAlignment = isCompactLayout ? HorizontalAlignment.Center : HorizontalAlignment.Left;

        HeroTitleText.FontSize = isCompactLayout ? 42 : isShortLayout ? 46 : 52;
        HeroSubtitleText.FontSize = isCompactLayout ? 19 : 22;
        HeroDescriptionText.FontSize = isCompactLayout ? 16 : 18;
        HeroDescriptionText.LineHeight = isCompactLayout ? 24 : 27;
        HeroCopyPanel.MaxWidth = isCompactLayout ? 860 : 620;
        NotesCard.MaxWidth = isCompactLayout ? 860 : 620;

        LoginCard.MinWidth = isCompactLayout ? 0 : 420;
        LoginCard.MaxWidth = isCompactLayout ? 760 : 560;
        LoginCard.Padding = isCompactLayout ? new Thickness(28) : new Thickness(32);
        LoginCard.VerticalAlignment = isCompactLayout ? VerticalAlignment.Top : VerticalAlignment.Center;

        NotesBottomRow.Height = isCompactLayout ? GridLength.Auto : new GridLength(0);
        NotesLeftColumn.Width = new GridLength(1, GridUnitType.Star);
        NotesRightColumn.Width = isCompactLayout ? new GridLength(0) : new GridLength(1, GridUnitType.Star);

        Grid.SetRow(ChangesPanel, 0);
        Grid.SetColumn(ChangesPanel, 0);
        Grid.SetColumnSpan(ChangesPanel, isCompactLayout ? 2 : 1);
        ChangesPanel.Margin = isCompactLayout ? new Thickness(0) : new Thickness(0, 0, 12, 0);

        Grid.SetRow(SecurityPanel, isCompactLayout ? 1 : 0);
        Grid.SetColumn(SecurityPanel, isCompactLayout ? 0 : 1);
        Grid.SetColumnSpan(SecurityPanel, 1);
        SecurityPanel.Margin = isCompactLayout ? new Thickness(0, 18, 0, 0) : new Thickness(12, 0, 0, 0);
    }

    private void ShowSignIn_Click(object sender, RoutedEventArgs e) => SetAuthMode(false);

    private void ShowSignUp_Click(object sender, RoutedEventArgs e) => SetAuthMode(true);

    private void SetAuthMode(bool signUpMode)
    {
        isSignUpMode = signUpMode;
        SignInPanel.Visibility = signUpMode ? Visibility.Collapsed : Visibility.Visible;
        SignUpPanel.Visibility = signUpMode ? Visibility.Visible : Visibility.Collapsed;

        SignInTabButton.Background = signUpMode ? InactiveTabBackground : ActiveTabBackground;
        SignInTabButton.BorderBrush = signUpMode ? InactiveTabBorder : ActiveTabBorder;
        SignInTabButton.Foreground = signUpMode ? InactiveTabForeground : ActiveTabForeground;

        SignUpTabButton.Background = signUpMode ? ActiveTabBackground : InactiveTabBackground;
        SignUpTabButton.BorderBrush = signUpMode ? ActiveTabBorder : InactiveTabBorder;
        SignUpTabButton.Foreground = signUpMode ? ActiveTabForeground : InactiveTabForeground;

        AuthBadgeText.Text = signUpMode ? "Encrypted local account setup" : "Encrypted local access";
        AuthTitleText.Text = signUpMode ? "Create Test Account" : "Open Workspace";
        AuthDescriptionText.Text = signUpMode
            ? "Capture first name, last name, phone, email, and password in the same protected local store while you validate the flow before SQL is wired in."
            : "Sign in with your local account to open the Titan dashboard and launch focused workspace windows without losing your place.";

        StatusText.Text = string.Empty;
        SignUpStatusText.Text = string.Empty;

        if (IsLoaded)
        {
            FocusActiveInput();
        }
    }

    private void FocusActiveInput()
    {
        if (isSignUpMode)
        {
            FirstNameBox.Focus();
            return;
        }

        UsernameBox.Focus();
    }

    private void Submit_Click(object sender, RoutedEventArgs e)
    {
        if (!credentials.TryAuthenticate(UsernameBox.Text, PasswordBox.Password, out AuthenticatedUser? user) || user is null)
        {
            StatusText.Text = "Incorrect email, username, or password.";
            PasswordBox.Clear();
            PasswordBox.Focus();
            UiAnimator.Shake(LoginCard);
            return;
        }

        StatusText.Text = string.Empty;
        OpenDashboard(user);
    }

    private void CreateAccount_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = string.Empty;
        SignUpStatusText.Text = string.Empty;

        if (!string.Equals(SignUpPasswordBox.Password, ConfirmPasswordBox.Password, StringComparison.Ordinal))
        {
            SignUpStatusText.Text = "Passwords do not match.";
            ConfirmPasswordBox.Clear();
            ConfirmPasswordBox.Focus();
            UiAnimator.Shake(LoginCard);
            return;
        }

        var request = new SignupRequest(
            FirstNameBox.Text,
            LastNameBox.Text,
            PhoneNumberBox.Text,
            EmailBox.Text,
            SignUpPasswordBox.Password);

        if (!credentials.TryRegister(request, out AuthenticatedUser? user, out string errorMessage) || user is null)
        {
            SignUpStatusText.Text = errorMessage;
            SignUpPasswordBox.Clear();
            ConfirmPasswordBox.Clear();
            UiAnimator.Shake(LoginCard);
            FocusActiveInput();
            return;
        }

        OpenDashboard(user);
    }

    private void OpenDashboard(AuthenticatedUser user)
    {
        var dashboard = new Window2(user);
        Application.Current.MainWindow = dashboard;
        dashboard.Show();
        Close();
    }
}

