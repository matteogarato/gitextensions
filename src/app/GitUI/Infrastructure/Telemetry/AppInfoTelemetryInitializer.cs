using System.Diagnostics;
using GitCommands.Settings;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;

namespace GitUI.Infrastructure.Telemetry;

internal sealed class AppInfoTelemetryInitializer(bool isDirty) : ITelemetryInitializer
{
    private static readonly string _appCurrentTranslationKey = nameof(AppSettings.CurrentTranslation).FormatKey();
    private static readonly string _appEnvironmentKey = "Environment".FormatKey();
    private static readonly string _appIsPortableKey = "IsPortable".FormatKey();
    private static readonly string _appIsReleaseKey = "IsRelease".FormatKey();

    private static readonly string _appStartWithRecentWorkingDirKey =
        nameof(AppSettings.StartWithRecentWorkingDir).FormatKey();

    void ITelemetryInitializer.Initialize(ITelemetry telemetry)
    {
        telemetry.Context.Component.Version = AppSettings.ProductVersion;
        telemetry.Context.GlobalProperties[_appIsReleaseKey] = (!isDirty).ToString();
        telemetry.Context.GlobalProperties[_appIsPortableKey] = AppSettings.IsPortable().ToString();
        telemetry.Context.GlobalProperties[_appCurrentTranslationKey] = AppSettings.CurrentTranslation;
        telemetry.Context.GlobalProperties[_appStartWithRecentWorkingDirKey] =
            AppSettings.StartWithRecentWorkingDir.ToString();

        // Always default to development if we're in the debugger
        if (Debugger.IsAttached)
        {
            telemetry.Context.GlobalProperties[_appEnvironmentKey] = "development";
        }
    }
}

internal static class TelemetryKeyExtensions
{
    public static string FormatKey(this string key)
    {
        return string.Concat("GE.", key);
    }
}
