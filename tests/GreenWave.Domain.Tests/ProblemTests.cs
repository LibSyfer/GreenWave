using GreenWave.Domain.Entities;
using GreenWave.Domain.Models;

namespace GreenWave.Domain.Tests;

public class ProblemTests
{
    private const double Eps = 1e-9;

    [Fact]
    public void SpeedGrid_EvenStep_IncludesBothEnds()
    {
        var grid = new Limits(MinSpeed: 2, MaxSpeed: 10, MaxTime: 100).SpeedGrid(2);

        Assert.Equal([2, 4, 6, 8, 10], grid);
    }

    [Fact]
    public void SpeedGrid_UnevenStep_AppendsMaxSpeed()
    {
        var grid = new Limits(MinSpeed: 2, MaxSpeed: 9, MaxTime: 100).SpeedGrid(3);

        Assert.Equal([2, 5, 8, 9], grid);
    }

    [Fact]
    public void SpeedGrid_KmhStep_HasNoNearDuplicateAtMax()
    {
        // 20..60 км/ч с шагом 2 км/ч — ровно 21 скорость, несмотря на погрешность деления на 3.6
        var grid = new Limits(20 / 3.6, 60 / 3.6, 100).SpeedGrid(2 / 3.6);

        Assert.Equal(21, grid.Length);
        Assert.Equal(60 / 3.6, grid[^1], Eps);
    }

    [Fact]
    public void SpeedGrid_MinEqualsMax_SingleSpeed()
    {
        Assert.Equal([5], new Limits(5, 5, 100).SpeedGrid(1));
    }

    [Fact]
    public void Validate_CorrectSample_DoesNotThrow()
    {
        Samples.TestProblem1().Validate();
    }

    public static TheoryData<string> InvalidProblems => ["NoSegments", "ZeroMinSpeed", "GreenLongerThanCycle", "GreenTooShortForMargin"];

    [Theory]
    [MemberData(nameof(InvalidProblems))]
    public void Validate_InvalidProblem_Throws(string name)
    {
        var problem = Samples.TestProblem1();
        var light = new TrafficLight(60, 0, 30);

        problem = name switch
        {
            "NoSegments" => problem with { Road = new Road([]) },
            "ZeroMinSpeed" => problem with { Limits = problem.Limits with { MinSpeed = 0 } },
            "GreenLongerThanCycle" => problem with { Road = new Road([new Segment(100, light with { GreenDuration = 70 })]) },
            "GreenTooShortForMargin" => problem with { Road = new Road([new Segment(100, light)]), SafetyMargin = 15 },
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };

        Assert.Throws<ArgumentException>(problem.Validate);
    }
}
