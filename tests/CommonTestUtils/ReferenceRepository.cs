using LibGit2Sharp;
using GitModule = GitCommands.Git.GitModule;

namespace CommonTestUtils;

public class ReferenceRepository : IDisposable
{
    private const string _fileName = "A.txt";

    public const string AuthorName = "GitUITests";
    public const string AuthorEmail = "unittests@gitextensions.com";
    public const string AuthorFullIdentity = $"{AuthorName} <{AuthorEmail}>";

    private static readonly Lock _nextLock = new();
    private static GitModuleTestSnapshot? _repoSnapshot;
    private static GitModuleTestSnapshot? _repoWithCommitSnapshot;
    private static string? _repoCommitHash;
    private static Task<(GitModuleTestHelper Helper, string? CommitHash)>? _nextGitModuleTestHelper;
    private static Task<(GitModuleTestHelper Helper, string? CommitHash)>? _nextGitModuleTestHelperWithCommit;
    private readonly GitModuleTestHelper _moduleTestHelper;

    static ReferenceRepository()
    {
        Epilogue.RegisterAfterSuiteAction(1, ReleaseNextRepositories);
    }

    public ReferenceRepository(bool createCommit = true)
    {
        lock (_nextLock)
        {
            if (createCommit)
            {
                _moduleTestHelper = ClaimNextModuleTestHelper(ref _nextGitModuleTestHelperWithCommit,
                    ref _repoWithCommitSnapshot, true);
            }
            else
            {
                _moduleTestHelper = ClaimNextModuleTestHelper(ref _nextGitModuleTestHelper, ref _repoSnapshot, false);
            }
        }
    }

    // We don't expect any failures so that we won't be switching to the main thread or showing messages
    public static Control DummyOwner { get; } = new();

    public GitModule Module
    {
        get => _moduleTestHelper.Module;
    }

    public string? CommitHash { get; private set; }

    void IDisposable.Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private static Task<(GitModuleTestHelper Helper, string? CommitHash)> BeginFastCloneGitModuleTestHelper(
        GitModuleTestSnapshot snapshot)
    {
        return Task.Run(() => (snapshot.Clone(), _repoCommitHash));
    }

    public void CheckoutBranch(string branchName)
    {
        using Repository repository = new(Module.WorkingDir);
        Commands.Checkout(repository, branchName, new CheckoutOptions { CheckoutModifiers = CheckoutModifiers.Force });
    }

    public void CheckoutRevision()
    {
        using Repository repository = new(Module.WorkingDir);
        Commands.Checkout(repository, CommitHash!, new CheckoutOptions { CheckoutModifiers = CheckoutModifiers.Force });
    }

    private GitModuleTestHelper ClaimNextModuleTestHelper(
        ref Task<(GitModuleTestHelper Helper, string? CommitHash)>? initializer, ref GitModuleTestSnapshot? snapshot,
        bool createCommit)
    {
        GitModuleTestHelper moduleTestHelper;

        if (initializer is null)
        {
            // Initialize a reference Git repository synchronously. This is considerably
            // simpler than doing it in a background task, because various parts of the
            // initialization depend on there being an ambient ThreadHelper.JoinableTaskContext,
            // and there is one in this context.

            (moduleTestHelper, CommitHash) = InitializeGitModuleTestHelper(createCommit);

            snapshot = moduleTestHelper.CaptureSnapshot();
        }
        else
        {
            (moduleTestHelper, CommitHash) = initializer.DetachFromSynchronizationContext().GetResultDirect();
        }

        // Start background initialization of the Git module for the next call.
        initializer = BeginFastCloneGitModuleTestHelper(snapshot!);

        return moduleTestHelper;
    }

    private static string Commit(Repository repository, string commitMessage)
    {
        var author = GetAuthorSignature();
        CommitOptions options = new() { PrettifyMessage = false };
        var commit = repository.Commit(commitMessage, author, author, options);
        repository.Index.Write();
        return commit.Id.Sha;
    }

    public void CreateAnnotatedTag(string tagName, string commitHash, string message)
    {
        using Repository repository = new(Module.WorkingDir);
        repository.Tags.Add(tagName, commitHash, GetAuthorSignature(), message);
    }

    public void CreateBranch(string branchName, string commitHash, bool allowOverwrite = false)
    {
        using Repository repository = new(Module.WorkingDir);
        repository.Branches.Add(branchName, commitHash, allowOverwrite);
        Console.WriteLine($"Created branch: {commitHash}, message: {branchName}");
    }

    public string CreateCommit(string commitMessage, string? content = null)
    {
        CommitHash = CreateCommit(_moduleTestHelper, commitMessage, content);

        return CommitHash;
    }

    public string CreateCommit(string commitMessage, string content1, string fileName1, string? content2 = null,
        string? fileName2 = null)
    {
        using Repository repository = new(Module.WorkingDir);
        _moduleTestHelper.CreateRepoFile(fileName1, content1);
        IndexAdd(repository, fileName1);
        if (content2 != null && fileName2 != null)
        {
            _moduleTestHelper.CreateRepoFile(fileName2, content2);
            IndexAdd(repository, fileName2);
        }

        CommitHash = Commit(repository, commitMessage);
        Console.WriteLine($"Created commit: {CommitHash}, message: {commitMessage}");
        return CommitHash;
    }

