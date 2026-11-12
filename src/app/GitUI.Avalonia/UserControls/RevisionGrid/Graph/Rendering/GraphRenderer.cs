using Avalonia.Media;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitUI.Compat;
using AvaloniaRect = Avalonia.Rect;

namespace GitUI.UserControls.RevisionGrid.Graph.Rendering;

// Twin of GitUI/UserControls/RevisionGrid/Graph/Rendering/GraphRenderer.cs.
// The lane/segment logic is kept identical to upstream; the GDI Graphics parameter is
// replaced by Avalonia's DrawingContext drawing into a per-row cell whose origin is (0,0)
// (GDI's RenderingOrigin). SmoothingMode switches do not apply and are dropped.
internal static class GraphRenderer
{
    internal const int MaxLanes = RevisionGraph.MaxLanes;

    private const int _noLane = -10;

    public static readonly int LaneLineWidth = DpiUtil.Scale(2);
    public static readonly int LaneWidth = DpiUtil.Scale(16);
    public static readonly int NodeDimension = DpiUtil.Scale(10);

    public static void DrawItem(RevisionGraphConfig config, DrawingContext g, int index, int rowHeight,
        Func<int, IRevisionGraphRow?> getSegmentsForRow,
        RevisionGraphDrawStyle revisionGraphDrawStyle,
        ObjectId headId)
    {
        DrawItem();

        return;

        void DrawItem()
        {
            var currentRow = getSegmentsForRow(index);
            if (currentRow is null)
            {
                return;
            }

            var previousRow = getSegmentsForRow(index - 1);
            var nextRow = getSegmentsForRow(index + 1);

            SegmentPointsInfo p = new();
            p.Center.Y = rowHeight / 2;
            p.Start.Y = p.Center.Y - rowHeight;
            p.End.Y = p.Center.Y + rowHeight;

            LaneInfo? currentRowRevisionLaneInfo = null;

            foreach (var revisionGraphSegment in currentRow.Segments.Reverse()
                         .OrderBy(s => s.Child.IsRelative))
            {
                var skipSecondarySharedSegments =
                    revisionGraphDrawStyle is not (RevisionGraphDrawStyle.DrawNonRelativesGray
                        or RevisionGraphDrawStyle.HighlightSelected);
                var lanes = GetLanesInfo(revisionGraphSegment, previousRow, currentRow, nextRow,
                    skipSecondarySharedSegments, config.MergeGraphLanesHavingCommonParent,
                    li => currentRowRevisionLaneInfo = li);
                if (!lanes.DrawFromStart && !lanes.DrawToEnd)
                {
                    continue;
                }

                p.Start.X = (int)((lanes.StartLane + 0.5) * LaneWidth);
                p.Center.X = (int)((lanes.CenterLane + 0.5) * LaneWidth);
                p.End.X = (int)((lanes.EndLane + 0.5) * LaneWidth);

                var laneBrush = GetBrushForLaneInfo(revisionGraphSegment.LaneInfo,
                    revisionGraphSegment.Child.IsRelative, revisionGraphDrawStyle);
                Pen lanePen = new(laneBrush, LaneLineWidth);
                SegmentRenderer segmentRenderer = new(new Context(config, g, lanePen, LaneWidth, rowHeight));

                if (config.RenderGraphWithDiagonals)
                {
                    Lazy<DiagonalSegmentInfo> previousSegmentInfo = new(() =>
                    {
                        Validates.NotNull(previousRow);
                        var previousLanes = GetLanesInfo(revisionGraphSegment,
                            getSegmentsForRow(index - 2), previousRow, currentRow, skipSecondarySharedSegments,
                            config.MergeGraphLanesHavingCommonParent);
                        return GetDiagonalSegmentInfo(previousLanes, config.MergeGraphLanesHavingCommonParent);
                    });
                    Lazy<DiagonalSegmentInfo> nextSegmentInfo = new(() =>
                    {
                        Validates.NotNull(nextRow);
                        var nextLanes = GetLanesInfo(revisionGraphSegment, currentRow, nextRow,
                            getSegmentsForRow(index + 2), skipSecondarySharedSegments,
                            config.MergeGraphLanesHavingCommonParent);
                        return GetDiagonalSegmentInfo(nextLanes, config.MergeGraphLanesHavingCommonParent);
                    });
                    var currentSegmentInfo =
                        GetDiagonalSegmentInfo(lanes, config.MergeGraphLanesHavingCommonParent);

                    DrawSegmentWithDiagonals(segmentRenderer, p, previousSegmentInfo, currentSegmentInfo,
                        nextSegmentInfo);
                }
                else
                {
                    DrawSegmentCurvy(segmentRenderer, p, lanes);
                }
            }

            if (currentRow.GetCurrentRevisionLane() < MaxLanes)
            {
                var centerX = (int)((currentRow.GetCurrentRevisionLane() + 0.5) * LaneWidth);
                Rectangle nodeRect = new(centerX - (NodeDimension / 2), p.Center.Y - (NodeDimension / 2), NodeDimension,
                    NodeDimension);

                var square = currentRow.Revision.GitRevision!.Refs.Count > 0;
                var hasOutline = currentRow.Revision.GitRevision.ObjectId == headId;

                var brush = GetBrushForLaneInfo(currentRowRevisionLaneInfo, currentRow.Revision.IsRelative,
                    revisionGraphDrawStyle);
                if (square)
                {
                    g.DrawRectangle(brush, null, ToAvalonia(nodeRect));
                }
                else //// Circle
                {
                    g.DrawEllipse(brush, null, ToAvalonia(nodeRect));
                }

                if (hasOutline)
                {
                    nodeRect.Inflate(1, 1);

                    // Upstream uses SystemColors.WindowText; a mid-gray works on both theme variants
                    // until the theming twin provides the actual foreground color.
                    Pen pen = new(Brushes.Gray, 2);
                    if (square)
                    {
                        g.DrawRectangle(null, pen, ToAvalonia(nodeRect));
                    }
                    else //// Circle
                    {
                        g.DrawEllipse(null, pen, ToAvalonia(nodeRect));
                    }
                }
            }
        }
    }

