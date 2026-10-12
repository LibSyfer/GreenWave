using GreenWave.Domain.Models;
using GreenWave.Domain.Motion;
using GreenWave.Domain.Solvers;
using static GreenWave.Domain.Tests.TestProblems;

namespace GreenWave.Domain.Tests;

/// <summary>Траектория должна совпадать с моделью: в концах сегментов — те же времена и скорости.</summary>
public class TrajectoryTests
{
    private const double Eps = 1e-6;

    [Theory]
    [MemberData(nameof(SampleNames), MemberType = typeof(TestProblems))]
    public void Samples_TrajectoryMatchesRoute(string name)
    {
        AssertMatches(Sample(name), Kmh(2));
    }

    [Theory]
    [MemberData(nameof(Seeds), MemberType = typeof(TestProblems))]
    public void RandomProblems_TrajectoryMatchesRoute(int seed)
    {
        AssertMatches(TestProblems.Random(seed), Kmh(4));
    }

    private static void AssertMatches(GreenWaveProblem problem, double speedStep)
    {
        var route = new DynamicProgrammingSolver(new DpOptions(speedStep)).Solve(new MotionModel(problem));
        if (route is null) return;

        var points = Trajectory.Build(problem, route.Segments);

        Assert.Equal(new TrajectoryPoint(0, 0, problem.InitialSpeed), points[0]);

        double topSpeed = Math.Max(problem.Limits.MaxSpeed, problem.InitialSpeed);
        for (int k = 1; k < points.Count; k++)
        {
            Assert.True(points[k].Time >= points[k - 1].Time - Eps, $"время убывает в точке {k}");
            Assert.True(points[k].Distance >= points[k - 1].Distance - Eps, $"путь убывает в точке {k}");
            Assert.InRange(points[k].Speed, -Eps, topSpeed + Eps);
        }

        double end = 0;
        for (int i = 0; i < route.Segments.Length; i++)
        {
            var result = route.Segments[i];
            end += problem.Road.Segments[i].Length;

            var atLine = points.Where(p => Math.Abs(p.Distance - end) < Eps).ToArray();
            Assert.NotEmpty(atLine);

            // у стоп-линии — в момент ArrivalTime, уезжает — в ResultState.Time
            Assert.Equal(result.ArrivalTime, atLine[0].Time, Eps);
            Assert.Equal(result.ResultState.Time, atLine[^1].Time, Eps);
            Assert.Equal(result.ResultState.Speed, atLine[^1].Speed, Eps);
            Assert.Equal(result.Stopped ? 0 : result.Speed, atLine[0].Speed, Eps);
        }

        Assert.Equal(route.Time, points[^1].Time, Eps);
        Assert.Equal(end, points[^1].Distance, Eps);
    }
}
