using GreenWave.Domain.Models;
using GreenWave.Domain.Motion;
using GreenWave.Domain.Solvers;
using static GreenWave.Domain.Tests.TestProblems;

namespace GreenWave.Domain.Tests;

/// <summary>
/// Трасса ДП должна полностью и непротиворечиво описывать ход решения:
/// по ней можно пересчитать каждого кандидата и восстановить ответ решателя.
/// </summary>
public class DpTraceTests
{
    private const double Eps = 1e-9;

    [Theory]
    [MemberData(nameof(SampleNames), MemberType = typeof(TestProblems))]
    public void Samples_TraceIsConsistent(string name)
    {
        AssertTraceConsistent(Sample(name), new DpOptions(Kmh(2)));
    }

    [Theory]
    [MemberData(nameof(Seeds), MemberType = typeof(TestProblems))]
    public void RandomProblems_TraceIsConsistent(int seed)
    {
        AssertTraceConsistent(TestProblems.Random(seed), new DpOptions(Kmh(4)));
    }

    [Fact]
    public void RandomProblems_CoverAllStatuses()
    {
        var statuses = SeedRange
            .SelectMany(seed => Trace(TestProblems.Random(seed), new DpOptions(Kmh(4))).Trace.Layers)
            .SelectMany(layer => layer.Candidates)
            .Select(c => c.Status)
            .ToHashSet();

        Assert.Equal(Enum.GetValues<DpCandidateStatus>().ToHashSet(), statuses);
    }

    [Fact]
    public void WithoutRemainderPruning_NoRemainderTimeLimit()
    {
        var statuses = SeedRange
            .SelectMany(seed => Trace(TestProblems.Random(seed), new DpOptions(Kmh(4), RemainderPruning: false)).Trace.Layers)
            .SelectMany(layer => layer.Candidates)
            .Select(c => c.Status);

        Assert.DoesNotContain(DpCandidateStatus.RemainderTimeLimit, statuses);
    }

    [Fact]
    public void Infeasible_TraceEndsWithoutSurvivors()
    {
        var (route, trace) = Trace(Samples.StopIsBetter(90), new DpOptions(Kmh(2)));

        Assert.Null(route);
        Assert.Empty(trace.BestPath);
        Assert.NotEmpty(trace.Layers);
        Assert.DoesNotContain(trace.Layers[^1].Candidates, c => c.Status == DpCandidateStatus.Accepted);
    }

    private static DpTracedResult Trace(GreenWaveProblem problem, DpOptions options)
        => new DynamicProgrammingSolver(options).SolveTraced(new MotionModel(problem));

