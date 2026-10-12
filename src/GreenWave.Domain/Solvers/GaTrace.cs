using GreenWave.Domain.Motion;

namespace GreenWave.Domain.Solvers;

/// <summary>Как особь появилась в поколении.</summary>
public enum GaOriginKind
{
    /// <summary>Случайная особь начального поколения.</summary>
    Random,

    /// <summary>Копия одной из лучших особей прошлого поколения без изменений.</summary>
    Elite,

    /// <summary>Потомок скрещивания двух родителей (затем возможны мутации).</summary>
    Crossover,

    /// <summary>Копия одного родителя — скрещивание не выпало (затем возможны мутации).</summary>
    Clone,
}

/// <summary>Турнир: случайные участники из прошлого поколения и победитель (лучший из них).</summary>
public sealed record GaSelection(IReadOnlyList<int> ContestantIds, int WinnerId);

/// <summary>Мутация гена: номер скорости From заменён на To.</summary>
public sealed record GaMutation(int Gene, int From, int To);

/// <param name="Parent1Id">Родитель, давший гены [0, CrossoverPoint) — или все гены, если скрещивания не было.</param>
/// <param name="Parent2Id">Родитель, давший гены [CrossoverPoint, n); null, если скрещивания не было.</param>
/// <param name="Selection1">Турнир, выбравший Parent1 (нет для Random и Elite).</param>
/// <param name="Selection2">Турнир, выбравший Parent2.</param>
/// <param name="CrossoverPoint">Точка разреза хромосомы для Crossover.</param>
/// <param name="Mutations">Мутации, применённые после скрещивания/копирования.</param>
public sealed record GaOrigin(
    GaOriginKind Kind,
    int? Parent1Id,
    int? Parent2Id,
    GaSelection? Selection1,
    GaSelection? Selection2,
    int? CrossoverPoint,
    IReadOnlyList<GaMutation> Mutations)
{
    public static GaOrigin Random { get; } = new(GaOriginKind.Random, null, null, null, null, null, []);
}

/// <param name="Id">Уникальный номер особи в пределах трассы.</param>
/// <param name="Genes">Номера скоростей из SpeedGrid по сегментам.</param>
/// <param name="Segments">Проезд маршрута по модели (считается и для недопустимых особей).</param>
/// <param name="Energy">Затраты энергии.</param>
/// <param name="Shortage">Суммарная нехватка длины сегментов на разгон/торможение, м.</param>
/// <param name="Overrun">Опоздание относительно MaxTime, с.</param>
public sealed record GaIndividual(
    int Id,
    IReadOnlyList<int> Genes,
    IReadOnlyList<SegmentResult> Segments,
    double Energy,
    double Shortage,
    double Overrun,
    GaOrigin Origin)
{
    public double Time => Segments[^1].ResultState.Time;

    /// <summary>Мера недопустимости: 0 — маршрут допустим.</summary>
    public double Violation => Shortage + Overrun;

    public bool Feasible => Violation == 0;
}

/// <param name="Index">Номер поколения, 0 — начальное.</param>
/// <param name="Population">Особи поколения.</param>
/// <param name="BestId">Лучшая особь поколения.</param>
public sealed record GaGeneration(int Index, IReadOnlyList<GaIndividual> Population, int BestId);

/// <param name="SpeedGrid">Скорости, на которые ссылаются гены, м/с.</param>
/// <param name="Generations">Все поколения по порядку.</param>
/// <param name="BestId">Лучшая особь за весь запуск (она же — ответ, если допустима).</param>
public sealed record GaTrace(
    IReadOnlyList<double> SpeedGrid,
    IReadOnlyList<GaGeneration> Generations,
    int BestId);

public sealed record GaTracedResult(RouteResult? Route, GaTrace Trace);
