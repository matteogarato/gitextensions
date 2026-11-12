using System.ComponentModel;

namespace GitExtensions.Shims.WinForms;

/// <summary>
///     Stand-in for <c>System.Windows.Forms.ToolStripItem</c>: a headless menu/toolbar item
///     model with disposal tracking.
/// </summary>
/// <remarks>
///     Consumed by: <c>GitExtensions.Extensibility/Plugins/IRepositoryHostPlugin.cs</c>
///     (via <see cref="ContextMenuStrip" />), <c>GitExtUtils/GitUI/ControlThreadingExtensions.cs</c>
///     (only <see cref="IsDisposed" /> and <see cref="Disposed" />).
/// </remarks>
public class ToolStripItem : IComponent
{
    /// <summary>
    ///     Gets or sets the item name; used as the translation key by the xlf system.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the item text.
    /// </summary>
    public virtual string Text { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets arbitrary user data attached to the item.
    /// </summary>
    public object? Tag { get; set; }

    /// <summary>
    ///     Gets a value indicating whether <see cref="Dispose()" /> has been called.
    /// </summary>
    public bool IsDisposed { get; private set; }

    ISite? IComponent.Site { get; set; }

    /// <summary>
    ///     Occurs when the item is disposed.
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

    /// <summary>
    ///     Occurs when the item is disposed.
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
