using GitCommands.Git;

namespace GitCommandsTests.Git;

public sealed class GitBranchNameNormaliserTest
{
    private readonly GitBranchNameNormaliser _gitBranchNameNormaliser = null!;
    private GitBranchNameOptions _gitBranchNameOptions = null!;

    public GitBranchNameNormaliserTest()
    {
        _gitBranchNameNormaliser = new GitBranchNameNormaliser();
        _gitBranchNameOptions = new GitBranchNameOptions("_");
    }

    [Theory]
    [InlineData("[BUG-1234] some long description located [//test/*/dir?/.lock]{3,} and foo:bar\\can...", "_",
        "_BUG-1234]_some_long_description_located__/test/_/dir_/_lock]{3,}_and_foo_bar_can_")]
    [InlineData("[BUG-1234] some long description located [//test/*/dir?/.lock]{3,} and foo:bar\\can...", "-",
        "-BUG-1234]-some-long-description-located--/test/-/dir-/-lock]{3,}-and-foo-bar-can-")]
    [InlineData("[BUG-1234] some long description located [//test/*/dir?/.lock]{3,} and foo:bar\\can...", "",
        "BUG-1234]somelongdescriptionlocated/test/dir/lock]{3,}andfoobarcan")]
    public void Normalise(string input, string replacementToken, string expected)
    {
        _gitBranchNameOptions = new GitBranchNameOptions(replacementToken);
        _gitBranchNameNormaliser.Normalise(input, _gitBranchNameOptions).Should().Be(expected);
    }

    // They can include slash / for hierarchical (directory) grouping,
    // but no slash-separated component can begin with a dot . or end with the sequence .lock.
    [Theory]
    [InlineData(".test", "_test")]
    [InlineData("hierarchy/.test", "hierarchy/_test")]
    [InlineData("hierarchy/.test/foo", "hierarchy/_test/foo")]
    [InlineData(".lock", "_lock")]
    [InlineData("pad.lock", "pad_lock")]
    [InlineData("pad.lock.lock", "pad.lock_lock")]
    [InlineData("hierarchy/sher.lock", "hierarchy/sher_lock")]
    [InlineData("hierarchy/sher.lok", "hierarchy/sher.lok")]
    [InlineData("hierarchy/sher.lock/foo", "hierarchy/sher_lock/foo")]
    [InlineData("hierarchy/sher.lok/foo", "hierarchy/sher.lok/foo")]
    public void Normalise_rule01(string input, string expected)
    {
        GitBranchNameNormaliser.Rule01(input, _gitBranchNameOptions).Should().Be(expected);
    }

    // They cannot have two consecutive dots .. anywhere.
    [Theory]
    [InlineData("..test", "_test")]
    [InlineData("...test", "_test")]
    [InlineData("hierarchy/..test", "hierarchy/_test")]
    [InlineData("hierarchy/...test", "hierarchy/_test")]
    [InlineData("hierarchy/..test/foo", "hierarchy/_test/foo")]
    [InlineData("hierarchy/...test/foo", "hierarchy/_test/foo")]
    [InlineData("..lock", "_lock")]
    [InlineData("pad..lock", "pad_lock")]
    [InlineData("pad...lock", "pad_lock")]
    [InlineData("hierarchy/sher..lock", "hierarchy/sher_lock")]
    [InlineData("padlock...", "padlock_")]
    [InlineData("hierarchy/sher...lock", "hierarchy/sher_lock")]
    [InlineData("hierarchy/sher..lock/foo", "hierarchy/sher_lock/foo")]
    [InlineData("hierarchy/sher...lock/foo", "hierarchy/sher_lock/foo")]
    [InlineData("hierarchy/sher..lok/foo", "hierarchy/sher_lok/foo")]
    [InlineData("hier.....archy/sher...lok/fo..o", "hier_archy/sher_lok/fo_o")]
    public void Normalise_rule03(string input, string expected)
    {
        GitBranchNameNormaliser.Rule03(input, _gitBranchNameOptions).Should().Be(expected);
    }