    private static SegmentLanesInfo GetLanesInfo(in RevisionGraphSegment revisionGraphSegment,
        in IRevisionGraphRow? previousRow,
        in IRevisionGraphRow currentRow,
        in IRevisionGraphRow? nextRow,
        in bool skipSecondarySharedSegments,
        in bool mergeGraphLanesHavingCommonParent,
        in Action<LaneInfo?>? setLaneInfo = null)
    {
        var currentLane = currentRow.GetLaneForSegment(revisionGraphSegment);

        var startLane = _noLane;
        var centerLane = _noLane;
        var endLane = _noLane;

        // Avoid drawing the same curve twice (caused aliasing artifacts, particularly when in different colors)
        if (skipSecondarySharedSegments && currentLane.Sharing == LaneSharing.Entire)
        {
            return new SegmentLanesInfo(startLane, centerLane, endLane, endLane, false, false, false);
        }

        centerLane = currentLane.Index;
        var isTheRevisionLane = true;
        if (revisionGraphSegment.Parent == currentRow.Revision)
        {
            // This lane ends here
            startLane = GetLaneForRow(previousRow, revisionGraphSegment);
            setLaneInfo?.Invoke(revisionGraphSegment.LaneInfo);
        }
        else
        {
            if (revisionGraphSegment.Child == currentRow.Revision)
            {
                // This lane starts here
                endLane = GetLaneForRow(nextRow, revisionGraphSegment);
                setLaneInfo?.Invoke(revisionGraphSegment.LaneInfo);
            }
            else
            {
                // This lane crosses
                startLane = GetLaneForRow(previousRow, revisionGraphSegment);
                endLane = GetLaneForRow(nextRow, revisionGraphSegment);
                isTheRevisionLane = false;
            }
        }

        var primaryEndLane = endLane;
        switch (currentLane.Sharing)
        {
            case LaneSharing.DifferentStart:
                if (mergeGraphLanesHavingCommonParent)
                {
                    if (skipSecondarySharedSegments)
                    {
                        endLane = _noLane;
                    }
                }
                else if (endLane != _noLane)
                {
                    throw new Exception(
                        $"{currentRow.Revision.Objectid.ToShortString()}: lane {centerLane} has DifferentStart but has EndLane {endLane} (StartLane {startLane})");
                }

                break;

            case LaneSharing.DifferentEnd:
                if (startLane != _noLane)
                {
                    throw new Exception(
                        $"{currentRow.Revision.Objectid.ToShortString()}: lane {centerLane} has DifferentEnd but has StartLane {startLane} (EndLane {endLane})");
                }

                break;
        }

        return new SegmentLanesInfo(startLane, centerLane, endLane, primaryEndLane, isTheRevisionLane,
            startLane >= 0 && centerLane >= 0 && (startLane <= MaxLanes || centerLane <= MaxLanes),
            endLane >= 0 && centerLane >= 0 && (endLane <= MaxLanes || centerLane <= MaxLanes));
    }