    private static void AssertTraceConsistent(GreenWaveProblem problem, DpOptions options)
    {
        var model = new MotionModel(problem);
        var solver = new DynamicProgrammingSolver(options);
        var (route, trace) = solver.SolveTraced(model);
        var plain = solver.Solve(model);

        // трассировка не меняет ответ
        Assert.Equal(plain?.Segments, route?.Segments);

        Assert.Equal(problem.Limits.SpeedGrid(options.SpeedStep), trace.SpeedGrid);
        Assert.Equal(options.TimeStep, trace.TimeStep);

        var all = trace.Layers.SelectMany(l => l.Candidates).ToArray();
        Assert.Equal(all.Length, all.Select(c => c.Id).Distinct().Count());
        var byId = all.ToDictionary(c => c.Id);

        var segments = problem.Road.Segments;
        double topSpeed = Math.Max(problem.Limits.MaxSpeed, problem.InitialSpeed);

        for (int i = 0; i < trace.Layers.Count; i++)
        {
            var layer = trace.Layers[i];
            Assert.Equal(i, layer.SegmentIndex);

            DpCandidate?[] parents = i == 0
                ? [null]
                : [.. trace.Layers[i - 1].Candidates.Where(c => c.Status == DpCandidateStatus.Accepted)];

            // от каждого выжившего родителя — по одному кандидату на каждую скорость сетки (порядок родителей не важен)
            Assert.Equal(parents.Length * trace.SpeedGrid.Count, layer.Candidates.Count);
            Assert.Equal(
                parents.SelectMany(p => trace.SpeedGrid.Select(v => (p?.Id, v))).Order(),
                layer.Candidates.Select(c => (c.ParentId, c.Result.Speed)).Order());

            double remaining = segments.Skip(i + 1).Sum(s => s.Length);

            foreach (var c in layer.Candidates)
            {
                var parent = c.ParentId is { } pid ? byId[pid] : null;
                var start = parent?.Result.ResultState ?? new CarState(0, problem.InitialSpeed);

                Assert.Equal(model.Drive(segments[i], start, c.Result.Speed), c.Result);
                Assert.Equal((parent?.TotalEnergy ?? 0) + c.Result.Energy, c.TotalEnergy, Eps);
                Assert.Equal((long)Math.Floor(c.Result.ResultState.Time / trace.TimeStep), c.TimeCell);

                double time = c.Result.ResultState.Time;
                bool feasible = c.Result.LengthShortage == 0
                    && time <= problem.Limits.MaxTime
                    && !(options.RemainderPruning && time + remaining / topSpeed > problem.Limits.MaxTime);

                switch (c.Status)
                {
                    case DpCandidateStatus.LengthShortage:
                        Assert.True(c.Result.LengthShortage > 0);
                        break;
                    case DpCandidateStatus.TimeLimit:
                        Assert.Equal(0, c.Result.LengthShortage);
                        Assert.True(time > problem.Limits.MaxTime);
                        break;
                    case DpCandidateStatus.RemainderTimeLimit:
                        Assert.Equal(0, c.Result.LengthShortage);
                        Assert.True(time <= problem.Limits.MaxTime);
                        Assert.True(time + remaining / topSpeed > problem.Limits.MaxTime);
                        break;
                    case DpCandidateStatus.Dominated:
                        Assert.True(feasible);
                        var winner = byId[c.DominatedById!.Value];
                        Assert.Contains(winner, layer.Candidates);
                        Assert.Equal(DpCandidateStatus.Accepted, winner.Status);
                        Assert.Equal(c.TimeCell, winner.TimeCell);
                        Assert.Equal(c.Result.ResultState.Speed, winner.Result.ResultState.Speed);
                        Assert.True(winner.TotalEnergy <= c.TotalEnergy);
                        break;
                    case DpCandidateStatus.Accepted:
                        Assert.True(feasible);
                        break;
                }

                if (c.Status != DpCandidateStatus.Dominated)
                    Assert.Null(c.DominatedById);
            }

            // в каждой ячейке (время, скорость) — ровно один принятый
            var accepted = layer.Candidates.Where(c => c.Status == DpCandidateStatus.Accepted).ToArray();
            Assert.Equal(
                accepted.Length,
                accepted.Select(c => (c.TimeCell, c.Result.ResultState.Speed)).Distinct().Count());
        }

        if (route is null)
        {
            Assert.Empty(trace.BestPath);
            return;
        }

        // лучший путь — цепочка принятых кандидатов, совпадающая с маршрутом
        Assert.Equal(segments.Length, trace.Layers.Count);
        Assert.Equal(segments.Length, trace.BestPath.Count);

        for (int i = 0; i < trace.BestPath.Count; i++)
        {
            var c = byId[trace.BestPath[i]];
            Assert.Contains(c, trace.Layers[i].Candidates);
            Assert.Equal(DpCandidateStatus.Accepted, c.Status);
            Assert.Equal(i == 0 ? null : trace.BestPath[i - 1], c.ParentId);
            Assert.Equal(route.Segments[i], c.Result);
        }

        var finish = byId[trace.BestPath[^1]];
        double minEnergy = trace.Layers[^1].Candidates
            .Where(c => c.Status == DpCandidateStatus.Accepted)
            .Min(c => c.TotalEnergy);
        Assert.Equal(minEnergy, finish.TotalEnergy);
    }
}
