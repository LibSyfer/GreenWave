using GreenWave.Domain.Motion;

namespace GreenWave.Domain.Solvers;

public sealed record DpOptions(double Dt, bool RemainderPruning);

public sealed class DynamicProgrammingSolver
    : ISolver
{
    private sealed record DpNode(double TotalEnergy, SegmentResult? Via, DpNode? Parent);
    private readonly IReadOnlyList<double> _speeds;
    private readonly double _timeStep;
    public string Name => nameof(DynamicProgrammingSolver);

    public DynamicProgrammingSolver(IReadOnlyList<double> speed, double timeStep = 0.1)
    {
        _speeds = speed;
        _timeStep = timeStep;
    }

    public RouteResult? Solve(MotionModel model)
    {
        var problem = model.Problem;
        var startState = new CarState(0, problem.InitialSpeed);

        IEnumerable<DpNode> layer = [new DpNode(0, null, null)];

        foreach (var segment in problem.Road.Segments)
        {
            var next = new Dictionary<(long Cell, double Speed), DpNode>();

            foreach (var node in layer)
            {
                CarState state = node.Via?.ResultState ?? startState;
                
                foreach (double speed in _speeds)
                {
                    var result = model.Drive(segment, state, speed);

                    if (result.LengthShortage > 0) continue;
                    if (result.ResultState.Time > problem.Limits.MaxTime) continue;

                    var condidate = new DpNode(node.TotalEnergy + result.Energy, result, node);

                    var nextCell = (long)Math.Floor(result.ResultState.Time / _timeStep);
                    var nextSpeed = result.ResultState.Speed;
                    var key = (nextCell, nextSpeed);

                    if (!next.TryGetValue(key, out var existing) || condidate.TotalEnergy < existing.TotalEnergy)
                        next[key] = condidate;
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
