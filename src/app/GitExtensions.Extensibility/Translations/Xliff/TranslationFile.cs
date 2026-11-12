using System.Xml.Serialization;

namespace GitExtensions.Extensibility.Translations.Xliff;

/// <summary>Provides a translation for a specific language.</summary>
[XmlRoot("xliff")]
public class TranslationFile() : ITranslation
{
    public TranslationFile(string sourceLanguage, string? targetLanguage)
        : this()
    {
        GitExVersion = "";
        SourceLanguage = sourceLanguage;
        TargetLanguage = targetLanguage;
    }

    [XmlAttribute("version")] public string Version { get; set; } = "1.0";

    [XmlAttribute("GitExVersion")] public string? GitExVersion { get; set; }

    [XmlIgnore] public string? SourceLanguage { get; set; }

    [XmlIgnore] public string? TargetLanguage { get; set; }

    [XmlElement(ElementName = "file")] public List<TranslationCategory> TranslationCategories { get; set; } = [];

    void ITranslation.AddTranslationItem(string category, string item, string property, string neutralValue)
    {
        FindOrAddTranslationCategory(category).Body
            .AddTranslationItemIfNotExist(new TranslationItem(item, property, neutralValue));
    }

    string? ITranslation.TranslateItem(string category, string item, string property, Func<string?> provideDefaultValue)
    {
        var tc = FindOrAddTranslationCategory(category);

        var ti = tc.Body.GetTranslationItem(item, property);

        if (ti is null)
        {
            // if an item is not translated, then store its default value
            // to be able to retrieve it later (eg. when to a caption
            // is added an additional information like 'Commit (<number of changes>)',
            // and then the caption needs to be refreshed)
            var defaultValue = provideDefaultValue();
            tc.Body.AddTranslationItemIfNotExist(new TranslationItem(item, property, defaultValue));
            return defaultValue;
        }

        if (string.IsNullOrEmpty(ti.Value))
        {
            return ti.Source;
        }

        return ti.Value;
    }

    public TranslationCategory FindOrAddTranslationCategory(string? translationCategory)
    {
        var tc = GetTranslationCategory(translationCategory);
        if (tc is null)
        {
            tc = new TranslationCategory(translationCategory, SourceLanguage, TargetLanguage);
            AddTranslationCategory(tc);
        }

        return tc;
    }

    public void AddTranslationCategory(TranslationCategory translationCategory)
    {
        if (string.IsNullOrEmpty(translationCategory.Name))
        {
            throw new InvalidOperationException("Cannot add translationCategory without name");
        }

        TranslationCategories.Add(translationCategory);
    }

    public TranslationCategory? GetTranslationCategory(string? name)
    {
        return TranslationCategories.Find(t =>
            t.Name is not null && name is not null && t.Name.TrimStart('_') == name.TrimStart('_'));
    }

    public void Sort()
    {
        TranslationCategories.Sort();
        foreach (var tc in TranslationCategories)
        {
            tc.Body.TranslationItems.Sort();
        }
    }
}
