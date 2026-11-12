using GitExtensions.Extensibility.Git;
using GitUI.UserControls.RevisionGrid.Graph;
using NSubstitute;
using Xunit.Sdk;

namespace GitUI.AvaloniaTests;

public class LaneNodeLocatorTests
{
    private readonly LaneNodeLocator _laneNodeLocator = null!;
    private readonly IRevisionGraphRowProvider _revisionGraphRowProvider = null!;

    public LaneNodeLocatorTests()
    {
        _revisionGraphRowProvider = Substitute.For<IRevisionGraphRowProvider>();
        _laneNodeLocator = new LaneNodeLocator(_revisionGraphRowProvider);
    }

    private RevisionGraphRevision SetupLaneRow(int row, int lane, int laneCount, int nodeLane = -1,
        RevisionGraphSegment firstSegment = null!, RevisionGraphRevision child = null!)
    {
        RevisionGraphRevision node = new(ObjectId.WorkTreeId, 0);
        var revisionGraphRow = Substitute.For<IRevisionGraphRow>();

        List<RevisionGraphSegment> segments = [];
        if (firstSegment is not null)
        {
            segments.Add(firstSegment);
        }

        if (lane < laneCount)
        {
            revisionGraphRow.GetSegmentsForIndex(lane).Returns(segments);
        }

        revisionGraphRow.GetCurrentRevisionLane().Returns(nodeLane);
        if (lane == nodeLane)
        {
            revisionGraphRow.Revision.Returns(node);
        }
        else
        {
            segments.Add(new RevisionGraphSegment(node, child));
        }

        _revisionGraphRowProvider.GetSegmentsForRow(row).Returns(x => revisionGraphRow);
        return node;
    }

    [Fact]
    public void FindPrevNode_should_return_null_if_lane_negative()
    {
        _laneNodeLocator.FindPrevNode(0, -1).Should().Be((null, false, null));
    }

    [Fact]
    public void FindPrevNode_should_return_null_if_rowIndex_negative()
    {
        _laneNodeLocator.FindPrevNode(-1, 0).Should().Be((null, false, null));
    }

    [Fact]
    public void FindPrevNode_should_return_null_if_model_does_not_have_row()
    {
        const int row = 100;
        _revisionGraphRowProvider.GetSegmentsForRow(row).Returns(x => null);
        _laneNodeLocator.FindPrevNode(row, 0).Should().Be((null, false, null));
    }

    [Fact]
    public void FindPrevNode_should_return_null_if_rowIndex_exceeds_model_row_count()
    {
        // It is not up to FindPrevNode() to check this, because:
        // The model does not provide a property "Count". Tough it returns null in this case.
        FindPrevNode_should_return_null_if_model_does_not_have_row();
    }

    [Fact]
    public void FindPrevNode_should_return_the_node_if_it_is_at_the_node_lane_although_lane_count_is_0()
    {
        const int row = 100;
        const int lane = 0;
        var node = SetupLaneRow(row, lane, 0, lane);

        // row.GetCurrentRevisionLane() == lane
        _laneNodeLocator.FindPrevNode(row, lane).Should().Be((node, true, null));
    }

    [Fact]
    public void FindPrevNode_should_return_null_if_lane_exceeds_lane_count_and_lane_is_not_the_node_lane()
    {
        const int row = 100;
        const int lane = 10;
        SetupLaneRow(row, lane, lane);

        // lane >= _revisionGraphRowProvider.GetSegmentsForRow(rowIndex).Count
        _laneNodeLocator.FindPrevNode(row, lane).Should().Be((null, false, null));
    }

    [Fact]
    public void FindPrevNode_should_return_null_if_there_is_no_lane_info()
    {
        const int row = 100;
        const int lane = 3;
        SetupLaneRow(row, lane, lane + 1);
        _revisionGraphRowProvider!.GetSegmentsForRow(row)!.GetSegmentsForIndex(lane)
            .Returns(x => new List<RevisionGraphSegment>());

        // segmentsForLane.Count() <= 0
        _laneNodeLocator.FindPrevNode(row, lane).Should().Be((null, false, null));
    }

    [Fact]
    public void FindPrevNode_should_return_the_parent_node_of_the_single_segment()
    {
        const int row = 100;
        const int lane = 3;
        var laneNode = SetupLaneRow(row, lane, lane + 1);

        // innermost "return"
        _laneNodeLocator.FindPrevNode(row, lane).Should().Be((laneNode, false, null));
    }

    [Fact]
    public void FindPrevNode_should_return_the_parent_and_child_nodes_of_the_single_segment()
    {
        const int row = 100;
        const int lane = 3;
        RevisionGraphRevision childNode = new(ObjectId.WorkTreeId, 0);
        var laneNode = SetupLaneRow(row, lane, lane + 1, child: childNode);

        // innermost "return"
        _laneNodeLocator.FindPrevNode(row, lane).Should().Be((laneNode, false, childNode));
    }

    [Fact]
    public void FindPrevNode_should_return_the_parent_node_of_the_first_segment_and_throw_in_debug_build()
    {
        const int row = 100;
        const int lane = 3;
        RevisionGraphRevision parentNode = new(ObjectId.IndexId, 0);
        RevisionGraphRevision childNode = new(ObjectId.WorkTreeId, 0);
        RevisionGraphSegment segment = new(parentNode, childNode);
        var laneNode = SetupLaneRow(row, lane, lane + 1, firstSegment: segment);

#if !DEBUG
        // innermost "return" in RELEASE build
        _laneNodeLocator.FindPrevNode(row, lane).Should().Be((parentNode, false, segment.Child));
#else
        try
        {
            // Exception before innermost "return" in DEBUG build
            _laneNodeLocator.FindPrevNode(row, lane).Should().Be((parentNode, false, null));
            throw new XunitException("The debug build should throw an exception!");
        }
        catch (Exception x)
        {
            x.Message.Should().Be(string.Format("All segments for a lane should have the same parent.\n"
                                                + "Not fulfilled for rowIndex {0} lane {1} with {2} segments.",
                row, lane, 2));
        }
#endif
    }
}
