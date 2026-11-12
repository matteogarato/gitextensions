using System.Text;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitUI.CommandsDialogs;
using NSubstitute;

namespace GitUI.AvaloniaTests;

public sealed class PatchWorkflowTests
{
    [Fact]
    public void ReadPatchFile_should_preserve_all_patch_bytes()
    {
        var patchFile = Path.Combine(Path.GetTempPath(), $"patch {Guid.NewGuid():N}.patch");
        byte[] patchBytes = [.. Encoding.Latin1.GetBytes("diff --git a/café.txt b/café.txt\n")];
        File.WriteAllBytes(patchFile, patchBytes);

        try
        {
            PatchWorkflow.ReadPatchFile(patchFile)
                .Should().Be(Encoding.Latin1.GetString(patchBytes));
        }
        finally
        {
            File.Delete(patchFile);
        }
    }

    [Fact]
    public void BuildApply_should_build_git_apply_for_raw_diff_patch()
    {
        var patchFile = Path.Combine(Path.GetTempPath(), $"patch {Guid.NewGuid():N}.diff");
        File.WriteAllText(patchFile, "diff --git a/file.txt b/file.txt\n");

        try
        {
            var module = Substitute.For<IGitModule>();
            module.GetPathForGitExecution(patchFile).Returns(patchFile);

            PatchWorkflow.BuildApply(module, patchFile).ToString()
                .Should().Be($"apply \"{patchFile}\"");
        }
        finally
        {
            File.Delete(patchFile);
        }
    }

    [Fact]
    public void BuildApply_should_build_git_am_for_mailbox_patch()
    {
        var patchFile = Path.Combine(Path.GetTempPath(), $"patch {Guid.NewGuid():N}.patch");
        File.WriteAllText(patchFile, "From: author@example.com\n");

        try
        {
            var module = Substitute.For<IGitModule>();
            module.GetPathForGitExecution(patchFile).Returns(patchFile);

            PatchWorkflow.BuildApply(module, patchFile).ToString()
                .Should().Be($"am --3way \"{patchFile}\"");
        }
        finally
        {
            File.Delete(patchFile);
        }
    }

    [Fact]
    public void BuildFormatPatch_should_build_revision_range_arguments()
    {
        const string parent = "parent-id";
        const string revision = "revision-id";
        var outputDirectory = Path.Combine(Path.GetTempPath(), "patch output");
        var module = Substitute.For<IGitModule>();
        module.GetPathForGitExecution(outputDirectory).Returns(outputDirectory);

        PatchWorkflow.BuildFormatPatch(module, parent, revision, outputDirectory).ToString()
            .Should().Be(
                $"format-patch --find-renames --find-copies --break-rewrites \"{parent}\"..\"{revision}\" -o \"{outputDirectory}\"");
    }

    [Fact]
    public void BuildFormatPatch_should_build_root_commit_arguments()
    {
        const string revision = "root-id";
        var outputDirectory = Path.Combine(Path.GetTempPath(), "patch output");
        var module = Substitute.For<IGitModule>();
        module.GetPathForGitExecution(outputDirectory).Returns(outputDirectory);

        PatchWorkflow.BuildFormatPatch(module, string.Empty, revision, outputDirectory).ToString()
            .Should().Be(
                $"format-patch --find-renames --find-copies --break-rewrites --root \"{revision}\" -o \"{outputDirectory}\"");
    }

    [Fact]
    public void BuildFormatPatches_should_create_a_numbered_patch_for_each_selected_revision()
    {
        const string firstParent = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string firstRevision = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        const string secondParent = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        const string secondRevision = "cccccccccccccccccccccccccccccccccccccccc";
        const string thirdParent = "cccccccccccccccccccccccccccccccccccccccc";
        const string thirdRevision = "dddddddddddddddddddddddddddddddddddddddd";
        var outputDirectory = Path.Combine(Path.GetTempPath(), "patch output");
        var module = Substitute.For<IGitModule>();
        module.GetPathForGitExecution(outputDirectory).Returns(outputDirectory);
        GitUIPluginInterfaces.GitRevision[] revisions =
        [
            new(ObjectId.Parse(firstRevision)) { ParentIds = [ObjectId.Parse(firstParent)] },
            new(ObjectId.Parse(secondRevision)) { ParentIds = [ObjectId.Parse(secondParent)] },
            new(ObjectId.Parse(thirdRevision)) { ParentIds = [ObjectId.Parse(thirdParent)] }
        ];

        var commands = PatchWorkflow.BuildFormatPatches(module, revisions, outputDirectory);

        commands.Select(command => command.ToString()).Should().Equal(
            $"format-patch --find-renames --find-copies --break-rewrites --start-number 1 \"{firstParent}\"..\"{firstRevision}\" -o \"{outputDirectory}\"",
            $"format-patch --find-renames --find-copies --break-rewrites --start-number 2 \"{secondParent}\"..\"{secondRevision}\" -o \"{outputDirectory}\"",
            $"format-patch --find-renames --find-copies --break-rewrites --start-number 3 \"{thirdParent}\"..\"{thirdRevision}\" -o \"{outputDirectory}\"");
    }

    [Fact]
    public void BuildFormatPatches_should_format_two_selected_revisions_as_a_range()
    {
        const string firstParent = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string firstRevision = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        const string secondRevision = "cccccccccccccccccccccccccccccccccccccccc";
        var outputDirectory = Path.Combine(Path.GetTempPath(), "patch output");
        var module = Substitute.For<IGitModule>();
        module.GetPathForGitExecution(outputDirectory).Returns(outputDirectory);
        GitUIPluginInterfaces.GitRevision[] revisions =
        [
            new(ObjectId.Parse(firstRevision)) { ParentIds = [ObjectId.Parse(firstParent)] },
            new(ObjectId.Parse(secondRevision))
        ];

        PatchWorkflow.BuildFormatPatches(module, revisions, outputDirectory)
            .Select(command => command.ToString())
            .Should().Equal(
                $"format-patch --find-renames --find-copies --break-rewrites \"{firstParent}\"..\"{secondRevision}\" -o \"{outputDirectory}\"");
    }
}
