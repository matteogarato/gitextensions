using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitUI.UserControls.RevisionGrid;
using GitUI.UserControls.RevisionGrid.Columns;
using GitUI.UserControls.RevisionGrid.Graph;
using NSubstitute;

namespace GitUITests.UserControls.RevisionGrid.Graph;

public class HoverHighlightCalculatorTests
{
    private const int _rowHeight = 42;

    [Fact]
    public void SetHoverHighlight_should_include_tip_and_ancestors_within_visible_range()
    {
        const string tipId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string parentId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        const string rootId = "cccccccccccccccccccccccccccccccccccccccc";
        const string otherTipId = "dddddddddddddddddddddddddddddddddddddddd";

        RevisionGraph revisionGraph = new();
        var main = CreateBranchRef("main", true);
        revisionGraph.Add(CreateRevision(tipId, [parentId], main));
        revisionGraph.Add(CreateRevision(otherTipId, [rootId], CreateBranchRef("feature", true)));
        revisionGraph.Add(CreateRevision(parentId, [rootId]));
        revisionGraph.Add(CreateRevision(rootId, []));

        const int rowCount = 4;
        revisionGraph.CacheTo(rowCount - 1, rowCount - 1, TestContext.Current.CancellationToken);

        var testAccessor = CreateProvider(revisionGraph);
        VisibleRowRange range = new(0, rowCount);
        testAccessor.RenderGraphToCache(range, rowCount - 1, _rowHeight);

        testAccessor.HoverHighlight.SetHoverHighlight(main, GetRowForId(revisionGraph, rowCount, tipId));

        testAccessor.HoverHighlight.HighlightedIds.Should().BeEquivalentTo(
        [
            ObjectId.Parse(tipId),
            ObjectId.Parse(parentId),
            ObjectId.Parse(rootId)
        ]);
    }

    [Fact]
    public void SetHoverHighlight_should_include_one_ancestor_outside_visible_range()
    {
        const string tipId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string parentId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        const string rootId = "cccccccccccccccccccccccccccccccccccccccc";

        RevisionGraph revisionGraph = new();
        var main = CreateBranchRef("main", true);
        revisionGraph.Add(CreateRevision(tipId, [parentId], main));
        revisionGraph.Add(CreateRevision(parentId, [rootId]));
        revisionGraph.Add(CreateRevision(rootId, []));

        const int rowCount = 3;
        revisionGraph.CacheTo(rowCount - 1, rowCount - 1, TestContext.Current.CancellationToken);

        var testAccessor = CreateProvider(revisionGraph);

        // Visible range covers only tip and parent; root is scrolled out of view.
        VisibleRowRange range = new(0, 2);
        testAccessor.RenderGraphToCache(range, 1, _rowHeight);

        testAccessor.HoverHighlight.SetHoverHighlight(main, GetRowForId(revisionGraph, rowCount, tipId));

        testAccessor.HoverHighlight.HighlightedIds.Should().BeEquivalentTo(
        [
            ObjectId.Parse(tipId),
            ObjectId.Parse(parentId),
            ObjectId.Parse(rootId)
        ]);
    }

    [Fact]
    public void SetHoverHighlight_should_clear_highlighted_ids_when_gitRef_is_null()
    {
        const string tipId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string parentId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        RevisionGraph revisionGraph = new();
        var main = CreateBranchRef("main", true);
        revisionGraph.Add(CreateRevision(tipId, [parentId], main));
        revisionGraph.Add(CreateRevision(parentId, []));

        const int rowCount = 2;
        revisionGraph.CacheTo(rowCount - 1, rowCount - 1, TestContext.Current.CancellationToken);

        var testAccessor = CreateProvider(revisionGraph);
        VisibleRowRange range = new(0, rowCount);
        testAccessor.RenderGraphToCache(range, rowCount - 1, _rowHeight);

        testAccessor.HoverHighlight.SetHoverHighlight(main, GetRowForId(revisionGraph, rowCount, tipId));
        testAccessor.HoverHighlight.HighlightedIds.Should().NotBeNull();

        testAccessor.RenderGraphToCache(range, rowCount - 1, _rowHeight);
        testAccessor.HoverHighlight.IsDirty.Should().BeFalse();

        testAccessor.HoverHighlight.SetHoverHighlight(null);
        testAccessor.HoverHighlight.HighlightedIds.Should().BeNull();
        testAccessor.HoverHighlight.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void SetHoverHighlight_should_not_mark_cache_dirty_when_hover_selection_is_unchanged()
    {
        const string tipId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string parentId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        RevisionGraph revisionGraph = new();
        var main = CreateBranchRef("main", true);
        revisionGraph.Add(CreateRevision(tipId, [parentId], main));
        revisionGraph.Add(CreateRevision(parentId, []));

        const int rowCount = 2;
        revisionGraph.CacheTo(rowCount - 1, rowCount - 1, TestContext.Current.CancellationToken);

        var testAccessor = CreateProvider(revisionGraph);
        VisibleRowRange range = new(0, rowCount);
        testAccessor.RenderGraphToCache(range, rowCount - 1, _rowHeight);

        var tipRow = GetRowForId(revisionGraph, rowCount, tipId);

        testAccessor.HoverHighlight.SetHoverHighlight(main, tipRow);
        testAccessor.HoverHighlight.IsDirty.Should().BeTrue();

        testAccessor.RenderGraphToCache(range, rowCount - 1, _rowHeight);
        testAccessor.HoverHighlight.IsDirty.Should().BeFalse();

        Assert.Throws<OperationCanceledException>(() => testAccessor.HoverHighlight.SetHoverHighlight(main, tipRow));
        testAccessor.HoverHighlight.IsDirty.Should().BeFalse();
    }

    private static int GetRowForId(RevisionGraph revisionGraph, int rowCount, string id)
    {
        var objectId = ObjectId.Parse(id);
        for (var row = 0; row < rowCount; row++)
        {
            if (revisionGraph.GetNodeForRow(row)?.Objectid == objectId)
            {
                return row;
            }
        }

        throw new InvalidOperationException($"No row found for revision {id}.");
    }

    private static IGitRef CreateBranchRef(string localName, bool isHead, bool isRemote = false)
    {
        var gitRef = Substitute.For<IGitRef>();
        gitRef.IsHead.Returns(isHead);
        gitRef.IsRemote.Returns(isRemote);
        gitRef.LocalName.Returns(localName);
        return gitRef;
    }

    private static RevisionGraphColumnProvider.TestAccessor CreateProvider(RevisionGraph revisionGraph)
    {
        var gitRevisionSummaryBuilder = Substitute.For<IGitRevisionSummaryBuilder>();
        RevisionGraphColumnProvider revisionGraphColumnProvider = new(revisionGraph, gitRevisionSummaryBuilder);
        return revisionGraphColumnProvider.GetTestAccessor();
    }

    private static GitRevision CreateRevision(string id, IReadOnlyList<string> parentIds, params IGitRef[] refs)
    {
        List<ObjectId> parsedParentIds = [];
        foreach (var parentId in parentIds)
        {
            parsedParentIds.Add(ObjectId.Parse(parentId));
        }

        return new GitRevision(ObjectId.Parse(id)) { ParentIds = parsedParentIds, Refs = refs };
    }
}
