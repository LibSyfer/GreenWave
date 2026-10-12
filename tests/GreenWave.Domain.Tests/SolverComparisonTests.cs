using GreenWave.Domain.Entities;
using GreenWave.Domain.Models;
using GreenWave.Domain.Motion;
using GreenWave.Domain.Solvers;

namespace GreenWave.Domain.Tests;

/// <summary>
/// Полный перебор — точный оптимум, ДП сверяется с ним.
/// </summary>
public class SolverComparisonTests
{
    private const double EnergyEps = 1e-6;

    private static double[] Speeds(int stepKmh) =>
        Enumerable.Range(0, 40 / stepKmh + 1).Select(k => (20.0 + stepKmh * k) / 3.6).ToArray();

    public static TheoryData<string> SampleNames => ["TestProblem1", "A120", "A110", "B106", "B90"];

    private static GreenWaveProblem Sample(string name) => name switch
    {
        "TestProblem1" => Samples.TestProblem1(),
        "A120" => Samples.MovingStart(),
        "A110" => Samples.MovingStart(110),
        "B106" => Samples.StopIsBetter(),
        "B90" => Samples.StopIsBetter(90),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    [Theory]
    [MemberData(nameof(SampleNames))]
    public void Samples_DpMatchesBruteForce(string name)
    {
        AssertSameOptimum(new MotionModel(Sample(name)), Speeds(2));
    }

    private static IEnumerable<int> SeedRange => Enumerable.Range(0, 200);

    public static TheoryData<int> Seeds => new(SeedRange);

    [Theory]
    [MemberData(nameof(Seeds))]
    public void RandomProblems_DpMatchesBruteForce(int seed)
    {
        AssertSameOptimum(new MotionModel(RandomProblem(seed)), Speeds(4));
    }

    /// <summary>Генератор должен давать разные случаи, иначе сравнение выше ничего не проверяет.</summary>
    [Fact]
    public void RandomProblems_CoverFeasibleInfeasibleAndStops()
    {
        var routes = SeedRange
            .Select(seed => new BruteForceSolver(Speeds(4)).Solve(new MotionModel(RandomProblem(seed))))
            .ToArray();

        int infeasible = routes.Count(r => r is null);
        int withStops = routes.Count(r => r is { Stops: > 0 });
        int withoutStops = routes.Count(r => r is { Stops: 0 });

        Assert.True(infeasible >= 10, $"без решения: {infeasible}");
        Assert.True(withStops >= 10, $"с остановками: {withStops}");
        Assert.True(withoutStops >= 10, $"без остановок: {withoutStops}");
    }

    private static void AssertSameOptimum(MotionModel model, double[] speeds)
    {
        var exact = new BruteForceSolver(speeds).Solve(model);
        var dp = new DynamicProgrammingSolver(speeds).Solve(model);

        Assert.Equal(exact is null, dp is null);
        if (exact is null || dp is null) return;

        AssertValid(model.Problem, dp);
        Assert.Equal(exact.Energy, dp.Energy, EnergyEps);
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

    /// <summary>
    /// Случайная дорога из 2–4 сегментов; последний сегмент без светофора с вероятностью 1/2.
    /// MaxTime — от 1.2 до 2.5 минимально возможного времени на максимальной скорости.
    /// </summary>
    private static GreenWaveProblem RandomProblem(int seed)
    {
        var rnd = new Random(seed);
        double Uniform(double min, double max) => min + rnd.NextDouble() * (max - min);

        int count = rnd.Next(2, 5);
        var segments = new Segment[count];

        for (int i = 0; i < count; i++)
        {
            TrafficLight? light = null;
            if (i < count - 1 || rnd.Next(2) == 0)
            {
                double cycle = Math.Round(Uniform(40, 90));
                light = new TrafficLight(
                    CycleDuration: cycle,
                    Offset: Math.Round(Uniform(0, cycle)),
                    GreenDuration: Math.Round(Uniform(0.3, 0.7) * cycle));
            }

            segments[i] = new Segment(Math.Round(Uniform(100, 500)), light);
        }

        double maxSpeed = 60 / 3.6;
        double minTime = segments.Sum(s => s.Length) / maxSpeed;

        return new GreenWaveProblem(
            Road: new Road(segments),
            Vehicle: new Vehicle(Mass: 1500, Acceleration: 1.5, Deceleration: 2.5),
            Limits: new Limits(MinSpeed: 20 / 3.6, MaxSpeed: maxSpeed, MaxTime: Math.Round(minTime * Uniform(1.2, 2.5))),
            InitialSpeed: rnd.Next(2) == 0 ? 0 : Math.Round(Uniform(20, 60)) / 3.6,
            SafetyMargin: 0);
    }
}
