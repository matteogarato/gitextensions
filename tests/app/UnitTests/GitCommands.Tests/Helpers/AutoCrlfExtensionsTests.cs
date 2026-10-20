using System.Reflection;
using CommonTestUtils;
using GitCommands;
using GitCommands.Settings;

namespace GitCommandsTests.Helpers;
public class AutoCrlfExtensionsTests
{
    [Theory]
    [InlineData(AutoCRLFType.@true, "UnixLines")]
    [InlineData(AutoCRLFType.@true, "MacLines")]
    [InlineData(AutoCRLFType.@true, "WindowsLines")]
    [InlineData(AutoCRLFType.@false, "UnixLines")]
    [InlineData(AutoCRLFType.@false, "MacLines")]
    [InlineData(AutoCRLFType.@false, "WindowsLines")]
    [InlineData(AutoCRLFType.input, "UnixLines")]
    [InlineData(AutoCRLFType.input, "MacLines")]
    [InlineData(AutoCRLFType.input, "WindowsLines")]
    public async Task DoAutoCRLF_should_not_unnecessarily_duplicate_line_ending(AutoCRLFType autoCRLFType, string file)
    {
        string content = EmbeddedResourceLoader.Load(Assembly.GetExecutingAssembly(), $"{GetType().Namespace}.MockData.{file}.bin");

        await Verifier.Verify(content.AdjustLineEndings(autoCRLFType));
    }
}
