using System.Collections;
using System.IO.Abstractions;
using CommonTestUtils;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using NSubstitute;

namespace GitCommandsTests;

public class CommitTemplateManagerTests
{
    private readonly FileBase _file = null!;
    private readonly IFileSystem _fileSystem = null!;
    private readonly IFullPathResolver _fullPathResolver = null!;
    private readonly CommitTemplateManager _manager = null!;
    private readonly IGitModule _module = null!;
    private readonly string _workingDir = @"c:\dev\repo";

    public CommitTemplateManagerTests()
    {
        _module = Substitute.For<IGitModule>();

        _fullPathResolver = Substitute.For<IFullPathResolver>();

        _file = Substitute.For<FileBase>();
        _fileSystem = Substitute.For<IFileSystem>();
        _fileSystem.File.Returns(_file);
        _manager = new CommitTemplateManager(() => _module, _fullPathResolver, _fileSystem);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void LoadGitCommitTemplate_commit_template_not_defined(string? template)
    {
        _module.GetEffectiveSetting("commit.template").Returns(template);

        _manager.LoadGitCommitTemplate().Should().BeNull();
    }

    [Theory]
    [InlineData("~/template.txt")]
    [InlineData("./template.txt")]
    [InlineData("template.txt")]
    public void LoadGitCommitTemplate_should_throw_if_template_not_found(string? template)
    {
        _module.WorkingDir.Returns(_workingDir);
        _module.GetEffectiveSetting("commit.template").Returns(template);

        ((Action)(() => _manager.LoadGitCommitTemplate())).Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void LoadGitCommitTemplate_should_load_file_content()
    {
        const string relativePath = "./template.txt";
        var fullPath = Path.GetFullPath(Path.Combine(_workingDir, relativePath));

        _module.WorkingDir.Returns(_workingDir);
        _fullPathResolver.Resolve(relativePath).Returns(fullPath);
        _module.GetEffectiveSetting("commit.template").Returns(relativePath);
        _file.Exists(fullPath).Returns(true);
        _file.ReadAllText(fullPath).Returns("line1");

        var content = _manager.LoadGitCommitTemplate();

        content.Should().NotBeEmpty();
    }

    [Fact]
    public void Register_should_not_add_duplicates()
    {
        const string templateName = "template1";
        var count = _manager.RegisteredTemplates.Count();
        _manager.Register(templateName, () => "text1", null);
        _manager.Register(templateName, () => "text2", null);
        _manager.RegisteredTemplates.Should().HaveCount(count + 1);
    }

    [Fact]
    public void RegisteredTemplates_should_be_threadsafe()
    {
        _manager.Register("template1", () => "text1", null);
        _manager.Register("template2", () => "text2", null);
        var i = _manager.RegisteredTemplates.Count() + 1;
        foreach (var managerRegisteredTemplate in _manager.RegisteredTemplates)
        {
            _manager.Unregister(managerRegisteredTemplate.Name);
            _manager.Register($"template{i}", () => "text", null);
            i++;
        }
    }

    [Fact]
    public void RegisteredTemplates_should_be_immutable()
    {
        _manager.Register("template1", () => "text1", null);
        var expectedCount = _manager.RegisteredTemplates.Count();
        expectedCount.Should().BeGreaterThan(0);
        ((IList)_manager.RegisteredTemplates).Clear();
        _manager.RegisteredTemplates.Should().HaveCount(expectedCount);
    }

    [Fact]
    public void RegisteredTemplates_should_be_store_IsRegex()
    {
        var count = _manager.RegisteredTemplates.Count();
        _manager.Register("regexTest", () => "text1", icon: null, isRegex: true);
        _manager.RegisteredTemplates.ElementAt(count).IsRegex.Should().BeTrue();
    }

    [Fact]
    public void RegisteredTemplates_default_IsRegex_should_be_False()
    {
        var count = _manager.RegisteredTemplates.Count();
        _manager.Register("regexTest2", () => "text1", icon: null);
        _manager.RegisteredTemplates.ElementAt(count).IsRegex.Should().BeFalse();
    }

    public class IntegrationTests
    {
        [Fact]
        public void LoadGitCommitTemplate_real_filesystem()
        {
            using GitModuleTestHelper helper = new();
            CommitTemplateManager manager = new(() => helper.Module);

            const string content = "line1\r\nline2\rline3\nline4";

            helper.Module.SetSetting("commit.template", "template.txt");
            helper.CreateRepoFile("template.txt", content);

            var body = manager.LoadGitCommitTemplate();

            body.Should().Be(content);
        }
    }
}
