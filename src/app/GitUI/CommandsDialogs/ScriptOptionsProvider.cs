using GitUI.ScriptsEngine;
using GitUI.UserControls;

namespace GitUI.CommandsDialogs;

internal sealed class ScriptOptionsProvider(
    Func<IEnumerable<string>> getSelectedRelativePaths,
    Func<int?> getCurrentLineNumber,
    Func<int?> getCurrentColumnNumber)
    : ScriptOptionsProviderBase
{
    private const string _selectedRelativePaths = "SelectedRelativePaths";
    internal const string LineNumber = "LineNumber";
    private const string _columnNumber = "ColumnNumber";

    public ScriptOptionsProvider(FileStatusList fileStatusList, Func<int?> getCurrentLineNumber,
        Func<int?> getCurrentColumn)
        : this(() => fileStatusList.SelectedFolder is { } folder
                ? [folder.Value]
                : fileStatusList.SelectedItems.Select(item => item.Item.Name),
            getCurrentLineNumber, getCurrentColumn)
    {
    }

    private static string[] ImplementedOptions
    {
        get => [_selectedRelativePaths, LineNumber, _columnNumber];
    }

    public override IEnumerable<string> GetValues(string option)
    {
        switch (option)
        {
            case _selectedRelativePaths:
                return getSelectedRelativePaths().Select(item => item.EscapeForCommandLine());
            case LineNumber:
                return getCurrentLineNumber() is { } lineNumber ? [lineNumber.ToString()] : [];
            case _columnNumber:
                return getCurrentColumnNumber() is { } columnNumber ? [columnNumber.ToString()] : [];
            default:
                return base.GetValues(option);
        }
    }
}
