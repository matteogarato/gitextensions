using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;
using System.Text;
using GitCommands;
using GitCommands.Config;
using GitCommands.Git;
using GitCommands.Settings;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Extensions;
using GitExtensions.Extensibility.Git;
using GitExtUtils.GitUI;
using GitExtUtils.GitUI.Theming;
using GitUI.CommandsDialogs;
using GitUI.Properties;
using GitUI.Theming;
using GitRevision = GitExtensions.Extensibility.Git.GitRevision;

namespace GitUI.UserControls.RevisionGrid.Columns;

internal sealed class MessageColumnProvider : ColumnProvider
{
    public const int MaxSuperprojectRefs = 4;
    private readonly Image _bisectBadImage = DpiUtil.Scale(Images.BisectBad);

    private readonly Image _bisectGoodImage = DpiUtil.Scale(Images.BisectGood);

    private readonly ICommitDataManager? _commitDataManager;
    private readonly Image _fixupAndSquashImage = DpiUtil.Scale(Images.FixupAndSquashMessageMarker);
    private readonly IGitRevisionSummaryBuilder _gitRevisionSummaryBuilder;
    private readonly RevisionGridControl _grid;

    // Pool of reusable lists to reduce allocations during scrolling.
    private readonly Stack<List<RefLabelHitInfo>> _hitInfoListPool = new();

    /// <summary>
    ///     Caches painted ref label hit regions per row index for mouse hit-testing.
    /// </summary>
    private readonly Dictionary<int, List<RefLabelHitInfo>> _refLabelHitInfoByRow = [];

    // Caches the configured push prefix per remote name to avoid repeated git-config reads during painting.
    private readonly Dictionary<string, string> _remotePrefixCache = [];

    private readonly StringBuilder _toolTipBuilder = new(200);

    private IReadOnlyDictionary<string, AheadBehindData>? _aheadBehindDataByLocalBranch;
    private IReadOnlyDictionary<string, AheadBehindData>? _aheadBehindDataByRemoteBranch;
    private IAheadBehindDataProvider? _aheadBehindDataProvider;

    // The ref currently under the mouse cursor, used to draw a highlight border.
    private IGitRef? _highlightedRef;

    // The row index of the currently highlighted ref label.
    private int _highlightedRowIndex = -1;

    // The row index of the currently highlighted stash label (which has no IGitRef).
    private int _highlightedStashRow = -1;

    private Settings _settings;

    public MessageColumnProvider(RevisionGridControl grid, IGitRevisionSummaryBuilder gitRevisionSummaryBuilder,
        ICommitDataManager? commitDataManager)
        : base("Message")
    {
        _commitDataManager = commitDataManager;
        _grid = grid;
        _gitRevisionSummaryBuilder = gitRevisionSummaryBuilder;

        Column = new DataGridViewTextBoxColumn
        {
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            HeaderText = "Message",
            ReadOnly = true,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            Width = DpiUtil.Scale(500),
            MinimumWidth = DpiUtil.Scale(25)
        };
    }

    public void SetAheadBehindDataProvider(IAheadBehindDataProvider? provider)
    {
        _aheadBehindDataProvider = provider;
        _aheadBehindDataByLocalBranch = null;
        _aheadBehindDataByRemoteBranch = null;
    }

    public override void ApplySettings()
    {
        _settings = new Settings(
            AppSettings.FillRefLabels,
            AppSettings.ShowGitNotesColumn.Value,
            AppSettings.ShowAnnotatedTagsMessages,
            AppSettings.ShowCommitBodyInRevisionGrid,
            AppSettings.ShowGitNotes,
            AppSettings.ShowGitStatusForArtificialCommits,
            AppSettings.ShowRemoteBranches,
            AppSettings.ShowRevisionGridTooltips.Value,
            AppSettings.ShowTags);
    }

