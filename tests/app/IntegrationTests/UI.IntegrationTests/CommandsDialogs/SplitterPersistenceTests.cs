using AwesomeAssertions.Execution;
using CommonTestUtils;
using GitCommands.Settings;
using GitExtensions.Extensibility;
using GitUI;
using GitUI.CommandsDialogs;
using GitUI.UserControls.RevisionGrid;
using NSubstitute;
using AppSettings = GitCommands.Settings.AppSettings;

namespace UITests.CommandsDialogs;

public class SplitterPersistenceTests : IDisposable
{
    // Created once for each test
    private readonly GitUICommands _commands = null!;

    // Created once for the fixture
    private readonly ReferenceRepository _referenceRepository = null!;
    private readonly MemorySettings _settings = null!;
    private readonly IWindowPositionManager _windowPositionManager = null!;

    public SplitterPersistenceTests()
    {
        _settings = new MemorySettings();

        // Stop loading custom diff tools
        AppSettings.ShowAvailableDiffTools = false;

        // We don't want avatars during tests, otherwise we will be attempting to download them from gravatar....
        AppSettings.ShowAuthorAvatarColumn = false;

        _windowPositionManager = Substitute.For<IWindowPositionManager>();

        _referenceRepository = new ReferenceRepository();

        _commands = new GitUICommands(GlobalServiceContainer.CreateDefaultMockServiceContainer(),
            _referenceRepository.Module);
    }

    void IDisposable.Dispose()
    {
        DisposeTest();
    }

    private void DisposeTest()
    {
        // Since we set AppSettings, we need to delete the backing file so that any subsequent tests won't load
        // outdated settings.
        File.Delete(AppSettings.SettingsContainer.SettingsCache.SettingsFilePath);

        _referenceRepository.Dispose();
    }

    [WinFormsFact]
    public async Task SplitterPositionsShouldBeDefault()
    {
        AppSettings.CommitInfoPosition.Should().Be(CommitInfoPosition.BelowList);

        await RunFormTestAsync(async form =>
        {
            var ta = form.GetTestAccessor();

            await WaitForRevisionsToBeLoadedAsync(ta.RevisionGrid);
        });
    }

    [WinFormsTheory]
    [InlineData(CommitInfoPosition.BelowList)]
    [InlineData(CommitInfoPosition.LeftwardFromList)]
    [InlineData(CommitInfoPosition.RightwardFromList)]
    public async Task SplitterPositionsShouldBeSaved(CommitInfoPosition commitInfoPosition)
    {
        AppSettings.CommitInfoPosition = commitInfoPosition;

        var leftPanelWidth = 300;
        var revisionGridHeight = 125;
        var commitInfoWidth = 124;
        var fileTreeWidth = 127;
        var diffListWidth = 126;
        int expectedCommitInfoWidth;

        switch (commitInfoPosition)
        {
            case CommitInfoPosition.BelowList:
                // Since the Commit Info is on the bottom, the size is largely immaterial
                // because RevisionsSplitContainer_Panel2Collapsed=true
                expectedCommitInfoWidth = commitInfoWidth + /* splitter width */6;
                break;

            case CommitInfoPosition.LeftwardFromList:
                commitInfoWidth = 300;

                // Since the Commit Info is on the left, the expected width is the same
                expectedCommitInfoWidth = commitInfoWidth;
                break;

            case CommitInfoPosition.RightwardFromList:
                commitInfoWidth = 350;

                // Since the Commit Info is on the right, the expected width is the same
                expectedCommitInfoWidth = commitInfoWidth - /* some magic number... no idea */38;
                break;

            default:
                throw new NotSupportedException();
        }

        SplitterInfo splitterInfoMainSplitContainer = new(_settings, "MainSplitContainer")
        {
            Size = 911, Distance = leftPanelWidth
        };

        SplitterInfo splitterInfoRightSplitContainer = new(_settings, "RightSplitContainer")
        {
            Size = 507, Distance = revisionGridHeight
        };

        SplitterInfo splitterInfoRevisionsSplitContainer = new(_settings, "RevisionsSplitContainer")
        {
            Size = 645, Distance = commitInfoWidth
        };

        SplitterInfo splitterInfoFileTreeSplitContainer = new(_settings, "FileTreeSplitContainer")
        {
            Size = 643, Distance = fileTreeWidth
        };

        SplitterInfo splitterInfoDiffSplitContainer = new(_settings, "DiffSplitContainer")
        {
            Size = 643, Distance = diffListWidth
        };

        await RunFormTestAsync(async form =>
        {
            var ta = form.GetTestAccessor();

            await WaitForRevisionsToBeLoadedAsync(ta.RevisionGrid);
        });

        using (new AssertionScope())
        {
            splitterInfoMainSplitContainer.Distance.Should().Be(leftPanelWidth);
            splitterInfoRightSplitContainer.Distance.Should().Be(revisionGridHeight);
            splitterInfoRevisionsSplitContainer.Distance.Should().Be(expectedCommitInfoWidth);
            splitterInfoFileTreeSplitContainer.Distance.Should().Be(fileTreeWidth);
            splitterInfoDiffSplitContainer.Distance.Should().Be(diffListWidth);
        }
    }

