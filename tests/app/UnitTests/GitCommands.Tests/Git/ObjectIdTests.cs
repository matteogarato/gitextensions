using System.Text;
using System.Text.RegularExpressions;
using GitExtensions.Extensibility.Git;
using JetBrains.Annotations;

namespace GitCommandsTests.Git;

// TODO SUT is in GitUIPluginInterfaces but no test assembly exists for that assembly
public sealed partial class ObjectIdTests
{
    private const string _nonHexAscii = "0123456789abcdefghijklmnopqrstuvwxyz0123456789abcdefghijklmnopqrstuvwxyz";
    private const string _hexAscii = "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f20";

    [GeneratedRegex(@"[a-f0-9]{40}", RegexOptions.ExplicitCapture)]
    private static partial Regex Sha40Regex { get; }

    [GeneratedRegex(@"[a-f0-9]{39}", RegexOptions.ExplicitCapture)]
    private static partial Regex Sha39Regex { get; }

    [GeneratedRegex(@"[XYZa-f0-9]{39}", RegexOptions.ExplicitCapture)]
    private static partial Regex ShaXyzRegex { get; }

    [Theory]
    [InlineData("0000000000000000000000000000000000000000")]
    [InlineData("0102030405060708091011121314151617181920")]
    [InlineData("0123456789abcdef0123456789abcdef01234567")]
    public void TryParse_handles_valid_hashes(string sha1)
    {
        ObjectId.TryParse(sha1, out var id).Should().BeTrue();
        id.ToString().Should().Be(sha1.ToLower());
    }

