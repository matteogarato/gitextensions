using GitCommands;
using GitCommands.Git;
using GitCommands.Settings;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitExtUtils.GitUI;

namespace GitUI.CommandsDialogs.WorktreeDialog;

public sealed partial class FormCreateWorktree : GitExtensionsDialog
{
    private readonly IGitBranchNameNormaliser _branchNameNormaliser;
    private readonly AsyncLoader _branchesLoader = new();
    private readonly GitBranchNameOptions _gitBranchNameOptions = new(AppSettings.AutoNormaliseSymbol);

    private readonly string? _initialDirectoryPath;
    private readonly char[] _invalidCharsInPath = Path.GetInvalidFileNameChars();

    public FormCreateWorktree(IGitUiCommands commands, string? path)
        : base(commands, false)
    {
        InitializeComponent();

        _branchNameNormaliser = commands.GetRequiredService<IGitBranchNameNormaliser>();

        tlpnlMain.AdjustWidthToSize(0, rbCheckoutExistingBranch, rbCreateNewBranch, lblNewWorktreeFolder);
        tlpnlCheckout.AdjustWidthToSize(0, rbCheckoutExistingBranch, rbCreateNewBranch, lblNewWorktreeFolder);

        MinimumSize = new Size(Width, PreferredMinimumHeight);

        InitializeComplete();
        _initialDirectoryPath = path;
    }

    public string WorktreeDirectory
    {
        get => txtWorktreeDirectory.Text;
    }

    public IReadOnlyList<IGitRef>? ExistingBranches { get; set; }

    private void FormCreateWorktree_Load(object sender, EventArgs e)
    {
        LoadBranchesAsync();

        UpdateWorktreePathAndValidateWorktreeOptions();

        Task LoadBranchesAsync()
        {
            var selectedBranch = UiCommands.Module.GetSelectedBranch();
            ExistingBranches = Module.GetRefs(RefsFilter.Heads);
            cbxBranches.Text = TranslatedStrings.LoadingData;
            ThreadHelper.FileAndForget(async () =>
            {
                await _branchesLoader.LoadAsync(
                    () => ExistingBranches.Where(r => r.Name != selectedBranch).ToList(),
                    list =>
                    {
                        cbxBranches.Text = string.Empty;
                        cbxBranches.DataSource = list;
                        cbxBranches.DisplayMember = nameof(IGitRef.LocalName);
                    });

                await this.SwitchToMainThreadAsync();
                if (cbxBranches.Items.Count == 0)
                {
                    rbCreateNewBranch.Checked = true;
                    rbCheckoutExistingBranch.Enabled = false;
                }
                else
                {
                    rbCheckoutExistingBranch.Checked = true;
                }

                ValidateWorktreeOptions();
            });

            return Task.CompletedTask;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _branchesLoader.Dispose();

            components?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void cbxBranches_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            CreateWorktree();
        }
    }

    private void txtNewBranchName_Leave(object sender, EventArgs e)
    {
        NormaliseNewBranchName();
    }

    private void NormaliseNewBranchName()
    {
        if (!AppSettings.AutoNormaliseBranchName || !txtNewBranchName.Text.Any(PathUtil.IsValidPathChar))
        {
            return;
        }

        var caretPosition = txtNewBranchName.SelectionStart;
        txtNewBranchName.Text = _branchNameNormaliser.Normalise(txtNewBranchName.Text, _gitBranchNameOptions);
        txtNewBranchName.SelectionStart = caretPosition;
    }

    private void btnCreateWorktree_Click(object sender, EventArgs e)
    {
        CreateWorktree();
    }

    private void CreateWorktree()
    {
        // The branch name may not have lost focus yet (e.g. when triggered via the accept button),
        // so normalise it here to avoid passing an invalid name (e.g. containing spaces) to git.
        if (rbCreateNewBranch.Checked)
        {
            NormaliseNewBranchName();
        }

        var relativePath = Path.GetRelativePath(Module.WorkingDir, WorktreeDirectory).ToPosixPath().Quote();
        var newBranchOption =
            rbCreateNewBranch.Checked
                ? $"-b {txtNewBranchName.Text}"
                : (cbxBranches.SelectedItem as GitRef)?.Name;
        DialogResult =
            UiCommands.StartGitCommandProcessDialog(this, CreateWorktreeCommand(Module, relativePath, newBranchOption!))
                ? DialogResult.OK
                : DialogResult.None;
    }

    private GitArgumentBuilder CreateWorktreeCommand(IGitModule module, string relativePath, string newBranchOption)
    {
        // https://git-scm.com/docs/git-worktree

        // Get the default value, set if unset in config.
        // Similar in DiffHighlightService.
        const string command = "worktree";
        GitCommandConfiguration commandConfiguration = new();
        var items = GitCommandConfiguration.Default.Get(command);
        foreach (var cfg in items)
        {
            commandConfiguration.Add(cfg, command);
        }

        SetIfUnsetInGit("worktree.useRelativePaths", "true");
        GitArgumentBuilder args = new(command, commandConfiguration) { "add", relativePath, newBranchOption };

        return args;

        void SetIfUnsetInGit(string key, string value)
        {
            if (string.IsNullOrEmpty(module.GetEffectiveSetting(key)))
            {
                commandConfiguration.Add(new GitConfigItem(key, value), command);
            }
        }
    }

    private void ValidateWorktreeOptions()
    {
        cbxBranches.Enabled = rbCheckoutExistingBranch.Checked;
        txtNewBranchName.Enabled = rbCreateNewBranch.Checked;
        if (rbCheckoutExistingBranch.Checked)
        {
            btnCreateWorktree.Enabled = cbxBranches.SelectedItem is not null;
        }
        else
        {
            btnCreateWorktree.Enabled = !(string.IsNullOrWhiteSpace(txtNewBranchName.Text)
                                          || ExistingBranches!.Any(b => b.Name == txtNewBranchName.Text));
        }

        if (btnCreateWorktree.Enabled)
        {
            btnCreateWorktree.Enabled = IsTargetFolderValid();
        }

        return;

        bool IsTargetFolderValid()
        {
            if (string.IsNullOrWhiteSpace(txtWorktreeDirectory.Text))
            {
                return false;
            }

            try
            {
                DirectoryInfo directoryInfo = new(txtWorktreeDirectory.Text);
                return !directoryInfo.Exists ||
                       (!directoryInfo.EnumerateFiles().Any() && !directoryInfo.EnumerateDirectories().Any());
            }
            catch
            {
                return false;
            }
        }
    }

    private void txtWorktreeDirectory_TextChanged(object sender, EventArgs e)
    {
        ValidateWorktreeOptions();
    }

    private void UpdateWorktreePathAndValidateWorktreeOptions(object sender, EventArgs e)
    {
        UpdateWorktreePathAndValidateWorktreeOptions();
    }

    private void UpdateWorktreePathAndValidateWorktreeOptions()
    {
        UpdateWorktreePath();

        ValidateWorktreeOptions();

        return;

        void UpdateWorktreePath()
        {
            var branchNameNormalized = NormalizeBranchName(rbCheckoutExistingBranch.Checked
                ? ((IGitRef?)cbxBranches.SelectedItem)?.Name ?? string.Empty
                : txtNewBranchName.Text);
            txtWorktreeDirectory.Text = $"{_initialDirectoryPath}_{branchNameNormalized}";
        }

        string NormalizeBranchName(string branchName)
        {
            return string.Join("_", branchName.Split(_invalidCharsInPath, StringSplitOptions.RemoveEmptyEntries))
                .TrimEnd('.');
        }
    }
}
