namespace GitExtUtils;

public sealed class StreamReaderFacade(StreamReader streamReader) : IStreamReader
{
    private readonly StreamReader _streamReader = streamReader;

    bool IStreamReader.EndOfStream
    {
        get => _streamReader.EndOfStream;
    }

    ValueTask<int> IStreamReader.ReadAsync(Memory<char> buffer, CancellationToken cancellationToken)
    {
        return _streamReader.ReadAsync(buffer, cancellationToken);
    }
}
