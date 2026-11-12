namespace GitUI.Interops;

internal static partial class NativeMethods
{
    [Flags]
    public enum Lvga : uint
    {
        HeaderLeft = 0x00000001,
        HeaderCenter = 0x00000002,
        HeaderRight = 0x00000004,
        FooterLeft = 0x00000008,
        FooterCenter = 0x00000010,
        FooterRight = 0x00000020
    }
}
