using GitUI.UserManual;
using AppSettings = GitCommands.Settings.AppSettings;

namespace GitUITests.UserManual;

public class StandardHtmlUserManualFixture
{
    [Theory]
    [InlineData(null, null,
        "https://git-extensions-documentation.readthedocs.org/en/main/")] // both null makes no sense atm
    [InlineData("merge_conflicts", null,
        "https://git-extensions-documentation.readthedocs.org/en/main/merge_conflicts.html")]
    [InlineData("merge_conflicts", "merge-conflicts",
        "https://git-extensions-documentation.readthedocs.org/en/main/merge_conflicts.html#merge-conflicts")]
    public void GetUrl(string? subFolder, string? anchor, string expected)
    {
        AppSettings.GetTestAccessor().ResetDocumentationBaseUrl();
        AppSettings.SetDocumentationBaseUrl("33.33.33");

        StandardHtmlUserManual sut = new(subFolder!, anchor!);

        sut.GetUrl().Should().Be(expected);
    }
}
