using static GitUI.Interops.Interop;

namespace GitUI.Interops;

internal class Interop
{
    /// <summary>
    ///     Blittable version of Windows BOOL type. It is convenient in situations where
    ///     manual marshalling is required, or to avoid overhead of regular bool marshalling.
    /// </summary>
    /// <remarks>
    ///     Some Windows APIs return arbitrary integer values although the return type is defined
    ///     as BOOL. It is best to never compare BOOL to TRUE. Always use bResult != BOOL.FALSE
    ///     or bResult == BOOL.FALSE .
    /// </remarks>
    public enum Bool
    {
        False = 0,
        True = 1
    }
}

internal static class BoolExtensions
{
    public static bool IsTrue(this Bool b)
    {
        return b != Bool.False;
    }

    public static bool IsFalse(this Bool b)
    {
        return b == Bool.False;
    }

    public static Bool ToBool(this bool b)
    {
        return b ? Bool.True : Bool.False;
    }
}
