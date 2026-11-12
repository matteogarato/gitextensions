using System.Runtime.InteropServices;

namespace GitUI.Interops;

internal static partial class NativeMethods
{
    public struct Dttopts
    {
        public int DwSize;
        public Dtt DwFlags;
        public int CrText;
        public int CrBorder;
        public int CrShadow;
        public Textshadowtype ITextShadowType;
        public Point PtShadowOffset;
        public int IBorderSize;
        public int IFontPropId;
        public int IColorPropId;
        public int IStateId;
        [MarshalAs(UnmanagedType.Bool)] public bool FApplyOverlay;
        public int IGlowSize;
        [MarshalAs(UnmanagedType.FunctionPtr)] public DttCallbackProc PfnDrawTextCallback;
        public IntPtr LParam;
    }
}
