using GreenWave.Domain.Solvers;

namespace GreenWave.Api;

/// <summary>
/// Трасса ГА для клиента. Особи всех поколений идут подряд (поколение за поколением)
/// и передаются по колонкам: i-й элемент каждого массива — i-я особь.
/// Поля «на сегмент» (гены, времена) — плоские массивы длиной Count × SegmentCount.
/// </summary>
/// <param name="OriginNames">Имена видов происхождения; Individuals.Origin — индексы в этом массиве.</param>
/// <param name="GenerationStart">Индекс первой особи каждого поколения; поколение g — [GenerationStart[g], GenerationStart[g + 1]).</param>
/// <param name="GenerationBest">Индекс лучшей особи каждого поколения.</param>
/// <param name="Best">Индекс лучшей особи за весь запуск.</param>
public sealed record GaTraceDto(
    IReadOnlyList<double> SpeedGrid,
    int SegmentCount,
    GaOptionsDto Options,
    IReadOnlyList<string> OriginNames,
    int[] GenerationStart,
    int[] GenerationBest,
    int Best,
    GaIndividualsDto Individuals)
{
    public const int MaxIndividuals = 50_000;

    public static GaTraceDto From(GaTrace trace, GaOptions options)
    {
        var all = trace.Generations.SelectMany(g => g.Population).ToArray();
        int n = all.Length == 0 ? 0 : all[0].Genes.Count;
        int k = options.TournamentSize;

        var index = new Dictionary<int, int>(all.Length);
        for (int i = 0; i < all.Length; i++)
            index[all[i].Id] = i;

        int Ref(int? id) => id is { } v ? index[v] : -1;

        var generationStart = new int[trace.Generations.Count + 1];
        for (int g = 0; g < trace.Generations.Count; g++)
            generationStart[g + 1] = generationStart[g] + trace.Generations[g].Population.Count;

        // турниры: по TournamentSize участников на особь, -1 — турнира не было
        int[] Contestants(Func<GaOrigin, GaSelection?> selection) => all
            .SelectMany(x => selection(x.Origin) is { } s
                ? s.ContestantIds.Select(id => index[id])
                : Enumerable.Repeat(-1, k))
            .ToArray();

        var mutationStart = new int[all.Length + 1];
        for (int i = 0; i < all.Length; i++)
            mutationStart[i + 1] = mutationStart[i] + all[i].Origin.Mutations.Count;
        var mutations = all.SelectMany(x => x.Origin.Mutations).ToArray();

        var individuals = new GaIndividualsDto(
            Generation: trace.Generations.SelectMany(g => g.Population.Select(_ => g.Index)).ToArray(),
            Genes: all.SelectMany(x => x.Genes).ToArray(),
            ArrivalTime: all.SelectMany(x => x.Segments.Select(s => Round(s.ArrivalTime))).ToArray(),
            PassTime: all.SelectMany(x => x.Segments.Select(s => Round(s.ResultState.Time))).ToArray(),
            Stopped: all.SelectMany(x => x.Segments.Select(s => s.Stopped)).ToArray(),
            Energy: all.Select(x => Math.Round(x.Energy)).ToArray(),
            Time: all.Select(x => Round(x.Time)).ToArray(),
            Shortage: all.Select(x => Round(x.Shortage)).ToArray(),
            Overrun: all.Select(x => Round(x.Overrun)).ToArray(),
            Origin: all.Select(x => (int)x.Origin.Kind).ToArray(),
            Parent1: all.Select(x => Ref(x.Origin.Parent1Id)).ToArray(),
            Parent2: all.Select(x => Ref(x.Origin.Parent2Id)).ToArray(),
            CrossoverPoint: all.Select(x => x.Origin.CrossoverPoint ?? -1).ToArray(),
            Tournament1: Contestants(o => o.Selection1),
            Tournament2: Contestants(o => o.Selection2),
            MutationStart: mutationStart,
            MutationGene: mutations.Select(m => m.Gene).ToArray(),
            MutationFrom: mutations.Select(m => m.From).ToArray(),
            MutationTo: mutations.Select(m => m.To).ToArray());

        return new GaTraceDto(
            trace.SpeedGrid,
            n,
            GaOptionsDto.From(options, n),
            Enum.GetNames<GaOriginKind>(),
            generationStart,
            trace.Generations.Select(g => index[g.BestId]).ToArray(),
            index[trace.BestId],
            individuals);
    }

    private static double Round(double value) => Math.Round(value, 3);
}

public sealed record GaOptionsDto(int PopulationSize, int Generations, int TournamentSize, double CrossoverRate, double MutationRate, int MaxCreep, int EliteCount, int Seed)
{
    public static GaOptionsDto From(GaOptions o, int segmentCount) => new(
        o.PopulationSize, o.Generations, o.TournamentSize, o.CrossoverRate,
        o.MutationRate ?? 1.0 / segmentCount, o.MaxCreep, o.EliteCount, o.Seed);
}

/// <param name="Generation">Номер поколения особи.</param>
/// <param name="Genes">Номера скоростей из SpeedGrid; особь i — [i·n, (i+1)·n).</param>
/// <param name="ArrivalTime">Момент у стоп-линии каждого сегмента, с (по n на особь).</param>
/// <param name="PassTime">Момент проезда конца каждого сегмента, с (по n на особь).</param>
/// <param name="Stopped">Остановка у светофора каждого сегмента (по n на особь).</param>
/// <param name="Shortage">Нехватка длины на разгон/торможение, м; > 0 — особь недопустима.</param>
/// <param name="Overrun">Опоздание относительно Tmax, с; > 0 — особь недопустима.</param>
/// <param name="Origin">Индекс вида происхождения в OriginNames.</param>
/// <param name="Parent1">Родитель, давший гены [0, CrossoverPoint) или все гены; -1 — нет.</param>
/// <param name="Parent2">Родитель, давший гены [CrossoverPoint, n); -1 — скрещивания не было.</param>
/// <param name="Tournament1">Участники турнира за Parent1, по TournamentSize на особь; -1 — турнира не было.</param>
/// <param name="MutationStart">Мутации особи i — [MutationStart[i], MutationStart[i + 1]) в массивах Mutation*.</param>
public sealed record GaIndividualsDto(
    int[] Generation,
    int[] Genes,
    double[] ArrivalTime,
    double[] PassTime,
    bool[] Stopped,
    double[] Energy,
    double[] Time,
    double[] Shortage,
    double[] Overrun,
    int[] Origin,
    int[] Parent1,
    int[] Parent2,
    int[] CrossoverPoint,
    int[] Tournament1,
    int[] Tournament2,
    int[] MutationStart,
    int[] MutationGene,
    int[] MutationFrom,
    int[] MutationTo);
