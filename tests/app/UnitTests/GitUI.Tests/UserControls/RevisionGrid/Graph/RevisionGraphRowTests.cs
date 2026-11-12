using GitExtensions.Extensibility.Git;
using GitUI.UserControls.RevisionGrid.Graph;

namespace GitUI.AvaloniaTests;

public class RevisionGraphRowTests
{
    private readonly RevisionGraphSegment _segment = new(new RevisionGraphRevision(ObjectId.IndexId, 0),
        new RevisionGraphRevision(ObjectId.WorkTreeId, 0));

    private readonly RevisionGraphSegment _segment1 = new(new RevisionGraphRevision(ObjectId.Random(), 0),
        new RevisionGraphRevision(ObjectId.Random(), 0));

    private readonly RevisionGraphSegment _segment2 = new(new RevisionGraphRevision(ObjectId.Random(), 0),
        new RevisionGraphRevision(ObjectId.Random(), 0));

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void MoveLanesRight_should_do_nothing_if_empty(int fromLane)
    {
        List<RevisionGraphSegment> segments = [];
        IRevisionGraphRow revisionGraphRow = new RevisionGraphRow(_segment.Child, segments, true);
        revisionGraphRow.GetLaneCount().Should().Be(1);

        revisionGraphRow.MoveLanesRight(fromLane);

        revisionGraphRow.GetLaneCount().Should().Be(1);
        revisionGraphRow.Segments.Should().BeEmpty();
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    public void MoveLanesRight_should_move_single_segment(int fromLane, int expectedLane)
    {
        List<RevisionGraphSegment> segments = [_segment];
        IRevisionGraphRow revisionGraphRow = new RevisionGraphRow(_segment.Child, segments, true);
        revisionGraphRow.GetLaneCount().Should().Be(segments.Count);

        revisionGraphRow.MoveLanesRight(fromLane);

        revisionGraphRow.GetLaneCount().Should().Be(fromLane >= segments.Count ? segments.Count : segments.Count + 1);
        revisionGraphRow.GetLaneForSegment(_segment).Index.Should().Be(expectedLane);
    }

    [Theory]
    [InlineData(-1, 1, 2, 3)]
    [InlineData(0, 1, 2, 3)]
    [InlineData(1, 0, 2, 3)]
    [InlineData(2, 0, 1, 3)]
    [InlineData(3, 0, 1, 2)]
    [InlineData(4, 0, 1, 2)]
    public void MoveLanesRight_should_move_segments(int fromLane, int expectedLane, int expectedLane1,
        int expectedLane2)
    {
        List<RevisionGraphSegment> segments = [_segment, _segment1, _segment2];
        IRevisionGraphRow revisionGraphRow = new RevisionGraphRow(_segment.Child, segments, true);
        revisionGraphRow.GetLaneCount().Should().Be(3);

        revisionGraphRow.MoveLanesRight(fromLane);

        revisionGraphRow.GetLaneCount().Should().Be(fromLane >= segments.Count ? segments.Count : segments.Count + 1);
        revisionGraphRow.GetLaneForSegment(_segment).Index.Should().Be(expectedLane);
        revisionGraphRow.GetLaneForSegment(_segment1).Index.Should().Be(expectedLane1);
        revisionGraphRow.GetLaneForSegment(_segment2).Index.Should().Be(expectedLane2);
    }

    [Theory]
    [InlineData(-1, 4, 1, 2, 3)]
    [InlineData(0, 1, 2, 3, 4)]
    [InlineData(0, 2, 1, 3, 4)]
    [InlineData(0, 3, 1, 2, 4)]
    [InlineData(0, 4, 1, 2, 3)]
    [InlineData(1, 0, 1, 2, 3)]
    [InlineData(1, 1, 0, 3, 4)]
    [InlineData(1, 2, 0, 3, 4)]
    [InlineData(1, 3, 0, 2, 4)]
    [InlineData(1, 4, 0, 2, 3)]
    [InlineData(2, 0, 1, 2, 3)]
    [InlineData(2, 1, 0, 2, 3)]
    [InlineData(2, 2, 0, 1, 4)]
    [InlineData(2, 3, 0, 1, 4)]
    [InlineData(2, 4, 0, 1, 3)]
    [InlineData(3, 0, 1, 2, 3)]
    [InlineData(3, 1, 0, 2, 3)]
    [InlineData(3, 2, 0, 1, 3)]
    [InlineData(3, 3, 0, 1, 2)]
    [InlineData(4, 3, 0, 1, 2)]
    public void MoveLanesRight_should_move_segments_twice(int fromLane1, int fromLane2, int expectedLane,
        int expectedLane1, int expectedLane2)
    {
        List<RevisionGraphSegment> segments = [_segment, _segment1, _segment2];
        IRevisionGraphRow revisionGraphRow = new RevisionGraphRow(_segment.Child, segments, true);
        revisionGraphRow.GetLaneCount().Should().Be(3);

        revisionGraphRow.MoveLanesRight(fromLane1);
        revisionGraphRow.MoveLanesRight(fromLane2);

        revisionGraphRow.GetLaneCount().Should().Be(expectedLane2 + 1);
        revisionGraphRow.GetLaneForSegment(_segment).Index.Should().Be(expectedLane);
        revisionGraphRow.GetLaneForSegment(_segment1).Index.Should().Be(expectedLane1);
        revisionGraphRow.GetLaneForSegment(_segment2).Index.Should().Be(expectedLane2);
    }
}
