using System.Text;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtensions.Plugins.GitStatistics.PieChart;
using GitExtUtils.GitUI;
using GitExtUtils.GitUI.Theming;
using ResourceManager;

namespace GitExtensions.Plugins.GitStatistics;

public partial class FormGitStatistics : GitExtensionsFormBase
{
    private readonly TranslationString _blankLinesP = new("{0:N0} Blank lines ({1:P1})");

    private readonly string _codeFilePattern;
    private readonly TranslationString _commentLinesP = new("{0:N0} Comment lines ({1:P1})");
    private readonly TranslationString _commits = new("{0:N0} Commits");
    private readonly TranslationString _commitsBy = new("{0:N0} Commits by {1}");
    private readonly bool _countSubmodules;
    private readonly IGitExecutorProvider _executorProvider;
    private readonly TranslationString _linesOfCode = new("{0:N0} Lines of code");
    private readonly TranslationString _linesOfCodeInFiles = new("{0:N0} Lines of code in {1} files ({2:P1})");
    private readonly TranslationString _linesOfCodeP = new("{0:N0} Lines of code ({1:P1})");
    private readonly TranslationString _linesOfDesignerFilesP = new("{0:N0} Lines in designer files ({1:P1})");
    private readonly TranslationString _linesOfProductionCodeP = new("{0:N0} Lines of production code ({1:P1})");
    private readonly TranslationString _linesOfTestCode = new("{0:N0} Lines of test code");
    private readonly TranslationString _linesOfTestCodeP = new("{0:N0} Lines of test code ({1:P1})");
    private readonly IGitModule _module;

    private LineCounter? _lineCounter;

    public FormGitStatistics(IGitExecutorProvider executorProvider, IGitModule module, string codeFilePattern,
        bool countSubmodules)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        _executorProvider = executorProvider;
        _module = module;
        _codeFilePattern = codeFilePattern;
        _countSubmodules = countSubmodules;
        InitializeComponent();

        splitContainer1.SplitterDistance = DpiUtil.Scale(splitContainer1.SplitterDistance);
        splitContainer2.SplitterDistance = DpiUtil.Scale(splitContainer2.SplitterDistance);
        splitContainer3.SplitterDistance = DpiUtil.Scale(splitContainer3.SplitterDistance);
        splitContainer4.SplitterDistance = DpiUtil.Scale(splitContainer4.SplitterDistance);
        splitContainer5.SplitterDistance = DpiUtil.Scale(splitContainer5.SplitterDistance);
        splitContainer6.SplitterDistance = DpiUtil.Scale(splitContainer6.SplitterDistance);
        splitContainer7.SplitterDistance = DpiUtil.Scale(splitContainer7.SplitterDistance);
        splitContainer8.SplitterDistance = DpiUtil.Scale(splitContainer8.SplitterDistance);

        TotalLinesOfCode.Font = new Font(TotalLinesOfCode.Font, FontStyle.Bold);
        TotalLinesOfCode2.Font = TotalLinesOfCode.Font;
        TotalLinesOfTestCode.Font = TotalLinesOfCode.Font;
        TotalCommits.Font = TotalLinesOfCode.Font;
        LoadingLabel.Font = TotalLinesOfCode.Font;

