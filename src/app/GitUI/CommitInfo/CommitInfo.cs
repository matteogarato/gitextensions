using System.ComponentModel;
using System.Net;
using System.Reactive.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GitCommands;
using GitCommands.ExternalLinks;
using GitCommands.Git;
using GitCommands.Remotes;
using GitCommands.Settings;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitExtUtils.GitUI;
using GitExtUtils.GitUI.Theming;
using GitUI.CommandsDialogs;
using GitUI.Editor;
using GitUI.UserControls;
using Microsoft.VisualStudio.Threading;
using ResourceManager;
using ResourceManager.CommitDataRenders;
using GitRevision = GitExtensions.Extensibility.Git.GitRevision;

namespace GitUI.CommitInfo;

public partial class CommitInfo : GitModuleControl
{
    private static readonly TranslationString _brokenRefs = new("The repository refs seem to be broken:");
    private static readonly TranslationString _copyLink = new("Copy &link ({0})");
    private static readonly TranslationString _trsLinksRelatedToRevision = new("Related links:");
    private static readonly TranslationString _derivesFromTag = new("Derives from tag:");
    private static readonly TranslationString _derivesFromNoTag = new("Derives from no tag");
    private static readonly TranslationString _plusCommits = new("commits");
    private static readonly TranslationString _repoFailure = new("Repository failure");
    private readonly CancellationTokenSequence _asyncLoadCancellation = new();

    private readonly ICommitDataManager _commitDataManager;
    private readonly IDisposable _commitMessageResizedSubscription;
    private readonly IConfiguredLinkDefinitionsProvider _effectiveLinkDefinitionsProvider;
    private readonly IExternalLinkRevisionParser _externalLinkRevisionParser;
    private readonly IExternalLinksStorage _externalLinksStorage;
    private readonly GitDescribeProvider _gitDescribeProvider;
    private readonly IGitRevisionExternalLinksParser _gitRevisionExternalLinksParser;
    private readonly IConfigFileRemoteSettingsManager _remotesManager;

    private readonly IDisposable _revisionInfoResizedSubscription;
    private string? _annotatedTagsInfo;
    private IDictionary<string, string>? _annotatedTagsMessages;
    private string? _branchInfo;
    private string[]? _branches;
    private IReadOnlyList<ObjectId>? _children;

    private ICommitDataBodyRenderer? _commitDataBodyRenderer;
    private int _commitMessageHeight;
    private string? _gitDescribeInfo;
    private ILinkFactory? _linkFactory;
    private string? _linksInfo;
    private RefsFormatter? _refsFormatter;

    private GitRevision? _revision;
    private int _revisionInfoHeight;
    private bool _showAllBranches;
    private bool _showAllTags;
    private string? _tagInfo;
    private string[]? _tags;
    private IDictionary<string, int>? _tagsOrderDict;

    public CommitInfo()
        : this(null)
    {
    }

    public CommitInfo(ICommitDataManager? commitDataManager)
    {
        InitializeComponent();
        InitializeComplete();

        _commitDataManager = commitDataManager ?? new CommitDataManager(() => Module);

        _externalLinksStorage = new ExternalLinksStorage();
        _effectiveLinkDefinitionsProvider = new ConfiguredLinkDefinitionsProvider(_externalLinksStorage);
        _remotesManager = new ConfigFileRemoteSettingsManager(() => Module);
        _externalLinkRevisionParser = new ExternalLinkRevisionParser(_remotesManager);
        _gitRevisionExternalLinksParser =
            new GitRevisionExternalLinksParser(_effectiveLinkDefinitionsProvider, _externalLinkRevisionParser);
        _gitDescribeProvider = new GitDescribeProvider(() => Module);

        var messageBackground = SystemColors.Window.MakeDarkerBy(0.04);
        pnlCommitMessage.BackColor = messageBackground;
        rtbxCommitMessage.BackColor = messageBackground;

        rtbxCommitMessage.Font = AppSettings.CommitFont;
        RevisionInfo.Font = AppSettings.Font;
        addNoteToolStripMenuItem.ShortcutKeyDisplayString = GetShortcutKeyDisplayString(FormBrowse.Command.AddNotes);

        _commitMessageResizedSubscription =
            SubscribeToContentsResized(rtbxCommitMessage, CommitMessage_ContentsResized);
        _revisionInfoResizedSubscription = SubscribeToContentsResized(RevisionInfo, RevisionInfo_ContentsResized);

        IDisposable SubscribeToContentsResized(RichTextBox richTextBox, Action<ContentsResizedEventArgs> handler)
        {
            return Observable
                .FromEventPattern<ContentsResizedEventHandler, ContentsResizedEventArgs>(
                    h => richTextBox.ContentsResized += h,
                    h => richTextBox.ContentsResized -= h)
                .Throttle(TimeSpan.FromMilliseconds(100))
                .ObserveOn(MainThreadScheduler.Instance)
                .Subscribe(eventPattern =>
                    TaskManager.HandleExceptions(() => handler(eventPattern.EventArgs), Application.OnThreadException));
        }

        commitInfoHeader.SetContextMenuStrip(commitInfoContextMenuStrip);

        // This issue surfaces at 150% scale factor.
        // At this point rtbxCommitMessage.Bounds = {X = 8 Y = 8 Width = 440 Height = 0}
        // and with Height=0 we won't be receiving any ContentsResizedEvents.
        // To workaround the zero-height - force the min size.
        rtbxCommitMessage.MinimumSize = new Size(1, 1);
    }

