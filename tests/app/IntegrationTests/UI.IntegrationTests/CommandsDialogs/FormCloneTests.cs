using CommonTestUtils;
using GitUI;
using GitUI.CommandsDialogs;
using Xunit;

namespace GitExtensions.UITests.CommandsDialogs;

public class FormCloneTests : System.IDisposable
{
    // Created once for the fixture
    private ReferenceRepository _referenceRepository = null!;

    // Created once for each test
    private GitUICommands _commands = null!;
    public FormCloneTests()
    {
        _referenceRepository = new ReferenceRepository();
        _commands = new GitUICommands(GlobalServiceContainer.CreateDefaultMockServiceContainer(), _referenceRepository.Module);
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
    [InlineData("git clone https://github.com/gitextensions/gitextensions && cd gitextensions", true, "https://github.com/gitextensions/gitextensions")]
    [InlineData("git clone ssh://username@gerrit-server:/PROJECT", true, "ssh://username@gerrit-server:/PROJECT")]
    [InlineData("git clone https://github.com/gitextensions/gitextensions && git clone https://github.com/gitextensions/git.hub", true, "https://github.com/gitextensions/gitextensions")]
    public void Test_Url_extract_from_string(
        string? text, bool expected, string expectedUrl)
    {
        RunFormTest(
            form =>
            {
                FormClone.TestAccessor accessor = form.GetTestAccessor();

                accessor.TryExtractUrl(text!, out string url).Should().Be(expected);

                // No need to compare URL if the result was expected to be false
                if (expected)
                {
                    url.Should().Be(expectedUrl);
                }
            });
    }

    private void RunFormTest(Action<FormClone> testDriver)
    {
        RunFormTest(
            form =>
            {
                testDriver(form);
                return Task.CompletedTask;
            });
    }

    private void RunFormTest(Func<FormClone, Task> testDriverAsync)
    {
        UITest.RunForm(
            () =>
            {
                _commands.StartCloneDialog(owner: null, url: null);
            },
            testDriverAsync);
    }

    public void Dispose()
    {
        DisposeTest();
    }
}
