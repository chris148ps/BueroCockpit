using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Markup.Xaml;
using BueroCockpit.Data;
using BueroCockpit.Services;

namespace BueroCockpit;

public partial class App : Application
{
    private const string LightMode = "Light Mode";
    private const string DarkMode = "Dark Mode";

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var storageLocationService = new StorageLocationService();
        try
        {
            storageLocationService.ApplyConfiguredDataDirectory();
        }
        catch (LocalDataDirectoryRedirectException ex)
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime blockedDesktop)
            {
                blockedDesktop.MainWindow = CreateLocalDataDirectoryErrorWindow(ex.Message);
            }

            base.OnFrameworkInitializationCompleted();
            return;
        }

        var settingsService = new AppSettingsService();
        var settings = settingsService.Load();
        ApplyAppearanceMode(settings.AppearanceMode);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static Window CreateLocalDataDirectoryErrorWindow(string message)
    {
        var closeButton = new Button
        {
            Content = "BüroCockpit schließen",
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 150
        };
        var window = new Window
        {
            Title = "Lokaler Datenordner erforderlich",
            Width = 720,
            MinWidth = 560,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = new Border
            {
                Padding = new Thickness(24),
                Child = new StackPanel
                {
                    Spacing = 14,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "BüroCockpit hat keine Produktivdaten geöffnet.",
                            FontSize = 20,
                            FontWeight = FontWeight.SemiBold,
                            TextWrapping = TextWrapping.Wrap
                        },
                        new TextBlock
                        {
                            Text = message,
                            TextWrapping = TextWrapping.Wrap
                        },
                        closeButton
                    }
                }
            }
        };
        closeButton.Click += (_, _) => window.Close();
        return window;
    }

    public void ApplyAppearanceMode(string? appearanceMode)
    {
        var mode = NormalizeAppearanceMode(appearanceMode);
        RequestedThemeVariant = mode == LightMode ? ThemeVariant.Light : ThemeVariant.Dark;
        ApplyThemeResources(mode);
    }

    private void ApplyThemeResources(string mode)
    {
        var isLightMode = string.Equals(mode, LightMode, StringComparison.OrdinalIgnoreCase);

        SetBrush("WindowBackgroundBrush", isLightMode ? "#F3F3F3" : "#202020");
        SetBrush("NavigationBackgroundBrush", isLightMode ? "#EBEBEB" : "#1C1C1C");
        SetBrush("SidebarBackgroundBrush", isLightMode ? "#EBEBEB" : "#1C1C1C");
        SetBrush("SidebarPanelBackgroundBrush", isLightMode ? "#F0F0F0" : "#242424");
        SetBrush("ContentBackgroundBrush", isLightMode ? "#F7F7F7" : "#272727");
        SetBrush("ContentSecondaryBackgroundBrush", isLightMode ? "#F0F0F0" : "#2B2B2B");
        SetBrush("SurfaceBackgroundBrush", isLightMode ? "#FFFFFF" : "#2D2D2D");
        SetBrush("CardBackgroundBrush", isLightMode ? "#FFFFFF" : "#2D2D2D");
        SetBrush("SurfaceElevatedBrush", isLightMode ? "#FFFFFF" : "#323232");
        SetBrush("InputBackgroundBrush", isLightMode ? "#FFFFFF" : "#242424");
        SetBrush("HoverBackgroundBrush", isLightMode ? "#E8E8E8" : "#383838");
        SetBrush("SelectedBackgroundBrush", isLightMode ? "#DCECF5" : "#243B4A");
        SetBrush("DisabledBackgroundBrush", isLightMode ? "#E5E5E5" : "#2A2A2A");

        SetBrush("BorderBrush", isLightMode ? "#D6D6D6" : "#404040");
        SetBrush("BorderBrushDark", isLightMode ? "#D6D6D6" : "#404040");
        SetBrush("BorderBrushStrong", isLightMode ? "#9F9F9F" : "#5A5A5A");
        SetBrush("FocusBorderBrush", "#60CDFF");

        SetBrush("TextPrimaryBrush", isLightMode ? "#1A1A1A" : "#F5F5F5");
        SetBrush("TextSecondaryBrush", isLightMode ? "#4D4D4D" : "#C8C8C8");
        SetBrush("TextTertiaryBrush", isLightMode ? "#666666" : "#9B9B9B");
        SetBrush("TextDisabledBrush", isLightMode ? "#929292" : "#707070");
        SetBrush("TextOnAccentBrush", "#102027");
        SetBrush("DeskInkBrush", "#1F2937");
        SetBrush("DeskStrongInkBrush", "#111827");
        SetBrush("DeskSelectionBrush", "#55D9B35C");

        SetBrush("AccentBrush", "#60CDFF");
        SetBrush("AccentHoverBrush", "#7BD8FF");
        SetBrush("AccentPressedBrush", "#4CC2FF");
        SetBrush("AccentSoftBrush", isLightMode ? "#DCECF5" : "#243B4A");
        SetBrush("AccentSoftBorderBrush", isLightMode ? "#75B9D6" : "#39738A");

        SetBrush("InformationBrush", "#60CDFF");
        SetBrush("SuccessBrush", isLightMode ? "#0F7B0F" : "#6CCB5F");
        SetBrush("WarningBrush", isLightMode ? "#8A5700" : "#FCE100");
        SetBrush("DangerBrush", isLightMode ? "#C42B1C" : "#FF99A4");
        SetBrush("PendingBrush", isLightMode ? "#8A5700" : "#F6C344");
        SetBrush("ConnectedBrush", isLightMode ? "#0F7B0F" : "#6CCB5F");
        SetBrush("DisconnectedBrush", isLightMode ? "#C42B1C" : "#FF99A4");
        SetBrush("OverdueBrush", isLightMode ? "#C42B1C" : "#FF99A4");
        SetBrush("ConfirmedBrush", isLightMode ? "#0F7B0F" : "#6CCB5F");
        SetBrush("InformationBackgroundBrush", isLightMode ? "#E3F3FA" : "#203843");
        SetBrush("SuccessBackgroundBrush", isLightMode ? "#E6F4EA" : "#243A27");
        SetBrush("WarningBackgroundBrush", isLightMode ? "#FFF4CE" : "#3D351D");
        SetBrush("DangerBackgroundBrush", isLightMode ? "#FDE7E9" : "#44272B");

        SetBrush("ButtonHoverBackgroundBrush", isLightMode ? "#E8E8E8" : "#383838");
        SetBrush("ButtonHoverBorderBrush", isLightMode ? "#9F9F9F" : "#5A5A5A");
        SetBrush("ButtonHoverForegroundBrush", isLightMode ? "#1A1A1A" : "#F5F5F5");
        SetBrush("ButtonPrimaryHoverBackgroundBrush", "#7BD8FF");
        SetBrush("ButtonPrimaryHoverBorderBrush", "#7BD8FF");
        SetBrush("ButtonDangerHoverBackgroundBrush", isLightMode ? "#A4262C" : "#C42B1C");
        SetBrush("ButtonDangerHoverBorderBrush", isLightMode ? "#A4262C" : "#E0525E");
        SetBrush("ButtonDangerHoverForegroundBrush", "#FFFFFF");

        Resources["CornerRadiusSmall"] = new CornerRadius(4);
        Resources["CornerRadiusMedium"] = new CornerRadius(6);
        Resources["CornerRadiusLarge"] = new CornerRadius(8);
        Resources["SpacingSmall"] = new Thickness(4);
        Resources["SpacingMedium"] = new Thickness(8);
        Resources["SpacingLarge"] = new Thickness(12);
        Resources["ControlPadding"] = new Thickness(12, 6);
        Resources["CardPadding"] = new Thickness(12);
        Resources["DialogPadding"] = new Thickness(18);
    }

    private void SetBrush(string key, string hex)
    {
        Resources[key] = new SolidColorBrush(Color.Parse(hex));
    }

    private static string NormalizeAppearanceMode(string? appearanceMode)
    {
        return string.Equals(appearanceMode?.Trim(), LightMode, StringComparison.OrdinalIgnoreCase)
            ? LightMode
            : DarkMode;
    }
}
