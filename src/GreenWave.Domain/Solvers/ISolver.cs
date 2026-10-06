using GreenWave.Domain.Motion;

namespace GreenWave.Domain.Solvers;

public sealed record RouteResult(SegmentResult[] Segments)
{
    public double[] Speeds => Segments.Select(s => s.Speed).ToArray();
    public double Energy => Segments.Sum(s => s.Energy);
    public double Time => Segments[^1].ResultState.Time;
    public int Stops => Segments.Count(s => s.Stopped);
}

public interface ISolver
{
    string Name { get; }
    RouteResult? Solve(MotionModel model);
}
