namespace GitUI.Interops;

internal static partial class NativeMethods
{
    [Flags]
    public enum Lvgf : uint
    {
        None = 0x00000000,
        Header = 0x00000001,
        Footer = 0x00000002,
        State = 0x00000004,
        Align = 0x00000008,
        Groupid = 0x00000010,
        Subtitle = 0x00000100,
        Task = 0x00000200,
        Descriptiontop = 0x00000400,
        Descriptionbottom = 0x00000800,
        Titleimage = 0x00001000,
        Extendedimage = 0x00002000,
        Items = 0x00004000,
        Subset = 0x00008000,
        Subsetitems = 0x00010000
    }
}
