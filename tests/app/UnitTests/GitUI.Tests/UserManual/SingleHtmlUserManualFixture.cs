using GitUI.UserManual;

namespace GitUITests.UserManual;

public class SingleHtmlUserManualFixture
{
    [Theory]
    [InlineData((string?)null)]
    [InlineData("merge-conflicts")]
    public void GetUrl(string? anchor)
    {
        SingleHtmlUserManual sut = new(anchor!);

        var expected = SingleHtmlUserManual.Location + "/index.html".Combine("#", anchor);

        sut.GetUrl().Should().Be(expected);
    }
}
