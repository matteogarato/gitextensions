using System.ComponentModel;
using System.Diagnostics;
using System.Security;
using System.Text;
using BugReporter.Serialization;
using GitCommands.Settings;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Settings;

namespace GitUI.NBugReports;

/// <summary>
///     Provides methods for reporting unhandled exceptions to the user
///     via UI dialogs or by launching the external bug reporter process.
/// </summary>
public static class BugReportInvoker
{
    /// <summary>
    ///     The git config command fragment used to detect dubious ownership security errors.
    /// </summary>
    public const string DubiousOwnershipSecurityConfigString = "config --global --add safe.directory";

    private static readonly IBugReporter _bugReporter = new UiReporter();
    private static bool _isReportingDubiousOwnershipSecurity;

    /// <summary>
    ///     Gets or sets the <see cref="IGitExecutorProvider" /> used to create git executors
    ///     that respect WSL routing for dubious ownership trust commands.
    /// </summary>
    public static IGitExecutorProvider? ExecutorProvider { get; set; }

    /// <summary>
    ///     Set to <see langword="true" /> on application exit
    ///     in order to suppress the popup to restart the app on missing runtime assembly.
    /// </summary>
    public static bool IgnoreFailedToLoadAnAssembly
    {
        get => UiReporter.IgnoreFailedToLoadAnAssembly;
        set => UiReporter.IgnoreFailedToLoadAnAssembly = value;
    }

    /// <summary>
    ///     Gets the root error.
    /// </summary>
    /// <param name="exception">An Exception to describe.</param>
    /// <returns>The inner-most exception message.</returns>
    internal static string GetRootError(Exception exception)
    {
        var rootError = exception.Message;
        for (var innerException = exception.InnerException;
             innerException is not null;
             innerException = innerException.InnerException)
        {
            if (!string.IsNullOrEmpty(innerException.Message))
            {
                rootError = innerException.Message;
            }
        }

        return rootError;
    }

    /// <summary>
    ///     Logs the exception details to a temporary file.
    /// </summary>
    /// <param name="exception">The exception to log.</param>
    /// <param name="isTerminating">Indicates whether the exception is terminating the application.</param>
    public static void LogError(Exception exception, bool isTerminating = false)
    {
        var tempFolder = Path.GetTempPath();
        var tempFileName =
            $"{AppSettings.ApplicationId}.{AppSettings.AppVersion}.{DateTime.Now:yyyyMMdd.HHmmssfff}.log";
        var tempFile = Path.Join(tempFolder, tempFileName);

        try
        {
            var content = $"Is fatal: {isTerminating}\r\n{exception}";
            File.WriteAllText(tempFile, content);
        }
        catch (Exception ex)
        {
            MessageBoxes.Show($"Failed to log error to {tempFile}\r\n{ex.Message}", "Error writing log",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    ///     Reports the specified exception to the user via the appropriate UI dialog.
    /// </summary>
    /// <param name="exception">The exception to report.</param>
    /// <param name="isTerminating">Indicates whether the exception is terminating the application.</param>
    public static void Report(Exception exception, bool isTerminating)
    {
        if (AppSettings.WriteErrorLog)
        {
            LogError(exception, isTerminating);
        }

        if (_bugReporter.ReportFailedToLoadAnAssembly(exception, isTerminating))
        {
            return;
        }

        // Ignore accessibility-specific exception (refer to https://github.com/gitextensions/gitextensions/issues/11385)
        if (exception is InvalidOperationException &&
            exception.StackTrace?.Contains("ListViewGroup.get_AccessibilityObject") is true)
        {
            Trace.WriteLine(exception);
            return;
        }

        // Do not report cancellation of async implementations awaited by the UI thread (refer to https://github.com/gitextensions/gitextensions/issues/11636)
        if (exception is OperationCanceledException or TaskCanceledException)
        {
            Debug.WriteLine(exception);
            return;
        }

        var externalOperationException = exception as ExternalOperationException;

        if (externalOperationException?.InnerException?.Message?.Contains(DubiousOwnershipSecurityConfigString) is true)
        {
            ReportDubiousOwnership(externalOperationException);
            return;
        }

        if (isTerminating)
        {
            // TODO: this is not very efficient
            // GetExceptionInfo() info is lost
            SerializableException serializableException = new(exception);
            var xml = serializableException.ToXmlString();
            var encoded = Base64Encode(xml);
            Process.Start("BugReporter.exe", encoded);

            return;
        }

        var isUserExternalOperation
            = exception is UserExternalOperationException
                  or GitConfigFormatException

              // Treat all git errors as user issues
              || string.Equals(AppSettings.GitCommand, externalOperationException?.Command,
                  StringComparison.InvariantCultureIgnoreCase)
              || string.Equals(AppSettings.WslCommand, externalOperationException?.Command,
                  StringComparison.InvariantCultureIgnoreCase);
        var isExternalOperation = isUserExternalOperation
                                  || exception is ExternalOperationException
                                      or IOException
                                      or SecurityException
                                      or FileNotFoundException
                                      or DirectoryNotFoundException
                                      or PathTooLongException
                                      or Win32Exception;

        var text = exception.GetExceptionInfo();
        var rootError = GetRootError(exception);

        _bugReporter.ReportError(
            exception,
            rootError,
            text,
            new OperationInfo
            {
                IsExternalOperation = isExternalOperation, IsUserExternalOperation = isUserExternalOperation
            });
    }

    private static void ReportDubiousOwnership(ExternalOperationException exception)
    {
        if (_isReportingDubiousOwnershipSecurity)
        {
            return;
        }

        try
        {
            _isReportingDubiousOwnershipSecurity = true;
            _bugReporter.ReportDubiousOwnership(exception);
        }
        finally
        {
            _isReportingDubiousOwnershipSecurity = false;
        }
    }

    private static string Base64Encode(string plainText)
    {
        var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
        return Convert.ToBase64String(plainTextBytes);
    }
}
