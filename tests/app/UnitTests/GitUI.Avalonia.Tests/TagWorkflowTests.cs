using System.Diagnostics;
using GitCommands.Git.Tag;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitUI.CommandsDialogs;
using NSubstitute;

namespace GitUI.AvaloniaTests;

public sealed class TagWorkflowTests
{
    private static readonly ObjectId _commitId = ObjectId.Parse("0123456789012345678901234567890123456789");

    [Fact]
    public void CanCreateTag_should_reject_artificial_and_zero_revisions()
    {
        TagWorkflow.CanCreateTag(ObjectId.IndexId).Should().BeFalse();
        TagWorkflow.CanCreateTag(default).Should().BeFalse();
        TagWorkflow.CanCreateTag(_commitId).Should().BeTrue();
    }

    [Fact]
    public void ValidateCreate_should_reject_invalid_duplicate_and_incomplete_tags()
    {
        var module = Substitute.For<IGitModule>();
        module.GetRefs(RefsFilter.Tags).Returns(Array.Empty<IGitRef>());

        TagConfiguration emptyName = new("", _commitId, TagOperation.Lightweight, "");
        TagWorkflow.ValidateCreate(module, emptyName).Should().Be(TranslatedStrings.EnterTagNameError);

        module.CheckBranchFormat("bad tag").Returns(false);
        TagConfiguration invalidName = new("bad tag", _commitId, TagOperation.Lightweight, "");
        TagWorkflow.ValidateCreate(module, invalidName).Should().Be(TranslatedStrings.InvalidTagNameError);

        module.CheckBranchFormat("release").Returns(true);
        var existingTag = Substitute.For<IGitRef>();
        existingTag.Name.Returns("release");
        module.GetRefs(RefsFilter.Tags).Returns([existingTag]);
        TagConfiguration duplicate = new("release", _commitId, TagOperation.Lightweight, "");
        TagWorkflow.ValidateCreate(module, duplicate).Should().Be(TranslatedStrings.TagAlreadyExistsError);

        module.GetRefs(RefsFilter.Tags).Returns(Array.Empty<IGitRef>());
        TagConfiguration missingMessage = new("release", _commitId, TagOperation.Annotate, " ");
        TagWorkflow.ValidateCreate(module, missingMessage).Should().Be(TranslatedStrings.TagMessageRequiredError);
    }

    [Fact]
    public void BuildDelete_should_quote_tag_name_and_mark_command_as_state_changing()
    {
        var command = TagWorkflow.BuildDelete("release candidate");

        command.Arguments.Should().Be("tag -d -- \"release candidate\"");
        command.ChangesRepoState.Should().BeTrue();
        command.AccessesRemote.Should().BeFalse();
    }

    [Fact]
    public void BuildCreate_and_BuildDelete_should_create_and_delete_annotated_tag_in_repository()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ge-tag-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            Git(directory, "init -q -b main");
            Git(directory, "config user.name t");
            Git(directory, "config user.email t@example.com");
            File.WriteAllText(Path.Combine(directory, "file.txt"), "tag target\n");
            Git(directory, "add file.txt");
            Git(directory, "commit -q -m initial");
            var commitId = ObjectId.Parse(Git(directory, "rev-parse HEAD"));

            var messageFile = Path.Combine(directory, "tag-message.txt");
            File.WriteAllText(messageFile, "Release tag message");
            TagConfiguration configuration = new("v1.0", commitId, TagOperation.Annotate, "Release tag message");
            Git(directory, TagWorkflow.BuildCreate(configuration, messageFile, path => path).Arguments);

            Git(directory, "cat-file -t refs/tags/v1.0").Should().Be("tag");
            Git(directory, "cat-file -p refs/tags/v1.0").Should().Contain("Release tag message");

            Git(directory, TagWorkflow.BuildDelete("v1.0").Arguments);
            Git(directory, "show-ref --verify --quiet refs/tags/v1.0", 1);
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(directory, true);
        }
    }

    private static string Git(string workingDirectory, string arguments, int expectedExitCode = 0)
    {
        ProcessStartInfo startInfo = new("git", arguments)
        {
            WorkingDirectory = workingDirectory, RedirectStandardOutput = true, RedirectStandardError = true
        };
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start git.");
        var output = process.StandardOutput.ReadToEnd().Trim();
        var error = process.StandardError.ReadToEnd().Trim();
        process.WaitForExit();
        process.ExitCode.Should().Be(expectedExitCode, error);
        return output;
    }
}
