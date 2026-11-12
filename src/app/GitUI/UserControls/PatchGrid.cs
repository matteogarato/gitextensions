using System.ComponentModel;
using System.Diagnostics;
using System.Net.Mail;
using System.Text.RegularExpressions;
using GitCommands;
using GitCommands.Utils;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils.GitUI;
using GitExtUtils.GitUI.Theming;
using ResourceManager;

namespace GitUI.UserControls;

public partial class PatchGrid : GitModuleControl
{
    private readonly ICommitDataManager _commitDataManager;
    private readonly TranslationString _unableToShowPatchDetails = new("Unable to show details of patch file.");
    private bool _isManagingRebase;
    private IList<PatchFile> _skipped = [];

    public PatchGrid()
    {
        InitializeComponent();
        InitializeComplete();
        FileName.DataPropertyName = nameof(PatchFile.Name);
        Action.DataPropertyName = nameof(PatchFile.Action);
        CommitHash.DataPropertyName = nameof(PatchFile.ObjectId);
        subjectDataGridViewTextBoxColumn.DataPropertyName = nameof(PatchFile.Subject);
        authorDataGridViewTextBoxColumn.DataPropertyName = nameof(PatchFile.Author);
        dateDataGridViewTextBoxColumn.DataPropertyName = nameof(PatchFile.Date);
        Status.DataPropertyName = nameof(PatchFile.Status);

        Status.Width = DpiUtil.Scale(70);
        FileName.Width = DpiUtil.Scale(50);
        CommitHash.Width = DpiUtil.Scale(55);
        authorDataGridViewTextBoxColumn.Width = DpiUtil.Scale(140);
        Patches.RowTemplate.MinimumHeight = Patches.ColumnHeadersHeight;
        CommitHash.DefaultCellStyle.WrapMode = DataGridViewTriState.True;

        _commitDataManager = new CommitDataManager(() => Module);
    }

    [GeneratedRegex(@"^(?<header_key>[-A-Za-z0-9]+)(?::[ \t]*)(?<header_value>.*)$", RegexOptions.ExplicitCapture)]
    private static partial Regex HeadersRegex { get; }

    [GeneratedRegex(@"=\?(?<qr1>[\w-]+)\?q\?(<qr2>.*)\?=$", RegexOptions.ExplicitCapture)]
    private static partial Regex QuotedRegex { get; }

    [Category("Behavior")]
    [Description("Should it be used to display commit to rebase (otherwise patches).")]
    [DefaultValue(true)]
    public bool IsManagingRebase
    {
        get => _isManagingRebase;
        set
        {
            _isManagingRebase = value;
            UpdateState(value);
        }
    }

    public IReadOnlyList<PatchFile>? PatchFiles { get; private set; }

    private void DisplayPatches(IReadOnlyList<PatchFile> patchFiles)
    {
        PatchFiles = patchFiles;
        SortablePatchFilesList patchFilesList = new();
        patchFilesList.AddRange(patchFiles);
        Patches.DataSource = patchFilesList;

        if (patchFiles.Any())
        {
            var rowsInView = Patches.DisplayedRowCount(false);
            var currentPatchFileIndex = patchFiles.TakeWhile(pf => !pf.IsNext).Count() - 1;
            Patches.FirstDisplayedScrollingRowIndex = Math.Max(0, currentPatchFileIndex - (rowsInView / 2));
        }

        SelectCurrentlyApplyingPatch();
    }

