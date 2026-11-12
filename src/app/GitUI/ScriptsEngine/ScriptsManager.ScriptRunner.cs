using System.Text.RegularExpressions;
using GitCommands.Git;
using GitCommands.Settings;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtUtils;
using GitUI.HelperDialogs;
using GitUI.Plugin;

namespace GitUI.ScriptsEngine;

internal partial class ScriptsManager
{
    /// <summary>Runs scripts.</summary>
    internal static partial class ScriptRunner
    {
        private const string _pluginPrefix = "plugin:";
        private const string _navigateToPrefix = "navigateTo:";
        private const string _userInput = "UserInput";
        private const string _userFiles = "UserFiles";

        // Regex that ensure that in the default value, there is the same number of '{' than '}' to find the right end of the default value expression.
        [GeneratedRegex(@"\{UserInput:(?<label>[^}=]+)(=(?<defaultValue>[^{}]*(({[^{}]+})+[^{}]*)*))?\}",
            RegexOptions.ExplicitCapture)]
        private static partial Regex UserInputRegex { get; }

        [GeneratedRegex(@"\{plugin.(?<name>.+)\}", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture)]
        private static partial Regex PluginRegex { get; }

        public static bool RunScript(ScriptInfo script, IWin32Window owner, IGitUiCommands commands,
            IScriptOptionsProvider scriptOptionsProvider)
        {
            try
            {
                return RunScriptInternal(script, owner, commands, scriptOptionsProvider);
            }
            catch (ExternalOperationException ex) when (ex is not UserExternalOperationException)
            {
                throw new UserExternalOperationException(
                    $"{TranslatedStrings.ScriptErrorFailedToExecute}: '{script.GetDisplayName()}'", ex);
            }
        }

        internal static (string? arguments, bool abort, bool cancel) ParseUserInputs(string scriptName,
            string? arguments, IGitUiCommands uiCommands, IWin32Window owner,
            IScriptOptionsProvider scriptOptionsProvider)
        {
            if (arguments is null)
            {
                return (arguments: null, abort: false, cancel: false);
            }

            var userInputCaption = string.Format(TranslatedStrings.ScriptUserInputCaption, scriptName);

            // Specific handling of "UserInput" because the value entered should replace only "UserInput" with same label
            Match match;
            while ((match = UserInputRegex.Match(arguments)).Success)
            {
                var defaultValueMatch = match.Groups["defaultValue"];
                var defaultValue = defaultValueMatch is null
                    ? (string.Empty, false)
                    : ScriptOptionsParser.Parse(defaultValueMatch.Value ?? string.Empty, uiCommands, owner,
                        scriptOptionsProvider);

                if (defaultValue.abort)
                {
                    return (arguments: null, abort: true, cancel: false);
                }

                var label = match.Groups["label"].Value;

                using var prompt = uiCommands.GetRequiredService<ISimplePromptCreator>()
                    .Create(userInputCaption, label, defaultValue.arguments);
                var result = prompt.ShowDialog(owner);
                if (result != DialogResult.OK)
                {
                    return (arguments: null, abort: false, cancel: true);
                }

                arguments = ScriptOptionsParser.ReplaceOption($"UserInput:{label}", arguments, [prompt.UserInput]);
                arguments = ScriptOptionsParser.ReplaceOption(match.Value[1..^1], arguments, [prompt.UserInput]);
            }

            if (ScriptOptionsParser.Contains(arguments, _userInput))
            {
                userInputCaption = string.Format(TranslatedStrings.ScriptUserInputCaption, scriptName);
                using var prompt = uiCommands.GetRequiredService<ISimplePromptCreator>()
                    .Create(userInputCaption, null, string.Empty);
                var result = prompt.ShowDialog(owner);
                if (result == DialogResult.Cancel)
                {
                    return (arguments: null, abort: false, cancel: true);
                }

                arguments = ScriptOptionsParser.ReplaceOption(_userInput, arguments, [prompt.UserInput]);
            }

            if (ScriptOptionsParser.Contains(arguments, _userFiles))
            {
                using var prompt = uiCommands.GetRequiredService<IFilePromptCreator>().Create();
                if (prompt.ShowDialog(owner) != DialogResult.OK)
                {
                    return (arguments: null, abort: false, cancel: true);
                }

                arguments = ScriptOptionsParser.ReplaceOption(_userFiles, arguments, [prompt.UserInput]);
            }

            return (arguments, abort: false, cancel: false);
        }

