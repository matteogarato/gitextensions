using GitCommands.Settings;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Translations;
using ResourceManager;

namespace GitUI.CommandsDialogs.GitIgnoreDialog;

public class GitLocalExcludeModel : Translate, IGitIgnoreDialogModel
{
    private readonly TranslationString _cannotAccessLocalExclude =
        new("Failed to save .git/info/exclude." + Environment.NewLine +
            "Check if file is accessible.");

    private readonly TranslationString _cannotAccessLocalExcludeCaption =
        new("Failed to save .git/info/exclude");

    private readonly TranslationString _editLocalExcludeTitle =
        new("Edit .git/info/exclude");

    private readonly TranslationString _localExcludeOnlyInWorkingDirSupported =
        new(".git/info/exclude is only supported when there is a working directory.");

    private readonly IGitModule _module;

    private readonly TranslationString _saveFileQuestion =
        new("Save changes to .git/info/exclude?");

    public GitLocalExcludeModel(IGitModule module)
    {
        _module = module;

        Translator.Translate(this, AppSettings.CurrentTranslation);
    }

    string IGitIgnoreDialogModel.FormCaption
    {
        get => _editLocalExcludeTitle.Text;
    }

    string IGitIgnoreDialogModel.ExcludeFile
    {
        get => Path.Join(_module.ResolveGitInternalPath("info"), "exclude");
    }

    string IGitIgnoreDialogModel.FileOnlyInWorkingDirSupported
    {
        get => _localExcludeOnlyInWorkingDirSupported.Text;
    }

    string IGitIgnoreDialogModel.CannotAccessFile
    {
        get => _cannotAccessLocalExclude.Text;
    }

    string IGitIgnoreDialogModel.CannotAccessFileCaption
    {
        get => _cannotAccessLocalExcludeCaption.Text;
    }

    string IGitIgnoreDialogModel.SaveFileQuestion
    {
        get => _saveFileQuestion.Text;
    }
}