    private IReadOnlyList<PatchFile> GetInteractiveRebasePatchFiles()
    {
        var rebaseDir = Module.GetRebaseDir();
        var currentFilePath = $"{rebaseDir}stopped-sha";
        var doneFilePath = $"{rebaseDir}done";
        var rebaseTodoFilePath = $"{rebaseDir}git-rebase-todo";

        var doneCommits = ReadCommitsDataFromRebaseFile(doneFilePath);
        var todoCommits = ReadCommitsDataFromRebaseFile(rebaseTodoFilePath);
        var commentChar = Module.GetEffectiveSetting("core.commentchar", "#");

        // Filter comment lines and keep only lines containing at least 3 columns
        // (action, commit hash and commit subject -- that could contain spaces and be cut in more --)
        // ex: pick e0d861716540aa1ac83eaa2790ba5e79988b9489 this is the commit subject
        string[][] commitsInfos =
        [
            .. doneCommits.Concat(todoCommits).Where(l => !l.StartsWith(commentChar))
                .Select(l => l.Split(Delimiters.Space))
                .Where(p => p.Length >= 3)
        ];

        List<PatchFile> patchFiles = [];
        if (commitsInfos.Length == 0)
        {
            return patchFiles;
        }

        RevisionReader reader = new(Module, true);
        Dictionary<string, GitRevision> rebasedCommitsRevisions;
        try
        {
            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
            rebasedCommitsRevisions = reader.GetRevisionsFromRange(commitsInfos[0][1], commitsInfos[^1][1], cts.Token)
                .ToDictionary(r => r.Guid, r => r);
        }
        catch (OperationCanceledException)
        {
            // If retrieve of commit range failed, fall back on getting data commit by commit
            rebasedCommitsRevisions = [];
        }

        var currentCommitShortHash = File.Exists(currentFilePath) ? File.ReadAllText(currentFilePath).Trim() : null;
        var isCurrentFound = false;
        foreach (var parts in commitsInfos)
        {
            var commitHash = parts[1];
            var data = rebasedCommitsRevisions.TryGetValue(commitHash, out var commitRevision)
                ? _commitDataManager.CreateFromRevision(commitRevision, null)
                : _commitDataManager.GetCommitData(commitHash);

            var isApplying = currentCommitShortHash is not null && commitHash.StartsWith(currentCommitShortHash);
            isCurrentFound |= isApplying;

            ObjectId objectId;
            if (data is not null)
            {
                objectId = data.ObjectId;
            }
            else
            {
                if (!ObjectId.TryParse(parts[1], out objectId))
                {
                    Trace.Write(
                        $"PatchGrid: GetInteractiveRebasePatchFiles: Unable to parse commit hash '{parts[1]}' from '{todoCommits}'. Skipping this entry.");
                    continue;
                }
            }

            patchFiles.Add(new PatchFile
            {
                Action = parts[0],
                ObjectId = objectId,

                // During a rebase, "Patch" subject is filled with commit **body** to display it
                // packed in the grid and more readable in the cell tooltip
                Subject = data?.Body ?? string.Join(' ', parts.Skip(2)),
                Author = data?.Author,
                Date = data?.CommitDate.LocalDateTime.ToString(),
                IsNext = isApplying,
                IsApplied = !isCurrentFound
            });
        }

        return patchFiles;

        static string[] ReadCommitsDataFromRebaseFile(string filePath)
        {
            return File.Exists(filePath)
                ? File.ReadAllText(filePath).Trim().Split(Delimiters.LineFeedAndCarriageReturn,
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                : [];
        }
    }

    private IReadOnlyList<PatchFile> GetPatches()
    {
        var rebaseTodoFilePath = $"{Module.GetRebaseDir()}git-rebase-todo";
        var patches = File.Exists(rebaseTodoFilePath)
            ? GetInteractiveRebasePatchFiles()
            : GetRebasePatchFiles();

        if (!_skipped.Any())
        {
            return patches;
        }

        // Select commits with `ObjectId` and patches with `Name`
        var skippedPatches = patches
            .TakeWhile(p => !p.IsNext)
            .Where(p => _skipped.Any(s => p.ObjectId == s.ObjectId && p.Name == s.Name));
        foreach (var patchFile in skippedPatches)
        {
            patchFile.IsSkipped = true;
        }

        return patches;
    }

    private string GetNextRebasePatch()
    {
        var file = $"{Module.GetRebaseDir()}next";
        return File.Exists(file) ? File.ReadAllText(file).Trim() : "";
    }

    private IReadOnlyList<PatchFile> GetRebasePatchFiles()
    {
        List<PatchFile> patchFiles = [];

        var nextFile = GetNextRebasePatch();

        if (!int.TryParse(nextFile, out var next))
        {
            Trace.Write(
                $"PatchGrid: GetRebasePatchFiles: Unable to parse rebase next patch file name '{nextFile}'. Skipping this file.");
            next = 0;
        }

        var rebaseDir = Module.GetRebaseDir();

        var files = Directory.Exists(rebaseDir)
            ? Directory.GetFiles(rebaseDir)
            : [];

        foreach (var fullFileName in files)
        {
            var file = PathUtil.GetFileName(fullFileName);
            if (!int.TryParse(file, out var n))
            {
                continue;
            }

            PatchFile patchFile =
                new() { Name = file, FullName = fullFileName, IsApplied = n < next, IsNext = n == next };

            if (File.Exists(rebaseDir + file))
            {
                string? key = null;
                var value = "";
                foreach (var line in File.ReadLines(rebaseDir + file))
                {
                    var m = HeadersRegex.Match(line);
                    if (key is null)
                    {
                        if (!string.IsNullOrWhiteSpace(line) && !m.Success)
                        {
                            continue;
                        }
                    }
                    else if (string.IsNullOrWhiteSpace(line) || m.Success)
                    {
                        // decode QuotedPrintable text using .NET internal decoder
                        value = Attachment.CreateAttachmentFromString("", value).Name!;
                        switch (key)
                        {
                            case "From":
                                if (value.IndexOf('<') > 0 && value.IndexOf('<') < value.Length)
                                {
                                    var author = Rfc2047Decoder.Parse(value);
                                    patchFile.Author = author[..author.IndexOf('<')].Trim();
                                }
                                else
                                {
                                    patchFile.Author = value;
                                }

                                break;
                            case "Date":
                                if (value.IndexOf('+') > 0 && value.IndexOf('<') < value.Length)
                                {
                                    patchFile.Date = value[..value.IndexOf('+')].Trim();
                                }
                                else
                                {
                                    patchFile.Date = value;
                                }

                                break;
                            case "Subject":
                                patchFile.Subject = value;
                                break;
                        }
                    }

                    if (m.Success)
                    {
                        key = m.Groups["header_key"].Value;
                        value = m.Groups["header_value"].Value;
                    }
                    else if (!string.IsNullOrEmpty(line))
                    {
                        value = AppendQuotedString(value, line.Trim())!;
                    }

                    if (string.IsNullOrEmpty(line) ||
                        (!string.IsNullOrEmpty(patchFile.Author) &&
                         !string.IsNullOrEmpty(patchFile.Date) &&
                         !string.IsNullOrEmpty(patchFile.Subject)))
                    {
                        break;
                    }
                }
            }

            patchFiles.Add(patchFile);
        }

        return patchFiles;

        static string AppendQuotedString(string str1, string str2)
        {
            var m1 = QuotedRegex.Match(str1);
            var m2 = QuotedRegex.Match(str2);
            if (!m1.Success || !m2.Success)
            {
                return str1 + str2;
            }

            DebugHelpers.Assert(m1.Groups["qr1"].Value == m2.Groups["qr1"].Value,
                @"m1.Groups[""qr1""].Value == m2.Groups[""qr2""].Value");
            return $"{str1.AsSpan()[..^2]}{m2.Groups["qr2"].ValueSpan}?=";
        }
    }

    public void Initialize()
    {
        if (DesignMode)
        {
            return;
        }

        UpdateState(IsManagingRebase);
        DisplayPatches(GetPatches());
    }

    protected override void OnRuntimeLoad()
    {
        Initialize();
    }

    public void RefreshGrid()
    {
        Validates.NotNull(PatchFiles);

        var updatedPatches = GetPatches();
        if (updatedPatches.Count != PatchFiles.Count && !_isManagingRebase)
        {
            // Fail for popup in Debug
            var s =
                $"PatchGrid: RefreshGrid: PatchFiles count {PatchFiles.Count} is different from updatedPatches count {updatedPatches.Count}. This should not happen.";
            Trace.Write(s);
            DebugHelpers.Fail(s);
        }

        for (var i = 0; i < Math.Min(updatedPatches.Count, PatchFiles.Count); i++)
        {
            updatedPatches[i].IsSkipped = PatchFiles[i].IsSkipped;
        }

        DisplayPatches(updatedPatches);
    }

    public void SelectCurrentlyApplyingPatch()
    {
        if (PatchFiles?.Count is not > 0)
        {
            return;
        }

        var shouldSelectIndex = PatchFiles.IndexOf(p => p.IsNext);

        if (shouldSelectIndex >= 0)
        {
            Patches.ClearSelection();
            var dataGridViewRow = Patches.Rows[shouldSelectIndex];
            dataGridViewRow.DefaultCellStyle.ForeColor =
                Color.OrangeRed.AdaptForeColor(dataGridViewRow.InheritedStyle.BackColor);
            dataGridViewRow.Selected = true;
        }
    }

    public void SetSkipped(IList<PatchFile> skipped)
    {
        ArgumentNullException.ThrowIfNull(skipped);
        _skipped = skipped;
    }

    private void UpdateState(bool isManagingRebase)
    {
        Action.Visible = isManagingRebase;
        FileName.Visible = !isManagingRebase;
        CommitHash.Visible = isManagingRebase;
        dateDataGridViewTextBoxColumn.Width = isManagingRebase ? DpiUtil.Scale(110) : DpiUtil.Scale(160);
    }

    private void Patches_DoubleClick(object sender, EventArgs e)
    {
        if (Patches.SelectedRows.Count != 1)
        {
            return;
        }

        var patchFile = (PatchFile?)Patches.SelectedRows[0].DataBoundItem;

        if (patchFile?.ObjectId is { IsZeroOrArtificial: false } patchObjectId)
        {
            // Normal commit selected
            UiCommands.StartFormCommitDiff(patchObjectId);
            return;
        }

        if (string.IsNullOrEmpty(patchFile!.FullName))
        {
            MessageBoxes.Show(_unableToShowPatchDetails.Text, TranslatedStrings.Error, MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        UiCommands.StartViewPatchDialog(patchFile.FullName);
    }

    internal TestAccessor GetTestAccessor()
    {
        return new TestAccessor(this);
    }

    internal readonly struct TestAccessor(PatchGrid control)
    {
        public IReadOnlyList<PatchFile> GetInteractiveRebasePatchFiles()
        {
            return control.GetInteractiveRebasePatchFiles();
        }
    }

    private sealed class SortablePatchFilesList : SortableBindingList<PatchFile>
    {
        static SortablePatchFilesList()
        {
            AddSortableProperty(patchFile => patchFile.Status,
                (x, y) => string.Compare(x.Status, y.Status, StringComparison.CurrentCulture));
            AddSortableProperty(patchFile => patchFile.Name,
                (x, y) => string.Compare(x.Name, y.Name, StringComparison.CurrentCulture));
            AddSortableProperty(patchFile => patchFile.Subject,
                (x, y) => string.Compare(x.Subject, y.Subject, StringComparison.CurrentCulture));
            AddSortableProperty(patchFile => patchFile.Author,
                (x, y) => string.Compare(x.Author, y.Author, StringComparison.CurrentCulture));
            AddSortableProperty(patchFile => patchFile.Date,
                (x, y) => string.Compare(x.Date, y.Date, StringComparison.CurrentCulture));
        }
    }
}
