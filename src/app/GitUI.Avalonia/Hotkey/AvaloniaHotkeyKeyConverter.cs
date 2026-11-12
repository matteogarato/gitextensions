using Avalonia.Input;
using GitExtensions.Shims.WinForms;

namespace GitUI.Hotkey;

internal static class AvaloniaHotkeyKeyConverter
{
    public static bool TryCreateKeyData(Key key, KeyModifiers modifiers, out Keys keyData)
    {
        keyData = Keys.None;
        if (modifiers.HasFlag(KeyModifiers.Meta))
        {
            return false;
        }

        var keyName = key switch
        {
            Key.Enter => nameof(Keys.Return),
            Key.PageUp => nameof(Keys.Prior),
            Key.PageDown => nameof(Keys.Next),
            _ => key.ToString()
        };
        if (!Enum.TryParse(keyName, out Keys keyCode))
        {
            return false;
        }

        keyCode &= Keys.KeyCode;
        if (keyCode == Keys.None)
        {
            return false;
        }

        keyData = keyCode;
        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            keyData |= Keys.Control;
        }

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            keyData |= Keys.Shift;
        }

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            keyData |= Keys.Alt;
        }

        return true;
    }
}
