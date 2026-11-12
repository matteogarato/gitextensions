using System.Collections;
using Microsoft;

namespace GitExtensions.Extensibility;

/// <summary>
///     Splits a string by a delimiter, producing substrings lazily during enumeration.
/// </summary>
/// <remarks>
///     Unlike <see cref="string.Split(char[])" /> and overloads, <see cref="LazyStringSplit" />
///     does not allocate an array for the return, and allocates strings on demand during
///     enumeration. A custom enumerator type is used so that the only allocations made are
///     the substrings themselves. We also avoid the large internal arrays assigned by the
///     methods on <see cref="string" />.
/// </remarks>
public readonly struct LazyStringSplit : IEnumerable<string>
{
    private readonly string _input;
    private readonly char _delimiter;
    private readonly StringSplitOptions _options;

    public LazyStringSplit(string input, char delimiter, StringSplitOptions options = StringSplitOptions.None)
    {
        Requires.NotNull(input);

        _input = input;
        _delimiter = delimiter;
        _options = options;
    }

    public Enumerator GetEnumerator()
    {
        return new Enumerator(this, _options);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    IEnumerator<string> IEnumerable<string>.GetEnumerator()
    {
        return GetEnumerator();
    }

    public struct Enumerator : IEnumerator<string>
    {
        private readonly StringSplitOptions _options;
        private readonly string _input;
        private readonly char _delimiter;
        private int _index;

        internal Enumerator(in LazyStringSplit split, StringSplitOptions options)
        {
            _options = options;
            _index = 0;
            _input = split._input;
            _delimiter = split._delimiter;
            Current = null!;
        }

        private string Current { get; set; }

        string IEnumerator<string>.Current
        {
            get => Current;
        }

        bool IEnumerator.MoveNext()
        {
            while (_index < _input.Length)
            {
                var delimiterIndex = _input.IndexOf(_delimiter, _index);

                if (delimiterIndex == -1)
                {
                    Current = _input[_index..];
                    _index = _input.Length + 1;
                    return true;
                }

                var length = delimiterIndex - _index;

                if (length == 0)
                {
                    _index++;
                    if (_options == StringSplitOptions.RemoveEmptyEntries)
                    {
                        continue;
                    }

                    Current = "";
                    return true;
                }

                Current = _input.Substring(_index, length);
                _index = delimiterIndex + 1;

                return true;
            }

            if (_options == StringSplitOptions.None && _index == _input.Length)
            {
                _index++;
                Current = "";
                return true;
            }

            return false;
        }

        readonly object IEnumerator.Current
        {
            get => Current;
        }

        void IEnumerator.Reset()
        {
            _index = 0;
            Current = null!;
        }

        void IDisposable.Dispose()
        {
        }
    }
}

public static class LazyStringSplitExtensions
{
    public static LazyStringSplit LazySplit(this string s, char delimiter,
        StringSplitOptions options = StringSplitOptions.None)
    {
        return new LazyStringSplit(s, delimiter, options);
    }

    /// <summary>
    ///     Lazily splits <paramref name="stringToSplit" /> and returns ranges into the original string.
    /// </summary>
    /// <param name="stringToSplit">The string to split.</param>
    /// <param name="delimiter">The character that delimits the ranges.</param>
    /// <param name="options">Options controlling trimming and removal of empty ranges.</param>
    /// <returns>The zero-based beginning and length of each resulting range.</returns>
    public static IEnumerable<(int beginning, int length)> LazySplitRanges(this string stringToSplit, char delimiter,
        StringSplitOptions options = StringSplitOptions.None)
    {
        var trim = options.HasFlag(StringSplitOptions.TrimEntries);
        var removeEmpty = options.HasFlag(StringSplitOptions.RemoveEmptyEntries);
        for (var start = 0; start <= stringToSplit.Length;)
        {
            var indexOfNextDelimiter = stringToSplit.IndexOf(delimiter, start);
            var end = indexOfNextDelimiter < 0 ? stringToSplit.Length : indexOfNextDelimiter;

            var range = GetRange(stringToSplit, start, end, trim);

            if (!removeEmpty || range.length != 0)
            {
                yield return range;
            }

            if (indexOfNextDelimiter < 0)
            {
                yield break;
            }

            start = indexOfNextDelimiter + 1;
        }
    }

    private static (int beginning, int length) GetRange(string value, int start, int end, bool trim)
    {
        if (trim)
        {
            while (start < end && char.IsWhiteSpace(value[start]))
            {
                start++;
            }

            while (end > start && char.IsWhiteSpace(value[end - 1]))
            {
                end--;
            }
        }

        return (start, end - start);
    }

    public static IEnumerable<T> Select<T>(this LazyStringSplit split, Func<string, T> func)
    {
        foreach (var value in split)
        {
            yield return func(value);
        }
    }

    public static string First(this LazyStringSplit split)
    {
        return split.FirstOrDefault() ?? throw new InvalidOperationException("Sequence is empty.");
    }

    public static string? FirstOrDefault(this LazyStringSplit split)
    {
        using var enumerator = split.GetEnumerator();
        return enumerator.MoveNext() ? enumerator.Current : null;
    }

    public static string? LastOrDefault(this LazyStringSplit split)
    {
        using var enumerator = split.GetEnumerator();

        if (!enumerator.MoveNext())
        {
            return null;
        }

        while (true)
        {
            var last = enumerator.Current;

            if (!enumerator.MoveNext())
            {
                return last;
            }
        }
    }

    public static IEnumerable<string> Where(this LazyStringSplit split, Func<string, bool> predicate)
    {
        foreach (var s in split)
        {
            if (predicate(s))
            {
                yield return s;
            }
        }
    }
}
