using System.ComponentModel;
using System.Diagnostics;
using GitExtensions.Extensibility.Translations.Xliff;

namespace TranslationApp;

[DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
public class TranslationItemWithCategory(string? category, TranslationItem item) : INotifyPropertyChanged, ICloneable
{
    public TranslationItemWithCategory() : this(null, new TranslationItem())
    {
    }

    public string? Category { get; set; } = category;

    public string? Name
    {
        get => item.Name;
        set => item.Name = value;
    }

    public string? Property
    {
        get => item.Property;
        set => item.Property = value;
    }

    public string? NeutralValue
    {
        get => item.Source;
        set => item.Source = value;
    }

    public string? TranslatedValue
    {
        get => item.Value;
        set
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TranslatedValue)));
            item.Value = value;
        }
    }

    private string DebuggerDisplay
    {
        get => string.Format("\"{0}\" - \"{1}\"", Category, NeutralValue);
    }

    object ICloneable.Clone()
    {
        return Clone();
    }

    event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
    {
        add => PropertyChanged += value;
        remove => PropertyChanged -= value;
    }

    public TranslationItem GetTranslationItem()
    {
        return item;
    }

    private event PropertyChangedEventHandler? PropertyChanged;

    public bool IsSourceEqual(string value)
    {
        if (NeutralValue is null)
        {
            return true;
        }

        var equal = value == NeutralValue;
        if (!equal && value.Contains('\n'))
        {
            return value.Replace(Environment.NewLine, "\n") == NeutralValue.Replace(Environment.NewLine, "\n");
        }

        return equal;
    }

    public TranslationItemWithCategory Clone()
    {
        return new TranslationItemWithCategory(Category, item.Clone());
    }
}
