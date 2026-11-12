using GitExtensions.Extensibility.Git;

namespace GitCommands.Submodules;

public class DetailedSubmoduleInfo
{
    public bool IsDirty { get; set; }

    public SubmoduleStatus? Status
    {
        get => RawStatus?.Status ?? SubmoduleStatus.Unknown;
    }

    public string? AddedAndRemovedText
    {
        get => RawStatus?.AddedAndRemovedString() ?? string.Empty;
    }

    public GitSubmoduleStatus? RawStatus { get; set; }
}