    public override void OnCellPainting(DataGridViewCellPaintingEventArgs e, GitRevision revision, int rowHeight,
        in CellStyle style)
    {
        Validates.NotNull(e.Graphics);

        MultilineIndicator indicator = new(e, revision);
        var messageBounds = indicator.RemainingCellBounds;
        List<IGitRef> superprojectRefs = [];
        var offset = ColumnLeftMargin;

        List<RefLabelHitInfo>? hitInfos = null;

        if (_grid.TryGetSuperProjectInfo(out var spi))
        {
            // Draw super project references (for submodules)
            DrawSuperprojectInfo(e, spi, revision, style, messageBounds, ref offset);

            if (spi.Refs is not null && !revision.ObjectId.IsZero &&
                spi.Refs.TryGetValue(revision.ObjectId, out var refs))
            {
                superprojectRefs.AddRange(refs);
            }
        }

        if (revision.Refs.Count != 0)
        {
            var gitRefs = SortRefs(revision.Refs.Where(FilterRef));
            var trackedRemotes = BuildTrackedRemoteMap(gitRefs);

            // When there is only one local branch on this commit, remote-ref labels can omit the branch name if equal.
            var localBranchCount = gitRefs.Count(gitRef => gitRef.IsHead);
            var singleLocalBranchName = localBranchCount == 1 ? trackedRemotes.Keys.FirstOrDefault() : null;

            foreach (var gitRef in gitRefs)
            {
                if (offset > messageBounds.Width)
                {
                    // Stop drawing refs if we run out of room
                    break;
                }

                // Remote refs that are tracked by a local branch in this row
                // are drawn condensed immediately after that local branch instead.
                if (trackedRemotes.ContainsValue(gitRef))
                {
                    continue;
                }

                var superprojectRef =
                    superprojectRefs.FirstOrDefault(superGitRef => gitRef.CompleteName == superGitRef.CompleteName);
                if (superprojectRef is not null)
                {
                    superprojectRefs.Remove(superprojectRef);
                }

                var isHighlighted = _highlightedRowIndex == e.RowIndex && ReferenceEquals(_highlightedRef, gitRef);

                // If this branch is at its tracked remote, draw them condensed.
                if (gitRef.IsHead && trackedRemotes.TryGetValue(gitRef.Name, out var remote))
                {
                    DrawBranchWithNestledRemote(gitRef, superprojectRef, style, messageBounds, ref offset,
                        isHighlighted, remote, remote.Remote, ref hitInfos);
                    continue;
                }

                // If this branch has ahead/behind information, draw that info as virtual label of the tracked/tracking branch.
                (var aheadBehind, var trackedCompleteName, var isGone) = GetAheadBehind(gitRef, false);
                if (aheadBehind.Length > 0)
                {
                    NestledVirtualRef nestledRef = new(gitRef, trackedCompleteName, isGone);
                    DrawBranchWithNestledRemote(gitRef, superprojectRef, style, messageBounds, ref offset,
                        isHighlighted, nestledRef, aheadBehind, ref hitInfos);
                    continue;
                }

                DrawSeparateRef(gitRef, superprojectRef, style, messageBounds, ref offset, isHighlighted,
                    singleLocalBranchName, ref hitInfos);
            }
        }

        DrawSuperprojectRefs(e, superprojectRefs, style, messageBounds, ref offset);

        if (revision.IsStash || revision.IsAutostash)
        {
            var isStashHighlighted = _highlightedRowIndex == e.RowIndex && _highlightedRef is null &&
                                     _highlightedStashRow == e.RowIndex;
            var stashRect = RevisionGridRefRenderer.DrawRef(
                e.State.HasFlag(DataGridViewElementStates.Selected),
                style.NormalFont,
                ref offset,
                revision.IsAutostash
                    ? revision.Subject
                    : (revision.ReflogSelector ??
                       throw new InvalidOperationException($"{nameof(revision.ReflogSelector)} must not be null"))[5..],
                AppColor.OtherTag.GetThemeColor(),
                RefLabelIcon.Stash,
                messageBounds,
                e.Graphics,
                false,
                _settings.FillRefLabels,
                isStashHighlighted);
            if (stashRect != Rectangle.Empty)
            {
                hitInfos ??= RentHitInfoList();
                hitInfos.Add(new RefLabelHitInfo(stashRect, RefLabelShape.Rect, 0, null, revision.ReflogSelector));
            }
        }

        if (revision.IsArtificial)
        {
            DrawArtificialRevision(e, revision, style, messageBounds, ref offset);
        }
        else if (!revision.IsAutostash)
        {
            DrawCommitMessage(e, revision, style, messageBounds, indicator, ref offset);
        }

        if (hitInfos is not null)
        {
            if (_refLabelHitInfoByRow.Remove(e.RowIndex, out var oldList))
            {
                ReturnHitInfoList(oldList);
            }

            _refLabelHitInfoByRow[e.RowIndex] = hitInfos;
        }
        else if (_refLabelHitInfoByRow.Remove(e.RowIndex, out var oldList))
        {
            ReturnHitInfoList(oldList);
        }

        return;

        // Builds a map of local branch name → remote ref that tracks it. No I/O is performed.
        static Dictionary<string, IGitRef> BuildTrackedRemoteMap(IReadOnlyList<IGitRef> refs)
        {
            IReadOnlyList<IGitRef> localBranches = [.. refs.Where(r => r.IsHead)];
            if (localBranches.Count == 0)
            {
                return [];
            }

            Dictionary<string, IGitRef> remoteByLocal = [];
            foreach (var remote in refs)
            {
                if (!remote.IsRemote)
                {
                    continue;
                }

                foreach (var local in localBranches)
                {
                    if (!local.IsTrackingRemote(remote))
                    {
                        continue;
                    }

                    if (!remoteByLocal.TryAdd(local.LocalName, remote))
                    {
                        throw new InvalidOperationException(
                            $"Multiple remote refs {remote.Name} and {remoteByLocal[local.LocalName].Name} claim they were tracked by local branch '{local.LocalName}'.");
                    }
                }
            }

            return remoteByLocal;
        }

        // Draws a local branch capsule with its tracked remote capsule nestled against it, appearing as a single visual group.
        void DrawBranchWithNestledRemote(
            IGitRef gitRef,
            IGitRef? superprojectRef,
            CellStyle style,
            Rectangle messageBounds,
            ref int offset,
            bool isHighlighted,
            IGitRef nestledRef,
            string nestledName,
            ref List<RefLabelHitInfo>? hitInfos)
        {
            var initialOffset = offset;

            var isRemoteHighlighted = _highlightedRowIndex == e.RowIndex && Equals(_highlightedRef, nestledRef);

            if (!style.RemoteColors.TryGetValue(nestledRef.Remote, out var remoteColor))
            {
                remoteColor = RevisionGridRefRenderer.GetHeadColor(nestledRef);
            }

            // Draw the gitRef with a '>' / '<' right edge that meets the nestledRef's matching left indent.
            (var shape1, var shape2) = gitRef.IsRemote
                ? (RefLabelShape.NotchRight, RefLabelShape.PointLeft)
                : (RefLabelShape.PointRight, RefLabelShape.NotchLeft);
            (var branchRect, var drawBranchHighlight) = DrawRef(e, gitRef, superprojectRef, style,
                messageBounds, ref offset, isHighlighted, gitRef.Name, shape1);
            if (branchRect == Rectangle.Empty)
            {
                return;
            }

            // Compute the geometry to align the nestled notch/point exactly against the branch point/notch.
            var branchFont = gitRef.IsSelected ? style.BoldFont : style.NormalFont;
            var pointWidth = RevisionGridRefRenderer.GetPointWidth(branchFont, e.Graphics);

            // Position the NotchLeft rect so its notch tip (rect.X + pointWidth) aligns with the branch point tip (branchRect.Right), cancelling the inter-label margin.
            offset = Math.Max(initialOffset, branchRect.Right - messageBounds.X - pointWidth + 1);

            // Draw the nestled directly via DrawRefEx with RefLabelIcon.None — the nestled remote never shows a head indicator.
            var nestledVirtualRef = nestledRef as NestledVirtualRef;
            (var nestledRect, var drawNestledHighlight) = RevisionGridRefRenderer.DrawRefEx(
                e.State.HasFlag(DataGridViewElementStates.Selected),
                nestledVirtualRef is { TrackingBranchIsGone: true } ? style.BoldFont : style.NormalFont,
                ref offset,
                nestledName,
                remoteColor,
                RefLabelIcon.None,
                messageBounds,
                e.Graphics!,
                nestledVirtualRef is not null,
                _settings.FillRefLabels,
                isRemoteHighlighted,
                shape2);

            // Draw highlight frames last so neither capsule overwrites the other's highlight edge.
            drawBranchHighlight?.Invoke();
            drawNestledHighlight?.Invoke();

            // Register hit-boxes.
            hitInfos ??= RentHitInfoList();
            hitInfos.Add(new RefLabelHitInfo(branchRect, shape1, pointWidth, gitRef, null));

            if (nestledRect == Rectangle.Empty)
            {
                return;
            }

            hitInfos.Add(new RefLabelHitInfo(nestledRect, shape2, pointWidth, nestledRef, null));
        }

        void DrawSeparateRef(
            IGitRef gitRef,
            IGitRef? superprojectRef,
            CellStyle style,
            Rectangle messageBounds,
            ref int offset,
            bool isHighlighted,
            string? singleLocalBranchName,
            ref List<RefLabelHitInfo>? hitInfos)
        {
            var label = !isHighlighted
                        && singleLocalBranchName is not null
                        && gitRef.IsRemote
                        && gitRef.LocalName == GetRemotePrefix(gitRef.Module, gitRef.Remote) + singleLocalBranchName
                ? gitRef.Remote
                : gitRef.Name;
            var shape = gitRef.IsTag ? RefLabelShape.PointLeft : RefLabelShape.Rect;
            (var refRect, var drawHighlight) = DrawRef(e, gitRef, superprojectRef, style, messageBounds,
                ref offset, isHighlighted, label, shape);
            drawHighlight?.Invoke();
            if (refRect != Rectangle.Empty)
            {
                hitInfos ??= RentHitInfoList();
                var pointWidth = shape == RefLabelShape.Rect
                    ? 0
                    : RevisionGridRefRenderer.GetPointWidth(gitRef.IsSelected ? style.BoldFont : style.NormalFont,
                        e.Graphics!);
                hitInfos.Add(new RefLabelHitInfo(refRect, shape, pointWidth, gitRef, null));
            }
        }
    }

