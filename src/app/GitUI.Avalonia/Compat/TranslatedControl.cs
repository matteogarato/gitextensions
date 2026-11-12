using Avalonia.Controls;
using GitExtensions.Extensibility.Translations;

namespace GitUI.Compat;

// Twin of ResourceManager/TranslatedControl.cs: provides xlf translation for user controls.
// The translation category and item keys are identical to WinForms (type name + field names),
// so the existing language files apply unchanged.
public class TranslatedControl : UserControl, ITranslate
{
    void IDisposable.Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public virtual void AddTranslationItems(ITranslation translation)
    {
        TranslationUtils.AddTranslationItemsFromFields(GetType().Name, this, translation);
        AvaloniaTranslationUtils.AddTranslationItemsFromFields(GetType().Name, this, translation);
    }

    public virtual void TranslateItems(ITranslation translation)
    {
        TranslationUtils.TranslateItemsFromFields(GetType().Name, this, translation);
        AvaloniaTranslationUtils.TranslateItemsFromFields(GetType().Name, this, translation);
    }

    /// <summary>Performs post-initialisation tasks such as translation.</summary>
    /// <remarks>Subclasses must ensure this method is called in their constructor, ideally as the final statement.</remarks>
    protected void InitializeComplete()
    {
        if (Translator.GetTranslation(AppSettings.CurrentTranslation).Count == 0)
        {
            AvaloniaTranslationUtils.NormalizeMnemonicsFromFields(this);
        }

        Translator.Translate(this, AppSettings.CurrentTranslation);
    }
}
