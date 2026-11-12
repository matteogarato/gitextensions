namespace GitExtensions.Shims.WinForms;

/// <summary>
///     Stand-in for <c>System.Windows.Forms.MessageBoxButtons</c>; values match WinForms.
/// </summary>
public enum MessageBoxButtons
{
    Ok = 0,
    OkCancel = 1,
    AbortRetryIgnore = 2,
    YesNoCancel = 3,
    YesNo = 4,
    RetryCancel = 5,
    CancelTryContinue = 6
}