    public override void OnCellFormatting(DataGridViewCellFormattingEventArgs e, GitRevision revision)
    {
        // Set the grid cell's accessibility text
        e.Value = revision.Subject.Trim();
    }

    public override bool TryGetToolTip(DataGridViewCellMouseEventArgs e, GitRevision revision, IGitRef? highlightRef,
        [NotNullWhen(true)] out string? toolTip)
    {
        _toolTipBuilder.Clear();

        if (highlightRef is not null)
        {
            if (highlightRef is NestledVirtualRef aheadBehindRef)
            {
                var realRefIsRemote = !aheadBehindRef.IsRemote;
                var realRefLocalName = aheadBehindRef.MergeWith;
                var realRefCompleteName = realRefIsRemote
                    ? GitRefName.GetFullRemoteName(realRefLocalName, aheadBehindRef.TrackingRemote)
                    : GitRefName.GetFullBranchName(realRefLocalName);
                var realRefName = realRefIsRemote
                    ? realRefCompleteName[GitRefName.RefsRemotesPrefix.Length..]
                    : realRefLocalName;
                var data = GetAheadBehindData(realRefIsRemote, realRefCompleteName);
                _toolTipBuilder.Append('[').Append(realRefName).Append(']');
                if (realRefIsRemote)
                {
                    _toolTipBuilder.AppendLine().AppendFormat(TranslatedStrings.IsTrackedByBranchAheadBehind,
                        data?.Branch, data?.ToDisplay());
                }
                else
                {
                    var remoteBranch = data?.RemoteRef[GitRefName.RefsRemotesPrefix.Length..];
                    if (data?.AheadCount == AheadBehindData.Gone)
                    {
                        _toolTipBuilder.AppendLine()
                            .AppendFormat(TranslatedStrings.WasTrackingRemote, remoteBranch);
                    }
                    else
                    {
                        _toolTipBuilder.Append("   ").AppendLine(data?.ToDisplay())
                            .AppendFormat(TranslatedStrings.IsTrackingRemote, remoteBranch);
                    }
                }
            }
            else
            {
                _toolTipBuilder.Append('[').Append(highlightRef.Name).Append(']');
                if (highlightRef.IsRemote)
                {
                    if (GetAheadBehindData(true, highlightRef.CompleteName) is { } data)
                    {
                        _toolTipBuilder.AppendLine().AppendFormat(TranslatedStrings.IsTrackedByBranchAheadBehind,
                            data.Branch, data.ToDisplay());
                    }
                    else if (_settings.ShowRevisionGridTooltips)
                    {
                        _toolTipBuilder.AppendLine().Append(TranslatedStrings.IsRemoteBranch);
                    }
                    else
                    {
                        toolTip = null;
                        return false;
                    }
                }
                else if (highlightRef.IsHead)
                {
                    if (GetAheadBehindData(false, highlightRef.CompleteName) is { } data)
                    {
                        var remoteBranch = data.RemoteRef[GitRefName.RefsRemotesPrefix.Length..];
                        if (data.AheadCount == AheadBehindData.Gone)
                        {
                            _toolTipBuilder.AppendLine()
                                .AppendFormat(TranslatedStrings.WasTrackingRemote, remoteBranch);
                        }
                        else
                        {
                            _toolTipBuilder.Append("   ").AppendLine(data.ToDisplay())
                                .AppendFormat(TranslatedStrings.IsTrackingRemote, remoteBranch);
                        }
                    }
                    else if (_settings.ShowRevisionGridTooltips)
                    {
                        _toolTipBuilder.AppendLine().Append(TranslatedStrings.IsLocalBranch);
                    }
                    else
                    {
                        toolTip = null;
                        return false;
                    }
                }
                else if (highlightRef.IsTag)
                {
                    if (_settings.ShowRevisionGridTooltips)
                    {
                        _toolTipBuilder.AppendLine().Append(TranslatedStrings.IsTag);
                    }
                    else
                    {
                        toolTip = null;
                        return false;
                    }
                }
            }

            if (!_settings.ShowRevisionGridTooltips)
            {
                toolTip = _toolTipBuilder.ToString();
                return true;
            }

            _toolTipBuilder.AppendLine().AppendLine();
        }

        if (!revision.IsArtificial && (revision.HasMultiLineMessage || revision.Refs.Count != 0))
        {
            // The body is not stored for older commits (to save memory)
            var bodySummary = _gitRevisionSummaryBuilder.BuildSummary(GetBody(revision))
                              ?? revision.Subject +
                              (revision.HasMultiLineMessage ? TranslatedStrings.BodyNotLoaded : "");
            var initialLength = bodySummary.Length + 10;
            _toolTipBuilder.EnsureCapacity(initialLength);

            _toolTipBuilder.Append(bodySummary);

            if (revision.Refs.Count != 0)
            {
                if (_toolTipBuilder.Length != 0)
                {
                    _toolTipBuilder.AppendLine();
                    _toolTipBuilder.AppendLine();
                }

                foreach (var gitRef in SortRefs(revision.Refs))
                {
                    if (gitRef.IsBisectGood)
                    {
                        _toolTipBuilder.AppendLine(TranslatedStrings.MarkBisectAsGood);
                    }
                    else if (gitRef.IsBisectBad)
                    {
                        _toolTipBuilder.AppendLine(TranslatedStrings.MarkBisectAsBad);
                    }
                    else
                    {
                        _toolTipBuilder.Append('[').Append(gitRef.Name).Append(']');
                        if (GetAheadBehindData(gitRef.IsRemote, gitRef.CompleteName) is { } data)
                        {
                            _toolTipBuilder.Append("   ").Append(data.ToDisplay(reverse: gitRef.IsRemote));
                        }

                        _toolTipBuilder.AppendLine();
                    }
                }
            }

            toolTip = _toolTipBuilder.ToString();
            return true;
        }

        if (_settings.ShowGitStatusForArtificialCommits && _grid.GetChangeCount(revision.ObjectId) is { } changeCount)
        {
            toolTip = _toolTipBuilder.Append(changeCount.GetSummary()).ToString();
            return true;
        }

        return base.TryGetToolTip(e, revision, highlightRef, out toolTip);
    }

