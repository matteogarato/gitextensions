namespace GitUI.Interops;

internal static partial class NativeMethods
{
    /// <summary>
    ///     see http://msdn.microsoft.com/en-us/library/bb774754%28v=VS.85%29.aspx.
    /// </summary>
    [Flags]
    public enum Lvhittestflags : uint
    {
        LvhtNowhere = 0x00000001,
        LvhtOnitemicon = 0x00000002,
        LvhtOnitemlabel = 0x00000004,
        LvhtOnitemstateicon = 0x00000008,
        LvhtOnitem = LvhtOnitemicon | LvhtOnitemlabel | LvhtOnitemstateicon,
        LvhtAbove = 0x00000008,
        LvhtBelow = 0x00000010,
        LvhtToright = 0x00000020,
        LvhtToleft = 0x00000040,

        // Vista/Win7+ only
        LvhtExGroupHeader = 0x10000000,
        LvhtExGroupFooter = 0x20000000,
        LvhtExGroupCollapse = 0x40000000,
        LvhtExGroupBackground = 0x80000000,
        LvhtExGroupStateicon = 0x01000000,
        LvhtExGroupSubsetlink = 0x02000000
    }
}
