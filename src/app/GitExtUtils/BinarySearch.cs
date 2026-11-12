namespace GitExtUtils;

public static class BinarySearch
{
    /// <summary>
    ///     Find first integer between min and (min + count - 1) where "predicate" returns true.
    /// </summary>
    /// <remarks>
    ///     Assumes "predicate" is a non-decreasing function, i.e.
    ///     if predicate(i) == true then predicate(i + n) == true for any positive n.
    /// </remarks>
    public static int Find(int min, int count, Func<int, bool> predicate)
    {
        if (count == 0)
        {
            return -1;
        }

        if (predicate(min))
        {
            return min;
        }

        if (count == 1)
        {
            return -1;
        }

        var halfCount = count / 2;
        var middle = min + halfCount;
        var searchRightHalfResult = Find(middle, count - halfCount, predicate);

        if (searchRightHalfResult > middle)
        {
            return searchRightHalfResult;
        }

        if (searchRightHalfResult == -1)
        {
            return -1;
        }

        // searchRightHalfResult == middle

        var newLeft = min + 1;
        var searchLeftResult = Find(newLeft, middle - newLeft, predicate);

        if (searchLeftResult == -1)
        {
            return middle;
        }

        return searchLeftResult;
    }
}