    private void DrawArtificialRevision(
        DataGridViewCellPaintingEventArgs e,
        GitRevision revision,
        CellStyle style,
        Rectangle messageBounds,
        ref int offset)
    {
        var graphics = e.Graphics!; // validated by caller
        var baseOffset = offset;

        // Add fake "refs" for artificial commits
        RevisionGridRefRenderer.DrawRef(
            e.State.HasFlag(DataGridViewElementStates.Selected),
            style.NormalFont,
            ref offset,
            revision.Subject,
            AppColor.OtherTag.GetThemeColor(),
            revision.ObjectId == ObjectId.IndexId ? RefLabelIcon.CommitIndex : RefLabelIcon.WorkingDirectory,
            messageBounds,
            graphics,
            false,
            _settings.FillRefLabels);

        var max = Math.Max(
            TextRenderer.MeasureText(ResourceManager.TranslatedStrings.Workspace, style.NormalFont).Width,
            TextRenderer.MeasureText(ResourceManager.TranslatedStrings.Index, style.NormalFont).Width);

        offset = baseOffset + max + DpiUtil.Scale(6);

        // Summary of changes
        if (!_settings.ShowGitStatusForArtificialCommits ||
            _grid.GetChangeCount(revision.ObjectId) is not { } changeCount)
        {
            return;
        }

        if (changeCount.DataValid)
        {
            if (changeCount.HasChanges)
            {
                DrawArtificialCount(changeCount.Changed, Images.FileStatusModified, ref offset);
                DrawArtificialCount(changeCount.New, Images.FileStatusAdded, ref offset);
                DrawArtificialCount(changeCount.Deleted, Images.FileStatusRemoved, ref offset);
                DrawArtificialCount(changeCount.SubmodulesChanged, Images.SubmoduleRevisionDown, ref offset);
                DrawArtificialCount(changeCount.SubmodulesDirty, Images.SubmoduleDirty, ref offset);
            }
            else
            {
                DrawArtificialCount(null, Images.RepoStateClean, ref offset);
            }
        }
        else
        {
            DrawArtificialCount(null, Images.RepoStateUnknown, ref offset);
        }

        return;

        void DrawArtificialCount(
            IReadOnlyList<GitItemStatus>? items,
            Image icon,
            ref int offset)
        {
            if (items?.Count is 0)
            {
                return;
            }

            var imageVerticalPadding = DpiUtil.Scale(6);
            var textHorizontalPadding = DpiUtil.Scale(4);
            var imageSize = e.CellBounds.Height - imageVerticalPadding - imageVerticalPadding;
            Rectangle imageRect = new(
                messageBounds.Left + offset,
                e.CellBounds.Top + imageVerticalPadding,
                imageSize,
                imageSize);

            var container = graphics.BeginContainer();
            graphics.DrawImage(icon, imageRect);
            graphics.EndContainer(container);
            offset += imageSize + textHorizontalPadding;

            var text = items?.Count.ToString() ?? "";
            var bounds = messageBounds.ReduceLeft(offset);
            var textWidth = Math.Max(
                _grid.DrawColumnText(e, text, style.NormalFont, style.ForeColor, bounds),
                TextRenderer.MeasureText("88", style.NormalFont).Width);
            offset += textWidth + textHorizontalPadding;
        }
    }

