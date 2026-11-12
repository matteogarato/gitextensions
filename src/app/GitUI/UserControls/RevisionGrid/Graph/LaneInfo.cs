namespace GitUI.UserControls.RevisionGrid.Graph;

public sealed class LaneInfo
{
    public LaneInfo(RevisionGraphSegment startSegment, RevisionGraphSegment? segmentToTheLeft,
        RevisionGraphSegment? segmentToTheRight = null)
    {
        StartRevision = startSegment.Child;
        Color = GetColor(StartRevision.Objectid.GetHashCode() ^ startSegment.Parent.Objectid.GetHashCode(),
            segmentToTheLeft, segmentToTheRight);
    }

    public LaneInfo(RevisionGraphSegment startSegment, RevisionGraphSegment? segmentToTheLeft,
        RevisionGraphSegment? segmentToTheRight, LaneInfo derivedFrom)
    {
        StartRevision = startSegment.Parent;
        Color = GetColor(StartRevision.Objectid.GetHashCode(), segmentToTheLeft, segmentToTheRight, derivedFrom.Color);
    }

    public int Color { get; }

    public RevisionGraphRevision StartRevision { get; }

    public int StartScore
    {
        get => StartRevision.Score;
    }

    private static int GetColor(int colorSeed, RevisionGraphSegment? segmentToTheLeft,
        RevisionGraphSegment? segmentToTheRight, int? derivedFromColor = null)
    {
        var leftLaneColor = segmentToTheLeft?.LaneInfo?.Color;
        var rightLaneColor = segmentToTheRight?.LaneInfo?.Color;
        for (;; ++colorSeed)
        {
            var color = RevisionGraphLaneColor.GetColorForLane(colorSeed);
            if (color != leftLaneColor && color != rightLaneColor && color != derivedFromColor)
            {
                return color;
            }
        }
    }
}
