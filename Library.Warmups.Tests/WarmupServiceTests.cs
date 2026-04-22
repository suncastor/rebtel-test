using Xunit;

namespace Library.Warmups.Tests;

public class WarmupServiceTests
{
    private readonly WarmupService _service = new();

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(4, true)]
    [InlineData(5, false)]
    [InlineData(8, true)]
    [InlineData(256, true)]
    [InlineData(-256, false)]
    [InlineData(-1, false)]
    public void CheckIfIdIsPowerOfTwo_ShouldReturnCorrectValue(int idToCheck, bool expectedResult)
    {
        var result = _service.CheckIfIdIsPowerOfTwo(idToCheck);
        Assert.Equal(expectedResult, result);
    }

    [Theory]
    [InlineData("Moby Dick", "kciD yboM")]
    [InlineData("Read", "daeR")]
    [InlineData("a", "a")]
    [InlineData("", "")]
    [InlineData("ab", "ba")]
    public void ReverseTitle_ShouldReturnReversedTitle(string title, string expectedResult)
    {
        var result = _service.ReverseTitle(title);
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public void ReverseTitle_ShouldThrow_WhenTitleIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => _service.ReverseTitle(null!));
    }

    [Theory]
    [InlineData("Read", 3, "ReadReadRead")]
    [InlineData("Moby Dick", 2, "Moby DickMoby Dick")]
    [InlineData("x", 0, "")]
    [InlineData("x", 1, "x")]
    [InlineData("", 5, "")]
    public void RepeatTitle_ShouldReturnRepeatedTitle(string title, int count, string expectedResult)
    {
        var result = _service.RepeatTitle(title, count);
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public void RepeatTitle_ShouldThrow_WhenTitleIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => _service.RepeatTitle(null!, 3));
    }

    [Fact]
    public void RepeatTitle_ShouldThrow_WhenCountIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _service.RepeatTitle("Read", -1));
    }

    [Theory]
    [InlineData(0, 100, 1, 99, 50)]
    [InlineData(1, 99, 1, 99, 50)]
    [InlineData(1, 10, 1, 9, 5)]
    [InlineData(2, 10, 3, 9, 4)]
    [InlineData(5, 5, 5, 5, 1)]
    [InlineData(4, 4, 0, 0, 0)]
    [InlineData(-5, 5, -5, 5, 6)]
    [InlineData(-100, 0, -99, -1, 50)]
    public void GetOddIdsInRange_ShouldReturnAllOddNumbersInRange(int from, int to, int expectedFirst, int expectedLast, int expectedCount)
    {
        var result = _service.GetOddIdsInRange(from, to);

        Assert.Equal(expectedCount, result.Count);
        Assert.All(result, id => Assert.Equal(1, Math.Abs(id % 2)));
        if (expectedCount > 0)
        {
            Assert.Equal(expectedFirst, result[0]);
            Assert.Equal(expectedLast, result[^1]);
        }
    }

    [Fact]
    public void GetOddIdsInRange_ShouldThrow_WhenFromIsGreaterThanTo()
    {
        Assert.Throws<ArgumentException>(() => _service.GetOddIdsInRange(10, 1));
    }
}