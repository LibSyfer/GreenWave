using GreenWave.Domain.Models;
using GreenWave.Domain.Motion;
using GreenWave.Domain.Solvers;
using static GreenWave.Domain.Tests.TestProblems;

namespace GreenWave.Domain.Tests;

/// <summary>
/// Полный перебор — точный оптимум, ДП сверяется с ним.
/// </summary>
public class SolverComparisonTests
{
    private const double EnergyEps = 1e-6;

    [Theory]
    [MemberData(nameof(SampleNames), MemberType = typeof(TestProblems))]
    public void Samples_DpMatchesBruteForce(string name)
    {
        AssertSameOptimum(new MotionModel(Sample(name)), Kmh(2));
    }

    [Theory]
    [MemberData(nameof(Seeds), MemberType = typeof(TestProblems))]
    public void RandomProblems_DpMatchesBruteForce(int seed)
    {
        AssertSameOptimum(new MotionModel(TestProblems.Random(seed)), Kmh(4));
    }

    /// <summary>Генератор должен давать разные случаи, иначе сравнение выше ничего не проверяет.</summary>
    [Fact]
    public void RandomProblems_CoverFeasibleInfeasibleAndStops()
    {
        var problems = SeedRange.Select(TestProblems.Random).ToArray();
        var routes = problems
            .Select(problem => new BruteForceSolver(Kmh(4)).Solve(new MotionModel(problem)))
            .ToArray();

        int infeasible = routes.Count(r => r is null);
        int withStops = routes.Count(r => r is { Stops: > 0 });
        int withoutStops = routes.Count(r => r is { Stops: 0 });
        int withMargin = problems.Count(p => p.SafetyMargin > 0);

        Assert.True(infeasible >= 10, $"без решения: {infeasible}");
        Assert.True(withStops >= 10, $"с остановками: {withStops}");
        Assert.True(withoutStops >= 10, $"без остановок: {withoutStops}");
        Assert.True(withMargin >= 10, $"с SafetyMargin > 0: {withMargin}");
    }

    private static void AssertSameOptimum(MotionModel model, double speedStep)
    {
        var exact = new BruteForceSolver(speedStep).Solve(model);
        var dp = new DynamicProgrammingSolver(new DpOptions(speedStep)).Solve(model);
        var dpNoPruning = new DynamicProgrammingSolver(new DpOptions(speedStep, RemainderPruning: false)).Solve(model);

        Assert.Equal(exact is null, dp is null);
        Assert.Equal(exact is null, dpNoPruning is null);
        if (exact is null || dp is null || dpNoPruning is null) return;

        AssertValid(model.Problem, dp);
        Assert.Equal(exact.Energy, dp.Energy, EnergyEps);
        Assert.Equal(exact.Energy, dpNoPruning.Energy, EnergyEps);
    }

    /// <summary>Маршрут ДП должен быть реальным: пересчёт по модели даёт те же числа.</summary>
    private static void AssertValid(GreenWaveProblem problem, RouteResult route)
    {
        var model = new MotionModel(problem);
        var state = new CarState(0, problem.InitialSpeed);

        Assert.Equal(problem.Road.Segments.Length, route.Segments.Length);

        for (int i = 0; i < route.Segments.Length; i++)
        {
            var replay = model.Drive(problem.Road.Segments[i], state, route.Segments[i].Speed);
            Assert.Equal(route.Segments[i], replay);
            Assert.Equal(0, replay.LengthShortage);
            state = replay.ResultState;
        }

        Assert.True(state.Time <= problem.Limits.MaxTime);
    }
}
