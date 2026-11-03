using System.Text.RegularExpressions;

namespace GitUI.ScriptsEngine;

// Keep the XML property names aligned with the WinForms ScriptInfo model.

/// <summary>
///  Stores a user-defined script using the shared Git Extensions settings XML shape.
/// </summary>
public partial class ScriptInfo
{
    [GeneratedRegex("&(?!&)", RegexOptions.ExplicitCapture)]
    private static partial Regex MnemonicAmpersandRegex { get; }

    /// <summary>
    ///  Gets or sets whether the script is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///  Gets or sets the script's display name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///  Gets or sets the executable or plugin command.
    /// </summary>
    public string? Command { get; set; }

    /// <summary>
    ///  Gets or sets the script arguments.
    /// </summary>
    public string? Arguments { get; set; }

    /// <summary>
    ///  Gets or sets whether the script appears in the revision context menu.
    /// </summary>
    public bool AddToRevisionGridContextMenu { get; set; }

    /// <summary>
    ///  Gets or sets the lifecycle or user-menu event for the script.
    /// </summary>
    public ScriptEvent OnEvent { get; set; }

    /// <summary>
    ///  Gets or sets whether the user must confirm before execution.
    /// </summary>
    public bool AskConfirmation { get; set; }

    /// <summary>
    ///  Gets or sets whether the script runs without a process dialog.
    /// </summary>
    public bool RunInBackground { get; set; }

    /// <summary>
    ///  Gets or sets whether the command is interpreted by PowerShell.
    /// </summary>
    public bool IsPowerShell { get; set; }

    /// <summary>
    ///  Gets or sets the legacy hotkey command identifier.
    /// </summary>
    public int HotkeyCommandIdentifier { get; set; }

    /// <summary>
    ///  Gets or sets the icon resource name.
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    ///  Gets or sets the path to the script icon file.
    /// </summary>
    public string? IconFilePath { get; set; }

    /// <summary>
    ///  Gets the display name with mnemonic markers removed.
    /// </summary>
    public string GetDisplayName() => MnemonicAmpersandRegex.Replace(Name ?? string.Empty, "");
}
