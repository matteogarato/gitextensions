using GitCommands;
using GitCommands.Git;
using GitCommands.Git.Tag;
using GitExtensions.Extensibility.Git;

namespace GitCommandsTests.Git;

partial class CommandsTests
{
    private const string _tagName = "bla";
    private const string _tagMessage = "foo";
    private const string _keyId = "A9876F";
    private const string _tagMessageFile = "c:/.git/TAGMESSAGE";
    private static readonly ObjectId _revision = ObjectId.Parse("0123456789012345678901234567890123456789");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_should_throw_if_tag_name_invalid(string? tagName)
    {
        GitCreateTagArgs args = new(tagName!, _revision);
        ((Action)(() => GitCommands.Git.Commands.CreateTag(args, _tagMessageFile, PathUtil.ToPosixPath))).Should()
            .Throw<ArgumentException>();
    }

    [Fact]
    public void GitCreateTagArgs_should_throw_if_tag_revision_invalid()
    {
        Action act = () => new GitCreateTagArgs(_tagName, ObjectId.IndexId);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_should_throw_for_SignWithSpecificKey_if_tag_keyId_invalid(string? signKeyId)
    {
        GitCreateTagArgs args = new(_tagName, _revision, TagOperation.SignWithSpecificKey, signKeyId: signKeyId!);
        ((Action)(() => GitCommands.Git.Commands.CreateTag(args, _tagMessageFile, PathUtil.ToPosixPath))).Should()
            .Throw<ArgumentException>();
    }

    [Fact]
    public void ToLine_should_throw_if_operation_not_supported()
    {
        GitCreateTagArgs args = new(_tagName, _revision, (TagOperation)10);
        ((Action)(() => GitCommands.Git.Commands.CreateTag(args, _tagMessageFile, PathUtil.ToPosixPath))).Should()
            .Throw<NotSupportedException>();
    }

    [Theory]
    [InlineData(true, "tag -f -s -F \"c:/.git/TAGMESSAGE\" \"bla\" -- 0123456789012345678901234567890123456789")]
    [InlineData(false, "tag -s -F \"c:/.git/TAGMESSAGE\" \"bla\" -- 0123456789012345678901234567890123456789")]
    public void ToLine_should_render_force_flag(bool force, string expected)
    {
        GitCreateTagArgs args = new(_tagName, _revision, TagOperation.SignWithDefaultKey, _tagMessage, _keyId, force);
        var cmd = GitCommands.Git.Commands.CreateTag(args, _tagMessageFile, PathUtil.ToPosixPath);

        var cmdLine = cmd.Arguments;

        cmdLine.Should().Be(expected);
    }

    [Theory]
    [InlineData(TagOperation.Lightweight, "tag -f \"bla\" -- 0123456789012345678901234567890123456789")]
    [InlineData(TagOperation.Annotate,
        "tag -f -a -F \"c:/.git/TAGMESSAGE\" \"bla\" -- 0123456789012345678901234567890123456789")]
    [InlineData(TagOperation.SignWithDefaultKey,
        "tag -f -s -F \"c:/.git/TAGMESSAGE\" \"bla\" -- 0123456789012345678901234567890123456789")]
    [InlineData(TagOperation.SignWithSpecificKey,
        "tag -f -u A9876F -F \"c:/.git/TAGMESSAGE\" \"bla\" -- 0123456789012345678901234567890123456789")]
    public void ToLine_should_render_different_operations(TagOperation operation, string expected)
    {
        GitCreateTagArgs args = new(_tagName, _revision, operation, signKeyId: _keyId, force: true);
        var cmd = GitCommands.Git.Commands.CreateTag(args, _tagMessageFile, PathUtil.ToPosixPath);

        var actualCmdLine = cmd.Arguments;

        actualCmdLine.Should().Be(expected);
    }
}