    // Branch name cannot have ASCII control characters (i.e. bytes whose values are lower than \040, or \127 'DEL'),
    // space, tilde '~', caret '^', or colon ':' anywhere.
    [Theory]
    [InlineData("test:test", "test_test")]
    [InlineData("test test", "test_test")]
    [InlineData("test^test", "test_test")]
    [InlineData("test~test", "test_test")]
    [InlineData("hier archy:sher~lok/fo^o", "hier_archy_sher_lok/fo_o")]
    [InlineData("błąd", "błąd")]
    [InlineData("привет, ё-маё!", "привет,_ё-маё!")]
    [InlineData("Pokémon 195", "Pokémon_195")]
    [InlineData("Anhörung`!@#$%", "Anhörung`!@#$%")]
    [InlineData("test\"test", "test_test")]
    [InlineData("test<test>test", "test_test_test")]
    [InlineData("test|test", "test_test")]
    public void Normalise_rule04(string input, string expected)
    {
        GitBranchNameNormaliser.Rule04(input, _gitBranchNameOptions).Should().Be(expected);
    }

    // Branch name cannot have question-mark '?', asterisk '*', or open bracket '[' anywhere.
    [Theory]
    [InlineData("test?", "test_")]
    [InlineData("?test", "_test")]
    [InlineData("test???test", "test___test")]
    [InlineData("test*", "test_")]
    [InlineData("*test", "_test")]
    [InlineData("test***test", "test___test")]
    [InlineData("test[foo]", "test_foo]")]
    [InlineData("[test]", "_test]")]
    [InlineData("testing?[*]*test", "testing___]_test")]
    public void Normalise_rule05(string input, string expected)
    {
        GitBranchNameNormaliser.Rule05(input, _gitBranchNameOptions).Should().Be(expected);
    }

    // Branch name begin or end with a slash '/' or contain multiple consecutive slashes.
    [Theory]
    [InlineData("test/", false, "test")]
    [InlineData("test/", true, "test/")]
    [InlineData("/test", false, "test")]
    [InlineData("/test/", false, "test")]
    [InlineData("/test/", true, "test/")]
    [InlineData("/test/test/", false, "test/test")]
    [InlineData("/test/test/", true, "test/test/")]
    [InlineData("///test///test///", false, "test/test")]
    [InlineData("///test///test///", true, "test/test/")]
    public void Normalise_rule06(string input, bool allowTrailingSlash, string expected)
    {
        GitBranchNameOptions options = new("_", allowTrailingSlash);
        GitBranchNameNormaliser.Rule06(input, options).Should().Be(expected);
    }

    // Branch name end with a dot '.'.
    [Theory]
    [InlineData("test.", "test_")]
    [InlineData("test..", "test_")]
    [InlineData("test...", "test_")]
    public void Normalise_rule07(string input, string expected)
    {
        GitBranchNameNormaliser.Rule07(input, _gitBranchNameOptions).Should().Be(expected);
    }

    // Branch name cannot contain a sequence '@{'.
    [Theory]
    [InlineData("@{", "_")]
    [InlineData("@{test}", "_test}")]
    [InlineData("test@foo", "test@foo")]
    [InlineData("test@{bla}", "test_bla}")]
    public void Normalise_rule08(string input, string expected)
    {
        GitBranchNameNormaliser.Rule08(input, _gitBranchNameOptions).Should().Be(expected);
    }

    // Branch name cannot be the single character '@'.
    [Theory]
    [InlineData(@"@", "_")]
    public void Normalise_rule09(string input, string expected)
    {
        GitBranchNameNormaliser.Rule09(input, _gitBranchNameOptions).Should().Be(expected);
    }

    // Branch name cannot contain a '\'.
    [Theory]
    [InlineData(@"test\foo\\bar\", "test_foo_bar_")]
    public void Normalise_rule10(string input, string expected)
    {
        GitBranchNameNormaliser.Rule10(input, _gitBranchNameOptions).Should().Be(expected);
    }
}
