using GitCommands.Settings;
using GitCommands.Utils;

namespace GitCommands;

public sealed class CommitTemplateItem(string name, string text, Image? icon, bool isRegex)
{
    public CommitTemplateItem() : this(string.Empty, string.Empty, null, false)
    {
    }

    public string Name { get; set; } = name;
    public string Text { get; set; } = text;
    public Image? Icon { get; set; } = icon;
    public bool IsRegex { get; set; } = isRegex;

    public static void SaveToSettings(CommitTemplateItem[]? items)
    {
        var strVal = SerializeCommitTemplates(items);
        AppSettings.CommitTemplates = strVal;
    }

    public static CommitTemplateItem[]? LoadFromSettings()
    {
        var serializedString = AppSettings.CommitTemplates;
        var templates = DeserializeCommitTemplates(serializedString, out var shouldBeUpdated);
        if (shouldBeUpdated)
        {
            SaveToSettings(templates!);
        }

        return templates;
    }

    private static string SerializeCommitTemplates(CommitTemplateItem[]? items)
    {
        return JsonSerializer.Serialize(items);
    }

    private static CommitTemplateItem[]? DeserializeCommitTemplates(string serializedString, out bool shouldBeUpdated)
    {
        shouldBeUpdated = false;
        if (string.IsNullOrEmpty(serializedString))
        {
            return null;
        }

        CommitTemplateItem[]? commitTemplateItem = null;
        try
        {
            commitTemplateItem = JsonSerializer.Deserialize<CommitTemplateItem[]>(serializedString);
        }
        catch
        {
            // do nothing
        }

        return commitTemplateItem;
    }
}