    private static void DrawSuperprojectRefs(
        DataGridViewCellPaintingEventArgs e,
        List<IGitRef> superprojectRefs,
        CellStyle style,
        Rectangle messageBounds,
        ref int offset)
    {
        for (var i = 0; i < Math.Min(MaxSuperprojectRefs, superprojectRefs.Count); i++)
        {
            var gitRef = superprojectRefs[i];
            var headColor = RevisionGridRefRenderer.GetHeadColor(gitRef);
            var gitRefName = i < MaxSuperprojectRefs - 1 ? gitRef.Name : "…";

            var icon = gitRef.IsSelected
                ? RefLabelIcon.Head
                : gitRef.IsSelectedHeadMergeSource
                    ? RefLabelIcon.HeadMergeSource
                    : gitRef.IsTag
                        ? RefLabelIcon.Tag
                        : gitRef.IsRemote
                            ? RefLabelIcon.Remote
                            : RefLabelIcon.LocalBranch;
            var font = gitRef.IsSelected ? style.BoldFont : style.NormalFont;

            RevisionGridRefRenderer.DrawRef(
                e.State.HasFlag(DataGridViewElementStates.Selected),
                font,
                ref offset,
                gitRefName,
                headColor,
                icon,
                messageBounds,
                e.Graphics!,
                true);
        }
    }

