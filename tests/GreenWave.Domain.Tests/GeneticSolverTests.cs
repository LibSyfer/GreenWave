using GreenWave.Domain.Models;
using GreenWave.Domain.Motion;
using GreenWave.Domain.Solvers;
using Xunit.Abstractions;
using static GreenWave.Domain.Tests.TestProblems;

namespace GreenWave.Domain.Tests;

/// <summary>
/// ГА — эвристика: точный оптимум не гарантирован, поэтому проверяется,
/// что он не «лучше» полного перебора, находит допустимые решения и чаще всего — оптимум.
/// </summary>
public class GeneticSolverTests(ITestOutputHelper output)
{
    private const double EnergyEps = 1e-6;

    private static GaOptions Options(double speedStep, int seed = 1) => new(speedStep, Seed: seed);

    [Theory]
    [MemberData(nameof(SampleNames), MemberType = typeof(TestProblems))]
    public void Samples_FindsOptimum(string name)
    {
        var model = new MotionModel(Sample(name));
        var exact = new BruteForceSolver(Kmh(2)).Solve(model);
        var ga = new GeneticSolver(Options(Kmh(2))).Solve(model);

        Assert.Equal(exact is null, ga is null);
        if (exact is null || ga is null) return;

        Assert.Equal(exact.Energy, ga.Energy, EnergyEps);
    }

    [Theory]
    [MemberData(nameof(Seeds), MemberType = typeof(TestProblems))]
    public void RandomProblems_NeverBetterThanExact(int seed)
    {
        var model = new MotionModel(TestProblems.Random(seed));
        var exact = new BruteForceSolver(Kmh(4)).Solve(model);
        var ga = new GeneticSolver(Options(Kmh(4))).Solve(model);

        if (exact is null)
        {
            Assert.Null(ga);
            return;
        }

        if (ga is null) return;

        Assert.True(ga.Time <= model.Problem.Limits.MaxTime);
        Assert.All(ga.Segments, s => Assert.Equal(0, s.LengthShortage));
        Assert.True(ga.Energy >= exact.Energy - EnergyEps);
    }

    [Fact]
    public void RandomProblems_UsuallyFindsOptimum()
    {
        int solvable = 0, feasible = 0, optimal = 0;
        double maxGap = 0;

        foreach (int seed in SeedRange)
        {
            var model = new MotionModel(TestProblems.Random(seed));
            var exact = new BruteForceSolver(Kmh(4)).Solve(model);
            if (exact is null) continue;

            solvable++;
            var ga = new GeneticSolver(Options(Kmh(4))).Solve(model);
            if (ga is null) continue;

            feasible++;
            if (Math.Abs(ga.Energy - exact.Energy) <= EnergyEps) optimal++;
            if (exact.Energy > 0)
                maxGap = Math.Max(maxGap, (ga.Energy - exact.Energy) / exact.Energy);
        }

        output.WriteLine($"есть решение: {solvable}, ГА нашёл допустимое: {feasible}, оптимум: {optimal}, " +
                         $"макс. отклонение по энергии: {maxGap:P1}");

        Assert.True(feasible >= 0.95 * solvable, $"допустимых {feasible} из {solvable}");
        Assert.True(optimal >= 0.90 * solvable, $"оптимальных {optimal} из {solvable}");
    }

    [Fact]
    public void SameSeed_SameResult()
    {
        var model = new MotionModel(Samples.TestProblem1());

        var a = new GeneticSolver(Options(Kmh(2), seed: 7)).Solve(model);
        var b = new GeneticSolver(Options(Kmh(2), seed: 7)).Solve(model);

        Assert.Equal(a?.Segments, b?.Segments);
    }
}
