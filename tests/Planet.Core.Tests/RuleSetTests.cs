using Planet.Core.Model;

namespace Planet.Core.Tests;

public class RuleSetTests
{
    [Fact]
    public void Default_MatchesAssignment()
    {
        Assert.Equal("R1234/W123456/B23/P01/Z0123/M2/L10", RuleSet.Default.ToString());
    }

    [Fact]
    public void Parse_RoundTrips()
    {
        var rules = RuleSet.Parse("R3/W23/B2/P/Z01/M5/L25");

        Assert.True(rules.PlantBirth.Contains(3));
        Assert.False(rules.PlantBirth.Contains(2));
        Assert.True(rules.AnimalBirthPlants.IsEmpty);
        Assert.Equal(5, rules.MoveRange);
        Assert.Equal(25, rules.Lifespan);
        Assert.Equal("R3/W23/B2/P/Z01/M5/L25", rules.ToString());
    }

    [Fact]
    public void Parse_AcceptsAnySegmentOrder()
    {
        Assert.Equal(RuleSet.Default, RuleSet.Parse("L10/M2/Z0123/P01/B23/W123456/R1234"));
    }

    [Theory]
    [InlineData("R1234/W123456/B23/P01/Z0123/M2", "L")]
    [InlineData("R19/W1/B2/P0/Z0/M1/L1", "9")]
    [InlineData("R1/W1/B2/P0/Z0/M0/L1", "M")]
    [InlineData("R1/W1/B2/P0/Z0/M1/Lx", "L")]
    [InlineData("X1/R1/W1/B2/P0/Z0/M1/L1", "X")]
    public void TryParse_RejectsInvalidInput(string text, string expectedInError)
    {
        Assert.False(RuleSet.TryParse(text, out _, out var error));
        Assert.Contains(expectedInError, error);
    }

    [Theory]
    [InlineData("1,2, 3", "123")]
    [InlineData("3210", "0123")]
    [InlineData("", "")]
    public void CountSet_ParseNormalizes(string text, string expected)
    {
        Assert.Equal(expected, CountSet.Parse(text).ToString());
    }
}
