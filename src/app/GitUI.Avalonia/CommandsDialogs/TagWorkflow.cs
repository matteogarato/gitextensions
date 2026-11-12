using GitCommands.Git;
using GitCommands.Git.Extensions;
using GitCommands.Git.Tag;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;

namespace GitUI.CommandsDialogs;

/// <summary>
///     The supported options for creating a tag.
/// </summary>
/// <param name="Name">The tag name.</param>
/// <param name="ObjectId">The commit to tag.</param>
/// <param name="Operation">The lightweight or annotated tag operation.</param>
/// <param name="Message">The annotation message, when applicable.</param>
public sealed record TagConfiguration(string Name, ObjectId ObjectId, TagOperation Operation, string Message);

/// <summary>
///     Validates and builds tag operations with the shared Git command factories.
/// </summary>
public static class TagWorkflow
{
    /// <summary>
    ///     Only real commits can be tagged.
    /// </summary>
    public static bool CanCreateTag(ObjectId objectId)
    {
        return !objectId.IsZero && !objectId.IsArtificial;
    }

    /// <summary>
    ///     Only tags can be deleted from the tag context menu.
    /// </summary>
    public static bool CanDeleteTag(IGitRef? gitRef)
    {
        return gitRef is { IsTag: true };
    }

    /// <summary>
    ///     Returns an error when the name is invalid, already exists, or the message is missing for an annotated tag.
    /// </summary>
    /// <param name="module">The active repository module.</param>
    /// <param name="configuration">The options selected for tag creation.</param>
    public static string? ValidateCreate(IGitModule module, TagConfiguration configuration)
    {
        var name = configuration.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return TranslatedStrings.EnterTagNameError;
        }

        if (!module.CheckBranchFormat(name))
        {
            return TranslatedStrings.InvalidTagNameError;
        }

        if (module.GetRefs(RefsFilter.Tags).Any(gitRef => string.Equals(gitRef.Name, name, StringComparison.Ordinal)))
        {
            return TranslatedStrings.TagAlreadyExistsError;
        }

        if (!CanCreateTag(configuration.ObjectId))
        {
            return TranslatedStrings.TagRevisionRequiredError;
        }

        if (configuration.Operation.CanProvideMessage() && string.IsNullOrWhiteSpace(configuration.Message))
        {
            return TranslatedStrings.TagMessageRequiredError;
        }

        return null;
    }

    /// <summary>
    ///     Builds the create-tag command, passing an optional temporary annotation-message file.
    /// </summary>
    /// <param name="configuration">The tag options.</param>
    /// <param name="messageFileName">The file containing the annotation message, when applicable.</param>
    /// <param name="getPathForGitExecution">Converts a path to the form Git expects.</param>
    /// <returns>A state-changing local Git command.</returns>
    public static IGitCommand BuildCreate(TagConfiguration configuration, string? messageFileName,
        Func<string?, string?> getPathForGitExecution)
    {
        return Commands.CreateTag(
            new GitCreateTagArgs(configuration.Name, configuration.ObjectId, configuration.Operation,
                configuration.Message),
            messageFileName,
            getPathForGitExecution);
    }

    /// <summary>
    ///     Builds a local tag deletion command.
    /// </summary>
    /// <param name="tagName">The tag name to delete.</param>
    /// <returns>A state-changing local Git command.</returns>
    public static IGitCommand BuildDelete(string tagName)
    {
        return Commands.DeleteTag(tagName);
    }
}