    private static void DrawSuperprojectInfo(
        DataGridViewCellPaintingEventArgs e,
        SuperProjectInfo spi,
        GitRevision revision,
        CellStyle style,
        Rectangle messageBounds,
        ref int offset)
    {
        if (spi.CurrentCommit == revision.ObjectId)
        {
            DrawSuperProjectRef("", ref offset, true);
        }

        if (spi.ConflictBase == revision.ObjectId)
        {
            DrawSuperProjectRef("Base", ref offset, false);
        }

        if (spi.ConflictLocal == revision.ObjectId)
        {
            DrawSuperProjectRef("Local", ref offset, false);
        }

        if (spi.ConflictRemote == revision.ObjectId)
        {
            DrawSuperProjectRef("Remote", ref offset, false);
        }

        void DrawSuperProjectRef(string label, ref int currentOffset, bool isSelected)
        {
            // Rectangle does not have a BackColor property. Use the cell's background color instead.
            var backColor = e.CellStyle?.BackColor ?? ThemeSettings.Default.Theme.GetColor(AppColor.EditorBackground);
            RevisionGridRefRenderer.DrawRef(
                e.State.HasFlag(DataGridViewElementStates.Selected),
                style.NormalFont,
                ref currentOffset,
                label,
                Color.OrangeRed.AdaptForeColor(backColor),
                isSelected ? RefLabelIcon.Head : RefLabelIcon.HeadMergeSource,
                messageBounds,
                e.Graphics!,
                true);
        }
    }

    private (Rectangle Rect, Action? DrawHighlight) DrawRef(
        DataGridViewCellPaintingEventArgs e,
        IGitRef gitRef,
        IGitRef? superprojectRef,
        CellStyle style,
        Rectangle messageBounds,
        ref int offset,
        bool highlight,
        string name,
        RefLabelShape shape)
    {
        if (gitRef.IsBisect)
        {
            if (gitRef.IsBisectGood)
            {
                DrawImage(e, _bisectGoodImage, messageBounds, ref offset);
                return (Rectangle.Empty, DrawHighlight: null);
            }

            if (gitRef.IsBisectBad)
            {
                DrawImage(e, _bisectBadImage, messageBounds, ref offset);
                return (Rectangle.Empty, DrawHighlight: null);
            }
        }

        if (!style.RemoteColors.TryGetValue(gitRef.Remote, out var headColor))
        {
            headColor = RevisionGridRefRenderer.GetHeadColor(gitRef);
        }

        var icon = gitRef.IsSelected
            ? RefLabelIcon.Head
            : gitRef.IsSelectedHeadMergeSource
                ? RefLabelIcon.HeadMergeSource
                : gitRef.IsTag
                    ? RefLabelIcon.Tag
                    : gitRef.IsRemote
                        ? RefLabelIcon.Remote
                        : RefLabelIcon.LocalBranch;

        var font = gitRef.IsSelected
            ? style.BoldFont
            : style.NormalFont;

        if (gitRef.IsTag &&
            gitRef.IsDereference && // see note on using IsDereference in CommitInfo class
            _settings.ShowAnnotatedTagsMessages)
        {
            name += " [...]";
        }

        return RevisionGridRefRenderer.DrawRefEx(
            e.State.HasFlag(DataGridViewElementStates.Selected),
            font,
            ref offset,
            name,
            headColor,
            icon,
            messageBounds,
            e.Graphics!,
            superprojectRef is not null,
            _settings.FillRefLabels,
            highlight,
            shape);
    }

    private static void DrawImage(
        DataGridViewCellPaintingEventArgs e,
        Image image,
        Rectangle messageBounds,
        ref int offset)
    {
        var x = e.CellBounds.X + offset;
        if (x + image.Width < messageBounds.Right)
        {
            var y = e.CellBounds.Y + ((e.CellBounds.Height - image.Height) / 2);
            e.Graphics!.DrawImage(image, x, y);
            offset += image.Width + DpiUtil.Scale(4);
        }
    }

