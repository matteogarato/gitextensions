using System.Collections;
using System.Diagnostics.CodeAnalysis;
using GitExtUtils.GitUI.Theming;
using GitUI.LeftPanel.Interfaces;
using GitUI.Properties;
using ResourceManager;

namespace GitUI.LeftPanel.ContextMenu;

internal class MenuItemsGenerator<TNode> : IMenuItemsGenerator<TNode>
    where TNode : class, INode
{
    private readonly Lazy<List<ToolStripItemWithKey>> _items;
    private readonly Lazy<Dictionary<MenuItemKey, ToolStripItem>> _itemsIndex;
    private readonly IMenuItemFactory _menuItemFactory;

    public MenuItemsGenerator(IMenuItemFactory menuItemFactory)
    {
        _menuItemFactory = menuItemFactory;
        _items = new Lazy<List<ToolStripItemWithKey>>(() => CreateItems());
        _itemsIndex = new Lazy<Dictionary<MenuItemKey, ToolStripItem>>(() => CreateItemsIndex());
    }

    public MenuItemsStrings Strings { get; } = new();

    bool IMenuItemsGenerator<TNode>.TryGetMenuItem(MenuItemKey key, [NotNullWhen(true)] out ToolStripItem? item)
    {
        return _itemsIndex.Value.TryGetValue(key, out item);
    }

    private Dictionary<MenuItemKey, ToolStripItem> CreateItemsIndex()
    {
        Dictionary<MenuItemKey, ToolStripItem> itemsIndex = [];
        _items.Value.ForEach(r => itemsIndex.Add(r.Key, r.Item));
        return itemsIndex;
    }

    private List<ToolStripItemWithKey> CreateItems()
    {
        List<ToolStripItemWithKey> items = [];
        Generate(items);
        return items;
    }

    private void Generate(List<ToolStripItemWithKey> items)
    {
        items.AddRange(CreateGitRefItems());
        items.AddRange(CreateRename());
        items.AddRange(CreateDelete());
    }

    private IEnumerable<ToolStripItemWithKey> CreateGitRefItems()
    {
        if (!Implements<IGitRefActions>())
        {
            yield break;
        }

        yield return _menuItemFactory.CreateMenuItem<ToolStripMenuItem, TNode>(
                node => ((IGitRefActions)node).Checkout(), Strings.Checkout, GetTooltip(MenuItemKey.GitRefCheckout),
                Images.BranchCheckout)
            .WithKey(MenuItemKey.GitRefCheckout);

        yield return _menuItemFactory.CreateMenuItem<ToolStripMenuItem, TNode>(
                node => ((IGitRefActions)node).Merge(), Strings.Merge, GetTooltip(MenuItemKey.GitRefMerge),
                Images.Merge)
            .WithKey(MenuItemKey.GitRefMerge);

        yield return _menuItemFactory.CreateMenuItem<ToolStripMenuItem, TNode>(
                node => ((IGitRefActions)node).Rebase(), Strings.Rebase, GetTooltip(MenuItemKey.GitRefRebase),
                Images.Rebase)
            .WithKey(MenuItemKey.GitRefRebase);

        yield return _menuItemFactory.CreateMenuItem<ToolStripMenuItem, TNode>(
                node => ((IGitRefActions)node).CreateBranch(), Strings.CreateBranch,
                GetTooltip(MenuItemKey.GitRefCreateBranch), Images.Branch.AdaptLightness())
            .WithKey(MenuItemKey.GitRefCreateBranch);

        yield return _menuItemFactory.CreateMenuItem<ToolStripMenuItem, TNode>(
                node => ((IGitRefActions)node).Reset(), Strings.Reset, GetTooltip(MenuItemKey.GitRefReset),
                Images.ResetCurrentBranchToHere)
            .WithKey(MenuItemKey.GitRefReset);

        yield return new ToolStripSeparator().WithKey(MenuItemKey.GitRefActionsSeparator);
    }

    private IEnumerable<ToolStripItemWithKey> CreateRename()
    {
        if (Implements<ICanRename>())
        {
            var item = _menuItemFactory.CreateMenuItem<ToolStripMenuItem, TNode>(
                node => ((ICanRename)node).Rename(), Strings.Rename, GetTooltip(MenuItemKey.Rename), Images.Renamed);
            yield return item.WithKey(MenuItemKey.Rename);
        }
    }

    private IEnumerable<ToolStripItemWithKey> CreateDelete()
    {
        if (Implements<ICanDelete>())
        {
            var item = _menuItemFactory.CreateMenuItem<ToolStripMenuItem, TNode>(
                node => ((ICanDelete)node).Delete(), Strings.Delete, GetTooltip(MenuItemKey.Delete),
                Images.BranchDelete);
            yield return item.WithKey(MenuItemKey.Delete);
        }
    }

    protected TranslationString GetTooltip(MenuItemKey key)
    {
        return Strings.Tooltips[key];
    }

    private static bool Implements<TInterface>()
    {
        return typeof(TInterface).IsAssignableFrom(typeof(TNode));
    }

    #region IEnumerable

    IEnumerator<ToolStripItemWithKey> IEnumerable<ToolStripItemWithKey>.GetEnumerator()
    {
        return _items.Value.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    #endregion
}

internal static class ToolStripButtonExtensions
{
    public static ToolStripItemWithKey WithKey(this ToolStripItem item, MenuItemKey key)
    {
        return new ToolStripItemWithKey(key, item);
    }
}
