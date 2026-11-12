using System.ComponentModel;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitUI.HelperDialogs;
using ResourceManager;

namespace GitUI.UserControls;

public partial class BranchComboBox : GitExtensionsControl
{
    private readonly TranslationString _branchCheckoutError =
        new("Branch '{0}' is not selectable, this branch has been removed from the selection.");

    private IReadOnlyList<IGitRef>? _branchesToSelect;

    public BranchComboBox()
    {
        InitializeComponent();
        InitializeComplete();

        branches.DisplayMember = nameof(IGitRef.Name);
    }

    public IReadOnlyList<IGitRef>? BranchesToSelect
    {
        get => _branchesToSelect;
        set
        {
            _branchesToSelect = value;
            LoadBranches();
        }
    }

    /// <summary>
    ///     Occurs whenever the branch selection has changed.
    /// </summary>
    [Browsable(true)]
    [Category("Action")]
    [Description("Occurs whenever the branch selection has changed.")]
    public event EventHandler? SelectedValueChanged;

    private void LoadBranches()
    {
        if (_branchesToSelect is not null)
        {
            branches.Items.AddRange(_branchesToSelect.ToArray());
        }
    }

    public IEnumerable<IGitRef> GetSelectedBranches()
    {
        if (_branchesToSelect is null)
        {
            yield break;
        }

        foreach (var branch in branches.Text.LazySplit(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var gitHead = _branchesToSelect.FirstOrDefault(g => g.Name == branch);
            if (gitHead is null)
            {
                MessageBoxes.Show(string.Format(_branchCheckoutError.Text, branch), TranslatedStrings.Error,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                yield return gitHead;
            }
        }
    }

    public string GetSelectedText()
    {
        return branches.Text;
    }

    public void SetSelectedText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        branches.Text = text;
    }

    private void selectMultipleBranchesButton_Click(object sender, EventArgs e)
    {
        Validates.NotNull(_branchesToSelect);

        using FormSelectMultipleBranches formSelectMultipleBranches = new(_branchesToSelect);
        foreach (var branch in GetSelectedBranches())
        {
            formSelectMultipleBranches.SelectBranch(branch.Name);
        }

        formSelectMultipleBranches.ShowDialog(this);
        var branchesText = string.Empty;
        foreach (var branch in formSelectMultipleBranches.GetSelectedBranches().Cast<GitRef>())
        {
            if (!string.IsNullOrEmpty(branchesText))
            {
                branchesText += " ";
            }

            branchesText += branch.Name;
        }

        branches.Text = branchesText;

        OnSelectedValueChanged();
    }

    private void branches_SelectedValueChanged(object sender, EventArgs e)
    {
        OnSelectedValueChanged();
    }

    private void OnSelectedValueChanged()
    {
        SelectedValueChanged?.Invoke(this, EventArgs.Empty);
    }
}