    private void DrawCommitMessage(
        DataGridViewCellPaintingEventArgs e,
        GitRevision revision,
        CellStyle style,
        Rectangle messageBounds,
        MultilineIndicator indicator,
        ref int offset)
    {
        var lines = GetCommitMessageLines(revision);
        if (lines.Length == 0)
        {
            return;
        }

        var commitTitle = lines[0];

        // Draw markers for fixup! and squash! commits
        if (commitTitle.StartsWith(CommitKind.Fixup.GetPrefix()) ||
            commitTitle.StartsWith(CommitKind.Squash.GetPrefix()) ||
            commitTitle.StartsWith(CommitKind.Amend.GetPrefix()))
        {
            DrawImage(e, _fixupAndSquashImage, messageBounds, ref offset);
        }

        if (offset > messageBounds.Width)
        {
            return;
        }

        var font = revision.ObjectId == _grid.CurrentCheckout ? style.BoldFont : style.NormalFont;
        var titleBounds = messageBounds.ReduceLeft(offset);
        var titleWidth = _grid.DrawColumnText(e, commitTitle, font, style.ForeColor, titleBounds);
        offset += titleWidth;

        if (offset > messageBounds.Width)
        {
            return;
        }

        if (lines.Length > 1 && _settings.ShowCommitBodyInRevisionGrid)
        {
            var commitBody = string.Concat(lines.Skip(1).Select(_ => " " + _));
            var bodyBounds = messageBounds.ReduceLeft(offset);
            var bodyWidth = _grid.DrawColumnText(e, commitBody, font, style.CommitBodyForeColor, bodyBounds);
            offset += bodyWidth;
        }

        // Draw the multi-line indicator
        indicator.Render();
    }

    private bool FilterRef(IGitRef gitRef)
    {
        if (gitRef.IsTag)
        {
            return _settings.ShowTags;
        }

        if (gitRef.IsRemote)
        {
            return _settings.ShowRemoteBranches;
        }

        return true;
    }

    private static IReadOnlyList<IGitRef> SortRefs(IEnumerable<IGitRef> refs)
    {
        List<IGitRef> sortedRefs = [.. refs];
        sortedRefs.Sort(CompareRefs);
        return sortedRefs;

        static int CompareRefs(IGitRef left, IGitRef right)
        {
            var leftTypeRank = RefTypeRank(left);
            var rightTypeRank = RefTypeRank(right);

            var c = leftTypeRank.CompareTo(rightTypeRank);

            return c == 0
                ? string.Compare(left.Name, right.Name, StringComparison.Ordinal)
                : c;

            static int RefTypeRank(IGitRef gitRef)
            {
                if (gitRef.IsBisect)
                {
                    return 0;
                }

                if (gitRef.IsSelected)
                {
                    return 1;
                }

                if (gitRef.IsSelectedHeadMergeSource)
                {
                    return 2;
                }

                if (gitRef.IsHead)
                {
                    return 3;
                }

                if (gitRef.IsRemote)
                {
                    return 4;
                }

                return 5;
            }
        }
    }

    private string[] GetCommitMessageLines(GitRevision revision)
    {
        return GetBody(revision)?.Split(Delimiters.LineFeed, StringSplitOptions.RemoveEmptyEntries) ??
               [revision.Subject];
    }

    private string? GetBody(GitRevision revision)
    {
        if (revision.Body is null)
        {
            if (_commitDataManager is not null && (_settings.ShowCommitBodyInRevisionGrid || _settings.ShowGitNotes ||
                                                   _settings.NotesInSeparateColumn))
            {
                _commitDataManager.InitiateDelayedLoadingOfDetails(revision);
            }

            return null;
        }

        return _settings.NotesInSeparateColumn
            ? revision.Body
            : UiExtensions.FormatBodyAndNotes(revision.Body, revision.Notes);
    }

    private string GetRemotePrefix(IGitModule module, string remoteName)
    {
        if (!_remotePrefixCache.TryGetValue(remoteName, out var prefix))
        {
            prefix = module.GetEffectiveSetting(string.Format(SettingKeyString.RemotePrefix, remoteName));
            _remotePrefixCache[remoteName] = prefix;
        }

        return prefix;
    }

    /// <summary>
    ///     Performs a hit test to find which ref label (if any) contains the given point in the specified row.
    /// </summary>
    /// <returns>The matching <see cref="RefLabelHitInfo" />, or <see langword="null" /> if no ref label was hit.</returns>
    public RefLabelHitInfo? HitTest(int rowIndex, Point gridClientPoint)
    {
        if (!_refLabelHitInfoByRow.TryGetValue(rowIndex, out var hitInfos))
        {
            return null;
        }

        foreach (var hitInfo in hitInfos)
        {
            if (hitInfo.Contains(gridClientPoint))
            {
                return hitInfo;
            }
        }

        return null;
    }

