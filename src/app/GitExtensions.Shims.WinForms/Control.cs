using System.ComponentModel;

namespace GitExtensions.Shims.WinForms;

/// <summary>
///     Stand-in for <c>System.Windows.Forms.Control</c>: a headless property bag with disposal
///     tracking. The Avalonia UI layer adapts its own controls to this type where shared code
///     requires it; there is no rendering here.
/// </summary>
/// <remarks>
///     Consumed by: <c>GitExtUtils/GitUI/ControlThreadingExtensions.cs</c> (only
///     <see cref="IsDisposed" /> and <see cref="Disposed" />), <c>GitCommands/CommitMessageManager.cs</c>,
///     <c>GitExtensions.Extensibility/Settings/*</c>,
///     <c>GitExtensions.Extensibility/Translations/Xliff/TranslationUtil.cs</c>.
/// </remarks>
public class Control : IWin32Window, IComponent
{
    /// <summary>
    ///     Gets or sets the control name; used as the translation key by the xlf system.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the control text.
    /// </summary>
    public virtual string Text { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets arbitrary user data attached to the control.
    /// </summary>
    public object? Tag { get; set; }

    /// <summary>
    ///     Gets or sets the height in pixels; layout is owned by the real UI framework, the value
    ///     is only stored.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    ///     Gets a value indicating whether <see cref="Dispose()" /> has been called.
    /// </summary>
    public bool IsDisposed { get; private set; }

    ISite? IComponent.Site { get; set; }

    /// <summary>
    ///     Occurs when the control is disposed.
    /// </summary>
    event EventHandler? IComponent.Disposed
    {
        add => Disposed += value;
        remove => Disposed -= value;
    }

    void IDisposable.Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    nint IWin32Window.Handle
    {
        get => 0;
    }

    /// <summary>
    ///     Occurs when the control is disposed.
    /// </summary>
    private event EventHandler? Disposed;

    protected virtual void Dispose(bool disposing)
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;

        if (disposing)
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }
    }
}
