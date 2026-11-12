using GitExtensions.Extensibility.Git;
using GitUI.ScriptsEngine;
using ResourceManager;

namespace GitUI;

// Twin of GitUI/GitModuleForm.cs (reduced): access to IGitUICommands and the GitModule.
// The hotkey and scripts plumbing of the WinForms base arrives with the Phase 2 hotkey work.

/// <summary>Base window that provides access to the module and <see cref="IGitUiCommands" />.</summary>
public class GitModuleForm : GitExtensionsForm, IGitModuleForm, IScriptOptionsForm
{
    private IGitUiCommands? _uiCommands;

    /// <summary>For the visual designer and construction tests only, like WinForms.</summary>
    protected GitModuleForm()
    {
    }

    protected GitModuleForm(IGitUiCommands? commands, bool enablePositionRestore)
    {
        _uiCommands = commands;
    }

    protected IGitUiCommands UiCommands
    {
        get => _uiCommands
               ?? throw new InvalidOperationException(
                   $"{nameof(UiCommands)} is null. {GetType().FullName} was constructed incorrectly.");
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _uiCommands = value;
        }
    }

    /// <summary>Gets the module of the currently set <see cref="UiCommands" />.</summary>
    public IGitModule Module
    {
        get => UiCommands.Module;
    }

    IGitUiCommands IGitModuleForm.UiCommands
    {
        get => UiCommands;
    }

    IScriptOptionsProvider IScriptOptionsForm.GetScriptOptionsProvider()
    {
        return ScriptOptionsProviderBase.Default;
    }
}
