using GitCommands;
using GitCommands.Settings;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Translations;
using ResourceManager;

namespace GitUI.CommandsDialogs.GitIgnoreDialog;

public class GitIgnoreModel : Translate, IGitIgnoreDialogModel
{
    private readonly TranslationString _cannotAccessGitignore =
        new("Failed to save .gitignore." + Environment.NewLine + "Check if file is accessible.");

    private readonly TranslationString _cannotAccessGitignoreCaption =
        new("Failed to save .gitignore");

    private readonly TranslationString _editGitignoreTitle =
        new("Edit .gitignore");

    private readonly IFullPathResolver _fullPathResolver;

    private readonly TranslationString _gitignoreOnlyInWorkingDirSupported =
        new(".gitignore is only supported when there is a working directory.");

    private readonly TranslationString _saveFileQuestion =
        new("Save changes to .gitignore?");

    public GitIgnoreModel(IGitModule module)
    {
        Translator.Translate(this, AppSettings.CurrentTranslation);
        _fullPathResolver = new FullPathResolver(() => module.WorkingDir);
    }

    string IGitIgnoreDialogModel.FormCaption
    {
        get => _editGitignoreTitle.Text;
    }

    string? IGitIgnoreDialogModel.ExcludeFile
    {
        get => _fullPathResolver.Resolve(".gitignore");
    }

    string IGitIgnoreDialogModel.FileOnlyInWorkingDirSupported
    {
        get => _gitignoreOnlyInWorkingDirSupported.Text;
    }

    string IGitIgnoreDialogModel.CannotAccessFile
    {
        get => _cannotAccessGitignore.Text;
    }

    string IGitIgnoreDialogModel.CannotAccessFileCaption
    {
        get => _cannotAccessGitignoreCaption.Text;
    }

    string IGitIgnoreDialogModel.SaveFileQuestion
    {
        get => _saveFileQuestion.Text;
    }
}
