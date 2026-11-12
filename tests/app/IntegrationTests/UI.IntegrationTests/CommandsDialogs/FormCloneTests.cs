using CommonTestUtils;
using GitUI;
using GitUI.CommandsDialogs;

namespace UITests.CommandsDialogs;

public class FormCloneTests : IDisposable
{
    // Created once for each test
    private readonly GitUiCommands _commands = null!;

    // Created once for the fixture
    private readonly ReferenceRepository _referenceRepository = null!;

    public FormCloneTests()
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

    [WinFormsTheory]
    [InlineData(null, false, "")]
    [InlineData("", false, "")]
    [InlineData(" ", false, "")]
    [InlineData("blah", false, "")]
    [InlineData("git clone https://github.com/gitextensions/gitextensions && cd gitextensions", true,
        "https://github.com/gitextensions/gitextensions")]
    [InlineData("git clone ssh://username@gerrit-server:/PROJECT", true, "ssh://username@gerrit-server:/PROJECT")]
    [InlineData(
        "git clone https://github.com/gitextensions/gitextensions && git clone https://github.com/gitextensions/git.hub",
        true, "https://github.com/gitextensions/gitextensions")]
    public void Test_Url_extract_from_string(
        string? text, bool expected, string expectedUrl)
    {
        RunFormTest(form =>
        {
            var accessor = form.GetTestAccessor();

            accessor.TryExtractUrl(text!, out var url).Should().Be(expected);

            // No need to compare URL if the result was expected to be false
            if (expected)
            {
                url.Should().Be(expectedUrl);
            }
        });
    }

    private void RunFormTest(Action<FormClone> testDriver)
    {
        RunFormTest(form =>
        {
            testDriver(form);
            return Task.CompletedTask;
        });
    }

    private void RunFormTest(Func<FormClone, Task> testDriverAsync)
    {
        UiTest.RunForm(
            () =>
            {
                _commands.StartCloneDialog(owner: null, url: null);
            },
            testDriverAsync);
    }
}
