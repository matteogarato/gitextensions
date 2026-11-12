using GitCommands.Settings;
using GitExtensions.Extensibility.Translations;

namespace GitUI.CommandsDialogs.BrowseDialog;

internal abstract class MenuCommandsBase : ITranslate
{
    // for translation category
    protected abstract string TranslationCategoryName { get; }

    void IDisposable.Dispose()
    {
    }

    public virtual void AddTranslationItems(ITranslation translation)
    {
        TranslationUtils.AddTranslationItemsFromFields(TranslationCategoryName, this, translation);
        TranslationUtils.AddTranslationItemsFromList(TranslationCategoryName, translation,
            GetMenuCommandsForTranslationImpl());
    }

    public virtual void TranslateItems(ITranslation translation)
    {
        TranslationUtils.TranslateItemsFromFields(TranslationCategoryName, this, translation);
        TranslationUtils.TranslateItemsFromList(TranslationCategoryName, translation,
            GetMenuCommandsForTranslationImpl());
    }

    public void Translate()
    {
        Translator.Translate(this, AppSettings.CurrentTranslation);
    }

    // override and return all commands created by extending class
    protected abstract IEnumerable<MenuCommand> GetMenuCommandsForTranslation();

    private IEnumerable<(string name, object item)> GetMenuCommandsForTranslationImpl()
    {
        foreach (var menuCommand in GetMenuCommandsForTranslation())
        {
            Validates.NotNull(menuCommand.Name);
            yield return (menuCommand.Name, menuCommand);
        }
    }
}
