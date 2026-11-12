namespace GitExtensions.Extensibility.Git;

/// <summary>
///     A configuration key/value pair for use in git command lines.
/// </summary>
public readonly struct GitConfigItem(string key, string value)
{
    public string Key { get; } = key;
    public string Value { get; } = value;

    public void Deconstruct(out string key, out string value)
    {
        key = Key;
        value = Value;
    }
}
