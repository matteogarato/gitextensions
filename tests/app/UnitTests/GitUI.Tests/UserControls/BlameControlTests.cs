using System.Globalization;
using System.Text;
using CommonTestUtils;
using GitExtensions.Extensibility.Git;
using GitUI;
using GitUI.Editor;
using GitUI.UserControls;
using NSubstitute;
using AppSettings = GitCommands.Settings.AppSettings;

namespace GitUITests.UserControls;

[Culture("en-US")]
public class BlameControlTests : IDisposable
{
    private const string _fileName1 = "A.txt";
    private const string _fileName2 = "B.txt";
    private readonly BlameControl _blameControl = null!;
    private readonly string _commit2 = null!;
    private readonly string _commit3 = null!;
    private readonly GitBlameLine _gitBlameLine = null!;
    private readonly ReferenceRepository _referenceRepository = null!;

    public BlameControlTests()
    {
        GitBlameCommit blameCommit1 = new(
            ObjectId.Random(),
            "author1",
            "author1@mail.fake",
            new DateTime(2010, 3, 22, 12, 01, 02),
            "authorTimeZone",
            "committer1",
            "committer1@authormail.com",
            new DateTime(2010, 3, 22, 13, 01, 02),
            "committerTimeZone",
            "test summary commit1",
            "fileName.txt");

        GitBlameCommit blameCommit2 = new(
            ObjectId.Random(),
            "author2",
            "author2@mail.fake",
            new DateTime(2011, 3, 22, 12, 01, 02),
            "authorTimeZone",
            "committer2",
            "committer2@authormail.com",
            new DateTime(2011, 3, 22, 13, 01, 02),
            "committerTimeZone",
            "test summary commit2",
            "fileName.txt");

        _gitBlameLine = new GitBlameLine(blameCommit1, 1, 1, "line1");

        _blameControl = new BlameControl();

        var blameControlTestAccessor = _blameControl.GetTestAccessor();
        blameControlTestAccessor.Blame = new GitBlame(new[]
        {
            _gitBlameLine, new(blameCommit1, 2, 2, "line2"), new(blameCommit2, 3, 3, "line3"),
            new(blameCommit2, 4, 4, "line4")
        });

        _referenceRepository = new ReferenceRepository();

        // Creates/updates a file with name in DefaultRepoFileName
        _referenceRepository.CreateCommit("1",
            "1\n2\n3\n4\n5\n6\n7\n8\n", _fileName1,
            "1\n2\n3\n4\n5\n6\n7\n8\n", _fileName2);
        _commit2 = _referenceRepository.CreateCommit("2",
            "1\nb\n3\nd\n5\n6\n7\n8\n", _fileName1,
            "1\nb\n3\nd\n5\n6\n7\n8\n", _fileName2);
        _commit3 = _referenceRepository.CreateCommit("3",
            "1\nb\nc\nd\ne\n6\n7\n8\n", _fileName1,
            "1\nb\nc\nd\ne\n6\n7\n8\n", _fileName2);

        var uiCommandsSource = Substitute.For<IGitUiCommandsSource>();
        GitUICommands uiCommands = new(GitUICommands.EmptyServiceProvider, _referenceRepository.Module);
        uiCommandsSource.UiCommands.Returns(x => uiCommands);
        _blameControl.UiCommandsSource = uiCommandsSource;
    }

    void IDisposable.Dispose()
    {
        _blameControl.Dispose();
        _referenceRepository.Dispose();
    }

    [StaTheory]
    [InlineData(true, true, true, true, "author1 - 3/22/2010 - fileName.txt")]
    [InlineData(true, true, true, false, "3/22/2010 - author1 - fileName.txt")]
    [InlineData(false, true, true, false, "author1 - fileName.txt")]
    [InlineData(true, false, true, false, "3/22/2010 - fileName.txt")]
    [InlineData(true, true, false, false, "3/22/2010 - author1")]
    public void BuildAuthorLine_When_FilePath_IsDifferent(bool showAuthorDate, bool showAuthor, bool showFilePath,
        bool displayAuthorFirst, string expectedResult)
    {
        StringBuilder line = new();

        _blameControl.GetTestAccessor().BuildAuthorLine(_gitBlameLine, line, expectedResult.Length + 10,
            CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern,
            "fileName_different.txt", showAuthor, showAuthorDate, showFilePath, displayAuthorFirst);

        line.ToString().Should().StartWith(expectedResult);
    }

