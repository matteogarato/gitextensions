using GitExtensions.Extensibility.Git;

namespace ResourceManager;

public interface IGitModuleForm
{
    /// <summary>
    ///     Gets the currently assigned <see cref="IGitUiCommands" /> instance.
    /// </summary>
    IGitUiCommands UiCommands { get; }
}
