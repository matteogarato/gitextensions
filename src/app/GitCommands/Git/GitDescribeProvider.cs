using GitExtensions.Extensibility.Git;

namespace GitCommands.Git;

public interface IGitDescribeProvider
{
    /// <summary>
    ///     Runs <c>git describe</c> to find the most recent tag that is reachable from a commit.
    ///     If the tag points to the commit, then only the tag is shown. Otherwise, it suffixes the tag name with the number
    ///     of additional commits on top of the tagged object and the abbreviated object name of the most recent commit.
    /// </summary>
    /// <param name="objectId">An objectId to describe.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>Describe information.</returns>
    (string precedingTag, string commitCount) Get(ObjectId objectId, CancellationToken cancellationToken = default);
}

public sealed class GitDescribeProvider(Func<IGitModule> getModule) : IGitDescribeProvider
{
    /// <inheritdoc />
    (string precedingTag, string commitCount) IGitDescribeProvider.Get(ObjectId objectId,
        CancellationToken cancellationToken = default)
    {
        var description = GetModule().GetDescribe(objectId, cancellationToken);
        if (string.IsNullOrEmpty(description))
        {
            return (string.Empty, string.Empty);
        }

        var commitHashPos = description.LastIndexOf("-g", StringComparison.OrdinalIgnoreCase);
        if (commitHashPos == -1)
        {
            return (description, string.Empty);
        }

        var commitHash = description[(commitHashPos + 2)..];
        if (commitHash.Length == 0 || objectId.ToString() != commitHash)
        {
            return (description, string.Empty);
        }

        description = description[..commitHashPos];
        var commitCountPos = description.LastIndexOf('-');
        if (commitCountPos == -1)
        {
            return (description, string.Empty);
        }

        var commitCount = description[(commitCountPos + 1)..];
        description = description[..commitCountPos];
        return (description, commitCount);

        IGitModule GetModule()
        {
            return getModule() ?? throw new ArgumentException($"Require a valid instance of {nameof(IGitModule)}");
        }
    }
}
