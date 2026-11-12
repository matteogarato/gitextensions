using System.Reflection;

namespace CommonTestUtils;

public static class EmbeddedResourceLoader
{
    public static string Load(Assembly asm, string fullResourceName)
    {
        using var stream = asm.GetManifestResourceStream(fullResourceName);
        using StreamReader reader = new(stream!);
        var result = reader.ReadToEnd();
        return result;
    }
}
