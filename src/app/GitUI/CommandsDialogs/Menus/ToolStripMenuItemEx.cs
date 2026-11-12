using GitCommands.Settings;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Translations;
using ResourceManager.Hotkey;

namespace GitUI.CommandsDialogs.Menus;

internal abstract class ToolStripMenuItemEx : ToolStripMenuItem, ITranslate
{
    private Func<IGitUiCommands>? _getUiCommands;

    /// <summary>
    ///     Gets the current instance of the UI commands.
    /// </summary>
    protected IGitUiCommands UiCommands
    {
        get => (_getUiCommands ?? throw new InvalidOperationException("The button is not initialized"))();
    }

    /// <summary>
    ///     Gets the current instance of the git module.
    /// </summary>
    protected IGitModule Module
    {
        get => UiCommands.Module;
    }

    /// <summary>
    ///     Gets the form that is displaying the menu item.
    /// </summary>
    protected static Form? OwnerForm
    {
        get => Form.ActiveForm ?? (Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null);
    }

    void ITranslate.AddTranslationItems(ITranslation translation)
    {
        TranslationUtils.AddTranslationItemsFromFields("FormBrowse", this, translation);
    }

    void ITranslate.TranslateItems(ITranslation translation)
    {
        TranslationUtils.TranslateItemsFromFields("FormBrowse", this, translation);
    }

    /// <summary>
    ///     Initializes the menu item.
    /// </summary>
    /// <param name="getUiCommands">The method that returns the current instance of UI commands.</param>
    public void Initialize(Func<IGitUiCommands> getUiCommands)
    {
        Translator.Translate(this, AppSettings.CurrentTranslation);

        _getUiCommands = getUiCommands;

        OnInitialized();
    }

    /// <summary>
    ///     Allows the menu item to perform any initialization logic.
    /// </summary>
    protected virtual void OnInitialized()
    {
    }

    /// <summary>
    ///     Allows reloading/reassigning the configured shortcut key.
    /// </summary>
    //// <param name="hotkeys"></param>
    public virtual void RefreshShortcutKeys(IEnumerable<HotkeyCommand>? hotkeys)
    {
    }

    /// <summary>
    ///     Allows refreshing the state of the menu item depending on the state of the loaded git repository.
    /// </summary>
    /// <param name="bareRepository">
    ///     <see lang="true" /> if the current git repository is bare; otherwise, <see lang="false" />
    ///     .
    /// </param>
    public virtual void RefreshState(bool bareRepository)
    {
    }
}
