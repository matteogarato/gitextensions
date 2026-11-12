using GitExtensions.Extensibility.Git;
using GitExtUtils.GitUI;

namespace GitUI.HelperDialogs;

public partial class FormEdit : GitModuleForm
{
    public FormEdit(IGitUiCommands commands, string text, string filename = "")
        : base(commands)
    {
        InitializeComponent();
        InitializeComplete();
        Viewer.InvokeAndForget(() => Viewer.ViewTextAsync(filename, text));
        Viewer.IsReadOnly = false;
    }

    public bool IsReadOnly
    {
        get => Viewer.IsReadOnly;
        set => Viewer.IsReadOnly = value;
    }
}