    [DefaultValue(false)] public bool ShowBranchesAsLinks { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Browsable(false)]
    public GitRevision? Revision
    {
        get => _revision;
        set => SetRevisionWithChildren(value, null);
    }

    private event EventHandler<CommandEventArgs>? CommandClickedEvent;

    public event EventHandler<CommandEventArgs>? CommandClicked
    {
        add
        {
            CommandClickedEvent += value;
            commitInfoHeader.CommandClicked += value;
        }
        remove
        {
            CommandClickedEvent -= value;
            commitInfoHeader.CommandClicked -= value;
        }
    }

    /// <summary>
    ///     Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _asyncLoadCancellation.Dispose();

            components?.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnRuntimeLoad()
    {
        base.OnRuntimeLoad();
        ReloadHotkeys();
    }

    protected override void OnUICommandsSourceSet(IGitUiCommandsSource source)
    {
        base.OnUICommandsSourceSet(source);

        if (source is null)
        {
            _linkFactory = null;
            _commitDataBodyRenderer = null;
            _refsFormatter = null;
        }
        else
        {
            _linkFactory = source.UiCommands.GetRequiredService<ILinkFactory>();
            _commitDataBodyRenderer = new CommitDataBodyRenderer(() => Module, _linkFactory);
            _refsFormatter = new RefsFormatter(_linkFactory);

            source.UiCommandsChanged += delegate { RefreshSortedTags(); };

            // call this event handler also now (necessary for "Contained in branches/tags")
            RefreshSortedTags();
        }
    }

    internal void ReloadHotkeys()
    {
        LoadHotkeys(FormBrowse.HotkeySettingsName);
    }

    private void RefreshSortedTags()
    {
        if (!Module.IsValidGitWorkingDir())
        {
            return;
        }

        ThreadHelper.FileAndForget(LoadSortedTagsAsync);
    }

