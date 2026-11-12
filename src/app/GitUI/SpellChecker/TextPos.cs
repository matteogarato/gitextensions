namespace GitUI.SpellChecker;

public readonly struct TextPos(int start, int end)
{
    public int End { get; } = end;
    public int Start { get; } = start;
}
