using GitCommands.Settings;

namespace GitUI.UserControls.RevisionGrid.Graph;

internal readonly struct RevisionGraphConfig()
{
    public bool MergeGraphLanesHavingCommonParent { get; } = AppSettings.MergeGraphLanesHavingCommonParent.Value;

    public bool ReduceGraphCrossings
    {
        get => !MergeGraphLanesHavingCommonParent;
    }

    public bool RenderGraphWithDiagonals { get; } = AppSettings.RenderGraphWithDiagonals.Value;

    public bool StraightenGraphDiagonals { get; } = AppSettings.StraightenGraphDiagonals.Value;

    /// <summary>
    ///     <see cref="AppSettings.StraightenGraphSegmentsLimit" />
    /// </summary>
    public int StraightenGraphSegmentsLimit { get; } = AppSettings.StraightenGraphSegmentsLimit.Value;
}
