using CommonTestUtils;
using GitUI;
using GitUI.CommandsDialogs;
using Xunit;

namespace GitExtensions.UITests.CommandsDialogs;

public class FormInitTests : System.IDisposable
{
    // Created once for the fixture
    private ReferenceRepository _referenceRepository = null!;

    // Created once for each test
    private GitUICommands _commands = null!;
    public FormInitTests()
    {
        _referenceRepository = new ReferenceRepository();
        _commands = new GitUICommands(GlobalServiceContainer.CreateDefaultMockServiceContainer(), _referenceRepository.Module);
    }

    private void DisposeTest()
    {
        _referenceRepository.Dispose();
    }

    [WinFormsFact]
    public void Should_show_supplied_path()
    {
        string currentDir = "bla";
        RunFormTest(
            form =>
            {
                form.GetTestAccessor().DirectoryCombo.Text.Should().Be(currentDir);
            },
            currentDir);
    }

    // Strictly speaking this is a test for GitUICommands.StartInitializeDialog
    [WinFormsFact]
    public void Should_show_current_GitModuleWorkingDir_if_supplied_path_null()
    {
        RunFormTest(
            form =>
            {
                form.GetTestAccessor().DirectoryCombo.Text.Should().Be(_referenceRepository.Module.WorkingDir);
            },
            null!);
    }

    [WinFormsTheory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("    ")]
    [InlineData(@"foo\bar")]
    public void IsRootedDirectoryPath_should_detect_invalid_paths(string? input)
    {
        string currentDir = "bla";
        RunFormTest(
            form =>
            {
                form.GetTestAccessor().IsRootedDirectoryPath(input!).Should().BeFalse();
            },
            currentDir);
    }

    [WinFormsTheory]
    [InlineData(@"c:\foo\bar")]
    [InlineData(@"c:\foo\bar\")]
    [InlineData(@"c:")]
    [InlineData(@"  c:\foo\bar  ")]
    public void IsRootedDirectoryPath_returns_true_on_valid_paths(string? input)
    {
        string currentDir = "bla";
        RunFormTest(
            form =>
            {
                form.GetTestAccessor().IsRootedDirectoryPath(input!).Should().BeTrue();
            },
            currentDir);
    }

    private void RunFormTest(Action<FormInit> testDriver, string path)
    {
        RunFormTest(
            form =>
            {
                testDriver(form);
                return Task.CompletedTask;
            },
            path);
    }

    private void RunFormTest(Func<FormInit, Task> testDriverAsync, string path)
    {
        UITest.RunForm(
            () =>
            {
                _commands.StartInitializeDialog(owner: null, path).Should().BeTrue();
            },
            testDriverAsync);
    }

    public void Dispose()
    {
        DisposeTest();
    }
}