    private static DiagonalSegmentInfo GetDiagonalSegmentInfo(in SegmentLanesInfo currentLanes,
        bool mergeGraphLanesHavingCommonParent)
    {
        var drawFromStart = currentLanes.DrawFromStart;
        var drawToEnd = currentLanes.DrawToEnd;
        var isTheRevisionLane = currentLanes.IsTheRevisionLane;

        var startShift = currentLanes.CenterLane - currentLanes.StartLane;
        var endShift = currentLanes.EndLane - currentLanes.CenterLane;
        var startIsDiagonal = Math.Abs(startShift) == 1;
        var endIsDiagonal = Math.Abs(endShift) == 1;
        var isBowOfDiagonals = startIsDiagonal && endIsDiagonal && -Math.Sign(startShift) == Math.Sign(endShift);
        var bowOffset = LaneWidth / 6;
        var junctionBowOffset = mergeGraphLanesHavingCommonParent ? LaneLineWidth : bowOffset;
        var horizontalOffset = isBowOfDiagonals ? -Math.Sign(startShift) * junctionBowOffset : 0;

        // Go perpendicularly through the center in order to avoid crossing independend nodes
        var drawCenterToStartPerpendicularly =
            drawFromStart && (startShift == 0 || (!startIsDiagonal && !isTheRevisionLane));
        var drawCenterToEndPerpendicularly = drawToEnd && (endShift == 0 || (!endIsDiagonal && !isTheRevisionLane));
        var drawCenterPerpendicularly = isBowOfDiagonals;
        var drawCenter = drawCenterPerpendicularly
                         || !drawFromStart
                         || !drawToEnd
                         || (!drawCenterToStartPerpendicularly && !drawCenterToEndPerpendicularly);

        // handle non-straight junctions
        if (currentLanes.EndLane < 0 && currentLanes.PrimaryEndLane >= 0 && startShift != 0)
        {
            endShift = currentLanes.PrimaryEndLane - currentLanes.CenterLane;
            var sameDirection = Math.Sign(endShift) == Math.Sign(startShift);
            if (startIsDiagonal)
            {
                var endDelta = Math.Abs(endShift);
                if (!sameDirection || endDelta > 1)
                {
                    drawCenterToEndPerpendicularly = true;
                    drawCenter = false;
                    horizontalOffset = -Math.Sign(startShift) *
                                       (endDelta != 1 || sameDirection ? LaneLineWidth / 3 : bowOffset);
                }
            }
            else if (Math.Abs(endShift) == 1)
            {
                // multi-lane crossing continued by a diagonal
                drawCenterToStartPerpendicularly = false;
                if (!sameDirection)
                {
                    // bow
                    horizontalOffset = -Math.Sign(startShift) * LaneLineWidth * 2 / 3;
                }
            }
            else
            {
                // multi-lane crossing continued by a straight or a multi-lane crossing
                drawCenterToStartPerpendicularly = false;
            }
        }

        return new DiagonalSegmentInfo(drawFromStart, drawToEnd,
            drawCenterToStartPerpendicularly, drawCenter, drawCenterPerpendicularly, drawCenterToEndPerpendicularly,
            isTheRevisionLane, horizontalOffset);
    }

