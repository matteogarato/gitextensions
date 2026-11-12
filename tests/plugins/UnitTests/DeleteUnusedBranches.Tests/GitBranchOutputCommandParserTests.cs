using GitExtensions.Plugins.DeleteUnusedBranches;

namespace DeleteUnusedBranchesTests;

public class GitBranchOutputCommandParserTests
{
    private readonly GitBranchOutputCommandParser _parser = new();

    [Theory]
    [InlineData(null, true)]
    [InlineData(null, false)]
    [InlineData("", true)]
    [InlineData("", false)]
    [InlineData("   ", true)]
    [InlineData("   ", false)]
    public void GetBranchNames_should_return_empty_list(
        string? commandOutput,
        bool isRemote)
    {
        _parser.GetBranchNames(commandOutput!, isRemote).Should().BeEmpty();
    }

    [Fact]
    public void GetBranchNames_should_return_parse_correctly()
    {
        var commandOutput = @"  feature/branch-1
* fix/branch-2
  master
  HEAD
* (HEAD detached at dce3f9f)
  +_valid_branch_name_starting_with_+
  space/after 
  symbolic_ref -> origin/master
";

        _parser.GetBranchNames(commandOutput, false)
            .Should()
            .BeEquivalentTo("feature/branch-1", "fix/branch-2", "master", "+_valid_branch_name_starting_with_+",
                "space/after", "symbolic_ref");
    }

    [Fact]
    public void GetBranchNames_parse_for_remote()
    {
        var commandOutput = @"  just_remote_that_should_not_occur_but_is_tested
  myremote/mybranch
  other_remote/+_valid_branch_name_starting_with_+
  +remote/+branch+
  space/after 
  origin/symbolic_ref -> origin/master
  ignore_remote_HEAD/HEAD
";

        _parser.GetBranchNames(commandOutput, true)
            .Should()
            .BeEquivalentTo("just_remote_that_should_not_occur_but_is_tested", "myremote/mybranch",
                "other_remote/+_valid_branch_name_starting_with_+", "+remote/+branch+", "space/after",
                "origin/symbolic_ref");
    }

    [Fact]
    public void GetBranchNames_should_support_worktrees()
    {
        var commandOutput = @"+ branch_in_worktree
* master
+ +_valid_branch_name_starting_with_+
";

        _parser.GetBranchNames(commandOutput, false)
            .Should()
            .BeEquivalentTo("branch_in_worktree", "master", "+_valid_branch_name_starting_with_+");
    }
}
