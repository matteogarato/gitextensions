using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitExtUtils.GitUI;
using GitUI.ScriptsEngine;
using ResourceManager;

namespace GitUI;

/// <summary>
///     Base class for a <see cref="UserControl" /> requiring <see cref="IGitModule" /> and <see cref="GitUiCommands" />.
/// </summary>
public class GitModuleControl : GitExtensionsControl, IGitModuleControl
{
    private readonly Lock _lock = new();
    private int _isDisposed;
    private IGitUiCommandsSource? _uiCommandsSource;

    protected GitModuleControl()
    {
    }

    protected override IServiceProvider ServiceProvider
    {
        get => UiCommands;
    }

    /// <summary>
    ///     Gets a <see cref="IGitUiCommandsSource" /> for this control.
    /// </summary>
    /// <remarks>
    ///     If the commands source has not yet been initialised, this property's getter attempts
    ///     to find a control-tree ancestor of type <see cref="IGitUiCommandsSource" />.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    ///     Unable to initialise the source as
    ///     no ancestor of type <see cref="IGitUiCommandsSource" /> was found.
    /// </exception>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Browsable(false)]
    public IGitUiCommandsSource UiCommandsSource
    {
        get
        {
            if (_uiCommandsSource is null)
            {
                lock (_lock)
                {
                    // Double check locking
                    if (_uiCommandsSource is null)
                    {
                        // Search ancestors for an implementation of IGitUICommandsSource
                        UiCommandsSource = this.FindAncestors().OfType<IGitUiCommandsSource>().FirstOrDefault()
                                           ?? throw new InvalidOperationException(
                                               "The UI Command Source is not available for this control. Are you calling methods before adding it to the parent control?");
                    }
                }
            }

            // ReSharper disable once AssignNullToNotNullAttribute
            return _uiCommandsSource!;
        }
        set
        {
            if (_uiCommandsSource is not null)
            {
                throw new InvalidOperationException($"{nameof(UiCommandsSource)} is already set.");
            }

            ArgumentNullException.ThrowIfNull(value);
            _uiCommandsSource = value;

            OnUICommandsSourceSet(_uiCommandsSource);
        }
    }

    IGitUiCommands IGitModuleControl.UICommands
    {
        get => UiCommandsSource.UiCommands;
    }

    /// <summary>Gets the <see cref="UiCommands" />' <see cref="IGitModule" /> reference.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Browsable(false)]
    public IGitModule Module
    {
        get => UiCommands.Module;
    }

    /// <summary>
    ///     Gets the <see cref="UiCommandsSource" />'s <see cref="GitUiCommands" /> reference.
    /// </summary>
    [Browsable(false)]
    IGitUiCommands IGitModuleControl.UiCommands
    {
        get => UiCommandsSource.UiCommands;
    }

    /// <summary>
    ///     Occurs after the <see cref="UiCommandsSource" /> is set.
    ///     Will only occur once, as the source cannot change after being set.
    /// </summary>
    [Browsable(false)]
    public event EventHandler<GitUiCommandsSourceEventArgs>? UiCommandsSourceSet;

    /// <summary>
    ///     Gets the UI commands, if they've initialised.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This method will not attempt to initialise the commands if they have not
    ///         yet been initialised.
    ///     </para>
    ///     <para>
    ///         By contrast, the <see cref="UiCommands" /> property attempts to initialise
    ///         the value if not previously initialised.
    ///     </para>
    /// </remarks>
    internal bool TryGetUiCommandsDirect([NotNullWhen(true)] out IGitUiCommands? commands)
    {
        commands = _uiCommandsSource?.UiCommands;
        return commands is not null;
    }

    protected override void Dispose(bool disposing)
    {
        if (Interlocked.CompareExchange(ref _isDisposed, 1, 0) != 0)
        {
            return;
        }

        if (_uiCommandsSource is not null)
        {
            DisposeUiCommandsSource();
        }

        DisposeCustomResources();

        base.Dispose(disposing);
    }

    protected virtual void DisposeCustomResources()
    {
    }

    /// <summary>Occurs when the <see cref="UiCommandsSource" /> is disposed.</summary>
    protected virtual void DisposeUiCommandsSource()
    {
    }

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

            var scriptsRunner = UiCommands.GetRequiredService<IScriptsRunner>();
            _ = scriptsRunner.RunScript(scriptInfo, this, UiCommands, FindScriptOptionsProvider());
            return true;
        }
    }

    internal IScriptOptionsProvider FindScriptOptionsProvider()
    {
        for (Control? control = this; control is not null; control = control.Parent)
        {
            if (control is GitModuleControl gitModuleControl && gitModuleControl.GetScriptOptionsProvider() is
                    { } scriptOptionsProvider)
            {
                return scriptOptionsProvider;
            }

            if (control is GitModuleForm gitModuleForm)
            {
                return gitModuleForm.GetScriptOptionsProvider();
            }
        }

        return ScriptOptionsProviderBase.Default;
    }

    protected virtual IScriptOptionsProvider? GetScriptOptionsProvider()
    {
        return null;
    }

    /// <summary>Raises the <see cref="UiCommandsSourceSet" /> event.</summary>
    protected virtual void OnUICommandsSourceSet(IGitUiCommandsSource source)
    {
        UiCommandsSourceSet?.Invoke(this, new GitUiCommandsSourceEventArgs(source));
    }
}
