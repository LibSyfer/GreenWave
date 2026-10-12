using GreenWave.Domain.Motion;
using GreenWave.Domain.Solvers;

namespace GreenWave.Api;

/// <summary>
/// Трасса ДП для клиента. Кандидатов десятки тысяч, поэтому они передаются
/// не списком объектов, а по колонкам: i-й элемент каждого массива — i-й кандидат.
/// </summary>
/// <param name="StatusNames">Имена статусов; Candidates.Status — индексы в этом массиве.</param>
/// <param name="Layers">Счётчики статусов по сегментам.</param>
/// <param name="BestPath">Индексы кандидатов лучшего маршрута по сегментам.</param>
public sealed record DpTraceDto(
    IReadOnlyList<double> SpeedGrid,
    double TimeStep,
    IReadOnlyList<string> StatusNames,
    IReadOnlyList<DpLayerDto> Layers,
    DpCandidatesDto Candidates,
    IReadOnlyList<int> BestPath)
{
    /// <summary>Трассы больше этого размера не отдаём — браузер не справится.</summary>
    public const int MaxCandidates = 300_000;

    public static DpTraceDto From(DpTrace trace)
    {
        var all = trace.Layers.SelectMany(l => l.Candidates).ToArray();
        if (all.Length > MaxCandidates)
            throw new ArgumentException(
                $"Трасса ДП слишком большая: {all.Length:N0} кандидатов (допустимо {MaxCandidates:N0}). Увеличьте шаг скорости.");

        // В ДП Id выдаются подряд в порядке слоёв, поэтому индекс в массиве совпадает с Id.
        var index = new Dictionary<int, int>(all.Length);
        for (int i = 0; i < all.Length; i++)
            index[all[i].Id] = i;

        var layerOf = trace.Layers.SelectMany(l => l.Candidates.Select(_ => l.SegmentIndex)).ToArray();

        var candidates = new DpCandidatesDto(
            Parent: all.Select(c => c.ParentId is { } p ? index[p] : -1).ToArray(),
            Layer: layerOf,
            Speed: all.Select(c => Round(c.Result.Speed)).ToArray(),
            ArrivalTime: all.Select(c => Round(c.Result.ArrivalTime)).ToArray(),
            Time: all.Select(c => Round(c.Result.ResultState.Time)).ToArray(),
            EndSpeed: all.Select(c => Round(c.Result.ResultState.Speed)).ToArray(),
            Stopped: all.Select(c => c.Result.Stopped).ToArray(),
            Energy: all.Select(c => Math.Round(c.TotalEnergy)).ToArray(),
            Status: all.Select(c => (int)c.Status).ToArray(),
            DominatedBy: all.Select(c => c.DominatedById is { } d ? index[d] : -1).ToArray());

        var layers = trace.Layers
            .Select(l => new DpLayerDto(
                l.SegmentIndex,
                Enum.GetValues<DpCandidateStatus>().Select(s => l.Candidates.Count(c => c.Status == s)).ToArray()))
            .ToArray();

        return new DpTraceDto(
            trace.SpeedGrid,
            trace.TimeStep,
            Enum.GetNames<DpCandidateStatus>(),
            layers,
            candidates,
            trace.BestPath.Select(id => index[id]).ToArray());
    }

    private static double Round(double value) => Math.Round(value, 3);
}

/// <param name="Counts">Число кандидатов сегмента по каждому статусу (порядок — как StatusNames).</param>
public sealed record DpLayerDto(int SegmentIndex, IReadOnlyList<int> Counts);

/// <param name="Parent">Индекс кандидата-родителя; -1 — старт.</param>
/// <param name="Layer">Номер сегмента.</param>
/// <param name="Speed">Выбранная скорость на сегменте, м/с.</param>
/// <param name="ArrivalTime">Момент у стоп-линии, с.</param>
/// <param name="Time">Момент проезда светофора (конца сегмента), с.</param>
/// <param name="EndSpeed">Скорость после проезда, м/с (0 — была остановка).</param>
/// <param name="Stopped">Была ли остановка у светофора.</param>
/// <param name="Energy">Энергия от старта до конца сегмента, Дж.</param>
/// <param name="Status">Индекс статуса в StatusNames.</param>
/// <param name="DominatedBy">Для Dominated — индекс победителя в ячейке, иначе -1.</param>
public sealed record DpCandidatesDto(
    int[] Parent,
    int[] Layer,
    double[] Speed,
    double[] ArrivalTime,
    double[] Time,
    double[] EndSpeed,
    bool[] Stopped,
    double[] Energy,
    int[] Status,
    int[] DominatedBy);
