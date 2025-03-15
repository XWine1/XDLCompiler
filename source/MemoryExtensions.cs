namespace XDLCompiler;

public static class MemoryExtensions
{
    /// <summary>Truncates the span at the first occurrence of the specified value.</summary>
    public static ReadOnlySpan<T> TruncateAt<T>(this ReadOnlySpan<T> span, T value)
        where T : IEquatable<T>
    {
        int index = span.IndexOf(value);
        return index == -1 ? span : span[..index];
    }

    /// <summary>Truncates the span at the first occurrence of the specified value.</summary>
    public static Span<T> TruncateAt<T>(this Span<T> span, T value)
        where T : IEquatable<T>
    {
        int index = span.IndexOf(value);
        return index == -1 ? span : span[..index];
    }

    /// <summary>Truncates the span at the first occurrence of the specified value.</summary>
    public static ReadOnlySpan<T> TruncateAt<T>(this ReadOnlySpan<T> span, T value, int startIndex)
        where T : IEquatable<T>
    {
        int index = span.IndexOf(value, startIndex);
        return index == -1 ? span : span[..index];
    }

    /// <summary>Truncates the span at the first occurrence of the specified value.</summary>
    public static Span<T> TruncateAt<T>(this Span<T> span, T value, int startIndex)
        where T : IEquatable<T>
    {
        int index = ((ReadOnlySpan<T>)span).IndexOf(value, startIndex);
        return index == -1 ? span : span[..index];
    }

    /// <summary>Searches for the specified value and returns the index of its first occurrence.</summary>
    public static int IndexOf<T>(this ReadOnlySpan<T> span, T value, int startIndex)
        where T : IEquatable<T>
    {
        int index = span[startIndex..].IndexOf(value);
        return index == -1 ? -1 : startIndex + index;
    }

    /// <summary>Searches for the specified value and returns the index following its first occurrence.</summary>
    public static int IndexAfter<T>(this ReadOnlySpan<T> span, T value, int startIndex)
        where T : IEquatable<T>
    {
        int index = span[startIndex..].IndexOf(value);
        return index == -1 ? -1 : startIndex + index + 1;
    }

    /// <summary>Searches for the specified value and returns the index of its first occurrence.</summary>
    public static int IndexOf<T>(this ReadOnlySpan<T> span, ReadOnlySpan<T> value, int startIndex)
        where T : IEquatable<T>
    {
        int index = span[startIndex..].IndexOf(value);
        return index == -1 ? -1 : startIndex + index;
    }

    /// <summary>Searches for the specified value and returns the index following its first occurrence.</summary>
    public static int IndexAfter<T>(this ReadOnlySpan<T> span, ReadOnlySpan<T> value, int startIndex)
        where T : IEquatable<T>
    {
        int index = span[startIndex..].IndexOf(value);
        return index == -1 ? -1 : startIndex + index + value.Length;
    }

    /// <summary>Searches for the specified value and returns the index of its last occurrence.</summary>
    public static int LastIndexOf<T>(this ReadOnlySpan<T> span, T value, int startIndex)
        where T : IEquatable<T>
    {
        int index = span[startIndex..].LastIndexOf(value);
        return index == -1 ? -1 : startIndex + index;
    }

    /// <summary>Searches for the specified value and returns the index of its last occurrence.</summary>
    public static int LastIndexOf<T>(this ReadOnlySpan<T> span, ReadOnlySpan<T> value, int startIndex)
        where T : IEquatable<T>
    {
        int index = span[startIndex..].LastIndexOf(value);
        return index == -1 ? -1 : startIndex + index;
    }

    /// <summary>Searches for the specified value and returns the index of its first occurrence that is not preceded by <paramref name="escape"/>.</summary>
    public static int IndexOf<T>(this ReadOnlySpan<T> span, T value, T escape)
        where T : IEquatable<T>
    {
        return IndexOf(span, value, escape, startIndex: 0);
    }

    /// <summary>Searches for the specified value and returns the index of its first occurrence that is not preceded by <paramref name="escape"/>.</summary>
    public static int IndexOf<T>(this ReadOnlySpan<T> span, T value, T escape, int startIndex)
        where T : IEquatable<T>
    {
        int index = span.IndexOf(value, startIndex);

        if (index <= 0)
            return index;

        while (span[index - 1].Equals(escape))
        {
            int i;

            // Count the number of escape characters.
            for (i = index - 2; i >= 0; i--)
            {
                if (!span[i].Equals(escape))
                {
                    break;
                }
            }

            // If we have an even number of escape characters
            // preceding the value, the value is not escaped.
            if (((index - 1 - i) & 1) == 0)
                return index;
            else
                index = span.IndexOf(value, index + 1);

            if (index == -1)
            {
                return -1;
            }
        }

        return index;
    }

    /// <summary>Searches for the specified value and returns the index following its first occurrence that is not preceded by <paramref name="escape"/>.</summary>
    public static int IndexAfter<T>(this ReadOnlySpan<T> span, T value, T escape)
        where T : IEquatable<T>
    {
        return IndexAfter(span, value, escape, startIndex: 0);
    }

    /// <summary>Searches for the specified value and returns the index following its first occurrence that is not preceded by <paramref name="escape"/>.</summary>
    public static int IndexAfter<T>(this ReadOnlySpan<T> span, T value, T escape, int startIndex)
        where T : IEquatable<T>
    {
        int index = IndexOf(span, value, escape, startIndex);
        return index == -1 ? -1 : index + 1;
    }
}
