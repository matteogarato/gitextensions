using System.ComponentModel.Design;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitUI;
using GitUI.CommandsDialogs;
using NSubstitute;

namespace GitUITests.CommandsDialogs;
public class FormCommitTests : IDisposable
{
    private IGitModule _gitModule = Substitute.For<IGitModule>();
    private FormCommit _formCommit = null!;
    public FormCommitTests()
    {
        ServiceContainer serviceContainer = new();
        serviceContainer.AddService(Substitute.For<GitUI.Hotkey.IHotkeySettingsManager>());
        serviceContainer.AddService(Substitute.For<ResourceManager.IHotkeySettingsLoader>());
        serviceContainer.AddService(Substitute.For<GitUI.ScriptsEngine.IScriptsRunner>());
        GitUICommands commands = new(serviceContainer, _gitModule);
        _formCommit = new FormCommit(commands);
    }

    public void Dispose()
    {
        _formCommit.Dispose();
    }

    [StaTheory]
    [InlineData("Commit message")]
    [InlineData("Commit message ? () {} []")]
    [InlineData("Commit message ?? (()) {{}} [[]]")]
    [InlineData("Name: {{()-(.*)}}")]
    [InlineData("Name: {{([A-Z]+-\\d+)-(.*)}}[2]")]
    public void RegexReplaceDisabledTest(string msgToReplace)
    {
        _formCommit.GetTestAccessor().ReplaceMessage(msgToReplace, regexEnabled: false);
        _formCommit.GetTestAccessor().Message.Text.Should().Be(msgToReplace);
    }

    [StaTheory]
    [InlineData("Commit message")]
    [InlineData("Commit message begin ? () {} [] end")]
    [InlineData("Commit message begin {}[] end")]
    [InlineData("Commit message ?? (()) [[]] end")]
    [InlineData("Commit message {{ } end")]
    [InlineData("Commit message { }} end")]
    [InlineData("{ } end")]
    public void RegexReplaceEnabledShouldNotChange(string msgToReplace)
    {
        _formCommit.GetTestAccessor().ReplaceMessage(msgToReplace, regexEnabled: true);
        _formCommit.GetTestAccessor().Message.Text.Should().Be(msgToReplace);
    }

    [StaTheory]
    [InlineData("Commit message {{}}", "Commit message ")]
    [InlineData("Commit message {{}} end", "Commit message  end")]
    [InlineData("Commit message{{}}end", "Commit messageend")]
    [InlineData("Commit message {{.*}}", "Commit message ")]
    public void RegexReplaceEnabledShouldChangeEmptyBranchName(string msgToReplace, string expectedValue)
    {
        _formCommit.GetTestAccessor().ReplaceMessage(msgToReplace, regexEnabled: true);
        _formCommit.GetTestAccessor().Message.Text.Should().Be(expectedValue);
    }

    [StaTheory]
    [InlineData("master", "Message:{{(.*)}} end", "Message:master end")]
    [InlineData("master", "Message:{{.*}} end", "Message: end")] // no group
    [InlineData("master", @"Name: {{([A-Z]+-\d+)-(.*)}}", "Name: ")] // no matching
    [InlineData("feature/ABC-4587-commitMessageRegex", @"{{([A-Z]+-\d+)}}: My message, issue:{{[A-Z]+-(\d+)}}", "ABC-4587: My message, issue:4587")] // multiple regex
    [InlineData("feature/ABC-4587-commitMessageRegex", @"Name: {{([A-Z]+-\d+)-(.*)}}[2], issue: {{([A-Z]+-\d+)-(.*)}}[1]", "Name: commitMessageRegex, issue: ABC-4587")] // regex indexing
    [InlineData("feature/ABC-4587-commitMessageRegex", @"{{([A-Z]+-\d+)-(.*)}}: My message", "ABC-4587: My message")] // default index is 1
    [InlineData("feature/ABC-4587-commitMessageRegex", @"Commit from:{{^feature/(.*)$}}[2] branch", "Commit from: branch")] // overindexing
    [InlineData("feature/ABC-4587-commitMessageRegex", @"Commit from:{{^feature/(.*)$}} branch", "Commit from:ABC-4587-commitMessageRegex branch")]
    public void RegexReplaceEnabledShouldChangeBasedOnBranchName(string branch, string msgToReplace, string expectedValue)
    {
        _gitModule.GetSelectedBranch().Returns(branch);
        _formCommit.GetTestAccessor().ReplaceMessage(msgToReplace, regexEnabled: true);
        _formCommit.GetTestAccessor().Message.Text.Should().Be(expectedValue);
    }
}
