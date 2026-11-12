using GitCommands;

namespace GitCommandsTests;

public sealed class StreamExtensionsTests
{
    private const byte _nil = 0;

    [Theory]
    [InlineData(
        new byte[] { })]
    [InlineData(
        new byte[] { 1 },
        new byte[] { 1 })]
    [InlineData(
        new[] { _nil },
        new byte[0])]
    [InlineData(
        new[] { _nil, _nil },
        new byte[0], new byte[0])]
    [InlineData(
        new byte[] { 1, 2, 3, 4, 5, 6 },
        new byte[] { 1, 2, 3, 4, 5, 6 })]
    [InlineData(
        new byte[] { 2, 3, 4, 5, 6, _nil },
        new byte[] { 2, 3, 4, 5, 6 })]
    [InlineData(
        new byte[] { _nil, 1, 2, 3, 4, 5, 6 },
        new byte[0], new byte[] { 1, 2, 3, 4, 5, 6 })]
    [InlineData(
        new byte[] { 1, 2, 3, _nil, 4, 5, 6 },
        new byte[] { 1, 2, 3 }, new byte[] { 4, 5, 6 })]
    [InlineData(
        new byte[] { 1, 2, 3, _nil, _nil, 4, 5, 6 },
        new byte[] { 1, 2, 3 }, new byte[0], new byte[] { 4, 5, 6 })]
    [InlineData(
        new byte[] { 1, 2, 3, _nil, 4, 5, 6, _nil, 7, 8, 9 },
        new byte[] { 1, 2, 3 }, new byte[] { 4, 5, 6 }, new byte[] { 7, 8, 9 })]
    [InlineData(
        new byte[] { _nil, 1, _nil, 2, 3, _nil, 4, 5, 6, _nil, 7, 8, 9, 10 },
        new byte[0], new byte[] { 1 }, new byte[] { 2, 3 }, new byte[] { 4, 5, 6 }, new byte[] { 7, 8, 9, 10 })]
    public void ReadNullTerminatedLines(byte[] input, params byte[][] expectedChunks)
    {
        MemoryStream stream = new(input);

        // Run the test at multiple buffer sizes to test boundary conditions thoroughly
        for (var bufferSize = 1; bufferSize < input.Length + 2; bufferSize++)
        {
            stream.Position = 0;

            using var e = stream.SplitLogOutput(bufferSize).GetEnumerator();
            for (var chunkIndex = 0; chunkIndex < expectedChunks.Length; chunkIndex++)
            {
                var expected = expectedChunks[chunkIndex];
                e.MoveNext().Should().BeTrue();
                e.Current.ToArray().Should().Equal(expected,
                    $"input=[{string.Join(", ", expected)}] chunkIndex={chunkIndex} bufferSize={{bufferSize}}");
            }

            e.MoveNext().Should().BeFalse($"bufferSize={bufferSize}");
        }
    }
}