    private async Task RunFormTestAsync(Func<FormBrowse, Task> testDriverAsync)
    {
        Dictionary<string, string> splitters = [];

        UiTest.RunForm(
            () =>
            {
#pragma warning disable CS0618 // Type or member is obsolete
                FormBrowse form = new(_commands, new BrowseArguments(), _settings);
#pragma warning restore CS0618 // Type or member is obsolete

                form.Load += (_, _) =>
                {
                    // Collect the persisted splitter settings in a dictionary so that we can perform snapshot validation
                    // instead of manually validating each individual setting.
                    foreach (var splitterData in form.GetTestAccessor().SplitterManager
                                 .GetTestAccessor().Splitters)
                    {
                        splitters[splitterData.SizeSettingsKey] = null!;
                        splitters[splitterData.DpiSettingsKey] = null!;
                        splitters[splitterData.DistanceSettingsKey] = null!;
                        splitters[splitterData.FontSizeSettingsKey] = null!;
                        splitters[splitterData.Panel1CollapsedSettingsKey] = null!;
                        splitters[splitterData.Panel2CollapsedSettingsKey] = null!;
                    }
                };

                var test = form.GetGitExtensionsFormTestAccessor();
                test.WindowPositionManager = _windowPositionManager;

                if (Application.MessageLoop)
                {
                    form.Show(null);
                }
                else
                {
                    Application.Run(form);
                }
            },
            testDriverAsync);

        // Snapshot validation
        foreach (var key in splitters.Keys)
        {
            if (key.EndsWith("Collapsed"))
            {
                splitters[key] = _settings.GetBool(key).ToString()!;
            }
            else
            {
                splitters[key] = _settings.GetInt(key, -1).ToString();
            }
        }

        await Verify(splitters);
    }

    private static async Task WaitForRevisionsToBeLoadedAsync(RevisionGridControl revisionGridControl)
    {
        UiTest.ProcessUntil("Loading Revisions", () => revisionGridControl.GetTestAccessor().IsDataLoadComplete);
        try
        {
            await AsyncTestHelper.JoinPendingOperationsAsync(AsyncTestHelper.UnexpectedTimeout);
        }
        catch
        {
            // ignore the timeout and continue
        }
    }

    private record SplitterInfo
    {
        private readonly string _settingName;
        private readonly MemorySettings _settings;

        public SplitterInfo(MemorySettings settings, string settingName)
        {
            _settings = settings;
            _settingName = settingName;
        }

        // These are a copy from SplitterManager.SplitterData

        public string SizeSettingsKey
        {
            get => _settingName + "_Size";
        }

        public string DpiSettingsKey
        {
            get => _settingName + "_Dpi";
        }

        public string DistanceSettingsKey
        {
            get => _settingName + "_Distance";
        }

        public string FontSizeSettingsKey
        {
            get => _settingName + "_FontSize";
        }

        public string Panel1CollapsedSettingsKey
        {
            get => _settingName + "_Panel1Collapsed";
        }

        public string Panel2CollapsedSettingsKey
        {
            get => _settingName + "_Panel2Collapsed";
        }

        public int Distance
        {
            get => _settings.GetInt(DistanceSettingsKey) ?? throw new InvalidOperationException();
            set => _settings.SetInt(DistanceSettingsKey, value);
        }

        public int Size
        {
            get => _settings.GetInt(SizeSettingsKey) ?? throw new InvalidOperationException();
            set => _settings.SetInt(SizeSettingsKey, value);
        }
    }
}
