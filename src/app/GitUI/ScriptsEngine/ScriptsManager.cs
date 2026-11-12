using System.ComponentModel;
using System.Diagnostics;
using System.Xml;
using System.Xml.Serialization;
using GitCommands.Settings;
using GitExtensions.Extensibility.Git;

namespace GitUI.ScriptsEngine;

internal sealed partial class ScriptsManager : IScriptsManager, IScriptsRunner
{
    internal const int MinimumUserScriptId = 9000;
    private static readonly XmlSerializer _serializer = new(typeof(BindingList<ScriptInfo>));
    private BindingList<ScriptInfo>? _scripts;

    BindingList<ScriptInfo> IScriptsManager.GetScripts()
    {
        if (_scripts is null)
        {
            _scripts = DeserializeFromXml(AppSettings.OwnScripts);
            FixAmbiguousHotkeyCommandIdentifiers(_scripts);
        }

        return _scripts;

        static void FixAmbiguousHotkeyCommandIdentifiers(BindingList<ScriptInfo> loadedScripts)
        {
            HashSet<int> ids = [];

            foreach (var script in loadedScripts)
            {
                if (!ids.Add(script.HotkeyCommandIdentifier))
                {
                    script.HotkeyCommandIdentifier = NextHotkeyCommandIdentifier(loadedScripts);
                }
            }
        }
    }

    ScriptInfo? IScriptsManager.GetScript(int scriptId)
    {
        foreach (var script in GetScripts())
        {
            if (script.HotkeyCommandIdentifier == scriptId)
            {
                return script;
            }
        }

        return null;
    }

    string IScriptsManager.SerializeIntoXml()
    {
        try
        {
            XmlWriterSettings xmlWriterSettings = new() { Indent = true };
            using StringWriter sw = new();
            using var xmlWriter = XmlWriter.Create(sw, xmlWriterSettings);

            _serializer.Serialize(xmlWriter, _scripts);
            return sw.ToString();
        }
        catch
        {
            return null!;
        }
    }

    bool IScriptsRunner.RunEventScripts<THostForm>(ScriptEvent scriptEvent, THostForm form)
    {
        foreach (var script in GetScripts()
                     .Where(scriptInfo => scriptInfo.Enabled && scriptInfo.OnEvent == scriptEvent))
        {
            var executed = ScriptRunner.RunScript(script, form, form.UiCommands, form.GetScriptOptionsProvider());
            if (!executed)
            {
                return false;
            }
        }

        return true;
    }

    bool IScriptsRunner.RunScript(ScriptInfo scriptInfo, IWin32Window owner, IGitUiCommands commands,
        IScriptOptionsProvider scriptOptionsProvider)
    {
        return ScriptRunner.RunScript(scriptInfo, owner, commands, scriptOptionsProvider);
    }

