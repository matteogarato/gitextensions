using GitCommands.Settings;
using GitExtensions.Extensibility.Translations;
using SmartFormat;
using SmartFormat.Core.Settings;

namespace ResourceManager;

/// <summary>Contains common string literals which are translated.</summary>
public sealed class TranslatedStrings : Translate
{
    private static Lazy<TranslatedStrings> _instance = new();
    private readonly TranslationString _authorDateText = new("{0:Author date|Author dates}");
    private readonly TranslationString _authorText = new("{0:Author|Authors}");
    private readonly TranslationString _autostash = new("Autostash");
    private readonly TranslationString _childrenText = new("{0:Child|Children}");
    private readonly TranslationString _clearRecentRepositories = new("Clear recent repositories");
    private readonly TranslationString _clearShortcut = new("Clear shortcut");
    private readonly TranslationString _commitDateText = new("{0:Commit date|Commit dates}");
    private readonly TranslationString _commitHashText = new("{0:Commit hash|Commit hashes}");
    private readonly TranslationString _committerText = new("Committer");
    private readonly TranslationString _dateText = new("Date");
    private readonly TranslationString _daysAgo = new("{0} {1:day|days} ago");

    private readonly TranslationString _deleteFile = new("{0:Delete file|Delete files}");

    private readonly TranslationString _disableMenuItem = new("Disable this dropdown");
    private readonly TranslationString _duplicateShortcutError = new("Each shortcut must be unique.");
    private readonly TranslationString _findGitExecutable = new("Find git...");
    private readonly TranslationString _generalGitConfigExceptionCaption = new("Repository Configuration Error");

    private readonly TranslationString _generalGitConfigExceptionMessage = new(
        "Failed to read \"{0}\" due to the following error:{1}{1}{2}{1}{1}Due to the nature of this problem, the behavior of the application cannot be guaranteed and it must be closed.{1}{1}Please correct this issue and re-open Git Extensions.");

    private readonly TranslationString _gitExecutableNotFoundText =
        new("The Git executable could not be located on your system.");

    private readonly TranslationString _hoursAgo = new("{0} {1:hour|hours} ago");
    private readonly TranslationString _indexText = new("Commit index");

    private readonly TranslationString _installGitInstructions = new("Install git...");

    private readonly TranslationString _keyboardShortcutsHelp =
        new("Select a field, then press the shortcut you want to use.");

    private readonly TranslationString _keyboardShortcutsMenu = new("Keyboard shortcuts...");
    private readonly TranslationString _keyboardShortcutsTitle = new("Keyboard shortcuts");
    private readonly TranslationString _messageText = new("{0:Message|Messages}");
    private readonly TranslationString _minutesAgo = new("{0} {1:minute|minutes} ago");
    private readonly TranslationString _monthsAgo = new("{0} {1:month|months} ago");
    private readonly TranslationString _openRepositoryShortcut = new("Open repository");
    private readonly TranslationString _openSelectedRepository = new("Open selected repository");

    private readonly TranslationString _parentsText = new("{0:Parent|Parents}");
    private readonly TranslationString _pullShortcut = new("Pull");
    private readonly TranslationString _pushShortcut = new("Push");
    private readonly TranslationString _recentRepositoriesTitle = new("Recent repositories");
    private readonly TranslationString _refreshShortcut = new("Refresh repository");
    private readonly TranslationString _removeFromRecentList = new("Remove from recent list");
    private readonly TranslationString _returnToRepository = new("Return to repository");
    private readonly TranslationString _saveText = new("Save");
    private readonly TranslationString _scriptsMenu = new("Scripts");
    private readonly TranslationString _secondsAgo = new("{0} {1:second|seconds} ago");
    private readonly TranslationString _showAllText = new("Show all");

    private readonly TranslationString _telemetryPermissionCaption = new("Allow Capture Telemetry?");

