using CommonTestUtils;
using GitUI;
using GitUI.CommandsDialogs;

namespace UITests.CommandsDialogs;

public class FormInitTests : IDisposable
{
    // Created once for each test
    private readonly GitUiCommands _commands = null!;

    // Created once for the fixture
    private readonly ReferenceRepository _referenceRepository = null!;

    public FormInitTests()
    {
        _referenceRepository = new ReferenceRepository();
        _commands = new GitUiCommands(GlobalServiceContainer.CreateDefaultMockServiceContainer(),
            _referenceRepository.Module);
    }

    void IDisposable.Dispose()
    {
        DisposeTest();
    }

    private void DisposeTest()
    {
        _referenceRepository.Dispose();
    }

    [WinFormsFact]
    public void Should_show_supplied_path()
    {
        var currentDir = "bla";
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
        var currentDir = "bla";
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
        var currentDir = "bla";
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
        UiTest.RunForm(
            () =>
            {
                _commands.StartInitializeDialog(owner: null, path).Should().BeTrue();
            },
            testDriverAsync);
    }
}