    /// <summary>
    ///     Sets the ref or stash label to be drawn with a highlight border, triggering a repaint if the highlight changed.
    /// </summary>
    /// <returns><see langword="true" /> if the highlight state changed and a repaint is needed.</returns>
    public bool SetHighlight(int rowIndex, RefLabelHitInfo? hitInfo)
    {
        var gitRef = hitInfo?.GitRef;
        var stashRow = hitInfo?.StashReflogSelector is not null ? rowIndex : -1;

        if (_highlightedRowIndex == rowIndex && Equals(_highlightedRef, gitRef) && _highlightedStashRow == stashRow)
        {
            return false;
        }

        _highlightedRef = gitRef;
        _highlightedRowIndex = hitInfo is not null ? rowIndex : -1;
        _highlightedStashRow = stashRow;
        return true;
    }

    public override void Clear()
    {
        _aheadBehindDataByLocalBranch = null;
        _aheadBehindDataByRemoteBranch = null;

        foreach (var list in _refLabelHitInfoByRow.Values)
        {
            ReturnHitInfoList(list);
        }

        _refLabelHitInfoByRow.Clear();
        _remotePrefixCache.Clear();
        _highlightedRef = null;
        _highlightedRowIndex = -1;
        _highlightedStashRow = -1;
    }

    /// <summary>
    ///     Returns a tuple of the ahead/behind indicator for a local or remote branch ref label
    ///     and the <see cref="IGitRef.CompleteName" /> of the tracked (for a local ref) or tracking (for a remote ref) branch.
    /// </summary>
    /// <remarks>
    ///     Uses <see cref="AheadBehindData.ToDisplay" /> for consistent formatting with the push button and left panel.
    ///     When rendering a local branch's tracked remote as a virtual label, the perspective is inverted: what the local
    ///     branch
    ///     is ahead of the remote appears as the remote being behind, and vice versa — so
    ///     <see cref="AheadBehindData.BehindCount" />
    ///     and <see cref="AheadBehindData.AheadCount" /> are swapped before formatting.
    ///     Returns an empty display string for untracked refs or when the provider is unavailable.
    /// </remarks>
    private (string Display, string TrackedCompleteName, bool IsGone) GetAheadBehind(IGitRef gitRef,
        bool withCounts = true)
    {
        _aheadBehindDataByLocalBranch ??=
            _aheadBehindDataProvider?.GetData() ?? FrozenDictionary<string, AheadBehindData>.Empty;

        if (gitRef.IsRemote)
        {
            // Match the remote ref via AheadBehindData.RemoteRef, which holds the full refs/remotes/… name
            // regardless of whether the remote branch is named differently from the local tracking branch.
            _aheadBehindDataByRemoteBranch ??= _aheadBehindDataByLocalBranch.Values
                .DistinctBy(data => data.RemoteRef)
                .ToFrozenDictionary(data => data.RemoteRef, data => data);

            if (_aheadBehindDataByRemoteBranch.TryGetValue(gitRef.CompleteName, out var aheadBehind))
            {
                return (aheadBehind.ToDisplay(withCounts), GitRefName.RefsHeadsPrefix + aheadBehind.Branch,
                    aheadBehind.AheadCount == AheadBehindData.Gone);
            }
        }
        else
        {
            if (_aheadBehindDataByLocalBranch.TryGetValue(gitRef.Name, out var aheadBehind))
            {
                // This info is displayed in a virtual remote ref label.
                // From the remote ref's perspective, ahead/behind are swapped relative to the local branch.
                return (aheadBehind.ToDisplay(withCounts, true), aheadBehind.RemoteRef,
                    aheadBehind.AheadCount == AheadBehindData.Gone);
            }
        }

        return (string.Empty, string.Empty, false);
    }

    public AheadBehindData? GetAheadBehindData(bool isRemote, string completeName)
    {
        _aheadBehindDataByLocalBranch ??=
            _aheadBehindDataProvider?.GetData() ?? FrozenDictionary<string, AheadBehindData>.Empty;

        if (isRemote)
        {
            _aheadBehindDataByRemoteBranch ??= _aheadBehindDataByLocalBranch.Values
                .DistinctBy(data => data.RemoteRef)
                .ToFrozenDictionary(data => data.RemoteRef, data => data);
            return _aheadBehindDataByRemoteBranch.TryGetValue(completeName, out var dataByRemote)
                ? dataByRemote
                : null;
        }

        return _aheadBehindDataByLocalBranch.TryGetValue(completeName[GitRefName.RefsHeadsPrefix.Length..],
            out var data)
            ? data
            : null;
    }

    private List<RefLabelHitInfo> RentHitInfoList()
    {
        return _hitInfoListPool.TryPop(out var list) ? list : [];
    }

    private void ReturnHitInfoList(List<RefLabelHitInfo> list)
    {
        list.Clear();
        _hitInfoListPool.Push(list);
    }

    private record struct Settings(
        bool FillRefLabels,
        bool NotesInSeparateColumn,
        bool ShowAnnotatedTagsMessages,
        bool ShowCommitBodyInRevisionGrid,
        bool ShowGitNotes,
        bool ShowGitStatusForArtificialCommits,
        bool ShowRemoteBranches,
        bool ShowRevisionGridTooltips,
        bool ShowTags);
}