    private readonly TranslationString _telemetryPermissionMessage = new(
        @"We collect information so we can make the app better.

We won't collect any personal or identifiable information.
You can change your mind at any time.

Yes, I allow telemetry!");

    private readonly TranslationString _toggleDashboardShortcut = new("Show recent repositories");
    private readonly TranslationString _unsupportedShortcutKey = new("This key cannot be used for a shortcut.");
    private readonly TranslationString _weeksAgo = new("{0} {1:week|weeks} ago");
    private readonly TranslationString _workspaceText = new("Working directory");
    private readonly TranslationString _yearsAgo = new("{0} {1:year|years} ago");

    // public only because of FormTranslate
    public TranslatedStrings()
    {
        // Our original implementations were created against SmartFormat.NET pre-dating v2.0.0.
        // Since v2.5.0 the default error action was changed to ThrowError. See https://github.com/axuno/SmartFormat/issues/192.
        // This applies for the Formatter AND the Parser
        Smart.Default.Settings.Formatter.ErrorAction = FormatErrorAction.Ignore;
        Smart.Default.Settings.Parser.ErrorAction = ParseErrorAction.Ignore;

        Translator.Translate(this, AppSettings.CurrentTranslation);
    }

    public static string Autostash
    {
        get => _instance.Value._autostash.Text;
    }

    public static string FindGitExecutable
    {
        get => _instance.Value._findGitExecutable.Text;
    }

    public static string InstallGitInstructions
    {
        get => _instance.Value._installGitInstructions.Text;
    }

    public static string GitExecutableNotFound
    {
        get => _instance.Value._gitExecutableNotFoundText.Text;
    }

    public static string TelemetryPermissionCaption
    {
        get => _instance.Value._telemetryPermissionCaption.Text;
    }

    public static string TelemetryPermissionMessage
    {
        get => _instance.Value._telemetryPermissionMessage.Text;
    }

    public static string Date
    {
        get => _instance.Value._dateText.Text;
    }

    public static string Author
    {
        get => GetAuthor(1);
    }

    public static string AuthorDate
    {
        get => GetAuthorDate(1);
    }

    public static string Committer
    {
        get => _instance.Value._committerText.Text;
    }

    public static string CommitDate
    {
        get => GetCommitDate(1);
    }

    public static string CommitHash
    {
        get => GetCommitHash(1);
    }

    public static string ShowAll
    {
        get => _instance.Value._showAllText.Text;
    }

    public static string Workspace
    {
        get => _instance.Value._workspaceText.Text;
    }

    public static string Index
    {
        get => _instance.Value._indexText.Text;
    }

    public static string RecentRepositoriesTitle
    {
        get => _instance.Value._recentRepositoriesTitle.Text;
    }

    public static string OpenSelectedRepository
    {
        get => _instance.Value._openSelectedRepository.Text;
    }

    public static string RemoveFromRecentList
    {
        get => _instance.Value._removeFromRecentList.Text;
    }

    public static string ClearRecentRepositories
    {
        get => _instance.Value._clearRecentRepositories.Text;
    }

    public static string ReturnToRepository
    {
        get => _instance.Value._returnToRepository.Text;
    }

    public static string KeyboardShortcutsMenu
    {
        get => _instance.Value._keyboardShortcutsMenu.Text;
    }

    public static string KeyboardShortcutsTitle
    {
        get => _instance.Value._keyboardShortcutsTitle.Text;
    }

    public static string ScriptsMenu
    {
        get => _instance.Value._scriptsMenu.Text;
    }

    public static string KeyboardShortcutsHelp
    {
        get => _instance.Value._keyboardShortcutsHelp.Text;
    }

    public static string OpenRepositoryShortcut
    {
        get => _instance.Value._openRepositoryShortcut.Text;
    }

    public static string ToggleDashboardShortcut
    {
        get => _instance.Value._toggleDashboardShortcut.Text;
    }

    public static string RefreshShortcut
    {
        get => _instance.Value._refreshShortcut.Text;
    }

    public static string PullShortcut
    {
        get => _instance.Value._pullShortcut.Text;
    }

    public static string PushShortcut
    {
        get => _instance.Value._pushShortcut.Text;
    }

    public static string ClearShortcut
    {
        get => _instance.Value._clearShortcut.Text;
    }

    public static string UnsupportedShortcutKey
    {
        get => _instance.Value._unsupportedShortcutKey.Text;
    }

    public static string HotkeyDuplicateError
    {
        get => _instance.Value._duplicateShortcutError.Text;
    }

    public static string Save
    {
        get => _instance.Value._saveText.Text;
    }

    public static string GeneralGitConfigExceptionMessage
    {
        get => _instance.Value._generalGitConfigExceptionMessage.Text;
    }

    public static string GeneralGitConfigExceptionCaption
    {
        get => _instance.Value._generalGitConfigExceptionCaption.Text;
    }

    public static string DisableMenuItem
    {
        get => _instance.Value._disableMenuItem.Text;
    }

    public static void Reinitialize()
    {
        if (_instance.IsValueCreated)
        {
            _instance = new Lazy<TranslatedStrings>();
        }
    }

    public static string GetParents(int value)
    {
        return Smart.Format(AppSettings.CurrentCultureInfo, _instance.Value._parentsText.Text, value, Math.Abs(value));
    }

    public static string GetChildren(int value)
    {
        return Smart.Format(AppSettings.CurrentCultureInfo, _instance.Value._childrenText.Text, value, Math.Abs(value));
    }

    public static string GetDeleteFile(int value)
    {
        return Smart.Format(AppSettings.CurrentCultureInfo, _instance.Value._deleteFile.Text, value, Math.Abs(value));
    }

    public static string GetCommitDate(int value)
    {
        var v = Smart.Format(AppSettings.CurrentCultureInfo, _instance.Value._commitDateText.Text, value,
            Math.Abs(value));
        return v;
    }

    public static string GetCommitHash(int value)
    {
        return Smart.Format(AppSettings.CurrentCultureInfo, _instance.Value._commitHashText.Text, value,
            Math.Abs(value));
    }

    public static string GetMessage(int value)
    {
        return Smart.Format(AppSettings.CurrentCultureInfo, _instance.Value._messageText.Text, value, Math.Abs(value));
    }

    public static string GetAuthor(int value)
    {
        return Smart.Format(AppSettings.CurrentCultureInfo, _instance.Value._authorText.Text, value, Math.Abs(value));
    }

    public static string GetAuthorDate(int value)
    {
        return Smart.Format(AppSettings.CurrentCultureInfo, _instance.Value._authorDateText.Text, value,
            Math.Abs(value));
    }

    public static string GetNSecondsAgoText(int value)
    {
        return Smart.Format(AppSettings.CurrentCultureInfo, _instance.Value._secondsAgo.Text, value, Math.Abs(value));
    }

    public static string GetNMinutesAgoText(int value)
    {
        return Smart.Format(AppSettings.CurrentCultureInfo, _instance.Value._minutesAgo.Text, value, Math.Abs(value));
    }

    public static string GetNHoursAgoText(int value)
    {
        return Smart.Format(AppSettings.CurrentCultureInfo, _instance.Value._hoursAgo.Text, value, Math.Abs(value));
    }

    public static string GetNDaysAgoText(int value)
    {
        return Smart.Format(AppSettings.CurrentCultureInfo, _instance.Value._daysAgo.Text, value, Math.Abs(value));
    }

    public static string GetNWeeksAgoText(int value)
    {
        return Smart.Format(AppSettings.CurrentCultureInfo, _instance.Value._weeksAgo.Text, value, Math.Abs(value));
    }

    public static string GetNMonthsAgoText(int value)
    {
        return Smart.Format(AppSettings.CurrentCultureInfo, _instance.Value._monthsAgo.Text, value, Math.Abs(value));
    }

    public static string GetNYearsAgoText(int value)
    {
        return Smart.Format(AppSettings.CurrentCultureInfo, _instance.Value._yearsAgo.Text, value, Math.Abs(value));
    }
}
