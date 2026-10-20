using BugReporter;

namespace GitUITests;
public class BugReportFormTests
{
    [Theory]
    [InlineData("", false)]
    [InlineData("\t\r\n\t\t   \r   \n   \r", false)]
    [InlineData("\t\r\n\t\t  a \r   \n   \r", true)]
    public void Test(string input, bool expected)
    {
        BugReportForm.TestAccessor.CheckContainsInfo(input).Should().Be(expected);
    }
}
