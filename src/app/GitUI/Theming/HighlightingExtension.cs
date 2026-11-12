using GitExtUtils.GitUI.Theming;
using ICSharpCode.TextEditor.Document.HighlightingStrategy;

namespace GitUI.Theming;

internal static class HighlightingExtension
{
    public static HighlightColor Transform(this HighlightColor original)
    {
        var originalBackColor = original.BackgroundColor;
        var hasNoBackground = originalBackColor.IsEmpty || originalBackColor.A == 0;
        var backColor = !original.Adaptable || originalBackColor.IsSystemColor || hasNoBackground
            ? originalBackColor
            : originalBackColor.AdaptBackColor();

        var effectiveBackColor = hasNoBackground
            ? AppColor.EditorBackground.GetThemeColor()
            : originalBackColor;
        var foreColor = !original.Adaptable || original.Color.IsSystemColor
            ? original.Color
            : original.Color.AdaptForeColor(hasNoBackground ? effectiveBackColor : backColor);

        return new HighlightColor(original, foreColor, backColor);
    }
}