    private static void DrawSegmentCurvy(SegmentRenderer segmentRenderer, in SegmentPointsInfo p,
        in SegmentLanesInfo lanes)
    {
        if (lanes.DrawFromStart)
        {
            segmentRenderer.DrawTo(p.Start);
        }

        segmentRenderer.DrawTo(p.Center);

        if (lanes.DrawToEnd)
        {
            segmentRenderer.DrawTo(p.End);
        }
    }

    private static void DrawSegmentWithDiagonals(SegmentRenderer segmentRenderer,
        in SegmentPointsInfo p,
        in Lazy<DiagonalSegmentInfo> previousSegmentInfo,
        in DiagonalSegmentInfo current,
        in Lazy<DiagonalSegmentInfo> nextSegmentInfo)
    {
        var halfPerpendicularHeight = segmentRenderer.RowHeight / 6;

        if (current.DrawFromStart)
        {
            var previous = previousSegmentInfo.Value;
            DebugHelpers.Assert(previous.DrawToEnd || AppSettings.MergeGraphLanesHavingCommonParent.Value,
                nameof(previous.DrawToEnd));
            var startX = p.Start.X + previous.HorizontalOffset;
            if (previous.DrawCenterToEndPerpendicularly)
            {
                segmentRenderer.DrawTo(startX, p.Start.Y + halfPerpendicularHeight);
            }
            else if (previous.DrawCenter)
            {
                segmentRenderer.DrawTo(startX, p.Start.Y, previous.DrawCenterPerpendicularly);
            }
            else
            {
                segmentRenderer.DrawTo(startX, p.Start.Y - halfPerpendicularHeight);
            }
        }

        var centerX = p.Center.X + current.HorizontalOffset;

        if (current.DrawCenterToStartPerpendicularly)
        {
            segmentRenderer.DrawTo(centerX, p.Center.Y - halfPerpendicularHeight);
        }

        if (current.DrawCenter)
        {
            segmentRenderer.DrawTo(centerX, p.Center.Y, current.DrawCenterPerpendicularly);
        }

        if (current.DrawCenterToEndPerpendicularly)
        {
            segmentRenderer.DrawTo(centerX, p.Center.Y + halfPerpendicularHeight);
        }

        if (current.DrawToEnd)
        {
            var next = nextSegmentInfo.Value;
            DebugHelpers.Assert(next.DrawFromStart || AppSettings.MergeGraphLanesHavingCommonParent.Value,
                nameof(next.DrawFromStart));
            var endX = p.End.X + next.HorizontalOffset;
            if (next.DrawCenterToStartPerpendicularly)
            {
                segmentRenderer.DrawTo(endX, p.End.Y - halfPerpendicularHeight);
            }
            else if (next.DrawCenter)
            {
                segmentRenderer.DrawTo(endX, p.End.Y, next.DrawCenterPerpendicularly);
            }
            else
            {
                segmentRenderer.DrawTo(endX, p.End.Y + halfPerpendicularHeight);
            }
        }
    }

    private static IBrush GetBrushForLaneInfo(LaneInfo? laneInfo, bool isRelative,
        RevisionGraphDrawStyle revisionGraphDrawStyle)
    {
        // laneInfo can be null for revisions without parents and children, especially when filtering, draw them gray, too
        if (laneInfo is null
            || (!isRelative && revisionGraphDrawStyle is RevisionGraphDrawStyle.DrawNonRelativesGray
                or RevisionGraphDrawStyle.HighlightSelected))
        {
            return RevisionGraphLaneColor.NonRelativeBrush;
        }

        return RevisionGraphLaneColor.GetBrushForLane(laneInfo.Color);
    }

    private static int GetLaneForRow(IRevisionGraphRow? row, RevisionGraphSegment revisionGraphRevision)
    {
        if (row is not null)
        {
            var lane = row.GetLaneForSegment(revisionGraphRevision).Index;
            if (lane >= 0)
            {
                return lane;
            }
        }

        return _noLane;
    }

    private static AvaloniaRect ToAvalonia(in Rectangle rect)
    {
        return new AvaloniaRect(rect.X, rect.Y, rect.Width, rect.Height);
    }
}
