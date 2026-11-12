using System.Text.RegularExpressions;
using GitExtensions.Extensibility.Git;
using GitUI;
using GitUI.UserControls.RevisionGrid;
using AppSettings = GitCommands.Settings.AppSettings;

namespace GitUITests.UserControls;

[Culture("en-US")]
[Collection(nameof(FilterInfoTestsCollection))]
public class FilterInfoTests
{
    public FilterInfoTests()
    {
        AppSettings.ShowGitNotes = false;
        AppSettings.HideMergeCommits = false;
        AppSettings.ShowOnlyFirstParent = false;
        AppSettings.ShowSimplifyByDecoration = false;
        AppSettings.SimplifyMergesInFileHistory = false;
        AppSettings.MaxRevisionGraphCommits = 0;
    }

    public static IEnumerable<object?[]> FilterInfoHasFilterTestCases
    {
        get
        {
            foreach (var byDateFrom in new[] { false, true })
            {
                foreach (var byDateTo in new[] { false, true })
                {
                    foreach (var byAuthor in new[] { false, true })
                    {
                        foreach (var byCommitter in new[] { false, true })
                        {
                            foreach (var byMessage in new[] { false, true })
                            {
                                foreach (var byDiffContent in new[] { false, true })
                                {
                                    foreach (var showSimplifyByDecoration in new[] { false, true })
                                    {
                                        foreach (var hideMergeCommits in new[] { false, true })
                                        {
                                            foreach (var pathFilter in new[] { "file1", "", null })
                                            {
                                                foreach (var showCurrentBranchOnly in new[] { false, true })
                                                {
                                                    foreach (var showReflogReferences in new[] { false, true })
                                                    {
                                                        foreach (var branchFilter in new[] { "branch1", "", null })
                                                        {
                                                            yield return new object?[]
                                                            {
                                                                byDateFrom, byDateTo, byAuthor, byCommitter,
                                                                byMessage, byDiffContent, showSimplifyByDecoration,
                                                                hideMergeCommits, pathFilter, showCurrentBranchOnly,
                                                                showReflogReferences, branchFilter
                                                            };
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    public static IEnumerable<object?[]> FilterInfoNotesStash
    {
        get
        {
            foreach (var showNotes in new[] { false, true })
            {
                foreach (var showStash in new[] { false, true })
                {
                    foreach (var showSessionRefs in new[] { false, true })
                    {
                        foreach (var showReflog in new[] { false, true })
                        {
                            foreach (var showCurrentBranchOnly in new[] { false, true })
                            {
                                foreach (var isValidCheckout in new[] { false, true })
                                {
                                    foreach (var showFilteredBranches in new[] { false, true })
                                    {
                                        foreach (var branchFilter in new[] { "branch1", "", null })
                                        {
                                            yield return new object?[]
                                            {
                                                showNotes, showStash, showSessionRefs, showReflog,
                                                showCurrentBranchOnly, isValidCheckout, showFilteredBranches,
                                                branchFilter
                                            };
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public async Task FilterInfo_ctor_expected()
    {
        bool originalBranchFilterEnabled = AppSettings.BranchFilterEnabled;
        bool originalShowCurrentBranchOnly = AppSettings.ShowCurrentBranchOnly;
        bool originalShowReflogReferences = AppSettings.ShowReflogReferences;
        AppSettings.ShowReflogReferences.Value = false;
        AppSettings.ShowCurrentBranchOnly.Value = false;
        AppSettings.BranchFilterEnabled.Value = false;
        AppSettings.MaxRevisionGraphCommits = 1;

        try
        {
            FilterInfo filterInfo = new();
            await Verify(filterInfo);
        }
        finally
        {
            AppSettings.BranchFilterEnabled.Value = originalBranchFilterEnabled;
            AppSettings.ShowCurrentBranchOnly.Value = originalShowCurrentBranchOnly;
            AppSettings.ShowReflogReferences.Value = originalShowReflogReferences;
        }
    }

    [Fact]
    public async Task FilterInfo_ctor_with_Raw_expected()
    {
        bool originalBranchFilterEnabled = AppSettings.BranchFilterEnabled;
        bool originalShowCurrentBranchOnly = AppSettings.ShowCurrentBranchOnly;
        bool originalShowReflogReferences = AppSettings.ShowReflogReferences;
        AppSettings.ShowReflogReferences.Value = false;
        AppSettings.ShowCurrentBranchOnly.Value = false;
        AppSettings.BranchFilterEnabled.Value = false;
        AppSettings.MaxRevisionGraphCommits = 1;

        try
        {
            FilterInfo filterInfo = new() { IsRaw = true };
            await Verify(filterInfo);
        }
        finally
        {
            AppSettings.BranchFilterEnabled.Value = originalBranchFilterEnabled;
            AppSettings.ShowCurrentBranchOnly.Value = originalShowCurrentBranchOnly;
            AppSettings.ShowReflogReferences.Value = originalShowReflogReferences;
        }
    }

    [Fact]
    public void FilterInfo_ByDateFrom_expected()
    {
        FilterInfo filterInfo = new();
        filterInfo.ByDateFrom.Should().BeFalse();
        filterInfo.HasFilter.Should().BeFalse();

        filterInfo.ByDateFrom = true;
        filterInfo.ByDateFrom.Should().BeTrue();
        filterInfo.HasFilter.Should().BeTrue();

        filterInfo.ByDateFrom = false;
        filterInfo.ByDateFrom.Should().BeFalse();
        filterInfo.HasFilter.Should().BeFalse();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void FilterInfo_DateFrom_with_Raw_expected(bool isRaw, bool byDateFrom)
    {
        DateTime date = new(2022, 1, 15);

        FilterInfo filterInfo = new() { IsRaw = isRaw, ByDateFrom = byDateFrom, DateFrom = date };

        if (isRaw || byDateFrom)
        {
            filterInfo.DateFrom.Should().Be(date);
        }
        else
        {
            filterInfo.DateFrom.Should().Be(DateTime.MinValue);
        }
    }

    [Fact]
    public void FilterInfo_ByDateTo_expected()
    {
        FilterInfo filterInfo = new();
        filterInfo.ByDateTo.Should().BeFalse();
        filterInfo.HasFilter.Should().BeFalse();

        filterInfo.ByDateTo = true;
        filterInfo.ByDateTo.Should().BeTrue();
        filterInfo.HasFilter.Should().BeTrue();

        filterInfo.ByDateTo = false;
        filterInfo.ByDateTo.Should().BeFalse();
        filterInfo.HasFilter.Should().BeFalse();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void FilterInfo_DateTo_with_Raw_expected(bool isRaw, bool byDateTo)
    {
        DateTime date = new(2022, 1, 15);

        FilterInfo filterInfo = new() { IsRaw = isRaw, ByDateTo = byDateTo, DateTo = date };

        if (isRaw || byDateTo)
        {
            filterInfo.DateTo.Should().Be(date);
        }
        else
        {
            filterInfo.DateTo.Should().Be(DateTime.MinValue);
        }
    }

    [Fact]
    public void FilterInfo_ByAuthor_expected()
    {
        FilterInfo filterInfo = new();
        filterInfo.ByAuthor.Should().BeFalse();
        filterInfo.HasFilter.Should().BeFalse();

        filterInfo.ByAuthor = true;
        filterInfo.ByAuthor.Should().BeTrue();
        filterInfo.HasFilter.Should().BeTrue();

        filterInfo.ByAuthor = false;
        filterInfo.ByAuthor.Should().BeFalse();
        filterInfo.HasFilter.Should().BeFalse();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void FilterInfo_Author_with_Raw_expected(bool isRaw, bool byAuthor)
    {
        const string value = "The Value";
        FilterInfo filterInfo = new() { IsRaw = isRaw, ByAuthor = byAuthor, Author = value };

        if (isRaw || byAuthor)
        {
            filterInfo.Author.Should().Be(value);
        }
        else
        {
            filterInfo.Author.Should().BeEmpty();
        }
    }

    [Fact]
    public void FilterInfo_ByCommitter_expected()
    {
        FilterInfo filterInfo = new();
        filterInfo.ByCommitter.Should().BeFalse();
        filterInfo.HasFilter.Should().BeFalse();

        filterInfo.ByCommitter = true;
        filterInfo.ByCommitter.Should().BeTrue();
        filterInfo.HasFilter.Should().BeTrue();

        filterInfo.ByCommitter = false;
        filterInfo.ByCommitter.Should().BeFalse();
        filterInfo.HasFilter.Should().BeFalse();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void FilterInfo_Committer_with_Raw_expected(bool isRaw, bool byCommitter)
    {
        const string value = "The Value";
        FilterInfo filterInfo = new() { IsRaw = isRaw, ByCommitter = byCommitter, Committer = value };

        if (isRaw || byCommitter)
        {
            filterInfo.Committer.Should().Be(value);
        }
        else
        {
            filterInfo.Committer.Should().BeEmpty();
        }
    }

    [Fact]
    public void FilterInfo_ByMessage_expected()
    {
        FilterInfo filterInfo = new();
        filterInfo.ByMessage.Should().BeFalse();
        filterInfo.HasFilter.Should().BeFalse();

        filterInfo.ByMessage = true;
        filterInfo.ByMessage.Should().BeTrue();
        filterInfo.HasFilter.Should().BeTrue();

        filterInfo.ByMessage = false;
        filterInfo.ByMessage.Should().BeFalse();
        filterInfo.HasFilter.Should().BeFalse();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void FilterInfo_Message_with_Raw_expected(bool isRaw, bool byMessage)
    {
        const string value = "The Value";
        FilterInfo filterInfo = new() { IsRaw = isRaw, ByMessage = byMessage, Message = value };

        if (isRaw || byMessage)
        {
            filterInfo.Message.Should().Be(value);
        }
        else
        {
            filterInfo.Message.Should().BeEmpty();
        }
    }

    [Fact]
    public void FilterInfo_ByDiffContent_expected()
    {
        FilterInfo filterInfo = new();
        filterInfo.ByDiffContent.Should().BeFalse();
        filterInfo.HasFilter.Should().BeFalse();

        filterInfo.ByDiffContent = true;
        filterInfo.ByDiffContent.Should().BeTrue();
        filterInfo.HasFilter.Should().BeTrue();

        filterInfo.ByDiffContent = false;
        filterInfo.ByDiffContent.Should().BeFalse();
        filterInfo.HasFilter.Should().BeFalse();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void FilterInfo_DiffContent_with_Raw_expected(bool isRaw, bool byDiffContent)
    {
        const string value = "The Value";
        FilterInfo filterInfo = new() { IsRaw = isRaw, ByDiffContent = byDiffContent, DiffContent = value };

        if (isRaw || byDiffContent)
        {
            filterInfo.DiffContent.Should().Be(value);
        }
        else
        {
            filterInfo.DiffContent.Should().BeEmpty();
        }
    }

    [Fact]
    public void FilterInfo_IgnoreCase_expected()
    {
        FilterInfo filterInfo = new();
        filterInfo.IgnoreCase.Should().BeTrue();
        filterInfo.HasFilter.Should().BeFalse();

        filterInfo.IgnoreCase = false;
        filterInfo.IgnoreCase.Should().BeFalse();
        filterInfo.HasFilter.Should().BeFalse();

        filterInfo.IgnoreCase = true;
        filterInfo.IgnoreCase.Should().BeTrue();
        filterInfo.HasFilter.Should().BeFalse();
    }

    [Fact]
    public void FilterInfo_HasCommitsLimit_expected()
    {
        AppSettings.MaxRevisionGraphCommits = 1;
        FilterInfo filterInfo = new() { IsRaw = true };
        filterInfo.HasCommitsLimit.Should().BeTrue();
        filterInfo.HasFilter.Should().BeFalse();

        AppSettings.MaxRevisionGraphCommits = 0;
        filterInfo.ByCommitsLimit = true;
        filterInfo.CommitsLimit = 0;
        filterInfo.HasCommitsLimit.Should().BeFalse();
        filterInfo.HasFilter.Should().BeFalse();

        filterInfo.CommitsLimit = 1;
        filterInfo.HasCommitsLimit.Should().BeTrue();
        filterInfo.HasFilter.Should().BeFalse();
    }

    [Fact]
    public void FilterInfo_ByCommitsLimit_expected()
    {
        FilterInfo filterInfo = new();
        filterInfo.ByCommitsLimit.Should().BeFalse();
        filterInfo.HasFilter.Should().BeFalse();

        filterInfo.ByCommitsLimit = true;
        filterInfo.ByCommitsLimit.Should().BeTrue();
        filterInfo.HasFilter.Should().BeFalse();

        filterInfo.ByCommitsLimit = false;
        filterInfo.ByCommitsLimit.Should().BeFalse();
        filterInfo.HasFilter.Should().BeFalse();
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(100, false)]
    [InlineData(-100, true)]
    [InlineData(0, true)]
    [InlineData(100, true)]
    public void FilterInfo_CommitsLimit_with_Raw_expected(int value, bool byCommitsLimit)
    {
        FilterInfo filterInfo = new() { ByCommitsLimit = byCommitsLimit, CommitsLimit = value };

        if (byCommitsLimit && value >= 0)
        {
            filterInfo.CommitsLimit.Should().Be(value);
        }
        else
        {
            filterInfo.CommitsLimit.Should().Be(AppSettings.MaxRevisionGraphCommits);
        }
    }

    [Fact]
    public void FilterInfo_ByPathFilter_expected()
    {
        FilterInfo filterInfo = new();
        filterInfo.ByPathFilter.Should().BeFalse();
        filterInfo.HasFilter.Should().BeFalse();

        filterInfo.ByPathFilter = true;
        filterInfo.ByPathFilter.Should().BeTrue();
        filterInfo.HasFilter.Should().BeFalse();

        filterInfo.ByPathFilter = false;
        filterInfo.ByPathFilter.Should().BeFalse();
        filterInfo.HasFilter.Should().BeFalse();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void FilterInfo_PathFilter_with_Raw_expected(bool isRaw, bool byPathFilter)
    {
        const string value = "The Value";
        FilterInfo filterInfo = new() { IsRaw = isRaw, ByPathFilter = byPathFilter, PathFilter = value };

        if (isRaw || byPathFilter)
        {
            filterInfo.PathFilter.Should().Be(value);
        }
        else
        {
            filterInfo.PathFilter.Should().BeEmpty();
        }
    }

    [Fact]
    public void FilterInfo_ByBranchFilter_expected()
    {
        bool originalBranchFilterEnabled = AppSettings.BranchFilterEnabled;
        bool originalShowCurrentBranchOnly = AppSettings.ShowCurrentBranchOnly;
        bool originalShowReflogReferences = AppSettings.ShowReflogReferences;
        AppSettings.ShowReflogReferences.Value = false;
        AppSettings.ShowCurrentBranchOnly.Value = false;
        AppSettings.BranchFilterEnabled.Value = false;

        try
        {
            FilterInfo filterInfo = new();
            filterInfo.ByBranchFilter.Should().BeFalse();
            AppSettings.BranchFilterEnabled.Value.Should().BeFalse();
            filterInfo.HasFilter.Should().BeFalse();

            filterInfo.ByBranchFilter = true;
            filterInfo.ByBranchFilter.Should().BeTrue();
            AppSettings.BranchFilterEnabled.Value.Should().BeTrue();
            filterInfo.HasFilter.Should().BeFalse();

            filterInfo.ByBranchFilter = false;
            filterInfo.ByBranchFilter.Should().BeFalse();
            AppSettings.BranchFilterEnabled.Value.Should().BeFalse();
            filterInfo.HasFilter.Should().BeFalse();

            AppSettings.BranchFilterEnabled.Value = true;
            filterInfo.ByBranchFilter.Should().BeTrue();
            filterInfo.HasFilter.Should().BeFalse();
        }
        finally
        {
            AppSettings.BranchFilterEnabled.Value = originalBranchFilterEnabled;
            AppSettings.ShowCurrentBranchOnly.Value = originalShowCurrentBranchOnly;
            AppSettings.ShowReflogReferences.Value = originalShowReflogReferences;
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void FilterInfo_BranchFilter_with_Raw_expected(bool isRaw, bool byBranchFilter)
    {
        bool originalBranchFilterEnabled = AppSettings.BranchFilterEnabled;
        bool originalShowCurrentBranchOnly = AppSettings.ShowCurrentBranchOnly;
        bool originalShowReflogReferences = AppSettings.ShowReflogReferences;
        AppSettings.ShowReflogReferences.Value = false;
        AppSettings.ShowCurrentBranchOnly.Value = false;
        AppSettings.BranchFilterEnabled.Value = false;

        try
        {
            const string value = "The Value";
            FilterInfo filterInfo = new() { IsRaw = isRaw, ByBranchFilter = byBranchFilter, BranchFilter = value };

            if (isRaw || byBranchFilter)
            {
                filterInfo.BranchFilter.Should().Be(value);
            }
            else
            {
                filterInfo.BranchFilter.Should().BeEmpty();
            }
        }
        finally
        {
            AppSettings.BranchFilterEnabled.Value = originalBranchFilterEnabled;
            AppSettings.ShowCurrentBranchOnly.Value = originalShowCurrentBranchOnly;
            AppSettings.ShowReflogReferences.Value = originalShowReflogReferences;
        }
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    public void FilterInfo_IsShowAllBranchesChecked_expected(bool byBranchFilter, bool showCurrentBranchOnly,
        bool expected)
    {
        bool originalBranchFilterEnabled = AppSettings.BranchFilterEnabled;
        bool originalShowCurrentBranchOnly = AppSettings.ShowCurrentBranchOnly;
        bool originalShowReflogReferences = AppSettings.ShowReflogReferences;
        AppSettings.ShowReflogReferences.Value = false;
        AppSettings.ShowCurrentBranchOnly.Value = false;
        AppSettings.BranchFilterEnabled.Value = false;

        try
        {
            FilterInfo filterInfo = new()
            {
                ShowReflogReferences = false,
                ByBranchFilter = byBranchFilter,
                ShowCurrentBranchOnly = showCurrentBranchOnly
            };

            filterInfo.IsShowAllBranchesChecked.Should().Be(expected);
            filterInfo.HasFilter.Should().BeFalse();
        }
        finally
        {
            AppSettings.BranchFilterEnabled.Value = originalBranchFilterEnabled;
            AppSettings.ShowCurrentBranchOnly.Value = originalShowCurrentBranchOnly;
            AppSettings.ShowReflogReferences.Value = originalShowReflogReferences;
        }
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void FilterInfo_IsShowCurrentBranchOnlyChecked_expected(bool byBranchFilter, bool showCurrentBranchOnly,
        bool expected)
    {
        bool originalBranchFilterEnabled = AppSettings.BranchFilterEnabled;
        bool originalShowCurrentBranchOnly = AppSettings.ShowCurrentBranchOnly;
        bool originalShowReflogReferences = AppSettings.ShowReflogReferences;
        AppSettings.ShowReflogReferences.Value = false;
        AppSettings.ShowCurrentBranchOnly.Value = false;
        AppSettings.BranchFilterEnabled.Value = false;

        try
        {
            FilterInfo filterInfo = new()
            {
                ShowReflogReferences = false,
                ByBranchFilter = byBranchFilter,
                ShowCurrentBranchOnly = showCurrentBranchOnly
            };

            filterInfo.IsShowCurrentBranchOnlyChecked.Should().Be(expected);
            filterInfo.HasFilter.Should().BeFalse();
        }
        finally
        {
            AppSettings.BranchFilterEnabled.Value = originalBranchFilterEnabled;
            AppSettings.ShowCurrentBranchOnly.Value = originalShowCurrentBranchOnly;
            AppSettings.ShowReflogReferences.Value = originalShowReflogReferences;
        }
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    public void FilterInfo_IsShowFilteredBranchesChecked_expected(bool byBranchFilter, bool showCurrentBranchOnly,
        bool expected)
    {
        bool originalBranchFilterEnabled = AppSettings.BranchFilterEnabled;
        bool originalShowCurrentBranchOnly = AppSettings.ShowCurrentBranchOnly;
        bool originalShowReflogReferences = AppSettings.ShowReflogReferences;
        AppSettings.ShowReflogReferences.Value = false;
        AppSettings.ShowCurrentBranchOnly.Value = false;
        AppSettings.BranchFilterEnabled.Value = false;

        try
        {
            FilterInfo filterInfo = new()
            {
                ShowReflogReferences = false,
                ByBranchFilter = byBranchFilter,
                ShowCurrentBranchOnly = showCurrentBranchOnly
            };

            filterInfo.IsShowFilteredBranchesChecked.Should().Be(expected);
            filterInfo.HasFilter.Should().BeFalse();
        }
        finally
        {
            AppSettings.BranchFilterEnabled.Value = originalBranchFilterEnabled;
            AppSettings.ShowCurrentBranchOnly.Value = originalShowCurrentBranchOnly;
            AppSettings.ShowReflogReferences.Value = originalShowReflogReferences;
        }
    }

    [Fact]
    public void FilterInfo_ShowCurrentBranchOnly_expected()
    {
        bool originalBranchFilterEnabled = AppSettings.BranchFilterEnabled;
        bool originalShowCurrentBranchOnly = AppSettings.ShowCurrentBranchOnly;
        bool originalShowReflogReferences = AppSettings.ShowReflogReferences;
        AppSettings.ShowReflogReferences.Value = false;
        AppSettings.ShowCurrentBranchOnly.Value = false;
        AppSettings.BranchFilterEnabled.Value = false;

        try
        {
            FilterInfo filterInfo = new();
            filterInfo.ShowCurrentBranchOnly.Should().BeFalse();
            AppSettings.ShowCurrentBranchOnly.Value.Should().BeFalse();
            filterInfo.HasFilter.Should().BeFalse();

            filterInfo.ShowCurrentBranchOnly = true;
            filterInfo.ShowCurrentBranchOnly.Should().BeTrue();
            AppSettings.ShowCurrentBranchOnly.Value.Should().BeTrue();
            filterInfo.HasFilter.Should().BeFalse();

            filterInfo.ShowCurrentBranchOnly = false;
            filterInfo.ShowCurrentBranchOnly.Should().BeFalse();
            AppSettings.ShowCurrentBranchOnly.Value.Should().BeFalse();
            filterInfo.HasFilter.Should().BeFalse();

            AppSettings.ShowCurrentBranchOnly.Value = true;
            filterInfo.ShowCurrentBranchOnly.Should().BeTrue();
            filterInfo.HasFilter.Should().BeFalse();
        }
        finally
        {
            AppSettings.BranchFilterEnabled.Value = originalBranchFilterEnabled;
            AppSettings.ShowCurrentBranchOnly.Value = originalShowCurrentBranchOnly;
            AppSettings.ShowReflogReferences.Value = originalShowReflogReferences;
        }
    }

    [Fact]
    public void FilterInfo_ShowOnlyFirstParent_expected()
    {
        var originalShowOnlyFirstParent = AppSettings.ShowOnlyFirstParent;
        AppSettings.ShowOnlyFirstParent = false;

        try
        {
            FilterInfo filterInfo = new();
            filterInfo.ShowOnlyFirstParent.Should().BeFalse();
            AppSettings.ShowOnlyFirstParent.Should().BeFalse();
            filterInfo.HasFilter.Should().BeFalse();

            filterInfo.ShowOnlyFirstParent = true;
            filterInfo.ShowOnlyFirstParent.Should().BeTrue();
            AppSettings.ShowOnlyFirstParent.Should().BeTrue();
            filterInfo.HasFilter.Should().BeTrue();

            filterInfo.ShowOnlyFirstParent = false;
            filterInfo.ShowOnlyFirstParent.Should().BeFalse();
            AppSettings.ShowOnlyFirstParent.Should().BeFalse();
            filterInfo.HasFilter.Should().BeFalse();

            AppSettings.ShowOnlyFirstParent = true;
            filterInfo.ShowOnlyFirstParent.Should().BeTrue();
            filterInfo.HasFilter.Should().BeTrue();
        }
        finally
        {
            AppSettings.ShowOnlyFirstParent = originalShowOnlyFirstParent;
        }
    }

    [Fact]
    public void FilterInfo_ShowReflogReferences_expected()
    {
        bool originalBranchFilterEnabled = AppSettings.BranchFilterEnabled;
        bool originalShowCurrentBranchOnly = AppSettings.ShowCurrentBranchOnly;
        bool originalShowReflogReferences = AppSettings.ShowReflogReferences;
        AppSettings.ShowReflogReferences.Value = false;
        AppSettings.ShowCurrentBranchOnly.Value = false;
        AppSettings.BranchFilterEnabled.Value = false;

        try
        {
            FilterInfo filterInfo = new();
            filterInfo.ShowReflogReferences.Should().BeFalse();
            AppSettings.ShowReflogReferences.Value.Should().BeFalse();
            filterInfo.HasFilter.Should().BeFalse();

            filterInfo.ShowReflogReferences = true;
            filterInfo.ShowReflogReferences.Should().BeTrue();
            AppSettings.ShowReflogReferences.Value.Should().BeTrue();
            filterInfo.HasFilter.Should().BeFalse();

            filterInfo.ShowReflogReferences = false;
            filterInfo.ShowReflogReferences.Should().BeFalse();
            AppSettings.ShowReflogReferences.Value.Should().BeFalse();
            filterInfo.HasFilter.Should().BeFalse();

            AppSettings.ShowReflogReferences.Value = true;
            filterInfo.ShowReflogReferences.Should().BeTrue();
            filterInfo.HasFilter.Should().BeFalse();
        }
        finally
        {
            AppSettings.BranchFilterEnabled.Value = originalBranchFilterEnabled;
            AppSettings.ShowCurrentBranchOnly.Value = originalShowCurrentBranchOnly;
            AppSettings.ShowReflogReferences.Value = originalShowReflogReferences;
        }
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void FilterInfo_ShowReflogReferences_dominates_other_filters(bool byBranchFilter, bool showCurrentBranchOnly,
        bool showReflog)
    {
        bool originalBranchFilterEnabled = AppSettings.BranchFilterEnabled;
        bool originalShowCurrentBranchOnly = AppSettings.ShowCurrentBranchOnly;
        bool originalShowReflogReferences = AppSettings.ShowReflogReferences;
        AppSettings.ShowReflogReferences.Value = false;
        AppSettings.ShowCurrentBranchOnly.Value = false;
        AppSettings.BranchFilterEnabled.Value = false;

        try
        {
            FilterInfo filterInfo = new()
            {
                ByBranchFilter = byBranchFilter,
                ShowCurrentBranchOnly = showCurrentBranchOnly,
                ShowReflogReferences = showReflog
            };

            filterInfo.ShowReflogReferences.Should().Be(showReflog);
            filterInfo.HasFilter.Should().BeFalse();

            // showCurrentBranchOnly dominates byBranchFilter
            filterInfo.IsShowAllBranchesChecked.Should().Be(!byBranchFilter && !showCurrentBranchOnly);
            filterInfo.IsShowCurrentBranchOnlyChecked.Should().Be(showCurrentBranchOnly);
            filterInfo.IsShowFilteredBranchesChecked.Should().Be(byBranchFilter && !showCurrentBranchOnly);

            filterInfo.ByBranchFilter.Should().Be(byBranchFilter);
            AppSettings.BranchFilterEnabled.Value.Should().Be(byBranchFilter);
            filterInfo.ShowCurrentBranchOnly.Should().Be(showCurrentBranchOnly);
            AppSettings.ShowCurrentBranchOnly.Value.Should().Be(showCurrentBranchOnly);

            filterInfo.HasFilter.Should().BeFalse();
        }
        finally
        {
            AppSettings.ShowReflogReferences.Value = false;
            AppSettings.BranchFilterEnabled.Value = originalBranchFilterEnabled;
            AppSettings.ShowCurrentBranchOnly.Value = originalShowCurrentBranchOnly;
            AppSettings.ShowReflogReferences.Value = originalShowReflogReferences;
        }
    }

    [Fact]
    public void FilterInfo_ShowSimplifyByDecoration_expected()
    {
        var originalShowSimplifyByDecoration = AppSettings.ShowSimplifyByDecoration;
        AppSettings.ShowSimplifyByDecoration = false;

        try
        {
            FilterInfo filterInfo = new();
            filterInfo.ShowSimplifyByDecoration.Should().BeFalse();
            AppSettings.ShowSimplifyByDecoration.Should().BeFalse();
            filterInfo.HasFilter.Should().BeFalse();

            filterInfo.ShowSimplifyByDecoration = true;
            filterInfo.ShowSimplifyByDecoration.Should().BeTrue();
            AppSettings.ShowSimplifyByDecoration.Should().BeTrue();
            filterInfo.HasFilter.Should().BeTrue();

            filterInfo.ShowSimplifyByDecoration = false;
            filterInfo.ShowSimplifyByDecoration.Should().BeFalse();
            AppSettings.ShowSimplifyByDecoration.Should().BeFalse();
            filterInfo.HasFilter.Should().BeFalse();

            AppSettings.ShowSimplifyByDecoration = true;
            filterInfo.ShowSimplifyByDecoration.Should().BeTrue();
            filterInfo.HasFilter.Should().BeTrue();
        }
        finally
        {
            AppSettings.ShowSimplifyByDecoration = originalShowSimplifyByDecoration;
        }
    }

    [Theory]
    [MemberData(nameof(FilterInfoHasFilterTestCases))]
    public void FilterInfo_HasFilter_expected(bool byDateFrom, bool byDateTo, bool byAuthor, bool byCommitter,
        bool byMessage, bool byDiffContent, bool showSimplifyByDecoration, bool hideMergeCommits, string pathFilter,
        bool showCurrentBranchOnly, bool showReflogReferences, string branchFilter)
    {
        FilterInfo filterInfo = new()
        {
            ByDateFrom = byDateFrom,
            ByDateTo = byDateTo,
            ByAuthor = byAuthor,
            ByCommitter = byCommitter,
            ByMessage = byMessage,
            ByDiffContent = byDiffContent,
            ShowSimplifyByDecoration = showSimplifyByDecoration,
            HideMergeCommits = hideMergeCommits,
            ByPathFilter = true,
            PathFilter = pathFilter,
            ShowCurrentBranchOnly = showCurrentBranchOnly,
            ShowReflogReferences = showReflogReferences,
            ByBranchFilter = true,
            BranchFilter = branchFilter
        };

        filterInfo.HasFilter.Should().Be(byDateFrom || byDateTo || byAuthor || byCommitter || byMessage ||
                                         byDiffContent || showSimplifyByDecoration || hideMergeCommits ||
                                         !string.IsNullOrWhiteSpace(pathFilter) ||
                                         !string.IsNullOrWhiteSpace(branchFilter));
    }

    [Theory]
    [MemberData(nameof(FilterInfoHasFilterTestCases))]
    public void FilterInfo_ResetAllFilters_expected(bool byDateFrom, bool byDateTo, bool byAuthor, bool byCommitter,
        bool byMessage, bool byDiffContent, bool showSimplifyByDecoration, bool hideMergeCommits, string pathFilter,
        bool showCurrentBranchOnly, bool showReflogReferences, string branchFilter)
    {
        FilterInfo filterInfo = new()
        {
            ByDateFrom = byDateFrom,
            ByDateTo = byDateTo,
            ByAuthor = byAuthor,
            ByCommitter = byCommitter,
            ByMessage = byMessage,
            ByDiffContent = byDiffContent,
            ShowSimplifyByDecoration = showSimplifyByDecoration,
            HideMergeCommits = hideMergeCommits,
            ByPathFilter = true,
            PathFilter = pathFilter,
            ShowCurrentBranchOnly = showCurrentBranchOnly,
            ShowReflogReferences = showReflogReferences,
            ByBranchFilter = true,
            BranchFilter = branchFilter
        };

        filterInfo.ResetAllFilters();

        filterInfo.ByDateFrom.Should().BeFalse();
        filterInfo.ByDateTo.Should().BeFalse();
        filterInfo.ByAuthor.Should().BeFalse();
        filterInfo.ByCommitter.Should().BeFalse();
        filterInfo.ByMessage.Should().BeFalse();
        filterInfo.ByDiffContent.Should().BeFalse();
        filterInfo.ShowSimplifyByDecoration.Should().BeFalse();
        filterInfo.HideMergeCommits.Should().BeFalse();
        filterInfo.ByPathFilter.Should().BeFalse();
        filterInfo.ByBranchFilter.Should().BeFalse();

        filterInfo.IsShowCurrentBranchOnlyChecked.Should().Be(showCurrentBranchOnly);
        filterInfo.ShowReflogReferences.Should().Be(showReflogReferences);
    }

    [Theory]
    [InlineData("author1", "committer2", "message3", "diffContent4", true, true, "pathFilter7", false, false,
        "branchFilter8",
        "Since: 10/1/2021 1:30:34 AM\r\nUntil: 11/1/2021 1:30:34 AM\r\nPath filter: pathFilter7\r\nAuthor: author1\r\nCommitter: committer2\r\nSimplify by decoration\r\nMessage: message3\r\nDiff contains: diffContent4\r\nBranches: branchFilter8\r\n",
        @"--max-count=100000 --since=""2021-10-01 01:30:34"" --until=""2021-11-01 01:30:34"" --no-merges --simplify-by-decoration --author=""author1"" --committer=""committer2"" --regexp-ignore-case -G""diffContent4"" --grep=""message3"" --parents --glob=refs/stas[h] branchFilter8")]
    public void FilterInfo_GetRevisionFilter(string author, string committer, string message, string diffContent,
        bool showSimplifyByDecoration, bool hideMergeCommits, string pathFilter, bool showReflog,
        bool showCurrentBranchOnly, string branchFilter, string expectedSummary, string expectedArgs)
    {
        AppSettings.MaxRevisionGraphCommits = 100000;
        DateTime dateFrom = new(2021, 10, 1, 1, 30, 34, DateTimeKind.Local);
        DateTime dateTo = new(2021, 11, 1, 1, 30, 34, DateTimeKind.Local);
        FilterInfo filterInfo = new()
        {
            DateFrom = dateFrom,
            ByDateFrom = dateFrom != DateTime.MinValue,
            DateTo = dateTo,
            ByDateTo = dateTo != DateTime.MinValue,
            Author = author,
            ByAuthor = !string.IsNullOrEmpty(author),
            Committer = committer,
            ByCommitter = !string.IsNullOrEmpty(committer),
            Message = message,
            ByMessage = !string.IsNullOrEmpty(message),
            DiffContent = diffContent,
            ByDiffContent = !string.IsNullOrEmpty(message),
            ShowSimplifyByDecoration = showSimplifyByDecoration,
            HideMergeCommits = hideMergeCommits,
            PathFilter = pathFilter,
            ByPathFilter = !string.IsNullOrEmpty(pathFilter),
            ShowReflogReferences = showReflog,
            ShowCurrentBranchOnly = showCurrentBranchOnly,
            ByBranchFilter = !string.IsNullOrEmpty(branchFilter),
            BranchFilter = branchFilter
        };

        filterInfo.GetSummary().Should().Be(expectedSummary);
        filterInfo.GetRevisionFilter(new Lazy<ObjectId>(() => ObjectId.Random())).ToString().Should().Be(expectedArgs);
    }

    [Theory]
    [InlineData(false, false, "branchFilter")]
    [InlineData(false, false, "")]
    [InlineData(false, true, "branchFilter")]
    [InlineData(false, true, "")]
    [InlineData(true, false, "branchFilter")]
    [InlineData(true, false, "")]
    [InlineData(true, true, "branchFilter")]
    [InlineData(true, true, "")]
    public void FilterInfo_filter_should_add_reflog_if_requested(bool showRefLog, bool showCurrentBranchOnly,
        string branchFilter)
    {
        FilterInfo filterInfo = new()
        {
            ShowReflogReferences = showRefLog,
            ShowCurrentBranchOnly = showCurrentBranchOnly,
            ByBranchFilter = !string.IsNullOrEmpty(branchFilter),
            BranchFilter = branchFilter
        };

        string args = filterInfo.GetRevisionFilter(new Lazy<ObjectId>(() => ObjectId.Random()));

        if (showRefLog)
        {
            args.Should().MatchRegex(@"(^|\s)--reflog($|\s)");
        }
        else
        {
            args.Should().NotMatchRegex(@"(^|\s)--reflog($|\s)");
        }

        if (!showRefLog && showCurrentBranchOnly)
        {
            args.Should().NotMatchRegex(@"(^|\s)--all($|\s)");
            args.Should().NotMatchRegex(@"(^|\s)branchFilter($|\s)");
        }

        if (!showRefLog && !showCurrentBranchOnly && !string.IsNullOrWhiteSpace(branchFilter))
        {
            args.Should().MatchRegex(@"(^|\s)branchFilter($|\s)");
        }
    }

    [Theory]
    [InlineData("branchFilter", false)]
    [InlineData(@"branchFilter*", true)]
    [InlineData(@"branchFilter?", true)]
    [InlineData(@"branchFilter[", true)]
    public void FilterInfo_simple_branchfilter(string branchFilter, bool expectBranches)
    {
        FilterInfo filterInfo = new()
        {
            ShowReflogReferences = false,
            ShowCurrentBranchOnly = false,
            ByBranchFilter = !string.IsNullOrEmpty(branchFilter),
            BranchFilter = branchFilter
        };

        string args = filterInfo.GetRevisionFilter(new Lazy<ObjectId>(() => ObjectId.Random()));

        if (expectBranches)
        {
            args.Should().MatchRegex(@$"(^|\s)--branches={Regex.Escape(branchFilter)}($|\s)");
        }
        else
        {
            args.Should().MatchRegex(@$"(^|\s){Regex.Escape(branchFilter)}($|\s)");
            args.Should().NotMatchRegex(@$"(^|\s)--branches={Regex.Escape(branchFilter)}($|\s)");
        }
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void FilterInfo_filter_should_add_maxcount_if_requested(int maxCount, bool expected)
    {
        FilterInfo filterInfo = new() { CommitsLimit = maxCount, ByCommitsLimit = true };
        string args = filterInfo.GetRevisionFilter(new Lazy<ObjectId>(() => ObjectId.Random()));

        if (expected)
        {
            args.Should().MatchRegex(@$"(^|\s)--max-count={maxCount}($|\s)");
        }
        else
        {
            args.Should().NotMatchRegex(@"(^|\s)--max-count=\d+($|\s)");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FilterInfo_OnlyFirstParent(bool expected)
    {
        FilterInfo filterInfo = new() { ShowOnlyFirstParent = expected };

        string args = filterInfo.GetRevisionFilter(new Lazy<ObjectId>(() => ObjectId.Random()));

        if (expected)
        {
            args.Should().MatchRegex(@"(^|\s)--first-parent($|\s)");
        }
        else
        {
            args.Should().NotMatchRegex(@"(^|\s)--first-parent($|\s)");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FilterInfo_HideMerges(bool expected)
    {
        FilterInfo filterInfo = new() { HideMergeCommits = expected };

        string args = filterInfo.GetRevisionFilter(new Lazy<ObjectId>(() => ObjectId.Random()));

        if (expected)
        {
            args.Should().MatchRegex(@"(^|\s)--no-merges($|\s)");
        }
        else
        {
            args.Should().NotMatchRegex(@"(^|\s)--no-merges($|\s)");
        }
    }

    [Theory]
    [MemberData(nameof(FilterInfoNotesStash))]
    public void FilterInfo_GitNotes_Stashes(bool showGitNotes, bool showStash, bool showSessionRefs, bool showReflog,
        bool showCurrentBranchOnly, bool isValidCheckout, bool showFilteredBranches, string branchFilter)
    {
        var originalShowGitNotes = AppSettings.ShowGitNotes;
        AppSettings.ShowGitNotes = showGitNotes;
        var originalShowStash = AppSettings.ShowStashes;
        AppSettings.ShowStashes = showStash;
        var originalShowSessionRefs = AppSettings.ShowSessionRefs;
        AppSettings.ShowSessionRefs = showSessionRefs;
        FilterInfo filterInfo = new()
        {
            ShowReflogReferences = showReflog,
            ShowCurrentBranchOnly = showCurrentBranchOnly,
            ByBranchFilter = showFilteredBranches,
            BranchFilter = branchFilter
        };
        var objectId = isValidCheckout ? ObjectId.Random() : default;
        string args = filterInfo.GetRevisionFilter(new Lazy<ObjectId>(() => objectId));
        var showAll = !(showCurrentBranchOnly && !objectId.IsZero)
                      && !(!showCurrentBranchOnly && showFilteredBranches && !string.IsNullOrWhiteSpace(branchFilter));

        try
        {
            if (showAll && !showGitNotes)
            {
                args.Should().MatchRegex(@"(^|\s)--exclude=refs/notes/commits($|\s)");
            }
            else
            {
                args.Should().NotMatchRegex(@"(^|\s)--exclude=refs/notes/commits($|\s)");
            }

            if (showAll && !showStash)
            {
                args.Should().MatchRegex(@"(^|\s)--exclude=refs/stash($|\s)");
            }
            else
            {
                args.Should().NotMatchRegex(@"(^|\s)--exclude=refs/stash($|\s)");
            }

            string[] sessionRefExclusions =
            {
                @"(^|\s)--exclude=refs/agents/\*\*($|\s)", @"(^|\s)--exclude=refs/sessions/\*\*($|\s)",
                @"(^|\s)--exclude=refs/copilot/checkpoints/\*\*($|\s)"
            };

            foreach (var sessionRefExclusion in sessionRefExclusions)
            {
                if (showAll && !showSessionRefs)
                {
                    args.Should().MatchRegex(sessionRefExclusion);
                }
                else
                {
                    args.Should().NotMatchRegex(sessionRefExclusion);
                }
            }

            if (showCurrentBranchOnly && !objectId.IsZero)
            {
                var head = Regex.Escape(objectId.ToString());
                args.Should().MatchRegex(@$"(^|\s){head}($|\s)");
            }

            var stash = Regex.Escape($"--glob={"refs/stas[h]"}");
            if (showStash && ((showCurrentBranchOnly && !objectId.IsZero)
                              || (!showCurrentBranchOnly && showFilteredBranches &&
                                  !string.IsNullOrWhiteSpace(branchFilter))))
            {
                args.Should().MatchRegex(@$"(^|\s){stash}($|\s)");
            }
            else
            {
                args.Should().NotMatchRegex(@$"(^|\s){stash}($|\s)");
            }
        }
        finally
        {
            AppSettings.ShowGitNotes = originalShowGitNotes;
            AppSettings.ShowStashes = originalShowStash;
            AppSettings.ShowSessionRefs = originalShowSessionRefs;
        }
    }

    [Theory]
    [InlineData("message1", true)]
    [InlineData("message1 --not", true)]
    [InlineData("--not=message1", false)]
    [InlineData("--not message1", false)]
    [InlineData(" --not message1", true)]
    [InlineData("--exclude message1", false)]
    [InlineData("--exclude= message1", false)]
    [InlineData("--exclude= message1 ", false)]
    [InlineData("\t--exclude= message1", true)]
    public void FilterInfo_Message(string message, bool expectGrep)
    {
        FilterInfo filterInfo = new() { Message = message, ByMessage = true };
        string args = filterInfo.GetRevisionFilter(new Lazy<ObjectId>(() => ObjectId.Random()));
        var summary = filterInfo.GetSummary();

        if (expectGrep)
        {
            args.Should().MatchRegex(@$"[^\s]?--grep=""{message}""");
            summary.Should().MatchRegex(@$"[^\s]?{TranslatedStrings.Message}: {message}");
        }
        else
        {
            args.Should().NotMatchRegex(@"[^\s]?--grep");
            args.Should().MatchRegex(@$"[^\s]?{message}");
            summary.Should().NotMatchRegex(@$"[^\s]?{TranslatedStrings.Message}: {message}");
            summary.Should().MatchRegex(@$"[^\s]?{message}");
        }
    }

    [Fact]
    public void FilterInfo_Apply_ByCommitMessage()
    {
        FilterInfo filterInfo = new();
        var filterLaunched = filterInfo.Apply(new RevisionFilter("message", true, false, false, false));

        filterLaunched.Should().BeTrue();
        filterInfo.ByMessage.Should().BeTrue();
        filterInfo.Message.Should().Be("message");

        filterInfo.ByAuthor.Should().BeFalse();
        filterInfo.ByCommitter.Should().BeFalse();
        filterInfo.ByDiffContent.Should().BeFalse();

        filterInfo.HasFilter.Should().BeTrue();
    }

    [Fact]
    public void FilterInfo_Apply_ByCommitter()
    {
        FilterInfo filterInfo = new();
        var filterLaunched = filterInfo.Apply(new RevisionFilter("committer", false, true, false, false));

        filterLaunched.Should().BeTrue();
        filterInfo.ByCommitter.Should().BeTrue();
        filterInfo.Committer.Should().Be("committer");

        filterInfo.ByMessage.Should().BeFalse();
        filterInfo.ByAuthor.Should().BeFalse();
        filterInfo.ByDiffContent.Should().BeFalse();

        filterInfo.HasFilter.Should().BeTrue();
    }

    [Fact]
    public void FilterInfo_Apply_ByAuthor()
    {
        FilterInfo filterInfo = new();
        var filterLaunched = filterInfo.Apply(new RevisionFilter("author", false, false, true, false));

        filterLaunched.Should().BeTrue();
        filterInfo.ByAuthor.Should().BeTrue();
        filterInfo.Author.Should().Be("author");

        filterInfo.ByMessage.Should().BeFalse();
        filterInfo.ByCommitter.Should().BeFalse();
        filterInfo.ByDiffContent.Should().BeFalse();

        filterInfo.HasFilter.Should().BeTrue();
    }

    [Fact]
    public void FilterInfo_Apply_ByDiffContent()
    {
        FilterInfo filterInfo = new();
        var filterLaunched = filterInfo.Apply(new RevisionFilter("diff", false, false, false, true));

        filterLaunched.Should().BeTrue();
        filterInfo.ByDiffContent.Should().BeTrue();
        filterInfo.DiffContent.Should().Be("diff");

        filterInfo.ByMessage.Should().BeFalse();
        filterInfo.ByAuthor.Should().BeFalse();
        filterInfo.ByCommitter.Should().BeFalse();

        filterInfo.HasFilter.Should().BeTrue();
    }

    [Fact]
    public void FilterInfo_Apply_ByMessage_Then_By_DiffContent()
    {
        // FilterInfo keep a state, so simulating actions of user where filter is changed 2 times
        // to ensure that the filtering will be triggered i.e. returned value is always `true`

        FilterInfo filterInfo = new();
        var filterLaunched = filterInfo.Apply(new RevisionFilter("a_content", true, false, false, false));

        filterLaunched.Should().BeTrue();
        filterInfo.ByMessage.Should().BeTrue();
        filterInfo.Message.Should().Be("a_content");

        filterInfo.ByAuthor.Should().BeFalse();
        filterInfo.ByCommitter.Should().BeFalse();
        filterInfo.ByDiffContent.Should().BeFalse();

        filterInfo.HasFilter.Should().BeTrue();

        filterLaunched = filterInfo.Apply(new RevisionFilter("a_content", true, false, false, true));

        filterLaunched.Should().BeTrue();
        filterInfo.ByMessage.Should().BeTrue();
        filterInfo.Message.Should().Be("a_content");
        filterInfo.ByDiffContent.Should().BeTrue();
        filterInfo.DiffContent.Should().Be("a_content");

        filterInfo.ByAuthor.Should().BeFalse();
        filterInfo.ByCommitter.Should().BeFalse();

        filterInfo.HasFilter.Should().BeTrue();

        filterLaunched = filterInfo.Apply(new RevisionFilter("a_content", false, false, false, true));

        filterLaunched.Should().BeTrue();
        filterInfo.ByDiffContent.Should().BeTrue();
        filterInfo.DiffContent.Should().Be("a_content");

        filterInfo.ByMessage.Should().BeFalse();
        filterInfo.ByAuthor.Should().BeFalse();
        filterInfo.ByCommitter.Should().BeFalse();

        filterInfo.HasFilter.Should().BeTrue();
    }
}
