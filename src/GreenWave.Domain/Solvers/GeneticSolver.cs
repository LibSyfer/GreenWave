using GreenWave.Domain.Motion;

namespace GreenWave.Domain.Solvers;

/// <param name="SpeedStep">Шаг сетки скоростей, м/с.</param>
/// <param name="PopulationSize">Особей в поколении.</param>
/// <param name="Generations">Сколько поколений вывести после начального.</param>
/// <param name="TournamentSize">Участников турнира при выборе родителя.</param>
/// <param name="CrossoverRate">Вероятность скрещивания пары родителей (иначе дети — их копии).</param>
/// <param name="MutationRate">Вероятность мутации каждого гена; null — 1 / число сегментов.</param>
/// <param name="MaxCreep">Мутация сдвигает номер скорости на ±1..MaxCreep шагов сетки.</param>
/// <param name="EliteCount">Лучших особей, копируемых в следующее поколение без изменений.</param>
/// <param name="Seed">Начальное значение генератора случайных чисел — для воспроизводимости.</param>
public sealed record GaOptions(
    double SpeedStep,
    int PopulationSize = 60,
    int Generations = 100,
    int TournamentSize = 3,
    double CrossoverRate = 0.9,
    double? MutationRate = null,
    int MaxCreep = 3,
    int EliteCount = 2,
    int Seed = 1);

