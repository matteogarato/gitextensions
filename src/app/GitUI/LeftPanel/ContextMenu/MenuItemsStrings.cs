using GitCommands.Settings;
using GitExtensions.Extensibility.Translations;
using GitUI.LeftPanel.Interfaces;
using ResourceManager;

namespace GitUI.LeftPanel.ContextMenu;

public class MenuItemsStrings : Translate
{
    internal readonly TranslationString CreateBranch = new("Create &branch...");
    internal readonly TranslationString Merge = new("&Merge into current branch...");

    internal readonly TranslationString Reset = new("Re&set current branch to here...");
    // the name of the members act as keys for the generated entries in the translation files (xlf)

    /// <see cref="IGitRefActions" />
    internal TranslationString Checkout = null!;

    /// <see cref="ICanDelete" />
    internal TranslationString Delete = null!;

    internal TranslationString Rebase = null!;

    /// <see cref="ICanRename" />
    internal TranslationString Rename = new("R&ename branch...");

    public MenuItemsStrings()
    {
        Translator.Translate(this, AppSettings.CurrentTranslation);
    }

    internal Dictionary<MenuItemKey, TranslationString> Tooltips { get; } = [];
}

public class BranchMenuItemsStrings : Translate
{
    internal readonly TranslationString Checkout = new("Chec&kout branch...");

    internal readonly TranslationString CheckoutTooltip = new("Checkout this branch");
    internal readonly TranslationString CreateTooltip = new("Create a local branch from this branch");
    internal readonly TranslationString Delete = new("&Delete branch...");
    internal readonly TranslationString MergeTooltip = new("Merge this branch into current branch");
    internal readonly TranslationString Rebase = new("&Rebase current branch on this branch...");
    internal readonly TranslationString RebaseTooltip = new("Rebase current branch on this branch");
    internal readonly TranslationString RenameTooltip = new("Rename this branch");
    internal readonly TranslationString ResetTooltip = new("Reset current branch to here");

    public BranchMenuItemsStrings()
    {
        Translator.Translate(this, AppSettings.CurrentTranslation);
    }

    public void ApplyTo(MenuItemsStrings strings)
    {
        strings.Checkout = Checkout;
        strings.Rebase = Rebase;
        strings.Delete = Delete;

        strings.Tooltips[MenuItemKey.GitRefCheckout] = CheckoutTooltip;
        strings.Tooltips[MenuItemKey.GitRefCreateBranch] = CreateTooltip;
        strings.Tooltips[MenuItemKey.GitRefMerge] = MergeTooltip;
        strings.Tooltips[MenuItemKey.GitRefRebase] = RebaseTooltip;
        strings.Tooltips[MenuItemKey.GitRefReset] = ResetTooltip;
        strings.Tooltips[MenuItemKey.Rename] = RenameTooltip;
    }
}
