using GreenWave.Domain.Motion;

namespace GreenWave.Domain.Solvers;

/// <param name="SpeedStep">Шаг сетки скоростей, м/с.</param>
/// <param name="TimeStep">Ширина ячейки времени, с: состояния с одной скоростью в одной ячейке сливаются.</param>
/// <param name="RemainderPruning">Отбрасывать состояния, из которых даже на MaxSpeed не успеть к MaxTime.</param>
public sealed record DpOptions(double SpeedStep, double TimeStep = 0.1, bool RemainderPruning = true);

public sealed class DynamicProgrammingSolver
    : ISolver
{
    private sealed record DpNode(double TotalEnergy, SegmentResult? Via, DpNode? Parent);
    private readonly DpOptions _options;
    public string Name => nameof(DynamicProgrammingSolver);

    public DynamicProgrammingSolver(DpOptions options)
    {
        _options = options;
    }

    public RouteResult? Solve(MotionModel model)
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

        IEnumerable<DpNode> layer = [new DpNode(0, null, null)];

        for (int i = 0; i < segments.Length; i++)
        {
            var next = new Dictionary<(long Cell, double Speed), DpNode>();

            foreach (var node in layer)
            {
                CarState state = node.Via?.ResultState ?? startState;

                foreach (double speed in speeds)
                {
                    var result = model.Drive(segments[i], state, speed);

                    if (result.LengthShortage > 0) continue;

                    double minFinishTime = result.ResultState.Time
                        + (_options.RemainderPruning ? remainingLength[i + 1] / topSpeed : 0);
                    if (minFinishTime > problem.Limits.MaxTime) continue;

                    var candidate = new DpNode(node.TotalEnergy + result.Energy, result, node);

                    var nextCell = (long)Math.Floor(result.ResultState.Time / _options.TimeStep);
                    var nextSpeed = result.ResultState.Speed;
                    var key = (nextCell, nextSpeed);

                    if (!next.TryGetValue(key, out var existing) || candidate.TotalEnergy < existing.TotalEnergy)
                        next[key] = candidate;
                }
            }

            if (next.Count == 0)
                return null;

            layer = next.Values;
        }

        var best = layer
            .OrderBy(n => n.TotalEnergy)
            .ThenBy(n => n.Via!.ResultState.Time)
            .First();

        return BuildRoute(best);
    }

    private static RouteResult BuildRoute(DpNode finish)
    {
        var segments = new List<SegmentResult>();

        for (var node = finish; node.Via is not null; node = node.Parent!)
            segments.Add(node.Via);

        segments.Reverse();
        return new RouteResult(segments.ToArray());
    }
}
