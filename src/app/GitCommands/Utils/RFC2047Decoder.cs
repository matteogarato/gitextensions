using System.Globalization;
using System.Text;

namespace GitCommands.Utils;

// This class is taken from https://github.com/petrohi/Stratosphere.Imap/blob/master/Stratosphere/Imap/RFC2047Decoder.cs
public static class Rfc2047Decoder
{
    public static string Parse(string input)
    {
        StringBuilder sb = new();
        StringBuilder currentWord = new();
        StringBuilder currentSurroundingText = new();
        var readingWord = false;
        var hasSeenAtLeastOneWord = false;
        var wordQuestionMarkCount = 0;
        var i = 0;
        while (i < input.Length)
        {
            var currentChar = input[i];
            char peekAhead;
            switch (currentChar)
            {
                case '=':
                    peekAhead = i == input.Length - 1 ? ' ' : input[i + 1];
                    if (!readingWord && peekAhead == '?')
                    {
                        if (!hasSeenAtLeastOneWord || currentSurroundingText.ToString().Trim().Length > 0)
                        {
                            sb.Append(currentSurroundingText);
                        }

                        currentSurroundingText = new StringBuilder();
                        hasSeenAtLeastOneWord = true;
                        readingWord = true;
                        wordQuestionMarkCount = 0;
                    }

                    break;
                case '?':
                    if (readingWord)
                    {
                        wordQuestionMarkCount++;
                        peekAhead = i == input.Length - 1 ? ' ' : input[i + 1];
                        if (wordQuestionMarkCount > 3 && peekAhead == '=')
                        {
                            readingWord = false;
                            currentWord.Append(currentChar);
                            currentWord.Append(peekAhead);
                            sb.Append(ParseEncodedWord(currentWord.ToString()));
                            currentWord = new StringBuilder();
                            i += 2;
                            continue;
                        }
                    }

                    break;
            }

            if (readingWord)
            {
                currentWord.Append(currentChar == '_' ? ' ' : currentChar);
            }
            else
            {
                currentSurroundingText.Append(currentChar);
            }

            i++;
        }

        sb.Append(currentSurroundingText);
        return sb.ToString();
    }

    private static string ParseEncodedWord(string input)
    {
        StringBuilder sb = new();
        if (!input.StartsWith("=?"))
        {
            return input;
        }

        if (!input.EndsWith("?="))
        {
            return input;
        }

        // Get the name of the encoding but skip the leading =?
        var encodingName = input[2..input.IndexOf('?', 2)];
        var enc = Encoding.ASCII;
        if (!string.IsNullOrEmpty(encodingName))
        {
            enc = Encoding.GetEncoding(encodingName);
        }

        // Get the type of the encoding
        var type = input[encodingName.Length + 3];

        // Start after the name of the encoding and the other required parts
        var startPosition = encodingName.Length + 5;
        switch (char.ToLowerInvariant(type))
        {
            case 'q':
                sb.Append(ParseQuotedPrintable(enc, input, startPosition, true));
                break;
            case 'b':
                var baseString = input.Substring(startPosition, input.Length - startPosition - 2);
                var baseDecoded = Convert.FromBase64String(baseString);
                var intermediate = enc.GetString(baseDecoded);
                sb.Append(intermediate);
                break;
        }

        return sb.ToString();
    }

    public static string ParseQuotedPrintable(Encoding enc, string input)
    {
        return ParseQuotedPrintable(enc, input, 0, false);
    }

    private static string ParseQuotedPrintable(Encoding enc, string input, int startPos, bool skipQuestionEquals)
    {
        var workingBytes = Encoding.ASCII.GetBytes(input);
        var i = startPos;
        var outputPos = i;
        Span<char> peekAhead = stackalloc char[2];
        while (i < workingBytes.Length)
        {
            var currentByte = workingBytes[i];
            switch (currentByte)
            {
                case (byte)'=':
                    var canPeekAhead = i < workingBytes.Length - 2;
                    if (!canPeekAhead)
                    {
                        workingBytes[outputPos] = workingBytes[i];
                        ++outputPos;
                        ++i;
                        break;
                    }

                    var skipNewLineCount = 0;
                    for (var j = 0; j < 2; ++j)
                    {
                        var c = (char)workingBytes[i + j + 1];
                        if (c == '\r' || c == '\n')
                        {
                            ++skipNewLineCount;
                        }
                    }

                    if (skipNewLineCount > 0)
                    {
                        // If we have a lone equals followed by newline chars, then this is an artificial
                        // line break that should be skipped past.
                        i += 1 + skipNewLineCount;
                    }
                    else
                    {
                        try
                        {
                            peekAhead[0] = (char)workingBytes[i + 1];
                            peekAhead[1] = (char)workingBytes[i + 2];
                            var decodedByte = byte.Parse(peekAhead, NumberStyles.HexNumber);
                            workingBytes[outputPos] = decodedByte;
                            ++outputPos;
                            i += 3;
                        }
                        catch
                        {
                            // could not parse the peek-ahead chars as a hex number... so gobble the un-encoded '='
                            i += 1;
                        }
                    }

                    break;
                case (byte)'?':
                    if (skipQuestionEquals && workingBytes[i + 1] == (byte)'=')
                    {
                        i += 2;
                    }
                    else
                    {
                        workingBytes[outputPos] = workingBytes[i];
                        ++outputPos;
                        ++i;
                    }

                    break;
                default:
                    workingBytes[outputPos] = workingBytes[i];
                    ++outputPos;
                    ++i;
                    break;
            }
        }

        var output = string.Empty;
        var numBytes = outputPos - startPos;
        if (numBytes > 0)
        {
            output = enc.GetString(workingBytes, startPos, numBytes);
        }

        return output;
    }
}
