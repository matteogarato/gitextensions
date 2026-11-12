using GitExtensions.Extensibility.Git;

namespace GitUI;

public sealed class GitUiCommandsChangedEventArgs(IGitUiCommands? oldCommands) : EventArgs
{
    public IGitUiCommands? OldCommands { get; } = oldCommands;
}

/// <summary>Provides <see cref="IGitUiCommands" /> and a change notification.</summary>
public interface IGitUiCommandsSource
{
    /// <summary>Gets the <see cref="IGitUiCommands" /> value.</summary>
    /// <exception cref="InvalidOperationException">Attempting to get a value when none has been set.</exception>
    IGitUiCommands UiCommands { get; }

    /// <summary>Raised after <see cref="UiCommands" /> changes.</summary>
    event EventHandler<GitUiCommandsChangedEventArgs> UiCommandsChanged;
}
