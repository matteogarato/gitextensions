namespace GitExtensions.Shims.WinForms;

/// <summary>
///     Stand-in for <c>System.Drawing.Font</c>: an immutable family/size/style record.
///     Unlike <c>System.Drawing.Common</c>, it is fully functional on non-Windows platforms;
///     resolution to a renderable font is owned by the real UI framework.
/// </summary>
/// <remarks>
///     Consumed by: <c>GitExtensions.Extensibility/FontParser.cs</c>,
///     <c>GitExtensions.Extensibility/Settings/SettingsSource.cs</c> (font settings),
///     <c>GitCommands/Settings/AppSettings.cs</c> (font getters).
/// </remarks>
public sealed class Font(FontFamily family, float size, FontStyle style) : IDisposable
{
    public Font(string familyName, float size)
        : this(familyName, size, FontStyle.Regular)
    {
    }

    public Font(string familyName, float size, FontStyle style)
        : this(new FontFamily(familyName), size, style)
    {
    }

    public Font(FontFamily family, float size)
        : this(family, size, FontStyle.Regular)
    {
    }

    /// <summary>
    ///     Gets the font family.
    /// </summary>
    public FontFamily FontFamily { get; } = family;

    /// <summary>
    ///     Gets the font family name.
    /// </summary>
    public string Name
    {
        get => FontFamily.Name;
    }

    /// <summary>
    ///     Gets the em-size of the font in points.
    /// </summary>
    public float Size { get; } = size;

    /// <summary>
    ///     Gets the style flags.
    /// </summary>
    public FontStyle Style { get; } = style;

    /// <summary>
    ///     Gets a value indicating whether the font is bold.
    /// </summary>
    public bool Bold
    {
        get => Style.HasFlag(FontStyle.Bold);
    }

    /// <summary>
    ///     Gets a value indicating whether the font is italic.
    /// </summary>
    public bool Italic
    {
        get => Style.HasFlag(FontStyle.Italic);
    }

    void IDisposable.Dispose()
    {
        // Immutable data holder; nothing to release. Present because callers dispose fonts.
    }

    public override string ToString()
    {
        return $"[{nameof(Font)}: Name={Name}, Size={Size}, Style={Style}]";
    }
}
