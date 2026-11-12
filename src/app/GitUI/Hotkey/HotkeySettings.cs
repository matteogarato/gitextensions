using System.Xml.Serialization;
using ResourceManager.Hotkey;

namespace GitUI.Hotkey;

/// <summary>
///     Stores all hotkey mappings of one target.
/// </summary>
public class HotkeySettings
{
    public HotkeySettings()
    {
    }

    public HotkeySettings(string name, params HotkeyCommand[] commands)
    {
        Name = name;
        Commands = commands;
    }

    [XmlArray] public HotkeyCommand[]? Commands { get; set; }

    [XmlAttribute] public string? Name { get; set; }

    public override bool Equals(object? obj)
    {
        return obj is HotkeySettings other && Commands.SequenceEqual(other.Commands);
    }

    public override int GetHashCode()
    {
        return base.GetHashCode();
    }
}
