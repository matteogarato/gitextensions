using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BugReporter.Serialization;

namespace BugReporterTests;
public sealed partial class SerializableExceptionTests : IDisposable
{
    private readonly CultureInfo _previousCulture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _previousUICulture = CultureInfo.CurrentUICulture;

    public SerializableExceptionTests()
    {
        CultureInfo testCulture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentCulture = testCulture;
        CultureInfo.CurrentUICulture = testCulture;
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _previousCulture;
        CultureInfo.CurrentUICulture = _previousUICulture;
    }

    [GeneratedRegex(@"^(?<keep>.*)(?<codeLocationToBeRemoved>\sin\s.*)$", RegexOptions.ExplicitCapture)]
    private static partial Regex PathRegex { get; }

    [Theory]
    [MemberData(nameof(TestCases))]
    public async Task ToString_should_return_expected_details(string testName)
    {
        string message = string.Empty;
        try
        {
            ThrowException(testName);
        }
        catch (Exception ex)
        {
            message = new SerializableException(ex).ToString();
        }

        await Verifier.Verify(Sanitize(message))
            .UseParameters(testName);
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public async Task ToString_should_be_same_from_round_trip(string testName)
    {
        string xml = string.Empty;
        try
        {
            ThrowException(testName);
        }
        catch (Exception ex)
        {
            xml = new SerializableException(ex).ToXmlString();
        }

        SerializableException exception = SerializableException.FromXmlString(xml);

        await Verifier.Verify(Sanitize(exception.ToString()))
            .UseParameters(testName);
    }

    public static IEnumerable<object[]> TestCases
    {
        get
        {
            yield return new object[] { nameof(Code.SimpleException) };
            yield return new object[] { nameof(Code.NestedException) };
        }
    }

    private static void ThrowException(string testName)
    {
        switch (testName)
        {
            case nameof(Code.SimpleException):
                Code.SimpleException();
                break;
            case nameof(Code.NestedException):
                Code.DoubleNestedException();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(testName));
        }
    }

    private static string Sanitize(string exceptionMessage)
    {
        // message contains physical file paths, which are machine specific
        StringBuilder m = new();
        foreach (string line in exceptionMessage.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
        {
            m.AppendLine(PathRegex.Replace(line, "${keep}"));
        }

        return m.ToString();
    }

    private static class Code
    {
        public static void SimpleException()
        {
            throw new DivideByZeroException("BOOM!");
        }

        public static void NestedException()
        {
            try
            {
                SimpleException();
            }
            catch (Exception ex)
            {
                throw new ApplicationException("BAM!", ex);
            }
        }

        public static void DoubleNestedException()
        {
            try
            {
                NestedException();
            }
            catch (Exception ex)
            {
                throw new Exception("OPPS!", ex);
            }
        }
    }
}
