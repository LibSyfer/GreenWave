using GreenWave.Domain.Motion;

namespace GreenWave.Domain.Solvers;

/// <param name="SpeedStep">Шаг сетки скоростей, м/с.</param>
/// <param name="TimeStep">Ширина ячейки времени, с: состояния с одной скоростью в одной ячейке сливаются.</param>
/// <param name="RemainderPruning">Отбрасывать состояния, из которых даже на MaxSpeed не успеть к MaxTime.</param>
public sealed record DpOptions(double SpeedStep, double TimeStep = 0.1, bool RemainderPruning = true);

public sealed class DynamicProgrammingSolver
    : ISolver
{
    private sealed record DpNode(int Id, double TotalEnergy, SegmentResult? Via, DpNode? Parent);
    private const int RootId = -1;
    private readonly DpOptions _options;
    public string Name => nameof(DynamicProgrammingSolver);

    public DynamicProgrammingSolver(DpOptions options)
    {
        _options = options;
    }

    public RouteResult? Solve(MotionModel model)
    {
        var best = Search(model, null);
        return best is null ? null : new RouteResult(Path(best).Select(n => n.Via!).ToArray());
    }

    /// <summary>Решение с записью всех кандидатов и причин их отбрасывания.</summary>
    public DpTracedResult SolveTraced(MotionModel model)
    {
        var layers = new List<DpLayer>();
        var best = Search(model, layers);
        var path = best is null ? [] : Path(best);

        var trace = new DpTrace(
            model.Problem.Limits.SpeedGrid(_options.SpeedStep),
            _options.TimeStep,
            layers,
            path.Select(n => n.Id).ToArray());

        var route = best is null ? null : new RouteResult(path.Select(n => n.Via!).ToArray());
        return new DpTracedResult(route, trace);
    }

    /// <returns>Финальный узел лучшего маршрута или null, если решения нет.</returns>
    private DpNode? Search(MotionModel model, List<DpLayer>? trace)
    {
        var problem = model.Problem;
        var segments = problem.Road.Segments;
        var speeds = problem.Limits.SpeedGrid(_options.SpeedStep);
        var startState = new CarState(0, problem.InitialSpeed);

        // Нижняя оценка времени на остаток пути: быстрее max(MaxSpeed, InitialSpeed) машина не едет.
        double topSpeed = Math.Max(problem.Limits.MaxSpeed, problem.InitialSpeed);
        var remainingLength = new double[segments.Length + 1];
        for (int i = segments.Length - 1; i >= 0; i--)
            remainingLength[i] = remainingLength[i + 1] + segments[i].Length;

        int nextId = 0;
        IEnumerable<DpNode> layer = [new DpNode(RootId, 0, null, null)];

        for (int i = 0; i < segments.Length; i++)
        {
            var next = new Dictionary<(long Cell, double Speed), DpNode>();
            var candidates = trace is null ? null : new List<(DpNode Node, long Cell, DpCandidateStatus Status)>();

            foreach (var node in layer)
            {
                CarState state = node.Via?.ResultState ?? startState;

                foreach (double speed in speeds)
                {
                    var result = model.Drive(segments[i], state, speed);
                    var candidate = new DpNode(nextId++, node.TotalEnergy + result.Energy, result, node);
                    var nextCell = (long)Math.Floor(result.ResultState.Time / _options.TimeStep);

                    var status = Check(result, remainingLength[i + 1] / topSpeed, problem.Limits.MaxTime);
                    candidates?.Add((candidate, nextCell, status));

                    if (status != DpCandidateStatus.Accepted) continue;

                    var key = (nextCell, result.ResultState.Speed);

                    if (!next.TryGetValue(key, out var existing) || candidate.TotalEnergy < existing.TotalEnergy)
                        next[key] = candidate;
                }
            }

            if (trace is not null)
                trace.Add(BuildLayer(i, candidates!, next));

            if (next.Count == 0)
                return null;

            layer = next.Values;
        }

        return layer
            .OrderBy(n => n.TotalEnergy)
            .ThenBy(n => n.Via!.ResultState.Time)
            .First();
    }

    private DpCandidateStatus Check(SegmentResult result, double minRemainingTime, double maxTime)
    {
        if (result.LengthShortage > 0)
            return DpCandidateStatus.LengthShortage;

        if (result.ResultState.Time > maxTime)
            return DpCandidateStatus.TimeLimit;

        if (_options.RemainderPruning && result.ResultState.Time + minRemainingTime > maxTime)
            return DpCandidateStatus.RemainderTimeLimit;

        return DpCandidateStatus.Accepted;
    }

    /// <summary>Допустимые кандидаты, не ставшие лучшими в своей ячейке, помечаются как Dominated.</summary>
    private static DpLayer BuildLayer(
        int segmentIndex,
        List<(DpNode Node, long Cell, DpCandidateStatus Status)> candidates,
        Dictionary<(long Cell, double Speed), DpNode> winners)
    {
        var result = candidates.Select(c =>
        {
            var status = c.Status;
            int? dominatedBy = null;

            if (status == DpCandidateStatus.Accepted)
            {
                var winner = winners[(c.Cell, c.Node.Via!.ResultState.Speed)];
                if (winner.Id != c.Node.Id)
                {
                    status = DpCandidateStatus.Dominated;
                    dominatedBy = winner.Id;
                }
            }

            return new DpCandidate(
                c.Node.Id,
                c.Node.Parent!.Id == RootId ? null : c.Node.Parent.Id,
                c.Node.Via!,
                c.Node.TotalEnergy,
                c.Cell,
                status,
                dominatedBy);
        }).ToArray();

        return new DpLayer(segmentIndex, result);
    }

    /// <summary>Узлы маршрута от первого сегмента до <paramref name="finish"/>.</summary>
    private static DpNode[] Path(DpNode finish)
    {
        var nodes = new List<DpNode>();

        for (var node = finish; node.Via is not null; node = node.Parent!)
            nodes.Add(node);

        nodes.Reverse();
        return nodes.ToArray();
    }
}
