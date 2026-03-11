using System.Windows;
using Label_CRM_demo.Models;

namespace Label_CRM_demo;

public partial class ModuleWindow : SnapWindow
{
    private readonly ModuleWindowState state;

    public ModuleWindow(ModuleWindowState state)
    {
        this.state = state;
        InitializeComponent();
        ApplyState();
        InitializeInteractiveStates();

        Loaded += (_, _) => UiAnimator.PlayEntrance(new FrameworkElement[]
        {
            HeaderCard,
            HighlightCard,
            MetricsList,
            TableCard,
            FooterBand
        }, 26, 75);
    }

    private void InitializeInteractiveStates()
    {
        UiAnimator.AttachHoverLift(new FrameworkElement[]
        {
            HeaderCard,
            BehaviorCard,
            HighlightCard,
            TableCard
        }, -6, 1.008);
    }

    private void ApplyState()
    {
        Title = state.Title;
        WindowTitleText.Text = state.Title;
        SubtitleText.Text = state.Subtitle;
        HighlightText.Text = state.Highlight;
        FooterText.Text = state.Footer;

        Column1.Header = state.Column1Header;
        Column2.Header = state.Column2Header;
        Column3.Header = state.Column3Header;
        Column4.Header = state.Column4Header;

        MetricsList.ItemsSource = state.Metrics;
        RecordsGrid.ItemsSource = state.Rows;
    }
}
