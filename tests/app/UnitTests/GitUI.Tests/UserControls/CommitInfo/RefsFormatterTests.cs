using GitUI.CommitInfo;
using ResourceManager;
using TranslatedStrings = GitUI.TranslatedStrings;

namespace GitUITests.UserControls.CommitInfo;

public class RefsFormatterTests
{
    // Created once for each test
    private readonly ILinkFactory _linkFactory = null!;

    private readonly IReadOnlyList<string> _refs
        = new List<string>
        {
            "r1",
            null!,
            "r3",
            "r4",
            "r5",
            "r6",
            "r7",
            "r8",
            "r9",
            "r10",
            "r11",
            "r12",
            "r13"
        };

    private readonly RefsFormatter _refsFormatter = null!;

    public RefsFormatterTests()
    {
        _linkFactory = new LinkFactory();
        _refsFormatter = new RefsFormatter(_linkFactory);
    }

    [Fact]
    public void LinkFactoryNull()
    {
        ((Action)(() => new RefsFormatter(null!))).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RefsNull()
    {
        _refsFormatter.FormatBranches(null, true, true)
            .Should().Be(string.Empty);

        _refsFormatter.FormatTags(null!, true, true)
            .Should().Be(string.Empty);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void Empty(bool showAsLinks, bool limit)
    {
        IReadOnlyList<string> refs = new List<string>();

        _refsFormatter.FormatBranches(refs, showAsLinks, limit)
            .Should().Be(TranslatedStrings.ContainedInNoBranch);

        _refsFormatter.FormatTags(refs, showAsLinks, limit)
            .Should().Be(TranslatedStrings.ContainedInNoTag);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(3, false)]
    [InlineData(9, true)]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(10, false)]
    [InlineData(11, true)]
    [InlineData(11, false)]
    [InlineData(12, true)]
    [InlineData(12, false)]
    [InlineData(13, true)]
    [InlineData(13, false)]
    public void LimitNotExceededOrNotLimited(int count, bool showAsLinks)
    {
        var limit = count <= 12;
        IReadOnlyList<string> refs = _refs.Take(count).ToList();
        var formattedBranches = refs.Select(r => FormatRef(r, "branch", showAsLinks));
        var formattedTags = refs.Select(r => FormatRef(r, "tag", showAsLinks));

        _refsFormatter.FormatBranches(refs, showAsLinks, limit)
            .Should().Be(TranslatedStrings.ContainedInBranches
                         + Environment.NewLine
                         + formattedBranches.Join(Environment.NewLine));

        _refsFormatter.FormatTags(refs, showAsLinks, limit)
            .Should().Be(TranslatedStrings.ContainedInTags
                         + Environment.NewLine
                         + formattedTags.Join(Environment.NewLine));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LimitExceeded(bool showAsLinks)
    {
        var formattedBranches = _refs.Take(10).Select(r => FormatRef(r, "branch", showAsLinks));
        var formattedTags = _refs.Take(10).Select(r => FormatRef(r, "tag", showAsLinks));

        _refsFormatter.FormatBranches(_refs, showAsLinks, true)
            .Should().Be(TranslatedStrings.ContainedInBranches
                         + Environment.NewLine
                         + formattedBranches.Join(Environment.NewLine)
                         + GetShowAllLink("branches"));

        _refsFormatter.FormatTags(_refs, showAsLinks, true)
            .Should().Be(TranslatedStrings.ContainedInTags
                         + Environment.NewLine
                         + formattedTags.Join(Environment.NewLine)
                         + GetShowAllLink("tags"));
    }

    private static string FormatRef(string r, string type, bool showAsLinks)
    {
        return showAsLinks ? $"<a href='gitext://goto{type}/{r}'>{r}</a>" : r;
    }

    private static string GetShowAllLink(string type)
    {
        return
            $"{Environment.NewLine}<a href='gitext://showall/{type}'>[ {ResourceManager.TranslatedStrings.ShowAll} ]</a>";
    }
}
