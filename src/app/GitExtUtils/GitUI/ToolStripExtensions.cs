using System.Runtime.CompilerServices;

namespace GitExtUtils.GitUI;

public static class ToolStripExtensions
{
    private static readonly ConditionalWeakTable<ToolStrip, IMenuItemBackgroundFilter> _menuItemBackgroundFilters = [];
    private static readonly ConditionalWeakTable<ToolStrip, ToolStripExSystemRenderer> _extendedSystemRenderers = [];

    private static readonly ConditionalWeakTable<ToolStrip, ToolStripExProfessionalRenderer>
        _extendedProfessionalRenderers = [];

    public static void AttachMenuItemBackgroundFilter(this ToolStrip toolStrip, IMenuItemBackgroundFilter? value)
    {
        toolStrip.UseExtendedRenderer();
        _menuItemBackgroundFilters.Remove(toolStrip);

        if (value is not null)
        {
            _menuItemBackgroundFilters.Add(toolStrip, value);
        }
    }

    private static void UseExtendedRenderer(this ToolStrip toolStrip)
    {
        if (toolStrip.Renderer is ToolStripSystemRenderer and not ToolStripExSystemRenderer)
        {
            toolStrip.Renderer = _extendedSystemRenderers.GetOrCreateValue(toolStrip);
        }
        else if (toolStrip.Renderer is ToolStripProfessionalRenderer and not ToolStripExProfessionalRenderer)
        {
            toolStrip.Renderer = _extendedProfessionalRenderers.GetOrCreateValue(toolStrip);
        }
    }

    internal static IMenuItemBackgroundFilter? GetMenuItemBackgroundFilter(this ToolStrip toolStrip)
    {
        if (_menuItemBackgroundFilters.TryGetValue(toolStrip, out var filter))
        {
            return filter;
        }

        return null;
    }
}
