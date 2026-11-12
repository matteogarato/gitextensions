using GitCommands.Git;
using GitUI;
using NSubstitute;

namespace GitUITests;

public sealed class RepositoryCurrentBranchNameCacheTests
{
    private const string _path = @"C:\repo";
    private const string _branchName = "main";
    private readonly IRepositoryCurrentBranchNameCache _cache = null!;

    private readonly IRepositoryCurrentBranchNameProvider _inner = null!;

    public RepositoryCurrentBranchNameCacheTests()
    {
        _inner = Substitute.For<IRepositoryCurrentBranchNameProvider>();
        _cache = new RepositoryCurrentBranchNameCache(_inner);
    }

    [Fact]
    public void GetCurrentBranchName_should_delegate_to_inner_on_cache_miss()
    {
        _inner.GetCurrentBranchName(_path).Returns(_branchName);

        var result = _cache.GetCurrentBranchName(_path);

        result.Should().Be(_branchName);
        _inner.Received(1).GetCurrentBranchName(_path);
    }

    [Fact]
    public void GetCurrentBranchName_should_not_call_inner_on_cache_hit()
    {
        _inner.GetCurrentBranchName(_path).Returns(_branchName);
        _cache.GetCurrentBranchName(_path);
        _inner.ClearReceivedCalls();

        var result = _cache.GetCurrentBranchName(_path);

        result.Should().Be(_branchName);
        _inner.DidNotReceive().GetCurrentBranchName(Arg.Any<string>());
    }

    [Fact]
    public void GetCurrentBranchName_should_cache_result_after_first_call()
    {
        _inner.GetCurrentBranchName(_path).Returns(_branchName);
        _cache.GetCurrentBranchName(_path);

        var cached = _cache.GetCachedBranchName(_path);

        cached.Should().Be(_branchName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void GetCurrentBranchName_should_not_cache_blank_branch_name(string? branchName)
    {
        _inner.GetCurrentBranchName(_path).Returns(branchName ?? string.Empty);

        _cache.GetCurrentBranchName(_path);

        _cache.GetCachedBranchName(_path).Should().BeNull();
    }

    [Fact]
    public void GetCurrentBranchName_should_not_cache_unknown_branch_name()
    {
        _inner.GetCurrentBranchName(_path).Returns(DetachedHeadParser.UnknownBranchName);

        _cache.GetCurrentBranchName(_path);

        _cache.GetCachedBranchName(_path).Should().BeNull();
    }

    [Fact]
    public void GetCachedBranchName_should_return_null_before_any_call()
    {
        _cache.GetCachedBranchName(_path).Should().BeNull();
    }

    [Fact]
    public void GetCachedBranchName_should_return_cached_value_after_get()
    {
        _inner.GetCurrentBranchName(_path).Returns(_branchName);
        _cache.GetCurrentBranchName(_path);

        _cache.GetCachedBranchName(_path).Should().Be(_branchName);
    }

    [Fact]
    public void UpdateCache_should_store_branch_name()
    {
        _cache.UpdateCache(_path, _branchName);

        _cache.GetCachedBranchName(_path).Should().Be(_branchName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateCache_should_evict_blank_branch_name(string branchName)
    {
        _cache.UpdateCache(_path, _branchName);

        _cache.UpdateCache(_path, branchName);

        _cache.GetCachedBranchName(_path).Should().BeNull();
    }

    [Fact]
    public void UpdateCache_should_evict_unknown_branch_name()
    {
        _cache.UpdateCache(_path, _branchName);

        _cache.UpdateCache(_path, DetachedHeadParser.UnknownBranchName);

        _cache.GetCachedBranchName(_path).Should().BeNull();
    }

    [Fact]
    public void UpdateCache_should_overwrite_existing_entry()
    {
        const string newBranch = "feature/x";
        _cache.UpdateCache(_path, _branchName);

        _cache.UpdateCache(_path, newBranch);

        _cache.GetCachedBranchName(_path).Should().Be(newBranch);
    }

    [Fact]
    public void InvalidateAll_should_clear_all_entries()
    {
        const string otherPath = @"C:\other";
        _cache.UpdateCache(_path, _branchName);
        _cache.UpdateCache(otherPath, "develop");

        _cache.InvalidateAll();

        _cache.GetCachedBranchName(_path).Should().BeNull();
        _cache.GetCachedBranchName(otherPath).Should().BeNull();
    }

    [Fact]
    public void InvalidateAll_should_allow_inner_to_be_called_again_after_invalidation()
    {
        _inner.GetCurrentBranchName(_path).Returns(_branchName);
        _cache.GetCurrentBranchName(_path);
        _cache.InvalidateAll();
        _inner.ClearReceivedCalls();

        _cache.GetCurrentBranchName(_path);

        _inner.Received(1).GetCurrentBranchName(_path);
    }

    [Fact]
    public void GetCurrentBranchName_should_cache_independently_per_path()
    {
        const string otherPath = @"C:\other";
        _inner.GetCurrentBranchName(_path).Returns(_branchName);
        _inner.GetCurrentBranchName(otherPath).Returns("develop");

        _cache.GetCurrentBranchName(_path);
        _cache.GetCurrentBranchName(otherPath);

        _cache.GetCachedBranchName(_path).Should().Be(_branchName);
        _cache.GetCachedBranchName(otherPath).Should().Be("develop");
    }
}
