using GitCommands;
using GitCommands.Git;
using GitCommands.Git.Tag;
using GitExtensions.Extensibility.Git;

namespace GitCommandsTests_Git;

partial class CommandsTests
{
    private const string TagName = "bla";
    private static readonly ObjectId Revision = ObjectId.Parse("0123456789012345678901234567890123456789");
    private const string TagMessage = "foo";
    private const string KeyId = "A9876F";
    private const string TagMessageFile = "c:/.git/TAGMESSAGE";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_should_throw_if_tag_name_invalid(string? tagName)
    {
        GitCreateTagArgs args = new(tagName!, Revision);
        ((Action)(() => Commands.CreateTag(args, TagMessageFile, PathUtil.ToPosixPath))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GitCreateTagArgs_should_throw_if_tag_revision_invalid()
    {
        Action act = () => new GitCreateTagArgs(TagName, ObjectId.IndexId);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_should_throw_for_SignWithSpecificKey_if_tag_keyId_invalid(string? signKeyId)
    {
        GitCreateTagArgs args = new(TagName, Revision, TagOperation.SignWithSpecificKey, signKeyId: signKeyId!);
        ((Action)(() => Commands.CreateTag(args, TagMessageFile, PathUtil.ToPosixPath))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ToLine_should_throw_if_operation_not_supported()
    {
        GitCreateTagArgs args = new(TagName, Revision, (TagOperation)10);
        ((Action)(() => Commands.CreateTag(args, TagMessageFile, PathUtil.ToPosixPath))).Should().Throw<NotSupportedException>();
    }

    [Theory]
    [InlineData(true, "tag -f -s -F \"c:/.git/TAGMESSAGE\" \"bla\" -- 0123456789012345678901234567890123456789")]
    [InlineData(false, "tag -s -F \"c:/.git/TAGMESSAGE\" \"bla\" -- 0123456789012345678901234567890123456789")]
    public void ToLine_should_render_force_flag(bool force, string expected)
    {
        GitCreateTagArgs args = new(TagName, Revision, TagOperation.SignWithDefaultKey, TagMessage, KeyId, force);
        IGitCommand cmd = Commands.CreateTag(args, TagMessageFile, PathUtil.ToPosixPath);

        string cmdLine = cmd.Arguments;

        cmdLine.Should().Be(expected);
    }

    [Theory]
    [InlineData(TagOperation.Lightweight, "tag -f \"bla\" -- 0123456789012345678901234567890123456789")]
    [InlineData(TagOperation.Annotate, "tag -f -a -F \"c:/.git/TAGMESSAGE\" \"bla\" -- 0123456789012345678901234567890123456789")]
    [InlineData(TagOperation.SignWithDefaultKey, "tag -f -s -F \"c:/.git/TAGMESSAGE\" \"bla\" -- 0123456789012345678901234567890123456789")]
    [InlineData(TagOperation.SignWithSpecificKey, "tag -f -u A9876F -F \"c:/.git/TAGMESSAGE\" \"bla\" -- 0123456789012345678901234567890123456789")]
    public void ToLine_should_render_different_operations(TagOperation operation, string expected)
    {
        GitCreateTagArgs args = new(TagName, Revision, operation, signKeyId: KeyId, force: true);
        IGitCommand cmd = Commands.CreateTag(args, TagMessageFile, PathUtil.ToPosixPath);

        string actualCmdLine = cmd.Arguments;

        actualCmdLine.Should().Be(expected);
    }
}
