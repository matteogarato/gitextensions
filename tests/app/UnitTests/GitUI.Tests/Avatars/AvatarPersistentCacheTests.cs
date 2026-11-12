using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using GitCommands.Settings;
using GitUI.Avatars;
using NSubstitute;
using AppSettings = GitCommands.Settings.AppSettings;

namespace GitUITests.Avatars;

public sealed class AvatarPersistentCacheTests : AvatarCacheTestBase
{
    private readonly string _avatarImageCachePath = AppSettings.AvatarImageCachePath;
    private readonly DirectoryBase _directory = null!;
    private readonly string _email1AvatarPath = null!;
    private readonly FileBase _file = null!;
    private readonly FileInfoBase _fileInfo = null!;
    private readonly IFileInfoFactory _fileInfoFactory = null!;
    private readonly IFileSystem _fileSystem = null!;

    public AvatarPersistentCacheTests()
    {
        _fileSystem = Substitute.For<IFileSystem>();
        _directory = Substitute.For<DirectoryBase>();
        _fileSystem.Directory.Returns(_directory);
        _file = Substitute.For<FileBase>();
        _fileSystem.File.Returns(_file);
        _fileInfo = Substitute.For<FileInfoBase>();
        _fileInfo.Exists.Returns(true);
        _fileInfoFactory = Substitute.For<IFileInfoFactory>();
        _fileInfoFactory.New(Arg.Any<string>()).Returns(_fileInfo);
        _fileSystem.FileInfo.Returns(_fileInfoFactory);

        AppSettings.AvatarProvider = AvatarProvider.Default;

        Cache = new FileSystemAvatarCache(Inner, _fileSystem);
        _email1AvatarPath = Path.Combine(_avatarImageCachePath, $"{Email1}.{Size}px.png");
    }

    [Fact]
    public async Task GetAvatarAsync_should_create_if_folder_absent()
    {
        MockFileSystem fileSystem = new();
        fileSystem.Directory.Exists(_avatarImageCachePath).Should().BeFalse();
        Cache = new FileSystemAvatarCache(Inner, fileSystem);

        (await Cache.GetAvatarAsync(Email1, Name1, Size)).Should().BeSameAs(Img1);

        fileSystem.Directory.Exists(_avatarImageCachePath).Should().BeTrue();
    }

    [Fact]
    public async Task GetAvatarAsync_should_create_image_from_stream()
    {
        MockFileSystem fileSystem = new();
        fileSystem.Directory.Exists(_avatarImageCachePath).Should().BeFalse();
        Cache = new FileSystemAvatarCache(Inner, fileSystem);

        (await Cache.GetAvatarAsync(Email1, Name1, Size)).Should().BeSameAs(Img1);

        fileSystem.Directory.Exists(_avatarImageCachePath).Should().BeTrue();
        fileSystem.File.Exists(_email1AvatarPath).Should().BeTrue();
    }

    [Fact]
    public async Task GetAvatarAsync_uses_inner_if_file_expired()
    {
        _fileInfo.Exists.Returns(true);
        _fileInfo.LastWriteTime.Returns(new DateTime(2010, 1, 1));
        _fileSystem.File.OpenWrite(Arg.Any<string>()).Returns(_ => (Stream)new MemoryStream());
        _fileSystem.File.Delete(Arg.Any<string>());

        await MissAsync(Email1, Name1);

        _fileSystem.File.Received(1).Delete(_email1AvatarPath);

        _file.OpenRead(Arg.Any<string>()).Returns(c => GetPngStream());
        _fileInfo.LastWriteTime.Returns(DateTime.Now);
        _fileSystem.ClearReceivedCalls();
        _fileInfo.ClearReceivedCalls();
        _file.ClearReceivedCalls();

        var image = await Cache.GetAvatarAsync(Email1, Name1, 16);

        image.Should().NotBeNull();
        _ = _fileInfo.Received(1).LastWriteTime;

        _fileSystem.File.Received(1).OpenRead(_email1AvatarPath);
    }

    [Fact]
    public async Task ClearCacheAsync_should_return_if_folder_absent()
    {
        _directory.Exists(Arg.Any<string>()).Returns(false);

        await CacheCleaner.ClearCacheAsync();

        _directory.DidNotReceive().GetFiles(Arg.Any<string>());
    }

    [Fact]
    public async Task ClearCacheAsync_should_remove_all()
    {
        MockFileSystem fileSystem = new();
        Cache = new FileSystemAvatarCache(Inner, fileSystem);

        fileSystem.AddFile(Path.Combine(_avatarImageCachePath, "a@a.com.16px.png"), new MockFileData(""));
        fileSystem.AddFile(Path.Combine(_avatarImageCachePath, "b@b.com.16px.png"), new MockFileData(""));
        fileSystem.AllFiles.Should().HaveCount(2);

        await CacheCleaner.ClearCacheAsync();

        fileSystem.AllFiles.Should().BeEmpty();
    }

    [Fact]
    public void ClearCacheAsync_should_ignore_errors()
    {
        _directory.Exists(Arg.Any<string>()).Returns(true);
        _directory.GetFiles(_avatarImageCachePath).Returns(["c:\\file.txt", "boot.sys"]);
        _file.When(x => x.Delete(Arg.Any<string>()))
            .Do(x => throw new DivideByZeroException());

        var act = () => CacheCleaner.ClearCacheAsync();
        act.Should().NotThrowAsync();
    }
}
