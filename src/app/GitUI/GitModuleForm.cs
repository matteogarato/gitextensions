using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitUI.Infrastructure.Telemetry;
using GitUI.ScriptsEngine;
using ResourceManager;

namespace GitUI;

// NOTE do not make this class abstract as it breaks the WinForms designer in VS

/// <summary>Base <see cref="Form" /> that provides access to <see cref="GitModule" /> and <see cref="IGitUiCommands" />.</summary>
public class GitModuleForm : GitExtensionsForm, IGitUiCommandsSource, IGitModuleForm, IScriptOptionsForm
{
    private IHotkeySettingsLoader? _hotkeySettingsLoader;
    private bool _isReactivation;
    private IScriptsRunner? _scriptsRunner;
    private IGitUiCommands? _uiCommands;

    [Obsolete("For VS designer and translation test only. Do not remove.")]
    protected GitModuleForm()
    {
        if (!IsDesignMode && !IsUnitTestActive)
        {
            throw new InvalidOperationException(
                "This constructor is only to be called by the Visual Studio designer, and the translation unit tests.");
        }
    }

    protected GitModuleForm(IGitUiCommands? commands, bool enablePositionRestore)
        : base(enablePositionRestore)
    {
        if (commands is null && _uiCommands is null)
        {
            // In some cases we may want to initialise a form without a module,
            // e.g. a process dialog that executes a git command which is completely self contained.
        }
        else
        {
            UiCommands = commands!;
        }

        DiagnosticsClient.TrackPageView(GetType().FullName!);
    }

    protected GitModuleForm([NotNull] IGitUiCommands commands)
        : this(commands, true)
    {
    }

    /// <summary>
    ///     Indicates that the process is run by unit tests runner.
    /// </summary>
    internal static bool IsUnitTestActive { get; set; }

    public IHotkeySettingsLoader HotkeySettingsReader
    {
        get => _hotkeySettingsLoader ??
               throw new InvalidOperationException($"{GetType().FullName} was constructed incorrectly.");
        private set => _hotkeySettingsLoader = value;
    }

    public IScriptsRunner ScriptsRunner
    {
        get => _scriptsRunner ??
               throw new InvalidOperationException($"{GetType().FullName} was constructed incorrectly.");
        private set => _scriptsRunner = value;
    }

    /// <inheritdoc />
    [Browsable(false)]
    private IGitUiCommands UiCommands
    {
        get =>
            // If this exception is seen, it might be because the parameterless constructor was called.
            // That constructor is only for use by the VS designer, and translation unit tests.
            // Using it at run time is an error.
            // If no IGitUICommands instance has been passed to the protected ctor on purpose,
            // the properties UICommands and Module must not be accessed.
            _uiCommands
            ?? throw new InvalidOperationException(
                $"{nameof(UiCommands)} is null. {GetType().FullName} was constructed incorrectly.");
        protected set
        {
            ArgumentNullException.ThrowIfNull(value);

            var oldCommands = _uiCommands;
            _uiCommands = value;

            _hotkeySettingsLoader = _uiCommands.GetRequiredService<IHotkeySettingsLoader>();
            _scriptsRunner = _uiCommands.GetRequiredService<IScriptsRunner>();

            OnUICommandsChanged(new GitUiCommandsChangedEventArgs(oldCommands));
        }
    }

    /// <summary>
    ///     Gets the current instance of the git module.
    /// </summary>
    [Browsable(false)]
    public IGitModule Module
    {
        get => UiCommands.Module;
    }

    /// <inheritdoc />
    [Browsable(false)]
    IGitUiCommands IGitModuleForm.UiCommands
    {
        get => UiCommands;
    }

    /// <inheritdoc />
    event EventHandler<GitUiCommandsChangedEventArgs>? IGitUiCommandsSource.UiCommandsChanged
    {
        add => UiCommandsChanged += value;
        remove => UiCommandsChanged -= value;
    }

    /// <inheritdoc />
    [Browsable(false)]
    IGitUiCommands IGitUiCommandsSource.UiCommands
    {
        get => UiCommands;
    }

    public virtual IScriptOptionsProvider GetScriptOptionsProvider()
    {
        return ScriptOptionsProviderBase.Default;
    }

    /// <inheritdoc />
    private event EventHandler<GitUiCommandsChangedEventArgs>? UiCommandsChanged;

    protected override bool ExecuteCommand(int command)
    {
        return ExecuteScriptCommand()
               || base.ExecuteCommand(command);

        bool ExecuteScriptCommand()
        {
            var scriptsManager = UiCommands.GetRequiredService<IScriptsManager>();
            var scriptInfo = scriptsManager.GetScript(command);
            if (scriptInfo is null)
            {
                return false;
            }

            _ = ScriptsRunner.RunScript(scriptInfo, this, UiCommands, GetScriptOptionsProvider());
            return true;
        }
    }

    protected override void OnApplicationActivated()
    {
        base.OnApplicationActivated();

        if (_isReactivation && _uiCommands is { } uiCommands)
        {
            uiCommands.Module.InvalidateGitSettings();
        }
        else
        {
            _isReactivation = true;
        }
    }

    protected virtual void OnUICommandsChanged(GitUiCommandsChangedEventArgs e)
    {
        UiCommandsChanged?.Invoke(this, e);
    }

    public override bool TryGetUICommands([NotNullWhen(true)] out IGitUiCommands? commands)
    {
        commands = UiCommands;
        return commands is not null;
    }
}