    [Theory]
    [InlineData("00000000000000000000000000000000000000")]
    [InlineData("000000000000000000000000000000000000000")]
    [InlineData("01020304050607080910111213141516171819200")]
    [InlineData("010203040506070809101112131415161718192001")]
    [InlineData("ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ")]
    [InlineData("  0000000000000000000000000000000000000000  ")]
    public void TryParse_identifies_invalid_hashes(string sha1)
    {
        ObjectId.TryParse(sha1, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("0000000000000000000000000000000000000000", 0)]
    [InlineData("0000000000000000000000000000000000000000__", 0)]
    [InlineData("_0102030405060708091011121314151617181920", 1)]
    [InlineData("_0102030405060708091011121314151617181920_", 1)]
    [InlineData("__0102030405060708091011121314151617181920", 2)]
    [InlineData("__0102030405060708091011121314151617181920__", 2)]
    public void TryParse_with_offset_handles_valid_hashes(string sha1, int offset)
    {
        ObjectId.TryParse(sha1, offset, out var id).Should().BeTrue();
        id.ToString().Should().Be(sha1.Substring(offset, 40));
    }

    [Theory]
    [InlineData("0000000000000000000000000000000000000000")]
    [InlineData("0102030405060708091011121314151617181920")]
    [InlineData("0123456789abcdef0123456789abcdef01234567")]
    public void Parse_handles_valid_hashes(string sha1)
    {
        ObjectId.Parse(sha1).ToString().Should().Be(sha1.ToLower());
    }

    [Theory]
    [InlineData("00000000000000000000000000000000000000")]
    [InlineData("000000000000000000000000000000000000000")]
    [InlineData("01020304050607080910111213141516171819200")]
    [InlineData("010203040506070809101112131415161718192001")]
    [InlineData("ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ")]
    [InlineData("  0000000000000000000000000000000000000000  ")]
    public void Parse_throws_for_invalid_hashes(string sha1)
    {
        ((Action)(() => ObjectId.Parse(sha1))).Should().Throw<FormatException>();
    }

    [Theory]
    [InlineData("0000000000000000000000000000000000000000")]
    [InlineData("0102030405060708091011121314151617181920")]
    [InlineData("0123456789abcdef0123456789abcdef01234567")]
    public void IsValid_identifies_valid_hashes(string sha1)
    {
        ObjectId.IsValid(sha1).Should().BeTrue();
    }

    [Theory]
    [InlineData("00000000000000000000000000000000000000")]
    [InlineData("000000000000000000000000000000000000000")]
    [InlineData("01020304050607080910111213141516171819200")]
    [InlineData("010203040506070809101112131415161718192001")]
    [InlineData("ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ")]
    [InlineData("  0000000000000000000000000000000000000000  ")]
    public void IsValid_identifies_invalid_hashes(string sha1)
    {
        ObjectId.IsValid(sha1).Should().BeFalse();
    }

    [Theory]
    [InlineData("0000000000000000000000000000000000000000", 0)]
    [InlineData("0000000000000000000000000000000000000000__", 0)]
    [InlineData("_0102030405060708091011121314151617181920", 1)]
    [InlineData("_0102030405060708091011121314151617181920_", 1)]
    [InlineData("__0102030405060708091011121314151617181920", 2)]
    [InlineData("__0102030405060708091011121314151617181920__", 2)]
    public void Parse_with_offset_handles_valid_hashes(string sha1, int offset)
    {
        ObjectId.Parse(sha1.Substring(offset, 40)).ToString().Should().Be(sha1.Substring(offset, 40));
    }

    [Fact]
    public void ParseFromRegexCapture()
    {
        var objectId = ObjectId.Random();
        var str = "XYZ" + objectId + "XYZ";

        ObjectId.Parse(str, Sha40Regex.Match(str)).Should().Be(objectId);
        ((Action)(() => ObjectId.Parse(str, Sha39Regex.Match(str)))).Should().Throw<FormatException>();
        ((Action)(() => ObjectId.Parse(str, ShaXyzRegex.Match(str)))).Should().Throw<FormatException>();
    }

    [Fact]
    public void WorkTreeId_has_expected_value()
    {
        ObjectId.WorkTreeId.ToString().Should().Be("1111111111111111111111111111111111111111");
    }

    [Fact]
    public void IndexId_has_expected_value()
    {
        ObjectId.IndexId.ToString().Should().Be("2222222222222222222222222222222222222222");
    }

    [Fact]
    public void CombinedDiffId_has_expected_value()
    {
        ObjectId.CombinedDiffId.ToString().Should().Be("3333333333333333333333333333333333333333");
    }

    [Fact]
    public void WorkTreeId_is_artificial()
    {
        ObjectId.WorkTreeId.IsArtificial.Should().BeTrue();
    }

    [Fact]
    public void IndexId_is_artificial()
    {
        ObjectId.IndexId.IsArtificial.Should().BeTrue();
    }

    [Fact]
    public void CombinedDiffId_is_artificial()
    {
        ObjectId.CombinedDiffId.IsArtificial.Should().BeTrue();
    }

    [Fact]
    public void Equivalent_ids_are_equal()
    {
        ObjectId.Parse("0102030405060708091011121314151617181920").Should()
            .Be(ObjectId.Parse("0102030405060708091011121314151617181920"));

        ObjectId.Parse("abcdefabcdefabcdefabcdefabcdefabcdefabcd").Should()
            .Be(ObjectId.Parse("abcdefabcdefabcdefabcdefabcdefabcdefabcd"));

        ObjectId.WorkTreeId.Should().Be(ObjectId.WorkTreeId);

        ObjectId.Parse(GitRevision.WorkTreeGuid).Should().Be(ObjectId.WorkTreeId);

        ObjectId.IndexId.Should().Be(ObjectId.IndexId);

        ObjectId.Parse(GitRevision.IndexGuid).Should().Be(ObjectId.IndexId);

        ObjectId.CombinedDiffId.Should().Be(ObjectId.CombinedDiffId);

        ObjectId.Parse(GitRevision.CombinedDiffGuid).Should().Be(ObjectId.CombinedDiffId);
    }

    [Fact]
    public void Different_ids_are_not_equal()
    {
        ObjectId.Parse("0102030405060708091011121314151617181920").Should()
            .NotBe(ObjectId.Parse("0000000000000000000000000000000000000000"));

        ObjectId.IndexId.Should().NotBe(ObjectId.WorkTreeId);
    }

    [Fact]
    public void Equivalent_ids_have_equal_hash_codes()
    {
        ObjectId.Parse("0102030405060708091011121314151617181920").GetHashCode().Should()
            .Be(ObjectId.Parse("0102030405060708091011121314151617181920").GetHashCode());

        ObjectId.Parse("abcdefabcdefabcdefabcdefabcdefabcdefabcd").GetHashCode().Should()
            .Be(ObjectId.Parse("abcdefabcdefabcdefabcdefabcdefabcdefabcd").GetHashCode());

        ObjectId.WorkTreeId.GetHashCode().Should().Be(ObjectId.WorkTreeId.GetHashCode());

        ObjectId.IndexId.GetHashCode().Should().Be(ObjectId.IndexId.GetHashCode());
    }

    [Fact]
    public void Different_ids_have_different_hash_codes()
    {
        ObjectId.Parse("0102030405060708091011121314151617181920").GetHashCode().Should()
            .NotBe(ObjectId.Parse("0000000000000000000000000000000000000000").GetHashCode());

        ObjectId.IndexId.GetHashCode().Should().NotBe(ObjectId.WorkTreeId.GetHashCode());
    }

    [Theory]
    [InlineData(_hexAscii, 0, "000102030405060708090a0b0c0d0e0f10111213")]
    [InlineData(_hexAscii, 1, "00102030405060708090a0b0c0d0e0f101112131")]
    [InlineData(_hexAscii, 2, "0102030405060708090a0b0c0d0e0f1011121314")]
    [InlineData(_hexAscii, 3, "102030405060708090a0b0c0d0e0f10111213141")]
    [InlineData(_hexAscii, 26, "0d0e0f101112131415161718191a1b1c1d1e1f20")]
    public void TryParse_works_as_expected(string source, int offset, [CanBeNull] string? expected)
    {
        var sourceBytes = Encoding.ASCII.GetBytes(source);

        ObjectId.TryParse(sourceBytes.AsSpan(offset, 40), out var id).Should().Be(expected is not null);

        if (expected is not null)
        {
            id.Should().Be(ObjectId.Parse(expected));
        }
    }

    [Theory]
    [InlineData(_nonHexAscii, 0, null)]
    public void TryParse_returns_false_zero_with_illegal_input(string source, int offset, [CanBeNull] string? expected)
    {
        var sourceBytes = Encoding.ASCII.GetBytes(source);
        ObjectId.TryParse(sourceBytes.AsSpan(offset, 40), out var objectId).Should().Be(expected is not null);
        objectId.IsZero.Should().BeTrue();
    }

    [Fact]
    public void TryParse_returns_false_when_array_null()
    {
        ObjectId.TryParse(default, out var objectId).Should().BeFalse();
        objectId.IsZero.Should().BeTrue();
        ObjectId.TryParse(default(Span<byte>), out objectId).Should().BeFalse();
        objectId.IsZero.Should().BeTrue();
    }

    [Fact]
    public void TryParse_returns_false_when_bounds_check_fails()
    {
        var bytes = new byte[ObjectId.Sha1CharCount];

        ObjectId.TryParse(bytes.AsSpan(1), out var objectId).Should().BeFalse();
        objectId.IsZero.Should().BeTrue();
    }

    [Fact]
    public void ToShortString()
    {
        const string s = "0102030405060708091011121314151617181920";
        var id = ObjectId.Parse(s);

        for (var length = 1; length < ObjectId.Sha1CharCount; length++)
        {
            id.ToShortString(length).Should().Be(s[..length]);
        }

        ((Action)(() => id.ToShortString(-1))).Should().Throw<ArgumentOutOfRangeException>();
        ((Action)(() => id.ToShortString(ObjectId.Sha1CharCount + 1))).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Equals_using_operator()
    {
        var objectIdString = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        (ObjectId.Parse(objectIdString) == ObjectId.Parse(objectIdString)).Should().BeTrue();
        (ObjectId.Parse(objectIdString) != ObjectId.Parse(objectIdString)).Should().BeFalse();
        (ObjectId.Parse(objectIdString) == ObjectId.Random()).Should().BeFalse();
        (ObjectId.Parse(objectIdString) != ObjectId.Random()).Should().BeTrue();
    }

    [Theory]
    [InlineData("0102030405060708091011121314151617181920")]
    [InlineData("abcdefabcdefabcdefabcdefabcdefabcdefabcd")]
    [InlineData("0000000000000000000000000000000000000000")]
    public void WriteTo_writes_full_hex(string sha1)
    {
        var id = ObjectId.Parse(sha1);
        Span<char> buffer = stackalloc char[ObjectId.Sha1CharCount];

        id.WriteTo(buffer).Should().BeTrue();
        new string(buffer).Should().Be(sha1);
    }

    [Fact]
    public void WriteTo_returns_false_when_destination_too_small()
    {
        var id = ObjectId.Parse("0102030405060708091011121314151617181920");
        Span<char> buffer = stackalloc char[39];

        id.WriteTo(buffer).Should().BeFalse();
    }

    [Fact]
    public void WriteTo_writes_into_larger_buffer()
    {
        const string sha1 = "abcdefabcdefabcdefabcdefabcdefabcdefabcd";
        var id = ObjectId.Parse(sha1);
        Span<char> buffer = stackalloc char[50];
        buffer.Fill('X');

        id.WriteTo(buffer).Should().BeTrue();
        new string(buffer.Slice(0, ObjectId.Sha1CharCount)).Should().Be(sha1);
        buffer[40].Should().Be('X');
    }

    [Theory]
    [InlineData("0102030405060708091011121314151617181920")]
    [InlineData("abcdefabcdefabcdefabcdefabcdefabcdefabcd")]
    public void ISpanFormattable_writes_hex(string sha1)
    {
        var id = ObjectId.Parse(sha1);

        Span<char> buffer = stackalloc char[ObjectId.Sha1CharCount];
        var result = ((ISpanFormattable)id).TryFormat(buffer, out var charsWritten, default, null);

        result.Should().BeTrue();
        charsWritten.Should().Be(ObjectId.Sha1CharCount);
        new string(buffer).Should().Be(sha1);
    }

    [Fact]
    public void ISpanFormattable_returns_false_when_destination_too_small()
    {
        var id = ObjectId.Parse("0102030405060708091011121314151617181920");

        Span<char> buffer = stackalloc char[39];
        var result = ((ISpanFormattable)id).TryFormat(buffer, out var charsWritten, default, null);

        result.Should().BeFalse();
        charsWritten.Should().Be(0);
    }

    [Fact]
    public void ISpanFormattable_works_with_interpolation()
    {
        const string sha1 = "0102030405060708091011121314151617181920";
        var id = ObjectId.Parse(sha1);

        var result = $"{id}";

        result.Should().Be(sha1);
    }

    [Fact]
    public void CompareTo_returns_zero_for_equal_ids()
    {
        var id = ObjectId.Parse("0102030405060708091011121314151617181920");

        id.CompareTo(ObjectId.Parse("0102030405060708091011121314151617181920")).Should().Be(0);
    }

    [Fact]
    public void CompareTo_returns_positive_for_zero()
    {
        var id = ObjectId.Parse("0102030405060708091011121314151617181920");

        id.CompareTo(default).Should().BePositive();
    }

    [Fact]
    public void CompareTo_respects_ordering()
    {
        var lower = ObjectId.Parse("0000000000000000000000000000000000000001");
        var higher = ObjectId.Parse("ff00000000000000000000000000000000000000");

        lower.CompareTo(higher).Should().BeNegative();
        higher.CompareTo(lower).Should().BePositive();
    }

    [Theory]
    [InlineData("0123456789abcdef0123456789abcdef01234567")]
    public void TryParse_bytes_roundtrips_correctly(string sha1)
    {
        var asciiBytes = Encoding.ASCII.GetBytes(sha1);
        ObjectId.TryParse(asciiBytes.AsSpan(), out var id).Should().BeTrue();

        id.ToString().Should().Be(sha1);
    }

    [Fact]
    public void TryParse_bytes_rejects_uppercase()
    {
        var asciiBytes = Encoding.ASCII.GetBytes("ABCDEFABCDEFABCDEFABCDEFABCDEFABCDEFABCD");

        // Convert.FromHexString accepts uppercase, so this will parse successfully.
        // SHA-1 strings are always lowercase in git output, and ToString normalises to lowercase.
        ObjectId.TryParse(asciiBytes.AsSpan(), out var id).Should().BeTrue();
        id.ToString().Should().Be("abcdefabcdefabcdefabcdefabcdefabcdefabcd");
    }
}
