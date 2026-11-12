using System.Reflection;
using CommonTestUtils;
using GitCommands.Settings;
using GitCommands.Utils;

namespace GitCommandsTests.Helpers;

public class AutoCrlfExtensionsTests
{
    [Theory]
    [InlineData(AutoCrlfType.True, "UnixLines")]
    [InlineData(AutoCrlfType.True, "MacLines")]
    [InlineData(AutoCrlfType.True, "WindowsLines")]
    [InlineData(AutoCrlfType.False, "UnixLines")]
    [InlineData(AutoCrlfType.False, "MacLines")]
    [InlineData(AutoCrlfType.False, "WindowsLines")]
    [InlineData(AutoCrlfType.Input, "UnixLines")]
    [InlineData(AutoCrlfType.Input, "MacLines")]
    [InlineData(AutoCrlfType.Input, "WindowsLines")]
    public async Task DoAutoCRLF_should_not_unnecessarily_duplicate_line_ending(AutoCrlfType autoCrlfType, string file)
    {
        var content = EmbeddedResourceLoader.Load(Assembly.GetExecutingAssembly(),
            $"{GetType().Namespace}.MockData.{file}.bin");

        await Verify(content.AdjustLineEndings(autoCrlfType));
    }
}
