using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Extensibility.Translations;

namespace GitExtensions.Extensibility.Plugins;

public abstract class GitPluginBase(bool hasSettings) : IGitPlugin, ITranslate
{
    // required for the TranslationApp to work
    protected GitPluginBase()
        : this(false)
    {
    }

    protected Guid Id { get; set; }

    protected string? Description { get; set; }

    protected string? Name { get; set; }

    protected Image? Icon { get; set; }

    // Store settings to use later
    public SettingsSource Settings
    {
        get
        {
            ArgumentNullException.ThrowIfNull(SettingsContainer);
            return SettingsContainer.GetSettingsSource();
        }
    }

    Guid IGitPlugin.Id
    {
        get => Id;
    }

    string? IGitPlugin.Description
    {
        get => Description;
    }

    string? IGitPlugin.Name
    {
        get => Name;
    }

    Image? IGitPlugin.Icon
    {
        get => Icon;
    }

    bool IGitPlugin.HasSettings { get; } = hasSettings;

    IGitPluginSettingsContainer? IGitPlugin.SettingsContainer { get; set; }

    public virtual IEnumerable<ISetting> GetSettings()
    {
        return [];
    }

    public virtual void Register(IGitUiCommands gitUiCommands)
    {
        ArgumentNullException.ThrowIfNull(SettingsContainer);
        SettingsContainer.SetSettingsSource(gitUiCommands.Module.GetEffectiveSettings());
    }

    public virtual void Unregister(IGitUiCommands gitUiCommands)
    {
        ArgumentNullException.ThrowIfNull(SettingsContainer);
        SettingsContainer.SetSettingsSource(null);
    }

    /// <summary>
    ///     Run the plugin Execute method
    /// </summary>
    /// <param name="args">arguments from the UI</param>
    /// <returns>
    ///     true, if the revision grid need a refresh
    ///     false, otherwise
    /// </returns>
    public abstract bool Execute(GitUiEventArgs args);

    void IDisposable.Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public virtual void AddTranslationItems(ITranslation translation)
    {
        var name = GetType().Name;
        TranslationUtils.AddTranslationItem(name, this, "Description", translation);
        TranslationUtils.AddTranslationItemsFromFields(name, this, translation);
    }

    public virtual void TranslateItems(ITranslation translation)
    {
        var name = GetType().Name;
        TranslationUtils.TranslateProperty(name, this, nameof(Name), translation);
        TranslationUtils.TranslateItemsFromFields(name, this, translation);
    }

    protected void SetNameAndDescription(string name)
    {
        Name = name;
    }

    protected void Translate(string? currentTranslation)
    {
        // Description for old plugin setting processing as key
        Description = Name;
        Translator.Translate(this, currentTranslation);
    }
}