    private void LinkClicked(object sender, LinkClickedEventArgs e)
    {
        try
        {
            var linkUri = ((RichTextBox)sender).GetLink(e.LinkStart);
            Validates.NotNull(_linkFactory);
            _linkFactory?.ExecuteLink(linkUri,
                commandEventArgs => CommandClickedEvent?.Invoke(sender, commandEventArgs), ShowAll);
        }
        catch (Exception ex)
        {
            MessageBoxes.Show(this, ex.Message, TranslatedStrings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    public void SetRevisionWithChildren(GitRevision? revision, IReadOnlyList<ObjectId>? children)
    {
        var cancellationToken = _asyncLoadCancellation.Next();

        _revision = revision;
        _children = children;

        if (revision is null)
        {
            tableLayout.Visible = false;
            return;
        }

        tableLayout.Visible = true;
        commitInfoHeader.ShowCommitInfo(revision, children);
        ReloadCommitInfo(cancellationToken);
    }

    private void ShowAll(string? what)
    {
        switch (what)
        {
            case "branches":
                _showAllBranches = true;
                _branchInfo = null; // forces update
                break;
            case "tags":
                _showAllTags = true;
                _tagInfo = null; // forces update
                break;
            default:
                DebugHelpers.Fail($"Unsupported type in ShowAll('{what}')");
                return;
        }

        UpdateRevisionInfo();
    }

    private IDictionary<string, int> GetSortedTags()
    {
        GitArgumentBuilder args = new("for-each-ref")
        {
            @"--sort=""-taggerdate""", @"--format=""%(refname)""", "refs/tags/"
        };

        var tree = Module.GitExecutable.GetOutput(args);
        var warningPos = tree.IndexOf("warning:");
        if (warningPos >= 0)
        {
            throw new RefsWarningException(tree[warningPos..].LazySplit('\n', StringSplitOptions.RemoveEmptyEntries)
                .First());
        }

        var i = 0;
        Dictionary<string, int> dict = [];
        foreach (var entry in tree.LazySplit('\n'))
        {
            if (dict.TryAdd(entry, i))
            {
                ++i;
            }
        }

        return dict;
    }

    private async Task LoadSortedTagsAsync()
    {
        try
        {
            var tagsOrderDict = GetSortedTags();

            await this.SwitchToMainThreadAsync();
            _tagsOrderDict = tagsOrderDict;
            UpdateRevisionInfo();
        }
        catch (RefsWarningException ex)
        {
            await this.SwitchToMainThreadAsync();
            MessageBoxes.Show(this, string.Format("{0}{1}{1}{2}", _brokenRefs.Text, Environment.NewLine, ex.Message),
                _repoFailure.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ReloadCommitInfo()
    {
        ReloadCommitInfo(_asyncLoadCancellation.Next());
    }

    private void ReloadCommitInfo(CancellationToken cancellationToken)
    {
        showContainedInBranchesToolStripMenuItem.Checked = AppSettings.CommitInfoShowContainedInBranchesLocal;
        showContainedInBranchesRemoteToolStripMenuItem.Checked = AppSettings.CommitInfoShowContainedInBranchesRemote;
        showContainedInBranchesRemoteIfNoLocalToolStripMenuItem.Checked =
            AppSettings.CommitInfoShowContainedInBranchesRemoteIfNoLocal;
        showContainedInTagsToolStripMenuItem.Checked = AppSettings.CommitInfoShowContainedInTags;
        showMessagesOfAnnotatedTagsToolStripMenuItem.Checked = AppSettings.ShowAnnotatedTagsMessages;
        showTagThisCommitDerivesFromMenuItem.Checked = AppSettings.CommitInfoShowTagThisCommitDerivesFrom;

        _showAllBranches = false;
        _showAllTags = false;
        _branches = null;
        _tags = null;
        _annotatedTagsMessages = null;

        _annotatedTagsInfo = "";
        _linksInfo = "";
        _branchInfo = "";
        _tagInfo = "";
        _gitDescribeInfo = "";

        if (_revision is not null && !_revision.IsArtificial && !_revision.IsAutostash)
        {
            if (Module.GetEffectiveSettings() is DistributedSettings distributedSettings)
            {
                StartAsyncDataLoad(distributedSettings, cancellationToken);
            }
            else
            {
                DebugHelpers.Fail($"{nameof(Module.GetEffectiveSettings)} have unexpected type.");
            }
        }
        else
        {
            rtbxCommitMessage.SetXhtmlText(GetFixCommitMessage());
            RevisionInfo.Clear();
        }

        return;

        string GetFixCommitMessage()
        {
            if (_revision is null)
            {
                return string.Empty;
            }

            var data = _commitDataManager.CreateFromRevision(_revision, _children);
            return _commitDataBodyRenderer?.Render(data, false) ?? string.Empty;
        }

        async Task UpdateCommitMessageAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_revision.Body is null || (_revision.Notes is null &&
                                           (AppSettings.ShowGitNotesColumn.Value || AppSettings.ShowGitNotes)))
            {
                _commitDataManager.UpdateBodyAndNotes(_revision);
            }

            var data = _commitDataManager.CreateFromRevision(_revision, _children);

            cancellationToken.ThrowIfCancellationRequested();

            var commitDataBodyRenderer = _commitDataBodyRenderer;
            if (commitDataBodyRenderer is null)
            {
                // Cancel the update if the commands source has been unset
                return;
            }

            var commitMessage = commitDataBodyRenderer.Render(data, CommandClickedEvent is not null);

            await this.SwitchToMainThreadAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            rtbxCommitMessage.SetXhtmlText(commitMessage);
        }

        void StartAsyncDataLoad(DistributedSettings settings, CancellationToken cancellationToken)
        {
            var initialRevision = _revision;

            ThreadHelper.FileAndForget(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                List<Task> tasks =
                [
                    UpdateCommitMessageAsync(cancellationToken),
                    LoadLinksForRevisionAsync(initialRevision, settings).WithCancellation(cancellationToken)
                ];

                // No branch/tag data for artificial commands
                if (AppSettings.CommitInfoShowContainedInBranches)
                {
                    tasks.Add(LoadBranchInfoAsync(initialRevision.ObjectId).WithCancellation(cancellationToken));
                }

                if (AppSettings.ShowAnnotatedTagsMessages)
                {
                    tasks.Add(LoadAnnotatedTagInfoAsync(initialRevision.Refs).WithCancellation(cancellationToken));
                }

                if (AppSettings.CommitInfoShowContainedInTags)
                {
                    tasks.Add(LoadTagInfoAsync(initialRevision.ObjectId).WithCancellation(cancellationToken));
                }

                if (AppSettings.CommitInfoShowTagThisCommitDerivesFrom)
                {
                    tasks.Add(LoadDescribeInfoAsync(initialRevision.ObjectId).WithCancellation(cancellationToken));
                }

                cancellationToken.ThrowIfCancellationRequested();

                await Task.WhenAll(tasks);

                await this.SwitchToMainThreadAsync(cancellationToken);
                UpdateRevisionInfo();
            });

            return;

            async Task LoadLinksForRevisionAsync(GitRevision revision, DistributedSettings settings)
            {
                await TaskScheduler.Default;
                cancellationToken.ThrowIfCancellationRequested();

                var linkFactory = _linkFactory;
                if (linkFactory is null)
                {
                    // Cancel the update if the commands source has been unset
                    return;
                }

                var linksInfo = GetLinksForRevision(settings);

                // Most commits do not have link; do not switch to main thread if nothing is changed
                if (_linksInfo == linksInfo)
                {
                    return;
                }

                await this.SwitchToMainThreadAsync(cancellationToken);
                _linksInfo = linksInfo;

                return;

                string GetLinksForRevision(DistributedSettings settings)
                {
                    var links = _gitRevisionExternalLinksParser.Parse(revision, settings);
                    cancellationToken.ThrowIfCancellationRequested();
                    var result = string.Join(", ",
                        links.Distinct().Select(link => linkFactory.CreateLink(link.Caption, link.Uri)));

                    if (string.IsNullOrEmpty(result))
                    {
                        return "";
                    }

                    return $"{WebUtility.HtmlEncode(_trsLinksRelatedToRevision.Text)} {result}";
                }
            }

            async Task LoadAnnotatedTagInfoAsync(IReadOnlyList<IGitRef> refs)
            {
                await TaskScheduler.Default;

                var annotatedTagsMessages = GetAnnotatedTagsMessages();

                await this.SwitchToMainThreadAsync(cancellationToken);
                _annotatedTagsMessages = annotatedTagsMessages;

                return;

                IDictionary<string, string>? GetAnnotatedTagsMessages()
                {
                    if (refs is null)
                    {
                        return null;
                    }

                    Dictionary<string, string> result = [];

                    foreach (var gitRef in refs)
                    {
                        #region Note on annotated tags

                        // Notice that for the annotated tags, gitRef's come in pairs because they're produced
                        // by the "show-ref --dereference" command. GitRef's in such pair have the same Name,
                        // a bit different CompleteName's, and completely different checksums:
                        //      GitRef_1:
                        //      {
                        //          Name: "some_tag"
                        //          CompleteName: "refs/tags/some_tag"
                        //          Guid: <some_tag_checksum>
                        //      },
                        //
                        //      GitRef_2:
                        //      {
                        //          Name: "some_tag"
                        //          CompleteName: "refs/tags/some_tag^{}"   <- by "^{}", IsDereference is true.
                        //          Guid: <target_object_checksum>
                        //      }
                        //
                        // The 2nd one is a dereference: a link between the tag and the object which it references.
                        // GitRevision.Refs by design contains GitRefs where Guids are equal to the GitRevision.Guid,
                        // so this collection contains only dereferencing GitRef's - just because GitRef_2 has the same
                        // Guid as the GitRevision, while GitRef_1 doesn't. So annotated tag's GitRef would always be
                        // of 2nd type in GitRevision.Refs collection, i.e. the one that has IsDereference==true.

                        #endregion

                        if (gitRef is { IsTag: true, IsDereference: true })
                        {
                            var content =
                                WebUtility.HtmlEncode(Module.GetTagMessage(gitRef.LocalName, cancellationToken));
                            if (content is not null)
                            {
                                result.Add(gitRef.LocalName, content);
                            }
                        }
                    }

                    return result;
                }
            }

            async Task LoadTagInfoAsync(ObjectId objectId)
            {
                await TaskScheduler.Default;

                string[] tags = [.. Module.GetAllTagsWhichContainGivenCommit(objectId, cancellationToken)];

                await this.SwitchToMainThreadAsync(cancellationToken);
                _tags = tags;
            }

            async Task LoadBranchInfoAsync(ObjectId objectId)
            {
                await TaskScheduler.Default;

                // Include local branches if explicitly requested or when needed to decide whether to show remotes
                var getLocal = AppSettings.CommitInfoShowContainedInBranchesLocal ||
                               AppSettings.CommitInfoShowContainedInBranchesRemoteIfNoLocal;

                // Include remote branches if requested
                var getRemote = AppSettings.CommitInfoShowContainedInBranchesRemote ||
                                AppSettings.CommitInfoShowContainedInBranchesRemoteIfNoLocal;
                string[] branches =
                    [.. Module.GetAllBranchesWhichContainGivenCommit(objectId, getLocal, getRemote, cancellationToken)];

                await this.SwitchToMainThreadAsync(cancellationToken);
                _branches = branches;
            }

            async Task LoadDescribeInfoAsync(ObjectId commitId)
            {
                await TaskScheduler.Default;

                var linkFactory = _linkFactory;
                if (linkFactory is null)
                {
                    // Cancel the update if the commands source has been unset
                    return;
                }

                var info = GetDescribeInfoForRevision();

                await this.SwitchToMainThreadAsync(cancellationToken);
                _gitDescribeInfo = info;

                return;

                string GetDescribeInfoForRevision()
                {
                    (var precedingTag, var commitCount) = _gitDescribeProvider.Get(commitId, cancellationToken);

                    StringBuilder gitDescribeInfo = new();
                    if (!string.IsNullOrEmpty(precedingTag))
                    {
                        var tagString = ShowBranchesAsLinks
                            ? linkFactory.CreateTagLink(precedingTag)
                            : WebUtility.HtmlEncode(precedingTag);
                        gitDescribeInfo.Append(WebUtility.HtmlEncode(_derivesFromTag.Text)).Append(' ')
                            .Append(tagString);
                        if (!string.IsNullOrEmpty(commitCount))
                        {
                            gitDescribeInfo.Append(" + ").Append(commitCount).Append(' ')
                                .Append(WebUtility.HtmlEncode(_plusCommits.Text));
                        }
                    }
                    else
                    {
                        gitDescribeInfo.Append(WebUtility.HtmlEncode(_derivesFromNoTag.Text));
                    }

                    return gitDescribeInfo.ToString();
                }
            }
        }
    }

    private void UpdateRevisionInfo()
    {
        var refsFormatter = _refsFormatter;
        if (refsFormatter is null)
        {
            // Cancel the update if the commands source has been unset
            return;
        }

        if (_tagsOrderDict is not null)
        {
            if (_annotatedTagsMessages is not null &&
                _annotatedTagsMessages.Count > 0 &&
                string.IsNullOrEmpty(_annotatedTagsInfo) &&
                Revision is not null)
            {
                // having both lightweight & annotated tags in thisRevisionTagNames,
                // but GetAnnotatedTagsInfo will process annotated only:
                List<string> thisRevisionTagNames =
                [
                    .. Revision
                        .Refs
                        .Where(r => r.IsTag)
                        .Select(r => r.LocalName)
                ];

                thisRevisionTagNames.Sort(new TagsComparer(_tagsOrderDict));
                _annotatedTagsInfo = GetAnnotatedTagsInfo(thisRevisionTagNames, _annotatedTagsMessages);
            }

            if (_tags is not null && string.IsNullOrEmpty(_tagInfo))
            {
                Array.Sort(_tags, new TagsComparer(_tagsOrderDict));
                _tagInfo = refsFormatter.FormatTags(_tags, ShowBranchesAsLinks, !_showAllTags);
            }
        }

        if (_branches is not null && string.IsNullOrEmpty(_branchInfo))
        {
            Array.Sort(_branches, new BranchComparer(_branches, Module.GetSelectedBranch()));
            _branchInfo = refsFormatter.FormatBranches(_branches, ShowBranchesAsLinks, !_showAllBranches);
        }

        var body = string.Join(Environment.NewLine + Environment.NewLine,
            new[] { _annotatedTagsInfo, _linksInfo, _branchInfo, _tagInfo, _gitDescribeInfo }
                .Where(_ => !string.IsNullOrEmpty(_)));

        RevisionInfo.SetXhtmlText(body);
        return;

        static string GetAnnotatedTagsInfo(
            IEnumerable<string> tagNames,
            IDictionary<string, string> annotatedTagsMessages)
        {
            StringBuilder result = new();

            foreach (var tag in tagNames)
            {
                if (annotatedTagsMessages.TryGetValue(tag, out var annotatedContents))
                {
                    result.Append("<u>").Append(tag).Append("</u>: ").Append(annotatedContents).AppendLine();
                }
            }

            return result.ToString().TrimEnd();
        }
    }

    private void commitInfoContextMenuStrip_Opening(object sender, CancelEventArgs e)
    {
        if ((sender as ContextMenuStrip)?.SourceControl is not RichTextBox rtb)
        {
            copyLinkToolStripMenuItem.Visible = false;
            return;
        }

        var charIndex = rtb.GetCharIndexFromPosition(rtb.PointToClient(MousePosition));
        var link = rtb.GetLink(charIndex);
        copyLinkToolStripMenuItem.Visible = link is not null;
        copyLinkToolStripMenuItem.Text = string.Format(_copyLink.Text, link);
        copyLinkToolStripMenuItem.Tag = link;
    }

    private void copyLinkToolStripMenuItem_Click(object sender, EventArgs e)
    {
        ClipboardUtil.TrySetText((string)copyLinkToolStripMenuItem.Tag!);
    }

    private void showContainedInBranchesToolStripMenuItem_Click(object sender, EventArgs e)
    {
        AppSettings.CommitInfoShowContainedInBranchesLocal = !AppSettings.CommitInfoShowContainedInBranchesLocal;
        ReloadCommitInfo();
    }

    private void showContainedInTagsToolStripMenuItem_Click(object sender, EventArgs e)
    {
        AppSettings.CommitInfoShowContainedInTags = !AppSettings.CommitInfoShowContainedInTags;
        ReloadCommitInfo();
    }

    private void showTagThisCommitDerivesFromMenuItem_Click(object sender, EventArgs e)
    {
        AppSettings.CommitInfoShowTagThisCommitDerivesFrom = !AppSettings.CommitInfoShowTagThisCommitDerivesFrom;
        ReloadCommitInfo();
    }

    private void copyCommitInfoToolStripMenuItem_Click(object sender, EventArgs e)
    {
        var commitInfo =
            $"{commitInfoHeader.GetPlainText()}{Environment.NewLine}{Environment.NewLine}{rtbxCommitMessage.GetPlainText()}";
        ClipboardUtil.TrySetText(commitInfo);
    }

    private void showContainedInBranchesRemoteToolStripMenuItem_Click(object sender, EventArgs e)
    {
        AppSettings.CommitInfoShowContainedInBranchesRemote = !AppSettings.CommitInfoShowContainedInBranchesRemote;
        ReloadCommitInfo();
    }

    private void showContainedInBranchesRemoteIfNoLocalToolStripMenuItem_Click(object sender, EventArgs e)
    {
        AppSettings.CommitInfoShowContainedInBranchesRemoteIfNoLocal =
            !AppSettings.CommitInfoShowContainedInBranchesRemoteIfNoLocal;
        ReloadCommitInfo();
    }

    private void showMessagesOfAnnotatedTagsToolStripMenuItem_Click(object sender, EventArgs e)
    {
        AppSettings.ShowAnnotatedTagsMessages = !AppSettings.ShowAnnotatedTagsMessages;
        ReloadCommitInfo();
    }

    private void addNoteToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (_revision is null)
        {
            return;
        }

        Module.EditNotes(_revision.ObjectId);
        _revision.Body = null;
        _revision.Notes = null;
        ReloadCommitInfo();
    }

    private void _RevisionHeader_MouseDown(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.XButton1)
        {
            DoCommandClick("navigatebackward");
        }
        else if (e.Button == MouseButtons.XButton2)
        {
            DoCommandClick("navigateforward");
        }

        void DoCommandClick(string command)
        {
            CommandClickedEvent?.Invoke(this, new CommandEventArgs(command, null));
        }
    }

    private void RevisionInfo_ContentsResized(ContentsResizedEventArgs e)
    {
        _revisionInfoHeight = e.NewRectangle.Height;
        PerformLayout();
    }

    private void CommitMessage_ContentsResized(ContentsResizedEventArgs e)
    {
        _commitMessageHeight = e.NewRectangle.Height;

        // at scale factor of 150% there is rendering artifact - the last line is almost lost
        // at all other scaling factors from 100% to 350% everything is rendered as expected
        // see https://github.com/gitextensions/gitextensions/issues/6898 for more details
        //
        // absent of an obvious way to fix it, hardcode the fix - add an extra space at the bottom
        if (Math.Abs(DpiUtil.ScaleX - 1.5f) <= 0.01f)
        {
            _commitMessageHeight += 16;
        }

        PerformLayout();
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        tableLayout.SuspendLayout();

        int[] heights =
        [
            commitInfoHeader.Height + commitInfoHeader.Margin.Vertical,
            _commitMessageHeight + rtbxCommitMessage.Margin.Vertical + pnlCommitMessage.Margin.Vertical,
            _revisionInfoHeight + RevisionInfo.Margin.Vertical
        ];

        // leave 1st row SizeType = AutoWidth to let CommitInfoHeader.AutoSize be correctly applied
        for (var i = 1; i < tableLayout.RowStyles.Count; i++)
        {
            tableLayout.RowStyles[i].SizeType = SizeType.Absolute;
            tableLayout.RowStyles[i].Height = heights[i];
        }

        var height = heights.Sum();

        var clientWidth = Width;
        if (height > Height)
        {
            clientWidth -= SystemInformation.VerticalScrollBarWidth;
        }

        var width = Math.Max(clientWidth, commitInfoHeader.Width + commitInfoHeader.Margin.Horizontal);

        tableLayout.ColumnStyles[0].SizeType = SizeType.Absolute;
        tableLayout.ColumnStyles[0].Width = width;

        tableLayout.Size = new Size(width, height);

        tableLayout.ResumeLayout(false);
        tableLayout.PerformLayout();

        base.OnLayout(e);
    }

    private void RichTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (!e.Control || e.KeyCode != Keys.C || sender is not RichTextBox rtb)
        {
            return;
        }

        // Override RichTextBox Ctrl-c handling to copy plain text
        ClipboardUtil.TrySetText(rtb.GetSelectionPlainText());
        e.Handled = true;
    }

