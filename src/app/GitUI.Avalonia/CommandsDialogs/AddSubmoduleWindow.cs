using Avalonia.Controls;
using Avalonia.Layout;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitUI;
using ResourceManager;
using CheckBox = Avalonia.Controls.CheckBox;
using TextBox = Avalonia.Controls.TextBox;

namespace GitUI.CommandsDialogs;

/// <summary>
///  Adds a submodule to the current repository.
/// </summary>
public sealed class AddSubmoduleWindow : GitExtensionsFormBase
{
    private readonly IGitUICommands _uiCommands;
    private readonly TextBox _remotePath = new() { Name = "txtSubmoduleUrl" };
    private readonly TextBox _localPath = new() { Name = "txtSubmoduleLocalPath" };
    private readonly TextBox _branch = new() { Name = "txtSubmoduleBranch" };
    private readonly CheckBox _force = new() { Name = "chkSubmoduleForce" };
    private readonly Button _add = new() { Name = "btnAddSubmodule", IsDefault = true, IsEnabled = false };
    private string _suggestedLocalPath = string.Empty;

    /// <summary>
    ///  Gets the submodule path entered by the user.
    /// </summary>
    public string LocalPath => _localPath.Text ?? string.Empty;

    /// <summary>
    ///  Initializes a new instance of the <see cref="AddSubmoduleWindow"/> class.
    /// </summary>
    /// <param name="uiCommands">The commands used to run Git with process feedback.</param>
    public AddSubmoduleWindow(IGitUICommands uiCommands)
    {
        ArgumentNullException.ThrowIfNull(uiCommands);

        _uiCommands = uiCommands;
        NameScope nameScope = new();
        NameScope.SetNameScope(this, nameScope);
        nameScope.Register("txtSubmoduleUrl", _remotePath);
        nameScope.Register("txtSubmoduleLocalPath", _localPath);
        nameScope.Register("txtSubmoduleBranch", _branch);
        nameScope.Register("chkSubmoduleForce", _force);
        nameScope.Register("btnAddSubmodule", _add);

        Title = GitUI.TranslatedStrings.AddSubmodule;
        Width = 540;
        Height = 350;
        MinWidth = 440;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _remotePath.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                UpdateSuggestedLocalPath();
                UpdateActionState();
            }
        };
        _localPath.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                UpdateActionState();
            }
        };
        _add.Content = GitUI.TranslatedStrings.AddSubmodule;
        _add.Click += (_, _) => AddSubmodule();
        _force.Content = GitUI.TranslatedStrings.SubmoduleForce;
        Button cancel = new() { Content = GitUI.TranslatedStrings.Cancel, IsCancel = true };
        cancel.Click += (_, _) => Close(false);

        StackPanel actions = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Avalonia.Thickness(0, 8, 0, 0),
            Children =
            {
                _add,
                cancel,
            },
        };
        Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(12),
            Spacing = 8,
            Children =
            {
                CreateField(GitUI.TranslatedStrings.SubmoduleUrl, _remotePath),
                CreateField(GitUI.TranslatedStrings.SubmoduleLocalPath, _localPath),
                CreateField(GitUI.TranslatedStrings.SubmoduleBranch, _branch),
                _force,
                actions,
            },
        };
    }

    private static StackPanel CreateField(string label, TextBox input)
        => new()
        {
            Spacing = 4,
            Children =
            {
                new TextBlock { Text = label },
                input,
            },
        };

    private void AddSubmodule()
    {
        string remotePath = _remotePath.Text?.Trim() ?? string.Empty;
        string localPath = _localPath.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(remotePath) || string.IsNullOrWhiteSpace(localPath))
        {
            return;
        }

        ArgumentString command = SubmoduleWorkflow.BuildAdd(
            remotePath,
            localPath,
            _branch.Text,
            _force.IsChecked is true);
        if (_uiCommands.StartGitCommandProcessDialog(this, command))
        {
            Close(true);
        }
    }

    private void UpdateSuggestedLocalPath()
    {
        string suggestedLocalPath = PathUtil.GetRepositoryName(_remotePath.Text ?? string.Empty);
        if (string.IsNullOrEmpty(_localPath.Text) || _localPath.Text == _suggestedLocalPath)
        {
            _suggestedLocalPath = suggestedLocalPath;
            _localPath.Text = suggestedLocalPath;
        }

        UpdateActionState();
    }

    private void UpdateActionState()
    {
        _add.IsEnabled = !string.IsNullOrWhiteSpace(_remotePath.Text)
            && !string.IsNullOrWhiteSpace(_localPath.Text);
    }
}
