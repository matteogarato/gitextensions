using System.Diagnostics.CodeAnalysis;

namespace GitCommands.Git;

public static class GitConvert
{
    private const byte _lf = 0x0A;
    private const byte _cr = 0x0D;

    [return: NotNullIfNotNull(nameof(buf))]
    public static byte[]? ConvertCrLfToWorktree(byte[]? buf)
    {
        if (buf is null)
        {
            return buf;
        }

        BufStatistic bufStatistic = new(buf);

        if (bufStatistic.CntLf == 0)
        {
            return buf;
        }

        if (bufStatistic.CntLf == bufStatistic.CntCrlf)
        {
            return buf;
        }

        if (bufStatistic.CntCr != bufStatistic.CntCrlf)
        {
            return buf;
        }

        if (IsBinary())
        {
            return buf;
        }

        List<byte> bytes = new((int)(buf.Length * 1.01));

        if (buf.Length >= 1)
        {
            if (buf[0] == _lf)
            {
                bytes.Add(_cr);
                bytes.Add(_lf);
            }
            else
            {
                bytes.Add(buf[0]);
            }
        }

        for (var index = 1; index < buf.Length; index++)
        {
            if (buf[index] == _lf)
            {
                if (buf[index - 1] == _cr)
                {
                    bytes.Add(_lf);
                }
                else
                {
                    bytes.Add(_cr);
                    bytes.Add(_lf);
                }
            }
            else
            {
                bytes.Add(buf[index]);
            }
        }

        return [.. bytes];

        bool IsBinary()
        {
            if (bufStatistic.CntNul > 0)
            {
                return true;
            }

            if (bufStatistic.CntPrintable / 128 < bufStatistic.CntNonPrintable)
            {
                return true;
            }

            return false;
        }
    }

    private readonly struct BufStatistic
    {
        public readonly long CntNul;
        public readonly long CntCr;
        public readonly long CntLf;
        public readonly long CntCrlf;
        public readonly long CntPrintable;
        public readonly long CntNonPrintable;

        public BufStatistic(byte[] buf)
        {
            CntNul = 0;
            CntCr = 0;
            CntLf = 0;
            CntCrlf = 0;
            CntPrintable = 0;
            CntNonPrintable = 0;

            for (long i = 0; i < buf.Length; i++)
            {
                if (buf[i] == _cr)
                {
                    CntCr++;
                    if (i + 1 < buf.Length)
                    {
                        if (buf[i + 1] == _lf)
                        {
                            CntCrlf++;
                        }
                    }

                    continue;
                }

                if (buf[i] == _lf)
                {
                    CntLf++;
                    continue;
                }

                if (buf[i] == 0x7F)
                {
                    CntNonPrintable++;
                }
                else if (buf[i] < 0x20)
                {
                    switch (buf[i])
                    {
                        case 0x08:
                        case 0x09:
                        case 0x1B:
                        case 0x0C:
                            CntPrintable++;
                            break;

                        case 0:
                            CntNul++;
                            CntNonPrintable++;
                            break;

                        default:
                            CntNonPrintable++;
                            break;
                    }
                }
                else
                {
                    CntPrintable++;
                }
            }

            if (buf.Length >= 1)
            {
                if (buf[^1] == 0x1A)
                {
                    CntNonPrintable--;
                }
            }
        }
    }
}
