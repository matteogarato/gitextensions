using System.IO.Abstractions;
using System.Text;
using CommonTestUtils;
using GitCommands;
using GitCommands.Git;
using NSubstitute;
using AppSettings = GitCommands.Settings.AppSettings;

namespace GitCommandsTests;

public class CommitMessageManagerTests : IDisposable
{
    private const string _commitMessage = "commit message";
    private const string _mergeMessage = "merge message";
    private const string _newMessage = "new message";
    private const string _overriddenCommitMessage = "commandline message";

    private readonly string _amendSaveStatePath;
    private readonly string _commitMessagePath;
    private readonly Encoding _encoding = new UTF8Encoding(false);
    private readonly string _mergeMessagePath;

    // We don't expect any failures so that we won't be switching to the main thread or showing messages
    private readonly Control _owner = ReferenceRepository.DummyOwner;
    private readonly bool _rememberAmendCommitState;

    private readonly string _workingDirGitDir = @"c:\dev\repo\.git";
    private DirectoryBase _directory = null!;

    private FileBase _file = null!;
    private IFileSystem _fileSystem = null!;
    private CommitMessageManager _manager = null!;

    // Created for each test
    private ReferenceRepository _referenceRepository = null!;

    public CommitMessageManagerTests()
    {
        _amendSaveStatePath = Path.Combine(_workingDirGitDir, "GitExtensions.amend");
        _commitMessagePath = Path.Combine(_workingDirGitDir, "COMMITMESSAGE");
        _mergeMessagePath = Path.Combine(_workingDirGitDir, "MERGE_MSG");
        _rememberAmendCommitState = AppSettings.RememberAmendCommitState;

        Initialize();
    }

    void IDisposable.Dispose()
    {
        AppSettings.RememberAmendCommitState = _rememberAmendCommitState;

        _referenceRepository.Dispose();
    }

    private void Initialize()
    {
        _referenceRepository = new ReferenceRepository();

        _file = Substitute.For<FileBase>();
        _file.ReadAllTextAsync(_commitMessagePath, _encoding, TestContext.Current.CancellationToken)
            .Returns(_commitMessage);
        _file.ReadAllTextAsync(_mergeMessagePath, _encoding, TestContext.Current.CancellationToken)
            .Returns(_mergeMessage);
        _directory = Substitute.For<DirectoryBase>();

        var path = Substitute.For<PathBase>();
        path.Join(Arg.Any<string>(), Arg.Any<string>()).Returns(x => Path.Join((string)x[0], (string)x[1]));

        _fileSystem = Substitute.For<IFileSystem>();
        _fileSystem.File.Returns(_file);
        _fileSystem.Directory.Returns(_directory);
        _fileSystem.Path.Returns(path);

        _manager = new CommitMessageManager(_owner, _workingDirGitDir, _encoding, _fileSystem);
    }

    private void SetupExtra(string overriddenCommitMessage)
    {
        _manager = new CommitMessageManager(_owner, _workingDirGitDir, _encoding, _fileSystem, overriddenCommitMessage);
    }

    [Theory]
    [InlineData(null)]
    public void Constructor_should_throw(string? workingDirGitDir)
    {
        ((Action)(() => new CommitMessageManager(_owner, workingDirGitDir!, _encoding))).Should()
            .Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("::")]
    public void Constructor_should_not_throw(string? workingDirGitDir)
    {
        new CommitMessageManager(_owner, workingDirGitDir!, _encoding).Should().NotBeNull();
    }

