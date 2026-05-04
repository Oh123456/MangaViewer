namespace Viewer;

public sealed class NaturalStringComparer : IComparer<string>
{
    public static readonly NaturalStringComparer OrdinalIgnoreCase = new();

    private NaturalStringComparer()
    {
    }

    public int Compare(string? left, string? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        var leftIndex = 0;
        var rightIndex = 0;
        while (leftIndex < left.Length && rightIndex < right.Length)
        {
            var leftChar = left[leftIndex];
            var rightChar = right[rightIndex];
            if (char.IsDigit(leftChar) && char.IsDigit(rightChar))
            {
                var numberCompare = CompareNumberRuns(left, ref leftIndex, right, ref rightIndex);
                if (numberCompare != 0)
                {
                    return numberCompare;
                }

                continue;
            }

            var charCompare = char.ToUpperInvariant(leftChar).CompareTo(char.ToUpperInvariant(rightChar));
            if (charCompare != 0)
            {
                return charCompare;
            }

            leftIndex++;
            rightIndex++;
        }

        return left.Length.CompareTo(right.Length);
    }

    private static int CompareNumberRuns(string left, ref int leftIndex, string right, ref int rightIndex)
    {
        var leftStart = leftIndex;
        var rightStart = rightIndex;
        while (leftIndex < left.Length && char.IsDigit(left[leftIndex]))
        {
            leftIndex++;
        }

        while (rightIndex < right.Length && char.IsDigit(right[rightIndex]))
        {
            rightIndex++;
        }

        var leftSignificantStart = SkipLeadingZeros(left, leftStart, leftIndex);
        var rightSignificantStart = SkipLeadingZeros(right, rightStart, rightIndex);
        var leftSignificantLength = leftIndex - leftSignificantStart;
        var rightSignificantLength = rightIndex - rightSignificantStart;
        if (leftSignificantLength != rightSignificantLength)
        {
            return leftSignificantLength.CompareTo(rightSignificantLength);
        }

        for (var offset = 0; offset < leftSignificantLength; offset++)
        {
            var digitCompare = left[leftSignificantStart + offset].CompareTo(right[rightSignificantStart + offset]);
            if (digitCompare != 0)
            {
                return digitCompare;
            }
        }

        return (leftIndex - leftStart).CompareTo(rightIndex - rightStart);
    }

    private static int SkipLeadingZeros(string value, int start, int end)
    {
        while (start < end - 1 && value[start] == '0')
        {
            start++;
        }

        return start;
    }
}