    protected override void DisposeCustomResources()
    {
        try
        {
            _asyncLoadCancellation.Dispose();
            _revisionInfoResizedSubscription?.Dispose();
            _commitMessageResizedSubscription?.Dispose();

            base.DisposeCustomResources();
        }
        catch (InvalidOperationException)
        {
            // System.Reactive causes the app to fail with: 'Invoke or BeginInvoke cannot be called on a control until the window handle has been created.'
        }
    }

    internal TestAccessor GetTestAccessor()
    {
        return new TestAccessor(this);
    }

    internal sealed class BranchComparer : IComparer<string>
    {
        private const string _remoteBranchPrefix = "remotes/";
        private readonly string _currentBranch;
        private readonly bool _isDetachedHead;
        private readonly Dictionary<string, int> _orderByBranch = [];

        public BranchComparer(string[] branches, string currentBranch)
        {
            _currentBranch = currentBranch;
            _isDetachedHead = DetachedHeadParser.IsDetachedHead(currentBranch);
            var branchRegexes = AppSettings.PrioritizedBranchNames.Split(';',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            string[] localBranchRegexes = [.. branchRegexes.Select(regex => $"^({regex})$")];
            string[] remoteBranchRegexes =
                [.. branchRegexes.Select(regex => $"^{_remoteBranchPrefix}[^/]+/({regex})$")];
            string[] remoteRegexes =
            [
                .. AppSettings.PrioritizedRemoteNames
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(regex => $"^{_remoteBranchPrefix}({regex})/")
            ];

            foreach (var branch in branches)
            {
                _orderByBranch[branch] = GetBranchOrder(branch);
            }

            return;

            // Get the order for each branch.
            // Add max possible order value to next "level" to sort properly with the order for each regex.
            int GetBranchOrder(string branch)
            {
                var order = 0;
                if (_isDetachedHead ? DetachedHeadParser.IsDetachedHead(branch) : branch == _currentBranch)
                {
                    return order;
                }

                // length of "current branch" group
                order += 1;

                if (IsLocalBranch())
                {
                    if (!TryGetOrder(branch, localBranchRegexes, out var localBranchOrder))
                    {
                        // Non prioritized local branches added after prioritized remote branches
                        // localBranchOrder==localBranchRegexes.Length, an extra priority level
                        order += PrioritizedRemoteBranchesLength();
                    }

                    // Order by branch priority
                    order += localBranchOrder;

                    return order;
                }

                // Remote branches after local prioritized branches
                order += localBranchRegexes.Length;

                if (!TryGetOrder(branch, remoteBranchRegexes, out var remoteBranchOrder))
                {
                    // after non priority local branches (that are inserted after remote prioritzed branches)
                    const int localNonprioritizedBranchesLength = 1;
                    order += localNonprioritizedBranchesLength;
                }

                // Group by branch priority then order by remote
                order += (remoteBranchOrder * RemotesGroupLength()) + GetOrder(branch, remoteRegexes);

                return order;

                bool IsLocalBranch()
                {
                    return !branch.StartsWith(_remoteBranchPrefix);
                }

                // The groups for a prioritized remote branch adds the unprioritized remotes to the regexes
                int RemotesGroupLength()
                {
                    return remoteRegexes.Length + 1;
                }

                // Length of the block of all prioritized remote branches (non prioritized branches separate)
                int PrioritizedRemoteBranchesLength()
                {
                    return remoteBranchRegexes.Length * RemotesGroupLength();
                }

                // Get the index of the match for prioritized sorting,
                // set order to regexes.Length at no match
                bool TryGetOrder(string branch, string[] regexes, out int order)
                {
                    var currentOrder = 0;
                    foreach (var regex in regexes)
                    {
                        if (Regex.IsMatch(branch, regex, RegexOptions.ExplicitCapture))
                        {
                            order = currentOrder;
                            return true;
                        }

                        currentOrder++;
                    }

                    order = currentOrder;
                    return false;
                }

                int GetOrder(string branch, string[] regexes)
                {
                    TryGetOrder(branch, regexes, out var order);
                    return order;
                }
            }
        }

        int IComparer<string>.Compare(string? a, string? b)
        {
            if (b is null)
            {
                return -1;
            }

            if (a is null)
            {
                return 1;
            }

            var priorityA = _orderByBranch[a];
            var priorityB = _orderByBranch[b];
            return priorityA == priorityB ? StringComparer.Ordinal.Compare(a, b) : priorityA - priorityB;
        }
    }

    private sealed class TagsComparer(IDictionary<string, int> orderDict, string prefix = "refs/tags/")
        : IComparer<string>
    {
        int IComparer<string>.Compare(string? a, string? b)
        {
            return b is null ? -1 : a is null ? 1 : IndexOf(a) - IndexOf(b);

            int IndexOf(string s)
            {
                if (s.StartsWith("remotes/"))
                {
                    s = "refs/" + s;
                }
                else
                {
                    s = prefix + s;
                }

                if (orderDict.TryGetValue(s, out var index))
                {
                    return index;
                }

                return -1;
            }
        }
    }

    internal readonly struct TestAccessor(CommitInfo commitInfo)
    {
        public RichTextBox CommitMessage
        {
            get => commitInfo.rtbxCommitMessage;
        }

        public RichTextBox RevisionInfo
        {
            get => commitInfo.RevisionInfo;
        }

        public IDictionary<string, int> GetSortedTags()
        {
            return commitInfo.GetSortedTags();
        }

        public void LinkClicked(object sender, LinkClickedEventArgs e)
        {
            commitInfo.LinkClicked(sender, e);
        }
    }
}
