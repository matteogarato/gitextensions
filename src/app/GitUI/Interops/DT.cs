namespace GitUI.Interops;

internal static partial class NativeMethods
{
    public enum Dt
    {
        DtLeft = 0x0,
        DtTop = 0x0,
        DtCenter = 0x1,
        DtRight = 0x2,
        DtVcenter = 0x4,
        DtBottom = 0x8,
        DtWordbreak = 0x10,
        DtSingleline = 0x20,
        DtExpandtabs = 0x40,
        DtTabstop = 0x80,
        DtNoclip = 0x100,
        DtExternalleading = 0x200,
        DtCalcrect = 0x400,
        DtNoprefix = 0x800,
        DtInternal = 0x1000,
        DtEditcontrol = 0x2000,
        DtPathEllipsis = 0x4000,
        DtEndEllipsis = 0x8000,
        DtModifystring = 0x10000,
        DtRtlreading = 0x20000,
        DtWordEllipsis = 0x40000,
        DtNofullwidthcharbreak = 0x80000,
        DtHideprefix = 0x100000,
        DtPrefixonly = 0x200000
    }
}
