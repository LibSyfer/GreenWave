using GreenWave.Domain;
using GreenWave.Domain.Entities;
using GreenWave.Domain.Models;
using GreenWave.Domain.Motion;
using GreenWave.Domain.Solvers;

namespace GreenWave.Domain.Tests;

public class MonteCarloTests
{
    [Fact]
    public void Replay_ExactlyReconstructsAggregateStatistics()
    {
        var problem = Samples.TestProblem1();
        var route = new DynamicProgrammingSolver(new DpOptions(2 / 3.6)).Solve(new MotionModel(problem))!;
        var options = new MonteCarloOptions(100, 10);
        var summaries = new List<MonteCarloSummary>();
        var statistics = MonteCarloSimulation.Run(problem, route.Speeds, options, summaries: summaries);
        var replays = Enumerable.Range(0, options.Experiments)
            .Select(i => MonteCarloSimulation.Replay(problem, route.Speeds, options, i)).ToArray();
        var valid = replays.Where(x => x.Route is not null).Select(x => x.Route!).ToArray();
        Assert.Equal(options.Experiments, summaries.Count);
        for (int i = 0; i < summaries.Count; i++)
        {
            var summary = summaries[i];
            var replay = replays[i];
            Assert.Equal(i + 1, summary.Number);
            Assert.Equal(replay.InvalidSegment, summary.InvalidSegment);
            Assert.Equal(replay.Route?.Time, summary.Time);
            Assert.Equal(replay.Route?.Stops, summary.Stops);
            bool newStops = replay.Route is not null && replay.Route.Segments
                .Where((s, j) => s.Stopped && !route.Segments[j].Stopped).Any();
            Assert.Equal(newStops, summary.NewStops);
            Assert.Equal(replay.Route is not null && replay.Route.Time > problem.Limits.MaxTime, summary.OverTime);
        }
        Assert.Equal(statistics.ValidExperiments, valid.Length);
        Assert.Equal(statistics.InvalidExperiments, replays.Count(x => x.InvalidSegment is not null));
        Assert.Equal(statistics.WithStops, valid.Count(x => x.Stops > 0));
        Assert.Equal(statistics.OverTime, valid.Count(x => x.Time > problem.Limits.MaxTime));
        Assert.Equal(statistics.MeanTime!.Value, valid.Average(x => x.Time), 9);
        Assert.Equal(statistics.Percentile95!.Value, valid.Select(x => x.Time).Order().ElementAt((int)Math.Ceiling(0.95 * valid.Length) - 1));
        var otherSpeeds = Enumerable.Repeat(problem.Limits.MaxSpeed, route.Speeds.Length).ToArray();
        var other = MonteCarloSimulation.Replay(problem, otherSpeeds, options, 42);
        Assert.Equal(replays[42].Problem.Road.Segments, other.Problem.Road.Segments);
    }

    [Fact]
    public void ExistingStops_AreNotClassifiedAsNew()
    {
        var problem = new GreenWaveProblem(new Road([new Segment(100, new TrafficLight(60, 30, 20))]),
            new Vehicle(1500, 1.5, 2.5), new Limits(5, 5, 100), 5, 0);
        var summaries = new List<MonteCarloSummary>();
        var result = MonteCarloSimulation.Run(problem, new[] { 5.0 }, new(10, 0), summaries: summaries);
        Assert.Equal(10, result.WithStops);
        Assert.All(summaries, s => Assert.False(s.NewStops));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10)]
    public void Replay_RejectsIndexOutsideExperimentRange(int index)
    {
        Assert.Throws<ArgumentException>(() => MonteCarloSimulation.Replay(Samples.TestProblem1(),
            Enumerable.Repeat(50 / 3.6, 5).ToArray(), new(10), index));
    }

    [Fact]
    public void ZeroShift_ReplaysOriginalRoute()
    {
        var problem = Samples.TestProblem1();
        var route = new DynamicProgrammingSolver(new DpOptions(2 / 3.6)).Solve(new MotionModel(problem))!;
        var result = MonteCarloSimulation.Run(problem, route.Speeds, new(100, 0));
        Assert.Equal(100, result.ValidExperiments);
        Assert.Equal(0, result.InvalidExperiments);
        Assert.Equal(route.Time, result.MeanTime!.Value, 8);
        Assert.Equal(route.Time, result.Percentile95!.Value, 8);
        Assert.Equal((double)route.Stops, result.MeanStops!.Value);
    }

    [Fact]
    public void Seed_ReproducesStatistics_WithoutChangingProblem()
    {
        var problem = Samples.TestProblem1();
        var offsets = problem.Road.Segments.Select(s => s.Light?.Offset).ToArray();
        var route = new DynamicProgrammingSolver(new DpOptions(2 / 3.6)).Solve(new MotionModel(problem))!;
        var a = MonteCarloSimulation.Run(problem, route.Speeds, new(1000, 3));
        var b = MonteCarloSimulation.Run(problem, route.Speeds, new(1000, 3));
        Assert.Equal(a, b);
        Assert.Equal(offsets, problem.Road.Segments.Select(s => s.Light?.Offset).ToArray());
        Assert.Equal(a.Experiments, a.ValidExperiments + a.InvalidExperiments);
        Assert.True(a.Percentile95 > 0);
        Assert.InRange(a.WithStops, 0, a.ValidExperiments);
    }

    [Fact]
    public void PhaseShift_CanIntroduceStops_AndTimeOverruns()
    {
        var problem = new GreenWaveProblem(new Road([new Segment(100, new TrafficLight(60, 19, 20))]),
            new Vehicle(1500, 1.5, 2.5), new Limits(5, 5, 21), 5, 0);
        var result = MonteCarloSimulation.Run(problem, new[] { 5.0 }, new(1000, 3));
        Assert.Equal(1000, result.ValidExperiments);
        Assert.InRange(result.WithStops, 1, 999);
        Assert.InRange(result.OverTime, 1, 999);
        Assert.True(result.Percentile95 > 21);
    }

    [Fact]
    public void InvalidGeometry_ExcludedFromTimeStatistics()
    {
        var problem = new GreenWaveProblem(new Road([new Segment(1, null)]),
            new Vehicle(1500, 1.5, 2.5), new Limits(5, 20, 100), 0, 0);
        var result = MonteCarloSimulation.Run(problem, new[] { 20.0 }, new(10));
        Assert.Equal(10, result.InvalidExperiments);
        Assert.Equal(0, result.ValidExperiments);
        Assert.Null(result.MeanTime);
        Assert.Null(result.Percentile95);
        Assert.Null(result.StopProbability);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100001)]
    public void InvalidCount_Rejected(int count)
    {
        Assert.Throws<ArgumentException>(() => MonteCarloSimulation.Run(
            Samples.TestProblem1(), Enumerable.Repeat(50 / 3.6, 5).ToArray(), new(count)));
    }
}
