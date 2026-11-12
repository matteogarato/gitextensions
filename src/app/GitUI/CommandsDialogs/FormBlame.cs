using GitExtensions.Extensibility.Git;
using GitUI.Plugin;
using GitRevision = GitExtensions.Extensibility.Git.GitRevision;

namespace GitUI.CommandsDialogs;

public partial class FormBlame : GitModuleForm
{
    private FormBlame(IGitUiCommands commands) : base(commands)
    {
        InitializeComponent();
        InitializeComplete();
    }

    public FormBlame(IGitUiCommands commands, string fileName, GitRevision? revision, int? initialLine = null)
        : this(commands)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return;
        }

        FileName = fileName;

        _ = blameControl1.LoadBlameAsync(revision ?? Module.GetRevision(), null, fileName, null, null, null,
            Module.FilesEncoding, initialLine);
        blameControl1.ConfigureRepositoryHostPlugin(PluginRegistry.TryGetGitHosterForModule(Module));
    }

    public string? FileName { get; }

    private void FormBlameLoad(object sender, EventArgs e)
    {
        Text = $"Blame ({FileName})";
    }
}
