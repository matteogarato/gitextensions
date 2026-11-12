using System.Text;
using GitCommands.Utils;

namespace GitCommandsTests.Git;

public class EncodingHelperTest
{
    #region Unit tests of single methods

    [Fact]
    public void GetStringEncodingNull()
    {
        ((Action)(() => EncodingHelper.GetString([0x30], [0x31], null!))).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetStringTestSingleLineOutput()
    {
        var asciiString = "abcdefghijklmnop";
        var testBytes = new ASCIIEncoding().GetBytes(asciiString);

        var getString = EncodingHelper.GetString(testBytes, null, Encoding.ASCII);

        getString.Should().Be(asciiString);
    }

    [Fact]
    public void GetStringTestSingleLineError()
    {
        var asciiString = "abcdefghijklmnop";
        var testBytes = new ASCIIEncoding().GetBytes(asciiString);

        var getString = EncodingHelper.GetString(null, testBytes, Encoding.ASCII);

        getString.Should().Be(asciiString);
    }

    [Fact]
    public void GetStringTestOutputAndError()
    {
        var asciiString = "abcdefghijklmnop";
        var testBytesOutput = new ASCIIEncoding().GetBytes(asciiString);
        var testBytesError = new ASCIIEncoding().GetBytes(asciiString.ToUpper());

        var getString = EncodingHelper.GetString(testBytesOutput, testBytesError, Encoding.ASCII);

        getString.Should().Be(asciiString + Environment.NewLine + asciiString.ToUpper());
    }

    [Fact]
    public void GetStringTestUtf8()
    {
        var testBytes = new byte[4];
        testBytes[0] = 0xE3;
        testBytes[1] = 0x82;
        testBytes[2] = 0xA2;

        var utf8String = new UTF8Encoding().GetString(testBytes);

        var getString = EncodingHelper.GetString(null, testBytes, Encoding.UTF8);

        getString.Should().Be(utf8String);
    }

    [Fact]
    public void ConvertToTestEncodingNull()
    {
        ((Action)(() => EncodingHelper.ConvertTo(null!, "abcd"))).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ConvertToTestUtf8()
    {
        var unicodeString = "\u30a2\u30c3";
        var convertedBytes = EncodingHelper.ConvertTo(Encoding.UTF8, unicodeString);

        6.Should().Be(convertedBytes.Length);
        0xE3.Should().Be(convertedBytes[0]);
        0x82.Should().Be(convertedBytes[1]);
        0xA2.Should().Be(convertedBytes[2]);
        0xE3.Should().Be(convertedBytes[3]);
        0x83.Should().Be(convertedBytes[4]);
        0x83.Should().Be(convertedBytes[5]);
    }

    [Fact]
    public void ConvertToTestUtf16()
    {
        var unicodeString = "\u30a2\u30c3";
        var convertedBytes = EncodingHelper.ConvertTo(Encoding.Unicode, unicodeString);

        4.Should().Be(convertedBytes.Length);
        0xA2.Should().Be(convertedBytes[0]);
        0x30.Should().Be(convertedBytes[1]);
        0xC3.Should().Be(convertedBytes[2]);
        0x30.Should().Be(convertedBytes[3]);
    }

    [Fact]
    public void DecodeStringTestEncodingNull()
    {
        Encoding? enc = null;
        ((Action)(() => EncodingHelper.DecodeString([0x30], [0x31], ref enc!))).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DecodeStringTestSingleLineOutput()
    {
        var asciiString = "abcdefghijklmnop";
        var testBytes = new ASCIIEncoding().GetBytes(asciiString);

        Encoding enc = new ASCIIEncoding();

        var decodedString = EncodingHelper.DecodeString(testBytes, null, ref enc);

        decodedString.Should().Be(asciiString);
    }

    [Fact]
    public void DecodeStringTestSingleLineError()
    {
        var asciiString = "abcdefghijklmnop";
        var testBytes = new ASCIIEncoding().GetBytes(asciiString);

        Encoding enc = new ASCIIEncoding();

        var decodedString = EncodingHelper.DecodeString(null, testBytes, ref enc);

        decodedString.Should().Be(asciiString);
    }

    [Fact]
    public void DecodeStringTestOutputAndError()
    {
        var asciiString = "abcdefghijklmnop";
        var testBytesOutput = new ASCIIEncoding().GetBytes(asciiString);
        var testBytesError = new ASCIIEncoding().GetBytes(asciiString.ToUpper());

        Encoding enc = new ASCIIEncoding();

        var decodedString = EncodingHelper.DecodeString(testBytesOutput, testBytesError, ref enc);

        decodedString.Should().Be(asciiString + Environment.NewLine + asciiString.ToUpper());
    }

    [Fact]
    public void DecodeStringTestUtf8()
    {
        var testBytes = new byte[4];
        testBytes[0] = 0xE3;
        testBytes[1] = 0x82;
        testBytes[2] = 0xA2;

        Encoding enc = new UTF8Encoding();

        var utf8String = new UTF8Encoding().GetString(testBytes);

        var decodedString = EncodingHelper.DecodeString(null, testBytes, ref enc);

        decodedString.Should().Be(utf8String);
        enc.Should().Be(new UTF8Encoding());
    }

    // Insert a Test here which checks whether EncodingHelper.DecodeString
    // detects correct encoding (StreamReader.CurrentEncoding).
    // Couldn't find a test so far where StreamReader.CurrentEncoding is different than given Encoding

    #endregion
}
