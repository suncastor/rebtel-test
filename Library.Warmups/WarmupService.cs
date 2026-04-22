using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Library.Warmups;

public class WarmupService
{
    public bool CheckIfIdIsPowerOfTwo(int id)
    {
        if (id == 0)
        {
            return false;
        }

        return (id & (id - 1)) == 0;
    }

    public string ReverseTitle(string title)
    {
        ArgumentNullException.ThrowIfNull(title);

        return string.Create(title.Length, title, (span, source) =>
        {
            for (int i = 0; i < source.Length; i++)
            {
                span[i] = source[source.Length - 1 - i];
            }
        });
    }

    public string RepeatTitle(string title, int count)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        return string.Concat(Enumerable.Repeat(title, count));
    }

    public IReadOnlyList<int> GetOddIdsInRange(int from, int to)
    {
        if (from > to)
        {
            throw new ArgumentException($"'{nameof(from)}' must be less than or equal to '{nameof(to)}'.", nameof(from));
        }

        var firstOdd = from % 2 == 0 ? from + 1 : from;
        var result = new List<int>();
        for (int i = firstOdd; i <= to; i += 2)
        {
            result.Add(i);
        }

        return result;
    }
}