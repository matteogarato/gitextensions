namespace GitUI.AutoCompletion;

public class AutoCompleteWord : IEquatable<AutoCompleteWord?>
{
    private readonly string _camelHumps;

    public AutoCompleteWord(string word)
    {
        Word = word;
        _camelHumps = string.Join("", Word.Where(char.IsUpper));
    }

    public string Word { get; }

    bool IEquatable<AutoCompleteWord?>.Equals(AutoCompleteWord? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return string.Equals(Word, other.Word);
    }

    public bool Matches(string typedWord)
    {
        return Word.StartsWith(typedWord, StringComparison.OrdinalIgnoreCase) ||
               _camelHumps.StartsWith(typedWord, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != GetType())
        {
            return false;
        }

        return Equals((AutoCompleteWord)obj);
    }

    public override int GetHashCode()
    {
        return Word is not null ? Word.GetHashCode() : 0;
    }
}
