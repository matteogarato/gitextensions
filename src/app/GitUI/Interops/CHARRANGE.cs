namespace GitUI.Interops;

internal static partial class NativeMethods
{
    public struct Charrange
    {
        public int CpMin; // First character of range (0 for start of doc)
        public int CpMax; // Last character of range (-1 for end of doc)
    }
}