    private static BindingList<ScriptInfo> DeserializeFromXml(string? xml)
    {
        // When there is nothing to deserialize, add default scripts
        if (string.IsNullOrEmpty(xml))
        {
            return GetDefaultScripts();
        }

        try
        {
            using StringReader stringReader = new(xml);
            using XmlTextReader xmlReader = new(stringReader);
            return (BindingList<ScriptInfo>)_serializer.Deserialize(xmlReader)!;
        }
        catch (Exception ex)
        {
            Trace.WriteLine(ex.Message);
            return DeserializeFromOldFormat(xml);
        }

        BindingList<ScriptInfo> GetDefaultScripts()
        {
            return
            [
                new ScriptInfo
                {
                    HotkeyCommandIdentifier = 9000,
                    Name = "Fetch changes after commit",
                    Command = "git",
                    Arguments = "fetch",
                    RunInBackground = false,
                    AskConfirmation = true,
                    OnEvent = ScriptEvent.AfterCommit,
                    AddToRevisionGridContextMenu = false,
                    Enabled = false
                },
                new ScriptInfo
                {
                    HotkeyCommandIdentifier = 9001,
                    Name = "Update submodules after pull",
                    Command = "git",
                    Arguments = "submodule update --init --recursive",
                    RunInBackground = false,
                    AskConfirmation = true,
                    OnEvent = ScriptEvent.AfterPull,
                    AddToRevisionGridContextMenu = false,
                    Enabled = false
                },
                new ScriptInfo
                {
                    HotkeyCommandIdentifier = 9005,
                    Icon = "EditFile",
                    Name = "&Open in VS Code",
                    Command = "bash",
                    Arguments = "-c '"
                                + @"if [ -z ""{SelectedRelativePaths}"" ]; then code .; "
                                + @"elif [ -d ""{SelectedRelativePaths}"" ]; then code {{SelectedRelativePaths}}; "
                                + @"elif [ ! -f ""{SelectedRelativePaths}"" ]; then code . --goto {{SelectedRelativePaths}}; "
                                + @"else code . --goto {{SelectedRelativePaths}}:{LineNumber}:{ColumnNumber}; "
                                + @"fi'",
                    RunInBackground = true,
                    AskConfirmation = false,
                    OnEvent = ScriptEvent.ShowInUserMenuBar,
                    AddToRevisionGridContextMenu = false,
                    Enabled = false
                },
                new ScriptInfo
                {
                    HotkeyCommandIdentifier = 9002,
                    Name = "&Example",
                    Command = @"c:\windows\system32\calc.exe",
                    Arguments = "",
                    RunInBackground = false,
                    AskConfirmation = false,
                    OnEvent = ScriptEvent.ShowInUserMenuBar,
                    AddToRevisionGridContextMenu = false,
                    Enabled = false
                },
                new ScriptInfo
                {
                    HotkeyCommandIdentifier = 9003,
                    Name = "Open on GitHub",
                    Command = "{openurl}",
                    Arguments = "https://github.com{cDefaultRemotePathFromUrl}/commit/{sHash}",
                    RunInBackground = false,
                    AskConfirmation = false,
                    OnEvent = 0,
                    AddToRevisionGridContextMenu = true,
                    Enabled = false
                },
                new ScriptInfo
                {
                    HotkeyCommandIdentifier = 9004,
                    Name = "Fetch All Submodules",
                    Command = "git",
                    Arguments = "submodule foreach --recursive git fetch --all",
                    RunInBackground = false,
                    AskConfirmation = false,
                    OnEvent = 0,
                    AddToRevisionGridContextMenu = true,
                    Enabled = false
                },
                new ScriptInfo
                {
                    HotkeyCommandIdentifier = 9006,
                    Name = "Convert workspace file to LF",
                    Command = "bash.exe",
                    Arguments = "-c 'dos2unix.exe {{SelectedRelativePaths}}'",
                    RunInBackground = true,
                    AskConfirmation = false,
                    OnEvent = 0,
                    AddToRevisionGridContextMenu = false,
                    Enabled = false
                },
                new ScriptInfo
                {
                    HotkeyCommandIdentifier = 9007,
                    Name = "Convert workspace file to CRLF",
                    Command = "bash.exe",
                    Arguments = "-c 'unix2dos.exe {{SelectedRelativePaths}}'",
                    RunInBackground = true,
                    AskConfirmation = false,
                    OnEvent = 0,
                    AddToRevisionGridContextMenu = false,
                    Enabled = false
                }
            ];
        }

        BindingList<ScriptInfo> DeserializeFromOldFormat(string inputString)
        {
            const string paramSeparator = "<_PARAM_SEPARATOR_>";
            const string scriptSeparator = "<_SCRIPT_SEPARATOR_>";

            BindingList<ScriptInfo> scripts = [];

            if (inputString.Contains(paramSeparator) || inputString.Contains(scriptSeparator))
            {
                foreach (var script in inputString.Split([scriptSeparator], StringSplitOptions.RemoveEmptyEntries))
                {
                    var parameters = script.Split([paramSeparator], StringSplitOptions.None);

                    scripts.Add(new ScriptInfo
                    {
                        Name = parameters[0],
                        Command = parameters[1],
                        Arguments = parameters[2],
                        AddToRevisionGridContextMenu = parameters[3] == "yes",
                        Enabled = true,
                        RunInBackground = false
                    });
                }
            }

            return scripts;
        }
    }

    internal static int NextHotkeyCommandIdentifier(BindingList<ScriptInfo> scripts)
    {
        return scripts.Select(s => s.HotkeyCommandIdentifier).Max() + 1;
    }
}