        InitializeComplete();
    }

    protected Color[] DecentColors { get; } =
    [
        Color.Red,
        Color.Yellow,
        Color.DodgerBlue,
        Color.LightGreen,
        Color.Coral,
        Color.Goldenrod,
        Color.YellowGreen,
        Color.MediumPurple,
        Color.LightGray,
        Color.Brown,
        Color.Pink,
        Color.DarkBlue,
        Color.Purple
    ];

    public string DirectoriesToIgnore { get; set; } = "";

    private void FormGitStatisticsSizeChanged(object? sender, EventArgs e)
    {
        SetPieStyle(CommitCountPie);
        SetPieStyle(LinesOfCodeExtensionPie);
        SetPieStyle(LinesOfCodePie);
        SetPieStyle(TestCodePie);
    }

    public void Initialize()
    {
        InitializeCommitCount();
        InitializeLinesOfCode();
    }

    private void InitializeCommitCount()
    {
        ThreadHelper.FileAndForget(async () =>
        {
            var (totalCommits, commitsPerUser) = _module.GetCommitsByContributor();

            await this.SwitchToMainThreadAsync();

            TotalCommits.Text = string.Format(_commits.Text, totalCommits);

            StringBuilder builder = new();

            var commitCountValues = new decimal[commitsPerUser.Count];
            var commitCountLabels = new string[commitsPerUser.Count];
            var n = 0;
            foreach ((var user, var commits) in commitsPerUser)
            {
                builder.AppendLine($"{commits:N0} {user}");

                commitCountValues[n] = commits;
                commitCountLabels[n] = string.Format(_commitsBy.Text, commits, user);
                n++;
            }

            CommitCountPie.SetValues(commitCountValues);
            CommitCountPie.ToolTips = commitCountLabels;

            CommitStatistics.Text = builder.ToString();
        });
    }

    private void SetPieStyle(PieChartControl pie)
    {
        pie.SetLeftMargin(DpiUtil.Scale(10));
        pie.SetRightMargin(DpiUtil.Scale(10));
        pie.SetTopMargin(DpiUtil.Scale(10));
        pie.SetBottomMargin(DpiUtil.Scale(10));
        pie.SetFitChart(false);
        pie.SetEdgeColorType(EdgeColorType.DarkerThanSurface);
        pie.SetSliceRelativeHeight(0.20f);
        pie.SetColors([.. DecentColors.Select(c => c.AdaptBackColor())]);
        pie.SetShadowStyle(ShadowStyle.GradualShadow);

        if (pie.Parent!.Width > pie.Parent.Height)
        {
            pie.Height = pie.Parent.Height;
            pie.Width = pie.Parent.Height;
        }
        else
        {
            pie.Height = pie.Parent.Width;
            pie.Width = pie.Parent.Width;
        }
    }

    private void InitializeLinesOfCode()
    {
        if (_lineCounter is not null)
        {
            return;
        }

        _lineCounter = new LineCounter();
        _lineCounter.Updated += OnLineCounterUpdated;

        Task.Run(() => LoadLinesOfCode());

        void LoadLinesOfCode()
        {
            LoadLinesOfCodeForModule(_module);

            if (_countSubmodules)
            {
                var submodules = _module.GetSubmodulesInfo()
                    .WhereNotNull()
                    .Select(submodule =>
                        new GitModule(_executorProvider, Path.Combine(_module.WorkingDir, submodule.LocalPath)));

                foreach (var submodule in submodules)
                {
                    LoadLinesOfCodeForModule(submodule);
                }
            }

            // Send 'changed' event when done
            OnLineCounterUpdated(_lineCounter, EventArgs.Empty);

            return;

            void LoadLinesOfCodeForModule(IGitModule module)
            {
                List<string> filesToCheck =
                [
                    .. module
                        .GetTree(default, true)
                        .Select(file => Path.Combine(module.WorkingDir, file.Name))
                ];

                _lineCounter.FindAndAnalyzeCodeFiles(_codeFilePattern, DirectoriesToIgnore, filesToCheck);
            }
        }
    }

    private void OnLineCounterUpdated(object? sender, EventArgs e)
    {
        Validates.NotNull(_lineCounter);

        // Must do this synchronously because lineCounter.LinesOfCodePerExtension might change while we are iterating over it otherwise.
        var extensionValues = new decimal[_lineCounter.LinesOfCodePerExtension.Count];
        var extensionLabels = new string[_lineCounter.LinesOfCodePerExtension.Count];

        List<KeyValuePair<string, int>> linesOfCodePerExtension = [.. _lineCounter.LinesOfCodePerExtension];
        linesOfCodePerExtension.Sort((first, next) => -first.Value.CompareTo(next.Value));

        var n = 0;
        StringBuilder linesOfCodePerLanguageText = new();
        foreach ((var extension, var loc) in linesOfCodePerExtension)
        {
            var percent = (double)loc / _lineCounter.CodeLineCount;
            var line = string.Format(_linesOfCodeInFiles.Text, loc, extension, percent);
            linesOfCodePerLanguageText.AppendLine(line);
            extensionValues[n] = loc;
            extensionLabels[n] = line;
            n++;
        }

        // Sync rest to UI thread
        this.InvokeAndForget(() =>
            UpdateUi(_lineCounter, linesOfCodePerLanguageText.ToString(), extensionValues, extensionLabels));
    }

    private void UpdateUi(LineCounter lineCounter, string linesOfCodePerLanguageText, decimal[] extensionValues,
        string[] extensionLabels)
    {
        TotalLinesOfTestCode.Text = string.Format(_linesOfTestCode.Text, lineCounter.TestCodeLineCount);

        TestCodePie.SetValues(
        [
            lineCounter.TestCodeLineCount,
            lineCounter.CodeLineCount - lineCounter.TestCodeLineCount
        ]);

        var percentTest = (double)lineCounter.TestCodeLineCount / lineCounter.CodeLineCount;
        var percentProd = (double)(lineCounter.CodeLineCount - lineCounter.TestCodeLineCount) /
                          lineCounter.CodeLineCount;
        TestCodePie.ToolTips =
        [
            string.Format(_linesOfTestCodeP.Text, lineCounter.TestCodeLineCount, percentTest),
            string.Format(_linesOfProductionCodeP.Text, lineCounter.CodeLineCount - lineCounter.TestCodeLineCount,
                percentProd)
        ];

        TestCodeText.Text = string.Format(_linesOfTestCodeP.Text, lineCounter.TestCodeLineCount, percentTest) +
                            Environment.NewLine +
                            string.Format(_linesOfProductionCodeP.Text,
                                lineCounter.CodeLineCount - lineCounter.TestCodeLineCount, percentProd);

        var percentBlank = (double)lineCounter.BlankLineCount / lineCounter.TotalLineCount;
        var percentComments = (double)lineCounter.CommentLineCount / lineCounter.TotalLineCount;
        var percentCode = (double)lineCounter.CodeLineCount / lineCounter.TotalLineCount;
        var percentDesigner = (double)lineCounter.DesignerLineCount / lineCounter.TotalLineCount;
        LinesOfCodePie.SetValues(
        [
            lineCounter.BlankLineCount,
            lineCounter.CommentLineCount,
            lineCounter.CodeLineCount,
            lineCounter.DesignerLineCount
        ]);

        LinesOfCodePie.ToolTips =
        [
            string.Format(_blankLinesP.Text, lineCounter.BlankLineCount, percentBlank),
            string.Format(_commentLinesP.Text, lineCounter.CommentLineCount, percentComments),
            string.Format(_linesOfCodeP.Text, lineCounter.CodeLineCount, percentCode),
            string.Format(_linesOfDesignerFilesP.Text, lineCounter.DesignerLineCount, percentDesigner)
        ];

        LinesOfCodePerTypeText.Text = string.Join(Environment.NewLine, LinesOfCodePie.ToolTips);

        LinesOfCodePerLanguageText.Text = linesOfCodePerLanguageText;

        LinesOfCodeExtensionPie.SetValues(extensionValues);
        LinesOfCodeExtensionPie.ToolTips = extensionLabels;

        TotalLinesOfCode2.Text = TotalLinesOfCode.Text = string.Format(_linesOfCode.Text, lineCounter.CodeLineCount);
    }

    private void FormGitStatisticsShown(object sender, EventArgs e)
    {
        Initialize();

        Tabs.Visible = true;
        LoadingLabel.Visible = false;

        FormGitStatisticsSizeChanged(sender, EventArgs.Empty);
        SizeChanged += FormGitStatisticsSizeChanged;
    }

    private void TabsSelectedIndexChanged(object sender, EventArgs e)
    {
        FormGitStatisticsSizeChanged(sender, EventArgs.Empty);
    }

    private void FormGitStatistics_FormClosing(object sender, FormClosingEventArgs e)
    {
        _lineCounter?.Updated -= OnLineCounterUpdated;
    }
}