    [Fact]
    public async Task GetAmendStateAsync_should_be_false_if_file_is_missing()
    {
        _file.Exists(_amendSaveStatePath).Returns(false);

        AppSettings.RememberAmendCommitState = true;
        (await _manager.GetAmendStateAsync(TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    [Fact]
    public async Task GetAmendStateAsync_should_be_false_if_not_RememberAmendCommitState()
    {
        _file.Exists(_amendSaveStatePath).Returns(true);

        AppSettings.RememberAmendCommitState = false;
        (await _manager.GetAmendStateAsync(TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\n")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("false")]
    [InlineData("yes")]
    [InlineData("on")]
    [InlineData("checked")]
    [InlineData("true.")]
    [InlineData("true\nx")]
    public async Task GetAmendStateAsync_should_be_false_if_file_contains(string amendText)
    {
        _file.Exists(_amendSaveStatePath).Returns(true);
        _file.ReadAllText(_amendSaveStatePath, Encoding.Default).Returns(amendText);

        AppSettings.RememberAmendCommitState = true;
        (await _manager.GetAmendStateAsync(TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TrUe")]
    [InlineData("true ")]
    [InlineData("true\n")]
    public async Task GetAmendStateAsync_should_be_true_if_file_contains(string amendText)
    {
        _file.Exists(_amendSaveStatePath).Returns(true);
        _file.ReadAllTextAsync(_amendSaveStatePath, Encoding.Default, TestContext.Current.CancellationToken)
            .Returns(amendText);

        AppSettings.RememberAmendCommitState = true;
        (await _manager.GetAmendStateAsync(TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    [Fact]
    public async Task SetAmendStateAsync_true_should_write_true_to_file()
    {
        var correctlyWritten = false;
        _file.When(x => x.WriteAllTextAsync(_amendSaveStatePath, true.ToString(), Encoding.Default,
            TestContext.Current.CancellationToken)).Do(_ => correctlyWritten = true);
        _directory.Exists(Path.GetDirectoryName(_amendSaveStatePath)).Returns(true);

        AppSettings.RememberAmendCommitState = true;
        await _manager.SetAmendStateAsync(amendState: true, TestContext.Current.CancellationToken);

        correctlyWritten.Should().BeTrue();
    }

    [Fact]
    public async Task SetAmendStateAsync_true_should_delete_file_if_not_RememberAmendCommitState()
    {
        var correctlyDeleted = false;
        _file.Exists(_amendSaveStatePath).Returns(true);
        _file.When(x => x.Delete(_amendSaveStatePath)).Do(_ => correctlyDeleted = true);

        AppSettings.RememberAmendCommitState = false;
        await _manager.SetAmendStateAsync(amendState: true, TestContext.Current.CancellationToken);

        correctlyDeleted.Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SetAmendStateAsync_false_should_delete_file(bool rememberAmendCommitState)
    {
        var correctlyDeleted = false;
        _file.When(x => x.Delete(_amendSaveStatePath)).Do(_ => correctlyDeleted = true);
        _file.Exists(_amendSaveStatePath).Returns(true);

        AppSettings.RememberAmendCommitState = rememberAmendCommitState;
        await _manager.SetAmendStateAsync(amendState: false, TestContext.Current.CancellationToken);

        correctlyDeleted.Should().BeTrue();
    }

    [Fact]
    public void CommitMessagePath()
    {
        _manager.CommitMessagePath.Should().Be(_commitMessagePath);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void IsMergeCommit(bool fileExists, bool expected)
    {
        _file.Exists(_manager.MergeMessagePath).Returns(x => fileExists);

        _manager.IsMergeCommit.Should().Be(expected);
    }

    [Fact]
    public async Task WriteCommitMessageToFileAsync_should_write_COMMITMESSAGE()
    {
        CommitMessageManager manager = new(_owner, _referenceRepository.Module.WorkingDir,
            _referenceRepository.Module.CommitEncoding);

        File.Exists(manager.CommitMessagePath).Should().BeFalse();

        // null message isn't formatted, since we're only interested in testing File.Write logic
        string? message = null;
        await manager.WriteCommitMessageToFileAsync(message!, CommitMessageType.Normal, false, false,
            TestContext.Current.CancellationToken);

        File.Exists(manager.CommitMessagePath).Should().BeTrue();
    }

    [Theory]
    [InlineData("utf-8")]
    [InlineData("Utf-8")]
    [InlineData("UTF-8")]
    public async Task WriteCommitMessageToFileAsync_no_bom(string encodingName)
    {
        var module = _referenceRepository.Module;
        module.SetSetting("i18n.commitencoding", encodingName);
        module.CommitEncoding.Preamble.Length.Should().Be(0);
        CommitMessageManager manager = new(_owner, _referenceRepository.Module.WorkingDir,
            _referenceRepository.Module.CommitEncoding);

        File.Exists(manager.CommitMessagePath).Should().BeFalse();

        var message = "Test message";
        await manager.WriteCommitMessageToFileAsync(message, CommitMessageType.Normal, false, false,
            TestContext.Current.CancellationToken);

        File.Exists(manager.CommitMessagePath).Should().BeTrue();
        File.ReadAllBytes(manager.CommitMessagePath).Should().BeEquivalentTo(Encoding.ASCII.GetBytes(message + "\r\n"));
    }

    [Fact]
    public async Task WriteCommitMessageToFileAsync_should_write_MERGE_MSG()
    {
        CommitMessageManager manager = new(_owner, _referenceRepository.Module.WorkingDir,
            _referenceRepository.Module.CommitEncoding);

        File.Exists(manager.MergeMessagePath).Should().BeFalse();

        // null message isn't formatted, since we're only interested in testing File.Write logic
        string? message = null;
        await manager.WriteCommitMessageToFileAsync(message!, CommitMessageType.Merge, false, false,
            TestContext.Current.CancellationToken);

        File.Exists(manager.MergeMessagePath).Should().BeTrue();
    }

    [Fact]
    public void MergeMessagePath()
    {
        _manager.MergeMessagePath.Should().Be(_mergeMessagePath);
    }

    [Fact]
    public async Task GetMergeOrCommitMessageAsync_should_return_merge_message_if_exists()
    {
        _file.Exists(_commitMessagePath).Returns(true);
        _file.Exists(_mergeMessagePath).Returns(true);

        (await _manager.GetMergeOrCommitMessageAsync(TestContext.Current.CancellationToken)).Should().Be(_mergeMessage);

        _manager.IsMergeCommit.Should().BeTrue();
    }

    [Fact]
    public async Task GetMergeOrCommitMessageAsync_should_return_overridden_message_if_set()
    {
        SetupExtra(_overriddenCommitMessage);
        _file.Exists(_commitMessagePath).Returns(true);
        _file.Exists(_mergeMessagePath).Returns(true);

        (await _manager.GetMergeOrCommitMessageAsync(TestContext.Current.CancellationToken)).Should()
            .Be(_overriddenCommitMessage);

        _manager.IsMergeCommit.Should().BeTrue();
    }

    [Fact]
    public async Task GetMergeOrCommitMessageAsync_should_return_commit_message_if_exists_and_no_merge_message()
    {
        _file.Exists(_commitMessagePath).Returns(true);
        _file.Exists(_mergeMessagePath).Returns(false);

        (await _manager.GetMergeOrCommitMessageAsync(TestContext.Current.CancellationToken)).Should()
            .Be(_commitMessage);

        _manager.IsMergeCommit.Should().BeFalse();
    }

    [Fact]
    public async Task GetMergeOrCommitMessageAsync_should_return_empty_if_no_file_exists()
    {
        _file.Exists(_commitMessagePath).Returns(false);
        _file.Exists(_mergeMessagePath).Returns(false);

        (await _manager.GetMergeOrCommitMessageAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();

        _manager.IsMergeCommit.Should().BeFalse();
    }

    [Fact]
    public async Task GetMergeOrCommitMessageAsync_should_return_overridden_if_exist_and_if_no_file_exists()
    {
        SetupExtra(_overriddenCommitMessage);
        _file.Exists(_commitMessagePath).Returns(false);
        _file.Exists(_mergeMessagePath).Returns(false);

        (await _manager.GetMergeOrCommitMessageAsync(TestContext.Current.CancellationToken)).Should()
            .Be(_overriddenCommitMessage);

        _manager.IsMergeCommit.Should().BeFalse();
    }

    [Fact]
    public async Task SetMergeOrCommitMessageAsync_should_write_merge_message_if_exists()
    {
        var correctlyWritten = false;
        _file.When(x =>
                x.WriteAllTextAsync(_mergeMessagePath, _newMessage, _encoding, TestContext.Current.CancellationToken))
            .Do(_ => correctlyWritten = true);
        _file.Exists(_commitMessagePath).Returns(true);
        _file.Exists(_mergeMessagePath).Returns(true);
        _directory.Exists(Path.GetDirectoryName(_commitMessagePath)).Returns(true);

        await _manager.SetMergeOrCommitMessageAsync(_newMessage, TestContext.Current.CancellationToken);

        correctlyWritten.Should().BeTrue();
    }

    [Fact]
    public async Task
        SetMergeOrCommitMessageAsync_should_not_write_merge_message_if_exist_if_it_is_the_overridding_commitmessage_exists()
    {
        SetupExtra(_overriddenCommitMessage);
        var hasBeenWritten = false;
        _file.When(x =>
                x.WriteAllTextAsync(_mergeMessagePath, _newMessage, _encoding, TestContext.Current.CancellationToken))
            .Do(_ => hasBeenWritten = true);
        _file.Exists(_commitMessagePath).Returns(true);
        _file.Exists(_mergeMessagePath).Returns(true);

        await _manager.SetMergeOrCommitMessageAsync(_overriddenCommitMessage, TestContext.Current.CancellationToken);

        hasBeenWritten.Should().BeFalse();
    }

    [Fact]
    public async Task SetMergeOrCommitMessageAsync_should_write_commit_message_if_exists_and_no_merge_message()
    {
        var correctlyWritten = false;
        _file.When(x =>
                x.WriteAllTextAsync(_commitMessagePath, _newMessage, _encoding, TestContext.Current.CancellationToken))
            .Do(_ => correctlyWritten = true);
        _file.Exists(_commitMessagePath).Returns(true);
        _file.Exists(_mergeMessagePath).Returns(false);
        _directory.Exists(Path.GetDirectoryName(_commitMessagePath)).Returns(true);

        await _manager.SetMergeOrCommitMessageAsync(_newMessage, TestContext.Current.CancellationToken);

        correctlyWritten.Should().BeTrue();
    }

    [Fact]
    public async Task SetMergeOrCommitMessageAsync_should_write_commit_message_if_no_file_exists()
    {
        var correctlyWritten = false;
        _file.When(x =>
                x.WriteAllTextAsync(_commitMessagePath, _newMessage, _encoding, TestContext.Current.CancellationToken))
            .Do(_ => correctlyWritten = true);
        _file.Exists(_commitMessagePath).Returns(false);
        _file.Exists(_mergeMessagePath).Returns(false);
        _directory.Exists(Path.GetDirectoryName(_commitMessagePath)).Returns(true);

        await _manager.SetMergeOrCommitMessageAsync(_newMessage, TestContext.Current.CancellationToken);

        correctlyWritten.Should().BeTrue();
    }

    [Fact]
    public async Task SetMergeOrCommitMessageAsync_should_write_empty_message_if_null()
    {
        var correctlyWritten = false;
        _file.When(x =>
                x.WriteAllTextAsync(_commitMessagePath, string.Empty, _encoding, TestContext.Current.CancellationToken))
            .Do(_ => correctlyWritten = true);
        _file.Exists(_commitMessagePath).Returns(false);
        _file.Exists(_mergeMessagePath).Returns(false);
        _directory.Exists(Path.GetDirectoryName(_commitMessagePath)).Returns(true);

        await _manager.SetMergeOrCommitMessageAsync(message: null, TestContext.Current.CancellationToken);

        correctlyWritten.Should().BeTrue();
    }

    [Fact]
    public async Task SetMergeOrCommitMessageAsync_should_not_write_if_dir_no_longer_exists()
    {
        var correctlyWritten = false;
        _file.When(x =>
                x.WriteAllTextAsync(_commitMessagePath, string.Empty, _encoding, TestContext.Current.CancellationToken))
            .Do(_ => correctlyWritten = true);
        _file.Exists(_commitMessagePath).Returns(false);
        _file.Exists(_mergeMessagePath).Returns(false);
        _directory.Exists(Path.GetDirectoryName(_commitMessagePath)).Returns(false);

        await _manager.SetMergeOrCommitMessageAsync(message: null, TestContext.Current.CancellationToken);

        correctlyWritten.Should().BeFalse();
    }

    [Fact]
    public async Task ResetCommitMessageAsync()
    {
        var deletedA = false;
        var deletedC = false;
        var deletedM = false;
        _file.When(x => x.Delete(_amendSaveStatePath)).Do(_ => deletedA = true);
        _file.When(x => x.Delete(_commitMessagePath)).Do(_ => deletedC = true);
        _file.When(x => x.Delete(_mergeMessagePath)).Do(_ => deletedM = true);

        await _manager.ResetCommitMessageAsync();

        deletedA.Should().BeTrue();
        deletedC.Should().BeTrue();
        deletedM.Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(FormatCommitMessageTestData.FormatCommitMessageTestCases),
        MemberType = typeof(FormatCommitMessageTestData))]
    public void FormatCommitMessage(
        string commitMessageText, bool usingCommitTemplate, bool ensureCommitMessageSecondLineEmpty,
        string expectedMessage)
    {
        CommitMessageManager
            .FormatCommitMessage(commitMessageText, usingCommitTemplate, ensureCommitMessageSecondLineEmpty)
            .Should().Be(expectedMessage);
    }

    public class FormatCommitMessageTestData
    {
        private static readonly string _nl = Environment.NewLine;

        public static IEnumerable<object?[]> FormatCommitMessageTestCases
        {
            get
            {
                // string commitMessageText, bool usingCommitTemplate, bool ensureCommitMessageSecondLineEmpty, string expectedMessage
                yield return new object?[] { null, false, false, "" };
                yield return new object?[] { null, true, false, "" };
                yield return new object?[] { null, false, true, "" };
                yield return new object?[] { null, true, true, "" };
                yield return new object?[] { "", false, false, "" };
                yield return new object?[] { "", true, false, "" };
                yield return new object?[] { "", false, true, "" };
                yield return new object?[] { "", true, true, "" };
                yield return new object?[] { "\n", false, false, _nl + _nl };
                yield return new object?[] { "\n", true, false, _nl + _nl };
                yield return new object?[] { "\n", false, true, _nl + _nl };
                yield return new object?[] { "\n", true, true, _nl + _nl };
                yield return new object?[] { "1", true, false, "1" + _nl };
                yield return new object?[] { "#1", false, false, "#1" + _nl };
                yield return new object?[] { "#1", true, false, "" };
                yield return new object?[] { "1\n\n3", false, false, "1" + _nl + _nl + "3" + _nl };
                yield return new object?[] { "1\n\n3", false, true, "1" + _nl + _nl + "3" + _nl };
                yield return new object?[] { "1\n2\n3", false, false, "1" + _nl + "2" + _nl + "3" + _nl };
                yield return new object?[] { "1\n2\n3", false, true, "1" + _nl + _nl + "2" + _nl + "3" + _nl };
                yield return new object?[] { "#0\n1\n\n3", true, false, "1" + _nl + _nl + "3" + _nl };
                yield return new object?[] { "#0\n1\n\n3", true, true, "1" + _nl + _nl + "3" + _nl };
                yield return new object?[] { "#0\n1\n2\n3", true, false, "1" + _nl + "2" + _nl + "3" + _nl };
                yield return new object?[] { "#0\n1\n2\n3", true, true, "1" + _nl + _nl + "2" + _nl + "3" + _nl };
                yield return new object?[] { "#0\n1\n#0\n2\n3", true, true, "1" + _nl + _nl + "2" + _nl + "3" + _nl };
                yield return new object?[]
                {
                    "1\n2\n3\n4\n5\n\n7\n\n\n10", true, true,
                    "1" + _nl + _nl + "2" + _nl + "3" + _nl + "4" + _nl + "5" + _nl + _nl + "7" + _nl + _nl + _nl +
                    "10" + _nl
                };
                yield return new object?[]
                {
                    "1\n2\n3\n4\n5\n\n7\n\n\n10", false, true,
                    "1" + _nl + _nl + "2" + _nl + "3" + _nl + "4" + _nl + "5" + _nl + _nl + "7" + _nl + _nl + _nl +
                    "10" + _nl
                };
                yield return new object?[]
                {
                    "1\n2\n3\n4\n5\n\n7\n\n\n10\n", false, true,
                    "1" + _nl + _nl + "2" + _nl + "3" + _nl + "4" + _nl + "5" + _nl + _nl + "7" + _nl + _nl + _nl +
                    "10" + _nl + _nl
                };
            }
        }
    }
}
