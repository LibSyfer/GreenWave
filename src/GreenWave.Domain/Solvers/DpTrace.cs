using GreenWave.Domain.Motion;

namespace GreenWave.Domain.Solvers;

/// <summary>Почему кандидат ДП остался или был отброшен.</summary>
public enum DpCandidateStatus
{
    /// <summary>Лучший в своей ячейке (время, скорость) — переходит в следующий слой.</summary>
    Accepted,

    /// <summary>Не хватает длины сегмента на разгон/торможение.</summary>
    LengthShortage,

    /// <summary>Время после сегмента уже больше MaxTime.</summary>
    TimeLimit,

    /// <summary>Сейчас MaxTime не превышено, но остаток пути даже на максимальной скорости не успеть.</summary>
    RemainderTimeLimit,

    /// <summary>В той же ячейке есть кандидат с меньшей энергией (см. DominatedById).</summary>
    Dominated,
}

/// <param name="Id">Уникальный номер кандидата в пределах трассы.</param>
/// <param name="ParentId">Кандидат предыдущего слоя, из состояния которого сделан шаг; null — старт.</param>
/// <param name="Result">Результат проезда сегмента на выбранной скорости (Result.Speed).</param>
/// <param name="TotalEnergy">Энергия от старта до конца сегмента включительно.</param>
/// <param name="TimeCell">Номер ячейки времени: floor(Result.ResultState.Time / TimeStep).</param>
/// <param name="DominatedById">Для Dominated — победитель в той же ячейке.</param>
public sealed record DpCandidate(
    int Id,
    int? ParentId,
    SegmentResult Result,
    double TotalEnergy,
    long TimeCell,
    DpCandidateStatus Status,
    int? DominatedById);

/// <summary>Все кандидаты, порождённые на одном сегменте.</summary>
public sealed record DpLayer(int SegmentIndex, IReadOnlyList<DpCandidate> Candidates);

/// <param name="SpeedGrid">Скорости, которые перебирались на каждом сегменте, м/с.</param>
/// <param name="TimeStep">Ширина ячейки времени, с.</param>
/// <param name="Layers">Слои по сегментам; если решения нет, трасса обрывается на слое без выживших.</param>
/// <param name="BestPath">Id кандидатов лучшего маршрута по сегментам; пусто, если решения нет.</param>
public sealed record DpTrace(
    IReadOnlyList<double> SpeedGrid,
    double TimeStep,
    IReadOnlyList<DpLayer> Layers,
    IReadOnlyList<int> BestPath);

public sealed record DpTracedResult(RouteResult? Route, DpTrace Trace);