/// <summary>
/// Генетический алгоритм. Хромосома — номера скоростей из сетки по сегментам.
/// Особи сравниваются по правилу «допустимая лучше недопустимой»: сначала меньшее нарушение
/// (нехватка длины + опоздание), при равном — меньшая энергия, затем меньшее время.
/// </summary>
public sealed class GeneticSolver
    : ISolver
{
    private readonly GaOptions _options;
    public string Name => nameof(GeneticSolver);

    public GeneticSolver(GaOptions options)
    {
        if (options.PopulationSize < 2)
            throw new ArgumentOutOfRangeException(nameof(options), "в поколении должно быть хотя бы 2 особи");
        if (options.EliteCount < 0 || options.EliteCount >= options.PopulationSize)
            throw new ArgumentOutOfRangeException(nameof(options), "EliteCount должен быть в [0, PopulationSize)");
        if (options.TournamentSize < 1)
            throw new ArgumentOutOfRangeException(nameof(options), "TournamentSize должен быть >= 1");
        if (options.Generations < 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Generations должно быть >= 0");
        if (options.MaxCreep < 1)
            throw new ArgumentOutOfRangeException(nameof(options), "MaxCreep должен быть >= 1");

        _options = options;
    }

    public RouteResult? Solve(MotionModel model) => ToRoute(Run(model, null));

    /// <summary>Решение с записью всех поколений, турниров, скрещиваний и мутаций.</summary>
    public GaTracedResult SolveTraced(MotionModel model)
    {
        var generations = new List<GaGeneration>();
        var best = Run(model, generations);

        var trace = new GaTrace(model.Problem.Limits.SpeedGrid(_options.SpeedStep), generations, best.Id);
        return new GaTracedResult(ToRoute(best), trace);
    }

    private static RouteResult? ToRoute(GaIndividual best)
        => best.Feasible ? new RouteResult(best.Segments.ToArray()) : null;

    /// <returns>Лучшая особь за весь запуск.</returns>
    private GaIndividual Run(MotionModel model, List<GaGeneration>? trace)
    {
        var problem = model.Problem;
        var speeds = problem.Limits.SpeedGrid(_options.SpeedStep);
        int n = problem.Road.Segments.Length;
        double mutationRate = _options.MutationRate ?? 1.0 / n;

        var rnd = new Random(_options.Seed);
        int nextId = 0;

        GaIndividual Create(int[] genes, GaOrigin origin) => Evaluate(model, speeds, nextId++, genes, origin);

        var population = Enumerable.Range(0, _options.PopulationSize)
            .Select(_ => Create(RandomGenes(rnd, n, speeds.Length), GaOrigin.Random))
            .ToList();

        var best = Record(0, population, trace);

        for (int g = 1; g <= _options.Generations; g++)
        {
            var next = population
                .OrderBy(x => x, Comparer)
                .Take(_options.EliteCount)
                .Select(e => e with
                {
                    Id = nextId++,
                    Origin = new GaOrigin(GaOriginKind.Elite, e.Id, null, null, null, null, []),
                })
                .ToList();

            while (next.Count < _options.PopulationSize)
            {
                var s1 = Tournament(rnd, population);
                var s2 = Tournament(rnd, population);
                var p1 = population.First(x => x.Id == s1.WinnerId);
                var p2 = population.First(x => x.Id == s2.WinnerId);

                if (n > 1 && rnd.NextDouble() < _options.CrossoverRate)
                {
                    int cut = rnd.Next(1, n);
                    next.Add(Child(p1, s1, p2, s2, cut));
                    if (next.Count < _options.PopulationSize)
                        next.Add(Child(p2, s2, p1, s1, cut));
                }
                else
                {
                    next.Add(Child(p1, s1, null, null, null));
                    if (next.Count < _options.PopulationSize)
                        next.Add(Child(p2, s2, null, null, null));
                }
            }

            population = next;
            var generationBest = Record(g, population, trace);
            if (Comparer.Compare(generationBest, best) < 0)
                best = generationBest;
        }

        return best;

        // Гены [0, cut) от head, [cut, n) от tail; без tail — копия head. Затем мутации.
        GaIndividual Child(GaIndividual head, GaSelection headSelection, GaIndividual? tail, GaSelection? tailSelection, int? cut)
        {
            var genes = head.Genes.ToArray();
            if (tail is not null)
                for (int i = cut!.Value; i < n; i++)
                    genes[i] = tail.Genes[i];

            var mutations = Mutate(rnd, genes, speeds.Length, mutationRate);

            var origin = new GaOrigin(
                tail is null ? GaOriginKind.Clone : GaOriginKind.Crossover,
                head.Id,
                tail?.Id,
                headSelection,
                tailSelection,
                cut,
                mutations);

            return Create(genes, origin);
        }
    }

    private static GaIndividual Record(int index, List<GaIndividual> population, List<GaGeneration>? trace)
    {
        var best = population.MinBy(x => x, Comparer)!;
        trace?.Add(new GaGeneration(index, population.ToArray(), best.Id));
        return best;
    }

    private static int[] RandomGenes(Random rnd, int n, int speedCount)
        => Enumerable.Range(0, n).Select(_ => rnd.Next(speedCount)).ToArray();

    private GaSelection Tournament(Random rnd, List<GaIndividual> population)
    {
        var contestants = Enumerable.Range(0, _options.TournamentSize)
            .Select(_ => population[rnd.Next(population.Count)])
            .ToArray();

        var winner = contestants.MinBy(x => x, Comparer)!;
        return new GaSelection(contestants.Select(x => x.Id).ToArray(), winner.Id);
    }

    private List<GaMutation> Mutate(Random rnd, int[] genes, int speedCount, double rate)
    {
        var mutations = new List<GaMutation>();

        for (int i = 0; i < genes.Length; i++)
        {
            if (rnd.NextDouble() >= rate) continue;

            int shift = rnd.Next(1, _options.MaxCreep + 1) * (rnd.Next(2) == 0 ? -1 : 1);
            int mutated = Math.Clamp(genes[i] + shift, 0, speedCount - 1);
            if (mutated == genes[i]) continue;

            mutations.Add(new GaMutation(i, genes[i], mutated));
            genes[i] = mutated;
        }

        return mutations;
    }

    private static GaIndividual Evaluate(MotionModel model, double[] speeds, int id, int[] genes, GaOrigin origin)
    {
        var problem = model.Problem;
        var segments = new SegmentResult[genes.Length];
        var state = new CarState(0, problem.InitialSpeed);
        double energy = 0, shortage = 0;

        for (int i = 0; i < genes.Length; i++)
        {
            var result = model.Drive(problem.Road.Segments[i], state, speeds[genes[i]]);
            segments[i] = result;
            energy += result.Energy;
            shortage += result.LengthShortage;
            state = result.ResultState;
        }

        double overrun = Math.Max(0, state.Time - problem.Limits.MaxTime);
        return new GaIndividual(id, genes, segments, energy, shortage, overrun, origin);
    }

    /// <summary>Меньше — лучше: нарушение, затем энергия, затем время.</summary>
    public static IComparer<GaIndividual> Comparer { get; } = Comparer<GaIndividual>.Create((a, b) =>
    {
        int c = a.Violation.CompareTo(b.Violation);
        if (c != 0) return c;
        c = a.Energy.CompareTo(b.Energy);
        return c != 0 ? c : a.Time.CompareTo(b.Time);
    });
}
