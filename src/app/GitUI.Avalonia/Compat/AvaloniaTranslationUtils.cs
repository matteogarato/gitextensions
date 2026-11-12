using System.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using GitExtensions.Extensibility.Translations;

namespace GitUI.Compat;

internal static class AvaloniaTranslationUtils
{
    public static void AddTranslationItemsFromFields(string category, object host, ITranslation translation)
    {
        foreach (var (name, item) in TranslationUtils.GetObjFields(host, "$this"))
        {
            var text = GetText(item);
            if (text?.Any(char.IsLetter) is true)
            {
                translation.AddTranslationItem(category, name, "Text", ToWinFormsMnemonics(text));
            }
        }
    }

    public static void TranslateItemsFromFields(string category, object host, ITranslation translation)
    {
        foreach ((var name, var item) in TranslationUtils.GetObjFields(host, "$this"))
        {
            var text = GetText(item);
            if (text is null)
            {
                continue;
            }

            var translatedText = translation.TranslateItem(category, name, "Text", () => text);
            if (!string.IsNullOrEmpty(translatedText))
            {
                SetText(item, ToAvaloniaMnemonics(translatedText));
            }
        }
    }

    public static void NormalizeMnemonicsFromFields(object host)
    {
        foreach ((var _, var item) in TranslationUtils.GetObjFields(host, "$this"))
        {
            if (GetText(item) is { } text)
            {
                SetText(item, ToAvaloniaMnemonics(text));
            }
        }
    }

    public static void NormalizeMenuMnemonics(ItemsControl menu)
    {
        var items = menu.ItemsSource ?? menu.Items;
        foreach (var item in items)
        {
            if (item is MenuItem menuItem)
            {
                NormalizeMenuItem(menuItem);
            }
        }

        return;

        static void NormalizeMenuItem(MenuItem menuItem)
        {
            if (menuItem.Header is string header)
            {
                menuItem.Header = ToAvaloniaMnemonics(header);
            }

            var items = menuItem.ItemsSource ?? menuItem.Items;
            foreach (var item in items)
            {
                if (item is MenuItem childMenuItem)
                {
                    NormalizeMenuItem(childMenuItem);
                }
            }
        }
    }

    private static string? GetText(object item)
    {
        return item switch
        {
            MenuItem { Header: string header } => header,
            HeaderedContentControl { Header: string header } => header,
            HeaderedSelectingItemsControl { Header: string header } => header,
            HeaderedItemsControl { Header: string header } => header,
            ContentControl { Content: string content } => content,
            _ => null
        };
    }

    private static void SetText(object item, string text)
    {
        switch (item)
        {
            case MenuItem menuItem:
                menuItem.Header = text;
                break;
            case HeaderedContentControl headeredContentControl:
                headeredContentControl.Header = text;
                break;
            case HeaderedSelectingItemsControl headeredSelectingItemsControl:
                headeredSelectingItemsControl.Header = text;
                break;
            case HeaderedItemsControl headeredItemsControl:
                headeredItemsControl.Header = text;
                break;
            case ContentControl contentControl:
                contentControl.Content = text;
                break;
        }
    }

    public static string ToAvaloniaMnemonics(string text)
    {
        const string escapedAmpersand = "\u0001";
        return text
            .Replace("&&", escapedAmpersand, StringComparison.Ordinal)
            .Replace('&', '_')
            .Replace(escapedAmpersand, "&", StringComparison.Ordinal);
    }

    private static string ToWinFormsMnemonics(string text)
    {
        const string escapedUnderscore = "\u0001";
        return text
            .Replace("__", escapedUnderscore, StringComparison.Ordinal)
            .Replace('_', '&')
            .Replace(escapedUnderscore, "_", StringComparison.Ordinal);
    }
}