    private static string CreateCommit(GitModuleTestHelper moduleTestHelper, string commitMessage,
        string? content = null)
    {
        using Repository repository = new(moduleTestHelper.Module.WorkingDir);
        moduleTestHelper.CreateRepoFile(_fileName, content ?? commitMessage);
        IndexAdd(repository, _fileName);

        var commitHash = Commit(repository, commitMessage);
        Console.WriteLine($"Created commit: {commitHash}, message: {commitMessage}");
        return commitHash;
    }

    public string CreateCommitRelative(string fileRelativePath, string fileName, string commitMessage,
        string? content = null)
    {
        using Repository repository = new(Module.WorkingDir);
        _moduleTestHelper.CreateRepoFile(fileRelativePath, fileName, content ?? commitMessage);
        IndexAdd(repository, Path.Combine(fileRelativePath, fileName));

        CommitHash = Commit(repository, commitMessage!);
        Console.WriteLine($"Created commit: {CommitHash}, message: {commitMessage}");

        return CommitHash;
    }

    public void CreateRemoteForBranch(string branchName = "master")
    {
        using Repository repository = new(Module.WorkingDir);
        repository.Network.Remotes.Add("origin", "http://useless.url");
        var remote = repository.Network.Remotes["origin"];

        var branch = repository.Branches[branchName];

        repository.Branches.Update(branch,
            b => b.Remote = remote.Name,
            b => b.UpstreamBranch = branch.CanonicalName);

        Module.InvalidateGitSettings();
        Module.GetEffectiveSetting("reload now");
        Module.GetSettings("reload local settings, too");
    }

    public string CreateRepoFile(string fileName, string fileContent)
    {
        return _moduleTestHelper.CreateRepoFile(fileName, fileContent);
    }

    public void CreateTag(string tagName, string commitHash, bool allowOverwrite = false)
    {
        using Repository repository = new(Module.WorkingDir);
        repository.Tags.Add(tagName, commitHash, allowOverwrite);
    }

    public string DeleteRepoFile(string fileName)
    {
        return _moduleTestHelper.DeleteRepoFile(fileName);
    }

    protected virtual void Dispose(bool disposing)
    {
        _moduleTestHelper.Dispose();
    }

    public void Fetch(string remoteName)
    {
        using Repository repository = new(Module.WorkingDir);
        Commands.Fetch(repository, remoteName, [], new FetchOptions(), null);
    }

    private static Signature GetAuthorSignature()
    {
        return new Signature(AuthorName, AuthorEmail, DateTimeOffset.Now);
    }

    private static (GitModuleTestHelper Helper, string? CommitHash) InitializeGitModuleTestHelper(bool createCommit)
    {
        GitModuleTestHelper moduleTestHelper = new();

        if (createCommit)
        {
            _repoCommitHash = CreateCommit(moduleTestHelper, "A commit message", "A");
        }

        return (moduleTestHelper, _repoCommitHash);
    }

    private static void IndexAdd(Repository repository, string fileName)
    {
        repository.Index.Add(fileName);
        repository.Index.Write();
    }

    public static void ReleaseNextRepositories()
    {
        Task<(GitModuleTestHelper Helper, string? CommitHash)>? next;
        Task<(GitModuleTestHelper Helper, string? CommitHash)>? nextWithCommit;

        lock (_nextLock)
        {
            next = _nextGitModuleTestHelper;
            _nextGitModuleTestHelper = null;

            nextWithCommit = _nextGitModuleTestHelperWithCommit;
            _nextGitModuleTestHelperWithCommit = null;
        }

        next?.DetachFromSynchronizationContext().GetResultDirect().Helper.Dispose();
        nextWithCommit?.DetachFromSynchronizationContext().GetResultDirect().Helper.Dispose();

        // The disposal of GitModuleTestHelper instances registers after-test actions. But
        // we're already after all of the tests; there won't be any further after-test
        // actions. We need to execute them ourselves. Note that the after-test actions
        // don't synchronously perform the clean-up, they just file and forget the clean-up
        // with the clean-up TaskManager.
        Epilogue.ExecuteAfterTestActions();
    }

    public string RenameRepoFile(string fileRelativePath, string oldFileName, string newFileName,
        string? newContent = null, string? commitMessage = null)
    {
        using Repository repository = new(Module.WorkingDir);
        newContent ??= File.ReadAllText(Path.Combine(Module.WorkingDir, fileRelativePath, oldFileName));
        DeleteRepoFile(oldFileName);
        CreateRepoFile(newFileName, newContent);

        Commands.Stage(repository, Path.Combine(fileRelativePath, oldFileName));
        Commands.Stage(repository, Path.Combine(fileRelativePath, newFileName));

        CommitHash = Commit(repository, commitMessage!);
        Console.WriteLine($"Created commit: {CommitHash}, message: {commitMessage}");

        return CommitHash;
    }

    public void Stash(string stashMessage, string? content = null)
    {
        using Repository repository = new(Module.WorkingDir);
        _moduleTestHelper.CreateRepoFile(_fileName, content ?? stashMessage);
        IndexAdd(repository, _fileName);

        var stash = repository.Stashes.Add(GetAuthorSignature(), stashMessage);
        Console.WriteLine($"Created stash: {stash.Index.Sha}, message: {stashMessage}");
    }
}
