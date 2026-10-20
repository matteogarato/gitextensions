using GitCommands;

namespace GitCommandsTests;
public class FullPathResolverTests
{
    private readonly string _workingDir = @"c:\dev\repo";
    private FullPathResolver _resolver = null!;

    public FullPathResolverTests()
    {
        _resolver = new FullPathResolver(() => _workingDir);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Resolve_should_return_null_if_path_null_or_illegal_chars(string? path)
    {
        _resolver.Resolve(path).Should().BeNull();
    }

    [Theory]
    [InlineData(@"c:\")]
    public void Resolve_should_return_original_path_if_rooted(string? path)
    {
        _resolver.Resolve(path).Should().Be(path);
    }

    [Theory]
    [InlineData(@"folder\", 10000)]
    public void Resolve_should_return_long_full_path(string dir, int repeats)
    {
        string path = string.Concat(Enumerable.Repeat(dir, repeats)) + "filename.txt";
        _resolver.Resolve(path).Should().Be($"{_workingDir}\\{path}");
    }

    [Theory]
    [InlineData(@"file")]
    [InlineData("folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\filename.txt")]
    [InlineData("folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder#\\filename.txt")]
    [InlineData("folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\folder\\filename.txt")]
    public void Resolve_should_return_full_path(string? path)
    {
        _resolver.Resolve(path).Should().Be($"{_workingDir}\\{path}");
    }

    [Theory]
    [InlineData("drivers/gpu/drm/nouveau/nvkm/subdev/i2c/aux.c")]
    public void Resolve_handles_system_filenames(string? path)
    {
        _resolver.Resolve(path).Should().Be($"{_workingDir}\\{path!.Replace("/", "\\")}");
    }

    [Theory]
    [InlineData(@"C:\dev\repo")]
    [InlineData(@"C:\dev\repo\")]
    [InlineData(@"C:\dev\repo/")]
    [InlineData(@"C:\dev\c#\repo\")]
    public void Resolve_combines_paths(string? workingDir)
    {
        FullPathResolver resolver = new(() => workingDir!);
        resolver.Resolve("file.txt").Should().Be(Path.Combine(workingDir!, "file.txt").Replace("/", "\\"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Resolve_does_not_throw_on_invalid_workingDir(string? workingDir)
    {
        FullPathResolver resolver = new(() => workingDir!);
        resolver.Resolve("file.txt").Should().Be(Path.Combine(Environment.CurrentDirectory, "file.txt").Replace("/", "\\"));
    }
}
