using GreenWave.Domain.Models;
using GreenWave.Domain.Motion;
using GreenWave.Domain.Solvers;
using static GreenWave.Domain.Tests.TestProblems;

namespace GreenWave.Domain.Tests;

/// <summary>
/// Трасса ГА должна полностью описывать эволюцию: по ней можно восстановить
/// гены каждой особи из генов родителей, точки разреза и мутаций.
/// </summary>
public class GaTraceTests
{
    private const double Eps = 1e-9;

    [Theory]
    [MemberData(nameof(SampleNames), MemberType = typeof(TestProblems))]
    public void Samples_TraceIsConsistent(string name)
    {
        AssertTraceConsistent(Sample(name), new GaOptions(Kmh(2)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void RandomProblems_TraceIsConsistent(int seed)
    {
        AssertTraceConsistent(TestProblems.Random(seed), new GaOptions(Kmh(4), Generations: 30, Seed: seed));
    }

    [Fact]
    public void NoElites_OddPopulation_HighMutation_TraceIsConsistent()
    {
        var options = new GaOptions(Kmh(2), PopulationSize: 25, Generations: 20, EliteCount: 0, MutationRate: 0.5);
        AssertTraceConsistent(Samples.TestProblem1(), options);
    }

    [Fact]
    public void Trace_CoversAllOriginKindsAndMutations()
    {
        var trace = new GeneticSolver(new GaOptions(Kmh(2))).SolveTraced(new MotionModel(Samples.TestProblem1())).Trace;
        var individuals = trace.Generations.SelectMany(g => g.Population).ToArray();

        Assert.Equal(Enum.GetValues<GaOriginKind>().ToHashSet(), individuals.Select(x => x.Origin.Kind).ToHashSet());
        Assert.Contains(individuals, x => x.Origin.Mutations.Count > 0);
    }

    [Fact]
    public void TracedAndPlain_SameResult()
    {
        var model = new MotionModel(Samples.TestProblem1());
        var solver = new GeneticSolver(new GaOptions(Kmh(2)));

        Assert.Equal(solver.Solve(model)?.Segments, solver.SolveTraced(model).Route?.Segments);
    }

    private static void AssertTraceConsistent(GreenWaveProblem problem, GaOptions options)
    {
        var model = new MotionModel(problem);
        var (route, trace) = new GeneticSolver(options).SolveTraced(model);
        var segments = problem.Road.Segments;
        int n = segments.Length;

        Assert.Equal(problem.Limits.SpeedGrid(options.SpeedStep), trace.SpeedGrid);
        Assert.Equal(options.Generations + 1, trace.Generations.Count);

        var all = trace.Generations.SelectMany(g => g.Population).ToArray();
        Assert.Equal(all.Length, all.Select(x => x.Id).Distinct().Count());
        var byId = all.ToDictionary(x => x.Id);

        for (int g = 0; g < trace.Generations.Count; g++)
        {
            var generation = trace.Generations[g];
            Assert.Equal(g, generation.Index);
            Assert.Equal(options.PopulationSize, generation.Population.Count);

            foreach (var x in generation.Population)
                AssertEvaluation(model, trace.SpeedGrid, x);

            Assert.Equal(generation.Population.MinBy(x => x, GeneticSolver.Comparer)!.Id, generation.BestId);

            if (g == 0)
            {
                Assert.All(generation.Population, x => Assert.Equal(GaOriginKind.Random, x.Origin.Kind));
                continue;
            }

            var previous = trace.Generations[g - 1].Population;

            // элита — лучшие особи прошлого поколения, по порядку и без изменений
            var elites = generation.Population.Take(options.EliteCount).ToArray();
            var expectedElites = previous.OrderBy(x => x, GeneticSolver.Comparer).Take(options.EliteCount).ToArray();
            for (int i = 0; i < elites.Length; i++)
            {
                Assert.Equal(GaOriginKind.Elite, elites[i].Origin.Kind);
                Assert.Equal(expectedElites[i].Id, elites[i].Origin.Parent1Id);
                Assert.Equal(expectedElites[i].Genes, elites[i].Genes);
                Assert.Empty(elites[i].Origin.Mutations);
            }

            foreach (var child in generation.Population.Skip(options.EliteCount))
            {
                var origin = child.Origin;
                Assert.Contains(origin.Kind, new[] { GaOriginKind.Crossover, GaOriginKind.Clone });

                AssertSelection(previous, options, origin.Selection1!, origin.Parent1Id!.Value);
                var genes = byId[origin.Parent1Id.Value].Genes.ToArray();

                if (origin.Kind == GaOriginKind.Crossover)
                {
                    AssertSelection(previous, options, origin.Selection2!, origin.Parent2Id!.Value);
                    Assert.InRange(origin.CrossoverPoint!.Value, 1, n - 1);

                    var tail = byId[origin.Parent2Id.Value].Genes;
                    for (int i = origin.CrossoverPoint.Value; i < n; i++)
                        genes[i] = tail[i];
                }
                else
                {
                    Assert.Null(origin.Parent2Id);
                    Assert.Null(origin.Selection2);
                    Assert.Null(origin.CrossoverPoint);
                }

                // мутации — по возрастанию номера гена, каждая меняет ген на ±1..MaxCreep
                Assert.Equal(origin.Mutations.Select(m => m.Gene).Order(), origin.Mutations.Select(m => m.Gene));
                Assert.Equal(origin.Mutations.Count, origin.Mutations.Select(m => m.Gene).Distinct().Count());
                foreach (var m in origin.Mutations)
                {
                    Assert.Equal(genes[m.Gene], m.From);
                    Assert.NotEqual(m.From, m.To);
                    Assert.InRange(Math.Abs(m.To - m.From), 1, options.MaxCreep);
                    Assert.InRange(m.To, 0, trace.SpeedGrid.Count - 1);
                    genes[m.Gene] = m.To;
                }

                Assert.Equal(genes, child.Genes);
            }

            // с элитой лучший результат поколения не ухудшается
            if (options.EliteCount > 0)
                Assert.True(GeneticSolver.Comparer.Compare(byId[generation.BestId], byId[trace.Generations[g - 1].BestId]) <= 0);
        }

        var best = byId[trace.BestId];
        Assert.Equal(all.MinBy(x => x, GeneticSolver.Comparer)!.Violation, best.Violation);
        Assert.Equal(all.MinBy(x => x, GeneticSolver.Comparer)!.Energy, best.Energy);

        if (best.Feasible)
            Assert.Equal(best.Segments, route!.Segments);
        else
            Assert.Null(route);
    }

    private static void AssertSelection(IReadOnlyList<GaIndividual> previous, GaOptions options, GaSelection selection, int parentId)
    {
        Assert.Equal(options.TournamentSize, selection.ContestantIds.Count);
        var contestants = selection.ContestantIds.Select(id => previous.Single(x => x.Id == id)).ToArray();

        Assert.Equal(parentId, selection.WinnerId);
        Assert.Contains(parentId, selection.ContestantIds);
        Assert.Equal(contestants.MinBy(x => x, GeneticSolver.Comparer)!.Id, selection.WinnerId);
    }

    /// <summary>Энергия, нехватка длины и опоздание особи совпадают с пересчётом по модели.</summary>
    private static void AssertEvaluation(MotionModel model, IReadOnlyList<double> speeds, GaIndividual x)
    {
        var problem = model.Problem;
        var state = new CarState(0, problem.InitialSpeed);
        double energy = 0, shortage = 0;

        Assert.Equal(problem.Road.Segments.Length, x.Genes.Count);

        for (int i = 0; i < x.Genes.Count; i++)
        {
            var result = model.Drive(problem.Road.Segments[i], state, speeds[x.Genes[i]]);
            Assert.Equal(result, x.Segments[i]);
            energy += result.Energy;
            shortage += result.LengthShortage;
            state = result.ResultState;
        }

        Assert.Equal(energy, x.Energy, Eps);
        Assert.Equal(shortage, x.Shortage, Eps);
        Assert.Equal(Math.Max(0, state.Time - problem.Limits.MaxTime), x.Overrun, Eps);
    }
}
