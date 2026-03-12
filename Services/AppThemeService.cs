using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Label_CRM_demo.Models;

namespace Label_CRM_demo.Services;

public sealed class AppThemeService
{
    private static readonly IReadOnlyDictionary<string, string> DarkPalette = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["TitanTextPrimaryColor"] = "#F6FBFD",
        ["TitanTextSecondaryColor"] = "#C7D7E0",
        ["TitanTextMutedColor"] = "#8EA8B7",
        ["TitanMetricLabelColor"] = "#8FA9B7",
        ["TitanWindowFallbackColor"] = "#07131F",
        ["TitanShellStartColor"] = "#050816",
        ["TitanShellMidColor"] = "#0B1020",
        ["TitanShellEndColor"] = "#18374A",
        ["TitanGlassStartColor"] = "#D9172531",
        ["TitanGlassEndColor"] = "#C90E1821",
        ["TitanGlassBorderColor"] = "#294960",
        ["TitanCardColor"] = "#D9141F2A",
        ["TitanCardBorderColor"] = "#2F5165",
        ["TitanPanelColor"] = "#101A23",
        ["TitanPanelBorderColor"] = "#315063",
        ["TitanPanelAltColor"] = "#0E1921",
        ["TitanPanelAltBorderColor"] = "#315266",
        ["TitanInsetColor"] = "#CC0F172A",
        ["TitanInsetBorderColor"] = "#334155",
        ["TitanOverlayColor"] = "#16000000",
        ["TitanOverlayBorderColor"] = "#223047",
        ["TitanBadgeColor"] = "#15334450",
        ["TitanBadgeBorderColor"] = "#3D6478",
        ["TitanBadgeTextColor"] = "#DCE7EE",
        ["TitanReleaseItemColor"] = "#132130",
        ["TitanReleaseItemBorderColor"] = "#274255",
        ["TitanReleaseDateBadgeColor"] = "#18384A",
        ["TitanReleaseDateBadgeBorderColor"] = "#3F6478",
        ["TitanReadableSurfaceColor"] = "#EEF4F7",
        ["TitanReadableSurfaceAltColor"] = "#F6FAFC",
        ["TitanReadableSoftColor"] = "#EFF5F8",
        ["TitanReadableSoftBorderColor"] = "#D7E2EA",
        ["TitanReadableTextColor"] = "#0F1720",
        ["TitanReadableMutedTextColor"] = "#617A88",
        ["TitanReadableSubtleTextColor"] = "#58707E",
        ["TitanReadableFaintTextColor"] = "#6D8592",
        ["TitanInputBackgroundColor"] = "#0D1822",
        ["TitanInputBorderColor"] = "#355569",
        ["TitanInputFocusBackgroundColor"] = "#0F1926",
        ["TitanReadableInputFocusColor"] = "#F2FFFC",
        ["TitanReadableInputSelectionColor"] = "#CFEFEA",
        ["TitanSecondaryButtonColor"] = "#101D27",
        ["TitanSecondaryButtonBorderColor"] = "#315266",
        ["TitanSecondaryButtonHoverColor"] = "#162734",
        ["TitanSecondaryButtonHoverBorderColor"] = "#4B7084",
        ["TitanSecondaryButtonPressedColor"] = "#19303E",
        ["TitanGhostButtonColor"] = "#0E1921",
        ["TitanGhostButtonTextColor"] = "#EAF4F8",
        ["TitanDangerButtonColor"] = "#3B0F14",
        ["TitanDangerButtonBorderColor"] = "#9F3643",
        ["TitanDangerButtonTextColor"] = "#FFE4E7",
        ["TitanGridHeaderColor"] = "#172734",
        ["TitanGridHeaderBorderColor"] = "#294960",
        ["TitanGridRowHoverColor"] = "#1D3341",
        ["TitanGridRowSelectedColor"] = "#264355",
        ["TitanReadableSelectionColor"] = "#DCEAF5",
        ["TitanFooterBandColor"] = "#880C1822",
        ["TitanFooterBandBorderColor"] = "#2B4A61",
        ["TitanLoginChromeColor"] = "#1708111A",
        ["TitanLoginChromeBorderColor"] = "#263B4D",
        ["TitanLoginHeaderColor"] = "#AA0C1822",
        ["TitanLoginHeaderBorderColor"] = "#2B4A61",
        ["TitanLogoPanelColor"] = "#0E1B27",
        ["TitanLogoPanelBorderColor"] = "#335166",
        ["TitanLoginTabInactiveColor"] = "#101928",
        ["TitanLoginTabInactiveBorderColor"] = "#2A3A66",
        ["TitanLoginFieldBackgroundColor"] = "#0B1226",
        ["TitanLoginFieldBorderColor"] = "#2A3A66",
        ["TitanReadableCalendarBadgeColor"] = "#E9F7F4",
        ["TitanReadableCalendarBadgeBorderColor"] = "#CBEAE3",
        ["TitanReadableCalendarMetaColor"] = "#F2F5F8",
        ["TitanReadableCalendarMetaBorderColor"] = "#DCE4EA"
    };

    private static readonly IReadOnlyDictionary<string, string> BrightPalette = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["TitanTextPrimaryColor"] = "#F7FBFD",
        ["TitanTextSecondaryColor"] = "#D7E5EC",
        ["TitanTextMutedColor"] = "#A9C0CA",
        ["TitanMetricLabelColor"] = "#B4C8D1",
        ["TitanWindowFallbackColor"] = "#102132",
        ["TitanShellStartColor"] = "#0B1824",
        ["TitanShellMidColor"] = "#143244",
        ["TitanShellEndColor"] = "#2B6C79",
        ["TitanGlassStartColor"] = "#DE243746",
        ["TitanGlassEndColor"] = "#D61C2D39",
        ["TitanGlassBorderColor"] = "#537A8E",
        ["TitanCardColor"] = "#D9253947",
        ["TitanCardBorderColor"] = "#557A8E",
        ["TitanPanelColor"] = "#183243",
        ["TitanPanelBorderColor"] = "#567A90",
        ["TitanPanelAltColor"] = "#16303F",
        ["TitanPanelAltBorderColor"] = "#5C8196",
        ["TitanInsetColor"] = "#D1182C39",
        ["TitanInsetBorderColor"] = "#547080",
        ["TitanOverlayColor"] = "#26183042",
        ["TitanOverlayBorderColor"] = "#3E6477",
        ["TitanBadgeColor"] = "#22445B70",
        ["TitanBadgeBorderColor"] = "#5B869C",
        ["TitanBadgeTextColor"] = "#E6EFF4",
        ["TitanReleaseItemColor"] = "#1B3344",
        ["TitanReleaseItemBorderColor"] = "#4F7589",
        ["TitanReleaseDateBadgeColor"] = "#264E63",
        ["TitanReleaseDateBadgeBorderColor"] = "#6790A5",
        ["TitanReadableSurfaceColor"] = "#E5EFF4",
        ["TitanReadableSurfaceAltColor"] = "#EEF4F7",
        ["TitanReadableSoftColor"] = "#DDE8EE",
        ["TitanReadableSoftBorderColor"] = "#CBD8E1",
        ["TitanReadableTextColor"] = "#0F1720",
        ["TitanReadableMutedTextColor"] = "#5F7481",
        ["TitanReadableSubtleTextColor"] = "#546A77",
        ["TitanReadableFaintTextColor"] = "#6B8190",
        ["TitanInputBackgroundColor"] = "#143042",
        ["TitanInputBorderColor"] = "#5C8498",
        ["TitanInputFocusBackgroundColor"] = "#1A394D",
        ["TitanReadableInputFocusColor"] = "#F0FAF8",
        ["TitanReadableInputSelectionColor"] = "#D3F1EA",
        ["TitanSecondaryButtonColor"] = "#173648",
        ["TitanSecondaryButtonBorderColor"] = "#5A8196",
        ["TitanSecondaryButtonHoverColor"] = "#1D4256",
        ["TitanSecondaryButtonHoverBorderColor"] = "#76A8BC",
        ["TitanSecondaryButtonPressedColor"] = "#214B61",
        ["TitanGhostButtonColor"] = "#173243",
        ["TitanGhostButtonTextColor"] = "#F1F7FA",
        ["TitanDangerButtonColor"] = "#5A2028",
        ["TitanDangerButtonBorderColor"] = "#C56776",
        ["TitanDangerButtonTextColor"] = "#FFF0F2",
        ["TitanGridHeaderColor"] = "#1D3A4A",
        ["TitanGridHeaderBorderColor"] = "#5A8196",
        ["TitanGridRowHoverColor"] = "#214457",
        ["TitanGridRowSelectedColor"] = "#2B556A",
        ["TitanReadableSelectionColor"] = "#CFE3EE",
        ["TitanFooterBandColor"] = "#9A173243",
        ["TitanFooterBandBorderColor"] = "#537A8E",
        ["TitanLoginChromeColor"] = "#28172B39",
        ["TitanLoginChromeBorderColor"] = "#486A7F",
        ["TitanLoginHeaderColor"] = "#C2183242",
        ["TitanLoginHeaderBorderColor"] = "#567B90",
        ["TitanLogoPanelColor"] = "#183243",
        ["TitanLogoPanelBorderColor"] = "#587C90",
        ["TitanLoginTabInactiveColor"] = "#173243",
        ["TitanLoginTabInactiveBorderColor"] = "#5B7F93",
        ["TitanLoginFieldBackgroundColor"] = "#132A3A",
        ["TitanLoginFieldBorderColor"] = "#55788C",
        ["TitanReadableCalendarBadgeColor"] = "#DDF4EE",
        ["TitanReadableCalendarBadgeBorderColor"] = "#B8E1D7",
        ["TitanReadableCalendarMetaColor"] = "#E8EEF2",
        ["TitanReadableCalendarMetaBorderColor"] = "#CDD8E0"
    };

    private readonly ThemePreferenceRepository repository;

    public AppThemeService(ThemePreferenceRepository? repository = null)
    {
        this.repository = repository ?? new ThemePreferenceRepository();
    }

    public event EventHandler? ThemeChanged;

    public AppThemeMode CurrentMode { get; private set; } = AppThemeMode.Dark;

    public void Initialize(ResourceDictionary resources)
    {
        CurrentMode = repository.Load();
        Apply(resources, CurrentMode);
    }

    public void Toggle()
    {
        SetMode(CurrentMode == AppThemeMode.Dark ? AppThemeMode.Bright : AppThemeMode.Dark);
    }

    public void SetMode(AppThemeMode mode)
    {
        if (CurrentMode == mode)
        {
            return;
        }

        CurrentMode = mode;
        Apply(Application.Current.Resources, mode);
        repository.Save(mode);
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    public string GetToggleLabel()
        => CurrentMode == AppThemeMode.Dark ? "Light mode" : "Dark mode";

    private static void Apply(ResourceDictionary resources, AppThemeMode mode)
    {
        var palette = mode == AppThemeMode.Dark ? DarkPalette : BrightPalette;

        foreach (var entry in palette)
        {
            resources[entry.Key] = ParseColor(entry.Value);
        }
    }

    private static Color ParseColor(string value)
        => (Color)ColorConverter.ConvertFromString(value)!;
}
