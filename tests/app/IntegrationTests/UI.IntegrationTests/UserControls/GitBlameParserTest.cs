using GitCommands.Git;
using GitCommands.Settings;
using GitExtensions.Extensibility.Git;
using GitUI.UserControls;

namespace UITests.UserControls;

public sealed class GitBlameParserTest : IClassFixture<GitBlameParserTestFixture>
{
    [Fact]
    public void GetOriginalLineInPreviousCommit()
    {
        // Simulate "Blame previous revision" on line 2281
        // when blaming file FormBrowse.cs on commit 7edc265403b54dd8ea2b8402f8790219d8d42077
        // line content is: internal bool ExecuteCommand(Command cmd)

        var gitExtensionsRepoPath = Path.GetFullPath(@"..\..\..\..\..\..\..");

        GitModule gitModule = new(new GitExecutorProvider(new GitDirectoryResolver()), gitExtensionsRepoPath);

        // Revision corresponding to line 2281
        var selectedBlamedRevision =
            gitModule.GetRevision(ObjectId.Parse("52476f30670ba5338756b606841fb0a346fd6460"));

        // line content is still: internal bool ExecuteCommand(Command cmd)
        GetOriginalLineInPreviousCommit(2281).Should().Be(2192);

        // line content is still around: internal enum Command -> internal enum Commands
        GetOriginalLineInPreviousCommit(2090).Should().Be(2085);

        // line content from offset in diff
        //   kGitToolStripMenuItem.ShortcutKeyDisplayString = GetShortcutKeys(Command.GitGitK).ToShortcutKeyDisplayString();
        //  ->
        //   kGitToolStripMenuItem.ShortcutKeyDisplayString = GetShortcutKeys(Commands.GitGitK).ToShortcutKeyDisplayString();
        GetOriginalLineInPreviousCommit(824).Should().Be(824);

        int GetOriginalLineInPreviousCommit(int blamedLineNumber)
        {
            return new GitBlameParser(() => gitModule)
                .GetOriginalLineInPreviousCommit(selectedBlamedRevision, "GitUI/CommandsDialogs/FormBrowse.cs",
                    blamedLineNumber);
        }
    }
}

public sealed class GitBlameParserTestFixture : IDisposable
{
    private readonly bool _detectCopyInAllOnBlame;
    private readonly bool _detectCopyInFileOnBlame;

    private readonly bool _ignoreWhitespaceOnBlame;

    // Track the original setting value
    private readonly bool _useHistogramDiffAlgorithm;

    public GitBlameParserTestFixture()
    {
        _useHistogramDiffAlgorithm = AppSettings.UseHistogramDiffAlgorithm;
        _detectCopyInFileOnBlame = AppSettings.DetectCopyInFileOnBlame;
        _detectCopyInAllOnBlame = AppSettings.DetectCopyInAllOnBlame;
        _ignoreWhitespaceOnBlame = AppSettings.IgnoreWhitespaceOnBlame;
        AppSettings.UseHistogramDiffAlgorithm = false;
        AppSettings.DetectCopyInFileOnBlame = false;
        AppSettings.DetectCopyInAllOnBlame = false;
        AppSettings.IgnoreWhitespaceOnBlame = true;
    }

    void IDisposable.Dispose()
    {
        AppSettings.UseHistogramDiffAlgorithm = _useHistogramDiffAlgorithm;
        AppSettings.DetectCopyInFileOnBlame = _detectCopyInFileOnBlame;
        AppSettings.DetectCopyInAllOnBlame = _detectCopyInAllOnBlame;
        AppSettings.IgnoreWhitespaceOnBlame = _ignoreWhitespaceOnBlame;
    }
}
