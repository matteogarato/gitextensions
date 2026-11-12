using GitUI;

namespace GitUITests;

public class FindFilePredicateProviderTest
{
    private static readonly string _patternDefault = "test2";
    private static readonly string _workingDirDefault = @"D:\";

    private readonly IFindFilePredicateProvider _provider = null!;

    public FindFilePredicateProviderTest()
    {
        _provider = new FindFilePredicateProvider();
    }

    [Theory]
    [InlineData(null)]
    public void Get_should_throw_if_pattern_is_null(string? pattern)
    {
        var predicate = () => { _provider.Get(pattern!, _workingDirDefault); };
        predicate.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Get_should_not_throw_if_pattern_is_empty(string? pattern)
    {
        var predicate = () => { _provider.Get(pattern!, _workingDirDefault); };
        predicate.Should().NotThrow<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null)]
    public void Get_should_throw_if_workingDir_is_null(string? workingDir)
    {
        var predicate = () => { _provider.Get(_patternDefault, workingDir!); };
        predicate.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Get_should_throw_if_workingDir_is_empty(string? workingDir)
    {
        var predicate = () => { _provider.Get(_patternDefault, workingDir!); };
        predicate.Should().NotThrow<ArgumentNullException>();
    }

    [Theory]
    [InlineData(@"test2/t", "test1/test2/test3")]
    [InlineData(@"\test2\t", "test1/test2/test3")]
    public void Get_should_correct_work_with_slashes_and_backslashes_in_pattern(string? pattern, string? filePath)
    {
        var predicate = _provider.Get(pattern!, _workingDirDefault);
        predicate(filePath).Should().BeTrue();
    }

    [Theory]
    [InlineData(@"\test2\t", "D:/test1/test2/test3/")]
    public void Get_should_not_throw_then_workingDir_length_greater_that_pattern_length(string? pattern,
        string? workingDir)
    {
        var predicate = () => { _provider.Get(pattern!, workingDir!); };
        predicate.Should().NotThrow<ArgumentNullException>();
    }

    [Theory]
    [InlineData(@"D:\test1", @"D:/", "test1/test2/test3/")]
    [InlineData(@"D:/test1", @"D:/", "test1/test2/test3/")]
    [InlineData(@"D:\test1", @"D:\", "test1/test2/test3/")]
    [InlineData(@"D:/test1", @"D:\", "test1/test2/test3/")]
    [InlineData(@"D:\test1", @"D:", "test1/test2/test3/")]
    [InlineData(@"D:/test1", @"D:", "test1/test2/test3/")]
    public void Get_should_work_correct_when_workingDir_end_with_slash_or_not(string? pattern, string? workingDir,
        string? filePath)
    {
        var predicate = _provider.Get(pattern!, workingDir!);
        predicate(filePath).Should().BeTrue();
    }

    [Theory]
    [InlineData(@"tEsT2", @"D:/", "Test1/teST2/test3/")]
    [InlineData(@"D:\Test\test1", @"D:/TEST", "teSt1/test2/test3/")]
    public void Get_should_work_with_different_cases(string? pattern, string? workingDir, string? filePath)
    {
        var predicate = _provider.Get(pattern!, workingDir!);
        predicate(filePath).Should().BeTrue();
    }

    [Theory]
    [InlineData(@"D:/test1", @"D:/", "test1/test2/test3/", true)]
    [InlineData(@"D:/test2", @"D:/", "test1/test2/test3/", false)]
    [InlineData(@"D:/test/test1", @"D:/test", "test1/test2/test3/", true)]
    [InlineData(@"//d/test/test1", @"//d/test\", "test1/test2/test3/", true)]
    public void Get_should_use_startwith_when_pattern_started_with_workingDir(string? pattern, string? workingDir,
        string? filePath, bool expected)
    {
        var predicate = _provider.Get(pattern!, workingDir!);
        predicate(filePath).Should().Be(expected);
    }

    [Theory]
    [InlineData(@"test2", @"D:/", "test1/test2/test3/", true)]
    [InlineData(@"test1", @"D:/test", "test1/test2/test3/", true)]
    public void Get_should_use_contains_when_pattern_does_not_started_with_workingDir(string? pattern,
        string? workingDir, string? filePath, bool expected)
    {
        var predicate = _provider.Get(pattern!, workingDir!);
        predicate(filePath).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Get_should_not_throw_then_filePath_is_null_or_empty(string? filePath)
    {
        var predicate = _provider.Get(_patternDefault, _workingDirDefault);

        var executor = () => { predicate(filePath); };
        executor.Should().NotThrow<ArgumentNullException>();
    }
}
