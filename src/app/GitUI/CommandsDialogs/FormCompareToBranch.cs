using GitExtensions.Extensibility.Git;

namespace GitUI.CommandsDialogs;

public partial class FormCompareToBranch : GitModuleForm
{
    public FormCompareToBranch(IGitUiCommands commands, ObjectId selectedCommit)
        : base(commands)
    {
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        InitializeComponent();
        InitializeComplete();

        branchSelector.Initialize(true, null);
        branchSelector.CommitToCompare = selectedCommit;
        Activated += OnActivated;
    }

    public string? BranchName { get; private set; }

    private void OnActivated(object? sender, EventArgs eventArgs)
    {
        branchSelector.Focus();
    }

    private void btnCompare_Click(object sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(branchSelector.SelectedBranchName))
        {
            BranchName = branchSelector.SelectedBranchName;
            DialogResult = DialogResult.OK;
            Close();
        }

        branchSelector.Focus();
    }
}
