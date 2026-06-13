using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
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
        storageLocationService.ApplyConfiguredDataDirectory();

        var settingsService = new AppSettingsService();
        var settings = settingsService.Load();
        ApplyAppearanceMode(settings.AppearanceMode);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
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

        SetBrush("WindowBackgroundBrush", isLightMode ? "#F5F5F7" : "#1C1C1E");
        SetBrush("SidebarBackgroundBrush", isLightMode ? "#ECECF0" : "#242426");
        SetBrush("SidebarPanelBackgroundBrush", isLightMode ? "#F4F4F6" : "#2A2A2C");
        SetBrush("SurfaceBackgroundBrush", isLightMode ? "#FFFFFF" : "#2C2C2E");
        SetBrush("SurfaceElevatedBrush", isLightMode ? "#F8F8FA" : "#303033");
        SetBrush("InputBackgroundBrush", isLightMode ? "#FFFFFF" : "#1F1F21");
        SetBrush("BorderBrushDark", isLightMode ? "#D1D1D6" : "#3A3A3C");
        SetBrush("BorderBrushStrong", isLightMode ? "#B8B8BE" : "#48484A");
        SetBrush("TextPrimaryBrush", isLightMode ? "#1C1C1E" : "#F2F2F7");
        SetBrush("TextSecondaryBrush", isLightMode ? "#4A4A4F" : "#AEAEB2");
        SetBrush("TextTertiaryBrush", isLightMode ? "#6E6E73" : "#8E8E93");
        SetBrush("AccentBrush", "#0A84FF");
        SetBrush("AccentSoftBrush", isLightMode ? "#E4F0FF" : "#1A2F4A");
        SetBrush("AccentSoftBorderBrush", isLightMode ? "#B9D7FF" : "#2D5D8F");
        SetBrush("DangerBrush", isLightMode ? "#D92D20" : "#FF6B6B");
        SetBrush("ButtonHoverBackgroundBrush", "#0A84FF");
        SetBrush("ButtonHoverBorderBrush", "#0A84FF");
        SetBrush("ButtonHoverForegroundBrush", "#FFFFFF");
        SetBrush("ButtonPrimaryHoverBackgroundBrush", "#1B8FFF");
        SetBrush("ButtonPrimaryHoverBorderBrush", "#1B8FFF");
        SetBrush("ButtonDangerHoverBackgroundBrush", isLightMode ? "#D92D20" : "#FF453A");
        SetBrush("ButtonDangerHoverBorderBrush", isLightMode ? "#D92D20" : "#FF453A");
        SetBrush("ButtonDangerHoverForegroundBrush", "#FFFFFF");
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