        private static bool RunScriptInternal(ScriptInfo script, IWin32Window owner, IGitUiCommands uiCommands,
            IScriptOptionsProvider scriptOptionsProvider)
        {
            if (string.IsNullOrEmpty(script.Command))
            {
                return false;
            }

            var arguments = script.Arguments;
            if (!string.IsNullOrEmpty(arguments) && uiCommands.BrowseRepo is null)
            {
                var optionDependingOnSelectedRevision
                    = ScriptOptionsParser.Options.FirstOrDefault(option =>
                        ScriptOptionsParser.DependsOnSelectedRevision(option)
                        && ScriptOptionsParser.Contains(arguments, option));
                if (optionDependingOnSelectedRevision is not null)
                {
                    throw new UserExternalOperationException(
                        $"{TranslatedStrings.ScriptText}: '{script.GetDisplayName()}'{Environment.NewLine}'{optionDependingOnSelectedRevision}' {TranslatedStrings.ScriptErrorOptionWithoutRevisionGridText}",
                        new ExternalOperationException(script.Command, arguments, uiCommands.Module.WorkingDir));
                }
            }

            if (script.AskConfirmation &&
                MessageBoxes.Show(owner, $"{TranslatedStrings.ScriptConfirmExecute}: '{script.GetDisplayName()}'?",
                    TranslatedStrings.ScriptText,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
            {
                return false;
            }

            var originalCommand = script.Command;

            (arguments, var abort, var cancelled) = ParseUserInputs(script.GetDisplayName() ?? "<_nameless_script_>",
                script.Arguments, uiCommands, owner, scriptOptionsProvider);

            if (cancelled)
            {
                MessageBoxes.Show(owner, TranslatedStrings.ScriptUserCanceledRun, script.GetDisplayName(),
                    MessageBoxButtons.OK);
                return false;
            }

            (var argument, abort) = abort
                ? (null, true)
                : ScriptOptionsParser.Parse(arguments, uiCommands, owner, scriptOptionsProvider);
            if (abort)
            {
                throw new UserExternalOperationException(
                    $"{TranslatedStrings.ScriptText}: '{script.GetDisplayName()}'{Environment.NewLine}{TranslatedStrings.ScriptErrorOptionWithoutRevisionText}",
                    new ExternalOperationException(script.Command, arguments, uiCommands.Module.WorkingDir));
            }

            var command = OverrideCommandWhenNecessary(originalCommand);
            command = ExpandCommandVariables(command, uiCommands.Module);

            if (script.IsPowerShell)
            {
                PowerShellHelper.RunPowerShell(command, argument, uiCommands.Module.WorkingDir, script.RunInBackground);

                // 'RunPowerShell' always runs the script detached (yet).
                // Hence currently, it does not make sense to trigger the 'RepoChangedNotifier' if '!scriptInfo.RunInBackground'.
                return true;
            }

            if (command.StartsWith(_pluginPrefix))
            {
                command = command.Replace(_pluginPrefix, string.Empty);

                lock (PluginRegistry.Plugins)
                {
                    foreach (var plugin in PluginRegistry.Plugins)
                    {
                        if (string.Equals(plugin.Name, command, StringComparison.CurrentCultureIgnoreCase))
                        {
                            GitUiEventArgs eventArgs = new(owner, uiCommands);
                            if (plugin.Execute(eventArgs))
                            {
                                uiCommands.RepoChangedNotifier.Notify();
                            }

                            return true;
                        }
                    }
                }

                return false;
            }

            if (command.StartsWith(_navigateToPrefix))
            {
                if (uiCommands.BrowseRepo is null)
                {
                    return false;
                }

                command = command.Replace(_navigateToPrefix, string.Empty);
                if (!string.IsNullOrEmpty(command))
                {
                    var result = new Executable(command, uiCommands.Module.WorkingDir).Execute(argument);
                    var revisionRef = result.StandardOutput.Split('\n').FirstOrDefault();

                    if (revisionRef is not null)
                    {
                        uiCommands.BrowseRepo.GoToRef(revisionRef, true);
                    }
                }

                return true;
            }

            if (!script.RunInBackground)
            {
                var success = FormProcess.ShowDialog(owner, uiCommands, argument, uiCommands.Module.WorkingDir, null,
                    true, command);
                if (!success)
                {
                    return false;
                }

                uiCommands.RepoChangedNotifier.Notify();
            }
            else
            {
                if (originalCommand.Equals("{openurl}", StringComparison.CurrentCultureIgnoreCase))
                {
                    OsShellUtil.OpenUrlInDefaultBrowser(argument);
                }
                else
                {
                    // It is totally valid to have a command without an argument, e.g.:
                    //    Command  : myscript.cmd
                    //    Arguments: <blank>
                    new Executable(command, uiCommands.Module.WorkingDir).Start(argument ?? string.Empty);
                }
            }

            return true;
        }

        private static string ExpandCommandVariables(string originalCommand, IGitModule module)
        {
            return originalCommand.Replace("{WorkingDir}", module.WorkingDir);
        }

        private static string OverrideCommandWhenNecessary(string originalCommand)
        {
            // Make sure we are able to run git, even if git is not in the path
            if (originalCommand.Equals("git", StringComparison.CurrentCultureIgnoreCase) ||
                originalCommand.Equals("{git}", StringComparison.CurrentCultureIgnoreCase))
            {
                return AppSettings.GitCommand;
            }

            if (originalCommand.Equals("gitextensions", StringComparison.CurrentCultureIgnoreCase) ||
                originalCommand.Equals("{gitextensions}", StringComparison.CurrentCultureIgnoreCase) ||
                originalCommand.Equals("gitex", StringComparison.CurrentCultureIgnoreCase) ||
                originalCommand.Equals("{gitex}", StringComparison.CurrentCultureIgnoreCase))
            {
                return AppSettings.GetGitExtensionsFullPath();
            }

            if (originalCommand.Equals("{openurl}", StringComparison.CurrentCultureIgnoreCase))
            {
                return OperatingSystem.IsWindows() ? "explorer" : "xdg-open";
            }

            // Prefix should be {plugin:pluginname},{plugin=pluginname}
            var match = PluginRegex.Match(originalCommand);
            if (match.Success && match.Groups.Count > 1)
            {
                originalCommand = $"{_pluginPrefix}{match.Groups["name"].Value.ToLower()}";
            }

            return originalCommand;
        }
    }
}
