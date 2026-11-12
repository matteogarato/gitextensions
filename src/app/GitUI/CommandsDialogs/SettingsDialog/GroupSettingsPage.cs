using GitCommands.Settings;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Extensibility.Translations;
using ResourceManager;

namespace GitUI.CommandsDialogs.SettingsDialog;

/// <summary>
///     Page to group other pages.
/// </summary>
public abstract class GroupSettingsPage : Translate, ISettingsPage
{
    protected GroupSettingsPage(string title)
    {
        Title = title;
        Translator.Translate(this, AppSettings.CurrentTranslation);
    }

    public string Title { get; }

    string ISettingsPage.GetTitle()
    {
        return Title;
    }

    Control? ISettingsPage.GuiControl
    {
        get => null;
    }

    void ISettingsPage.OnPageShown()
    {
    }

    void ISettingsPage.LoadSettings()
    {
    }

    void ISettingsPage.SaveSettings()
    {
    }

    IEnumerable<string> ISettingsPage.GetSearchKeywords()
    {
        return [];
    }

    bool ISettingsPage.IsInstantSavePage
    {
        get => false;
    }

    SettingsPageReference ISettingsPage.PageReference
    {
        get => new SettingsPageReferenceByType(GetType());
    }
}
