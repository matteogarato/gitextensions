using System.Threading.Channels;
using GitExtUtils;
using GitUI;

namespace GitCommandsTests;
public sealed class AsyncStreamReaderTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task AsyncStreamReader(int repetition)
    {
        repetition.Should().BeInRange(0, 2);

        const int millisecondsDelayForAsyncNotification = 50;
        const int millisecondsDelayForPartialLine = 500;

        const string line1 = "line with LF\n";
        const string line2 = "line with CRLF\r\n";
        const string prompt = "prompt>";
        const string input = "input\n";
        const string progress1 = "33%\r";
        const string progress2 = "66%\r";
        const string progress3 = "100%\r";
        const string partialLine = "partial";
        const string blockOfEndedLines = $"{line1}{line2}";
        const string blockOfLinesWithoutEnd = $"{blockOfEndedLines}{prompt}";

        StreamReaderMock streamReaderMock = new();

        List<string> notifiedTexts = [];
        List<string> expectedTexts = [];

        AsyncStreamReader asyncStreamReader = new(streamReaderMock, notify: text => notifiedTexts.Add(text));

        streamReaderMock.Inject(blockOfEndedLines);
        await Task.Delay(millisecondsDelayForAsyncNotification, TestContext.Current.CancellationToken);
        expectedTexts.AddRange([line1, line2]);
        notifiedTexts.Should().BeEquivalentTo(expectedTexts);

        streamReaderMock.Inject(blockOfLinesWithoutEnd);
        await Task.Delay(millisecondsDelayForAsyncNotification, TestContext.Current.CancellationToken);
        expectedTexts.AddRange([line1, line2]);
        notifiedTexts.Should().BeEquivalentTo(expectedTexts);
        await Task.Delay(millisecondsDelayForPartialLine, TestContext.Current.CancellationToken);
        expectedTexts.AddRange([prompt]);
        notifiedTexts.Should().BeEquivalentTo(expectedTexts);

        streamReaderMock.Inject(input);
        await Task.Delay(millisecondsDelayForAsyncNotification, TestContext.Current.CancellationToken);
        expectedTexts.AddRange([input]);
        notifiedTexts.Should().BeEquivalentTo(expectedTexts);

        streamReaderMock.Inject("");
        await Task.Delay(millisecondsDelayForAsyncNotification, TestContext.Current.CancellationToken);
        expectedTexts.AddRange([]);
        notifiedTexts.Should().BeEquivalentTo(expectedTexts);
        await Task.Delay(millisecondsDelayForPartialLine, TestContext.Current.CancellationToken);
        expectedTexts.AddRange([]);
        notifiedTexts.Should().BeEquivalentTo(expectedTexts);

        streamReaderMock.Inject(progress1);
        await Task.Delay(millisecondsDelayForAsyncNotification, TestContext.Current.CancellationToken);
        expectedTexts.AddRange([]);
        notifiedTexts.Should().BeEquivalentTo(expectedTexts);
        await Task.Delay(millisecondsDelayForPartialLine, TestContext.Current.CancellationToken);
        expectedTexts.AddRange([progress1]);
        notifiedTexts.Should().BeEquivalentTo(expectedTexts);

        streamReaderMock.Inject(progress2);
        await Task.Delay(millisecondsDelayForAsyncNotification, TestContext.Current.CancellationToken);
        expectedTexts.AddRange([]);
        notifiedTexts.Should().BeEquivalentTo(expectedTexts);
        streamReaderMock.Inject(progress3);
        await Task.Delay(millisecondsDelayForAsyncNotification, TestContext.Current.CancellationToken);
        expectedTexts.AddRange([progress2]);
        notifiedTexts.Should().BeEquivalentTo(expectedTexts);
        streamReaderMock.Inject(partialLine);
        await Task.Delay(millisecondsDelayForAsyncNotification, TestContext.Current.CancellationToken);
        expectedTexts.AddRange([progress3]);
        notifiedTexts.Should().BeEquivalentTo(expectedTexts);

        streamReaderMock.EndOfStream = true;
        int start = Environment.TickCount;
        await asyncStreamReader.WaitUntilEofAsync(cancellationToken: TestContext.Current.CancellationToken);
        int delay = Environment.TickCount - start;
        delay.Should().BeLessThan(millisecondsDelayForAsyncNotification);
        expectedTexts.AddRange([partialLine]);
        notifiedTexts.Should().BeEquivalentTo(expectedTexts);

        streamReaderMock.Inject(blockOfEndedLines);
        await Task.Delay(millisecondsDelayForPartialLine + millisecondsDelayForAsyncNotification, TestContext.Current.CancellationToken);
        expectedTexts.AddRange([]);
        notifiedTexts.Should().BeEquivalentTo(expectedTexts);
    }

    private class StreamReaderMock : IStreamReader
    {
        private readonly Channel<string> _channel = Channel.CreateUnbounded<string>();
        private bool _endOfStream = false;
        private string _buffer = "";

        public bool EndOfStream
        {
            get => _endOfStream;
            set
            {
                _endOfStream = value;
                Inject("");
            }
        }

        public async ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken)
        {
            if (_buffer.Length == 0)
            {
                _buffer = await _channel.Reader.ReadAsync(cancellationToken);
            }

            int length = Math.Min(_buffer.Length, buffer.Length);
            for (int index = 0; index < length; ++index)
            {
                buffer.Span[index] = _buffer[index];
            }

            _buffer = _buffer[length..];

            return length;
        }

        internal void Inject(string text)
        {
            _channel.Writer.TryWrite(text);
        }
    }
}
