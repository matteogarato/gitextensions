using System.Runtime.CompilerServices;

namespace UITests.Properties;

internal class VerifyModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        VerifierSettings.UseStrictJson();
    }
}
