using GitExtUtils.GitUI.Theming;
using GitUI.Theming;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;

namespace GitUI.Infrastructure.Telemetry;

internal sealed class ThemingTelemetryInitializer : ITelemetryInitializer
{
    void ITelemetryInitializer.Initialize(ITelemetry telemetry)
    {
        var properties = telemetry.Context.GlobalProperties;
        var themeSettings = ThemeModule.Settings;
        properties["Theme dark"] = FlagString(themeSettings.Theme.SystemColorMode == SystemColorMode.Dark);
        properties["Theme builtin"] = FlagString(themeSettings.Theme.Id.IsBuiltin);
        properties["Theme systemstyles"] = FlagString(themeSettings.UseSystemVisualStyle);
    }

    private static string FlagString(bool value)
    {
        return value ? "1" : "0";
    }
}