    [StaFact]
    public void BuildAuthorLine_When_FilePath_Is_Identic()
    {
        StringBuilder line = new();

        _blameControl.GetTestAccessor().BuildAuthorLine(_gitBlameLine, line, 50,
            CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern,
            "fileName.txt", true, true, true, false);

        line.ToString().Should().StartWith("3/22/2010 - author1");
    }

    [StaFact]
    public void BuildAuthorLine_DoNotPadIfNotNeeded()
    {
        StringBuilder line = new();

        _blameControl.GetTestAccessor().BuildAuthorLine(_gitBlameLine, line, 5,
            CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern,
            "fileName.txt", true, false, false, false);

        line.ToString().Should().StartWith("author1");
    }

    [StaFact]
    public void BuildBlameContents_WithDateAndTime()
    {
        var originalValue = AppSettings.BlameShowAuthorTime;

        try
        {
            AppSettings.BlameShowAuthorTime = true;

            (var gutter, var content, _) = _blameControl.GetTestAccessor().BuildBlameContents("fileName.txt");

            content.Should()
                .Be(
                    $"line1{Environment.NewLine}line2{Environment.NewLine}line3{Environment.NewLine}line4{Environment.NewLine}");
            var gutterLines = gutter.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);
            gutterLines[0].TrimEnd().Should().Be("3/22/2010 12:01 PM - author1");
            gutterLines[1].Should().NotBeNullOrEmpty().And.BeNullOrWhiteSpace();
            gutterLines[2].TrimEnd().Should().Be("3/22/2011 12:01 PM - author2");
            gutterLines[3].Should().NotBeNullOrEmpty().And.BeNullOrWhiteSpace();

            // All the lines have the same length
            gutterLines.Select(l => l.Length).Should().AllBeEquivalentTo(gutterLines[0].Length);
        }
        finally
        {
            AppSettings.BlameShowAuthorTime = originalValue;
        }
    }

    [StaFact]
    public void BuildBlameContents_WithDateButNotTime()
    {
        var originalValue = AppSettings.BlameShowAuthorTime;

        try
        {
            // Given
            AppSettings.BlameShowAuthorTime = false;

            // When
            (var gutter, var content, _) = _blameControl.GetTestAccessor().BuildBlameContents("fileName.txt");

            // Then
            content.Should()
                .Be(
                    $"line1{Environment.NewLine}line2{Environment.NewLine}line3{Environment.NewLine}line4{Environment.NewLine}");
            var gutterLines = gutter.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);
            gutterLines[0].TrimEnd().Should().Be("3/22/2010 - author1");
            gutterLines[1].Should().NotBeNullOrEmpty().And.BeNullOrWhiteSpace();
            gutterLines[2].TrimEnd().Should().Be("3/22/2011 - author2");
            gutterLines[3].Should().NotBeNullOrEmpty().And.BeNullOrWhiteSpace();

            // All the lines have the same length
            gutterLines.Select(l => l.Length).Should().AllBeEquivalentTo(gutterLines[0].Length);
        }
        finally
        {
            AppSettings.BlameShowAuthorTime = originalValue;
        }
    }

    private static IEnumerable<GitBlameLine> CreateBlameLine(params DateTime[] lineDates)
    {
        for (var index = 0; index < lineDates.Length; index++)
        {
            var lineDate = lineDates[index];
            yield return new GitBlameLine(
                new GitBlameCommit(ObjectId.Random(), "Author1", "@Author1", lineDate, string.Empty,
                    "Commiter", "@Committer", lineDate, string.Empty, "Summary1", "file"),
                index + 1, index + 1, "text");
        }
    }

    [StaFact]
    public void
        CalculateBlameGutterData_When_date_is_older_than_artificial_old_boundary_Then_it_defines_first_age_bucket_and_so_falls_into_it()
    {
        // Given
        var sut = _blameControl.GetTestAccessor();
        IReadOnlyList<GitBlameLine> blameLines = CreateBlameLine(sut.ArtificialOldBoundary.AddDays(-1)).ToList();

        // When
        var blameEntries = sut.CalculateBlameGutterData(blameLines);

        // Then
        blameEntries.Should().ContainSingle();
        blameEntries[0].AgeBucketIndex.Should().Be(0);
    }

    [StaFact]
    public void
        CalculateBlameGutterData_When_date_is_newer_than_artificial_old_boundary_Then_it_falls_in_a_later_age_bucket()
    {
        // Given
        IReadOnlyList<GitBlameLine> blameLines = CreateBlameLine(DateTime.Now.AddMonths(-18)).ToList();

        // When
        var blameEntries = _blameControl.GetTestAccessor().CalculateBlameGutterData(blameLines);

        // Then
        blameEntries.Should().ContainSingle();
        blameEntries[0].AgeBucketIndex.Should().Be(3);
    }

    [StaFact]
    public void CalculateBlameGutterData_When_date_is_DateMin_Then_it_falls_in_a_first_age_bucket()
    {
        // Given
        IReadOnlyList<GitBlameLine> blameLines =
            CreateBlameLine(DateTime.Now.AddMonths(-18), DateTime.MinValue).ToList();

        // When
        var blameEntries = _blameControl.GetTestAccessor().CalculateBlameGutterData(blameLines);

        // Then
        blameEntries.Should().HaveCount(2);
        blameEntries[1].AgeBucketIndex.Should().Be(0);
    }

    [StaFact]
    public void CalculateBlameGutterData_When_all_dates_are_DateMin_Then_they_falls_in_a_first_age_bucket()
    {
        // Given
        IReadOnlyList<GitBlameLine> blameLines = CreateBlameLine(DateTime.MinValue, DateTime.MinValue).ToList();

        // When
        var blameEntries = _blameControl.GetTestAccessor().CalculateBlameGutterData(blameLines);

        // Then
        blameEntries.Should().HaveCount(2);
        blameEntries[0].AgeBucketIndex.Should().Be(0);
        blameEntries[1].AgeBucketIndex.Should().Be(0);
    }

    [StaFact]
    public void CalculateBlameGutterData_When_dates_just_after_age_bucket_limit_Then_One_date_in_each_age_bucket()
    {
        // Given
        var now = DateTime.Now;
        var marginError = TimeSpan.FromMinutes(10); // Due to the DateTime.Now value which is slightly different
        IReadOnlyList<GitBlameLine> blameLines = CreateBlameLine(
            now.AddDays(-7 * 365), // Because there are 7 age buckets (corresponding to the different colors)
            now.AddDays(-6 * 365).Add(marginError),
            now.AddDays(-5 * 365).Add(marginError),
            now.AddDays(-4 * 365).Add(marginError),
            now.AddDays(-3 * 365).Add(marginError),
            now.AddDays(-2 * 365).Add(marginError),
            now.AddDays(-1 * 365).Add(marginError),
            now.Add(-marginError)).ToList();

        // When
        var blameEntries = _blameControl.GetTestAccessor().CalculateBlameGutterData(blameLines);

        // Then
        blameEntries.Should().HaveCount(8);
        blameEntries[0].AgeBucketIndex.Should().Be(0);
        blameEntries[1].AgeBucketIndex.Should().Be(1);
        blameEntries[2].AgeBucketIndex.Should().Be(2);
        blameEntries[3].AgeBucketIndex.Should().Be(3);
        blameEntries[4].AgeBucketIndex.Should().Be(4);
        blameEntries[5].AgeBucketIndex.Should().Be(5);
        blameEntries[6].AgeBucketIndex.Should().Be(6);
        blameEntries[7].AgeBucketIndex.Should().Be(6);
    }

    [StaFact]
    public async Task BlameControlShouldStayOnLineIfInputOtherThanFileIsChanged()
    {
        GitRevision rev3 = new(ObjectId.Parse(_commit3));
        GitRevision rev2 = new(ObjectId.Parse(_commit2));

        // Avoid InvalidOperationException "The UI Command Source is not available for this control. Are you calling methods before adding it to the parent control?"
        _blameControl.HideCommitInfo();

        await _blameControl.LoadBlameAsync(rev3, null, _fileName1, null, null, null, null!);
        _blameControl.GetTestAccessor().BlameFile.CurrentFileLine.Should().Be(1);

        _blameControl.GetTestAccessor().BlameFile.GoToLine(3);
        _blameControl.GetTestAccessor().BlameFile.CurrentFileLine.Should().Be(3);
        await _blameControl.LoadBlameAsync(rev3, null, _fileName1, null, null, null, null!);
        _blameControl.GetTestAccessor().BlameFile.CurrentFileLine.Should().Be(3);

        _blameControl.GetTestAccessor().BlameFile.GoToLine(2);
        _blameControl.GetTestAccessor().BlameFile.CurrentFileLine.Should().Be(2);
        await _blameControl.LoadBlameAsync(rev3, null, _fileName1, null, null, null, Encoding.UTF8);
        _blameControl.GetTestAccessor().BlameFile.CurrentFileLine.Should().Be(2);

        _blameControl.GetTestAccessor().BlameFile.GoToLine(4);
        _blameControl.GetTestAccessor().BlameFile.CurrentFileLine.Should().Be(4);
        await _blameControl.LoadBlameAsync(rev3, null, _fileName1, null, null, null, null!);
        _blameControl.GetTestAccessor().BlameFile.CurrentFileLine.Should().Be(4);

        _blameControl.GetTestAccessor().BlameFile.GoToLine(3);
        _blameControl.GetTestAccessor().BlameFile.CurrentFileLine.Should().Be(3);
        await _blameControl.LoadBlameAsync(rev2, null, _fileName1, null, null, null, null!);
        _blameControl.GetTestAccessor().BlameFile.CurrentFileLine.Should().Be(3);
    }

    [StaFact]
    public async Task BlameControlShouldGoToFirstLineIfFileNameIsChanged()
    {
        GitRevision rev3 = new(ObjectId.Parse(_commit3));
        GitRevision rev2 = new(ObjectId.Parse(_commit2));

        // Avoid InvalidOperationException "The UI Command Source is not available for this control. Are you calling methods before adding it to the parent control?"
        _blameControl.HideCommitInfo();

        await _blameControl.LoadBlameAsync(rev3, null, _fileName1, null, null, null, null!);
        _blameControl.GetTestAccessor().BlameFile.CurrentFileLine.Should().Be(1);

        _blameControl.GetTestAccessor().BlameFile.GoToLine(3);
        _blameControl.GetTestAccessor().BlameFile.CurrentFileLine.Should().Be(3);
        await _blameControl.LoadBlameAsync(rev3, null, _fileName2, null, null, null, null!);
        _blameControl.GetTestAccessor().BlameFile.CurrentFileLine.Should().Be(1);

        _blameControl.GetTestAccessor().BlameFile.GoToLine(2);
        _blameControl.GetTestAccessor().BlameFile.CurrentFileLine.Should().Be(2);
        await _blameControl.LoadBlameAsync(rev3, null, _fileName2, null, null, null, Encoding.UTF8);
        _blameControl.GetTestAccessor().BlameFile.CurrentFileLine.Should().Be(2);

        _blameControl.GetTestAccessor().BlameFile.GoToLine(4);
        _blameControl.GetTestAccessor().BlameFile.CurrentFileLine.Should().Be(4);
        await _blameControl.LoadBlameAsync(rev3, null, _fileName1, null, null, null, null!);
        _blameControl.GetTestAccessor().BlameFile.CurrentFileLine.Should().Be(1);

        _blameControl.GetTestAccessor().BlameFile.GoToLine(3);
        _blameControl.GetTestAccessor().BlameFile.CurrentFileLine.Should().Be(3);
        await _blameControl.LoadBlameAsync(rev2, null, _fileName1, null, null, null, null!);
        _blameControl.GetTestAccessor().BlameFile.CurrentFileLine.Should().Be(3);
    }

    [StaFact]
    public async Task BlameControlShouldStayOnLineIfNullLineInput()
    {
        GitRevision rev1 = new(ObjectId.Parse(_referenceRepository.CommitHash!));

        // Avoid InvalidOperationException "The UI Command Source is not available for this control. Are you calling methods before adding it to the parent control?"
        _blameControl.HideCommitInfo();

        await _blameControl.LoadBlameAsync(rev1, null, _fileName1, null, null, null, null!);
        _blameControl.GetTestAccessor().BlameFile.CurrentFileLine.Should().Be(1);

        _blameControl.GetTestAccessor().BlameFile.GoToLine(3);
        _blameControl.GetTestAccessor().BlameFile.CurrentFileLine.Should().Be(3);
        await _blameControl.LoadBlameAsync(rev1, null, _fileName1, null, null, null, null!);
        _blameControl.GetTestAccessor().BlameFile.CurrentFileLine.Should().Be(3);
    }

    [StaFact]
    public async Task BlameControlShouldGotoRequestedLineAtStartAndIfReloaded()
    {
        GitRevision rev1 = new(ObjectId.Parse(_referenceRepository.CommitHash!));

        // Avoid InvalidOperationException "The UI Command Source is not available for this control. Are you calling methods before adding it to the parent control?"
        _blameControl.HideCommitInfo();

        await _blameControl.LoadBlameAsync(rev1, null, _fileName1, null, null, null, null!, 4);
        _blameControl.GetTestAccessor().BlameFile.CurrentFileLine.Should().Be(4);

        await _blameControl.LoadBlameAsync(rev1, null, _fileName1, null, null, null, null!, 7);
        _blameControl.GetTestAccessor().BlameFile.CurrentFileLine.Should().Be(7);

        await _blameControl.LoadBlameAsync(rev1, null, _fileName2, null, null, null, null!, 5);
        _blameControl.GetTestAccessor().BlameFile.CurrentFileLine.Should().Be(5);
    }
}
