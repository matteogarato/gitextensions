using System.Text.Json.Nodes;
using GitExtensions.Extensibility.Git;
using GitUI.UserControls;
using FileStatusList = GitUI.UserControls.FileStatusList;

namespace GitUITests.UserControls;

public class FileStatusListSorterTests : IDisposable
{
    void IDisposable.Dispose()
    {
    }

    [Fact]
    public void Compare_should_not_compare_path_separator()
    {
        FileStatusList.StatusSorter.TestAccessor.Compare("dir/sub/file", "dir.ext/file").Should().Be(-1);
        FileStatusList.StatusSorter.TestAccessor.Compare("dir.ext/file", "dir/sub/file").Should().Be(+1);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Sort_should_first_add_folders_then_files(bool flat, bool mergeSingleItemsWithFolder)
    {
        const string oldFolder = "oldfolder/of/renamed/file/";
        GitItemStatus[] statuses =
        [
            new("root_file"),
            new(".hidden_root_file"),
            new("ext/submodule/"),
            new("ext/subfolder/filees"),
            new("ext/s/"),
            new("ext/file"),
            new("ext2/file"),
            new("1/file1"),
            new("1/submodule_sorted_as_file/"),
            new("1/subfolder/file1s"),
            new("1/2/file12"),
            new("1/3/file13"),
            new("1/3/4/file134"),
            new("5/file1"),
            new("5/6/file56b"),
            new("5/6/file56a"),
            new("5/7/file57b"),
            new("5/7/file57a"),
            new($"{oldFolder}5/7/8/file578")
        ];
        var renamedFile = statuses[^1];
        renamedFile.OldName = renamedFile.Name;
        renamedFile.Name = renamedFile.Name[oldFolder.Length..];

        FileStatusList.StatusSorter statusSorter = new();
        var rootNode = statusSorter.CreateTreeSortedByPath(statuses, flat, mergeSingleItemsWithFolder,
            createNode: status => new TreeNode(status.ToString())
            {
                Tag = new FileStatusItem(firstRev: null, secondRev: new GitRevision(ObjectId.WorkTreeId), status)
            });
        await Verify(Serialize(rootNode).ToString());
    }

    [Fact]
    public async Task Sort_should_not_split_folders()
    {
        GitItemStatus[] statuses =
        [
            new("core/c.1"),
            new("core/c.2"),
            new("core.dot/cd.3"),
            new("core/api/c_a.0")
        ];

        FileStatusList.StatusSorter statusSorter = new();
        var rootNode = statusSorter.CreateTreeSortedByPath(statuses, flat: false,
            mergeSingleItemsWithFolder: false,
            createNode: status => new TreeNode(status.ToString())
            {
                Tag = new FileStatusItem(firstRev: null, secondRev: new GitRevision(ObjectId.WorkTreeId), status)
            });
        await Verify(Serialize(rootNode).ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Sort_should_not_merge_single_file_with_root_node(bool flat)
    {
        GitItemStatus[] statuses =
        [
            new("root_file")
        ];

        FileStatusList.StatusSorter statusSorter = new();
        var rootNode = statusSorter.CreateTreeSortedByPath(statuses, flat, mergeSingleItemsWithFolder: true,
            createNode: status => new TreeNode(status.ToString())
            {
                Tag = new FileStatusItem(firstRev: null, secondRev: new GitRevision(ObjectId.WorkTreeId), status)
            });
        await Verify(Serialize(rootNode).ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Sort_should_optionally_create_subfolder_nodes_for_single_files(bool mergeSingleItemsWithFolder)
    {
        GitItemStatus[] statuses =
        [
            new("1/2/file12"),
            new("1/3/file13"),
            new("1/4/file14")
        ];

        FileStatusList.StatusSorter statusSorter = new();
        var rootNode = statusSorter.CreateTreeSortedByPath(statuses, flat: false, mergeSingleItemsWithFolder,
            createNode: status => new TreeNode(status.ToString())
            {
                Tag = new FileStatusItem(firstRev: null, secondRev: new GitRevision(ObjectId.WorkTreeId), status)
            });
        await Verify(Serialize(rootNode).ToString());
    }

    [Theory]
    [InlineData("", "", "")]
    [InlineData("a", "a", "a")]
    [InlineData("a", "b", "")]
    [InlineData("a", "ab", "")]
    [InlineData("a", "a/b", "a")]
    [InlineData("a", "a/b/c", "a")]
    [InlineData("a/b", "a/bc", "a")]
    [InlineData("a/b", "a/b", "a/b")]
    [InlineData("a/b", "a/b/c", "a/b")]
    [InlineData("a/b/cc", "a/b/ccd", "a/b")]
    [InlineData("a/b/cc", "a/b/cc/de", "a/b/cc")]
    public void GetCommonPath(string a, string b, string expected)
    {
        FileStatusList.StatusSorter.TestAccessor.GetCommonPath(a, b).Should().Be(expected);
        FileStatusList.StatusSorter.TestAccessor.GetCommonPath(b, a).Should().Be(expected);
    }

    private static JsonObject Serialize(TreeNode node)
    {
        JsonObject json = [];

        JsonArray nodes = [];
        foreach (TreeNode subnode in node.Nodes)
        {
            nodes.Add(Serialize(subnode));
        }

        json.Add($"{nameof(node.Text)}", node.Text);
        json.Add($"{nameof(node.Tag)}", $"{node.Tag?.GetType().Name}: {node.Tag}");
        json.Add($"{nameof(node.Nodes)}", nodes);

        return json;
    }
}
