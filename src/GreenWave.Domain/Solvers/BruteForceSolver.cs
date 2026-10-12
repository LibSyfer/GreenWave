using GreenWave.Domain.Motion;

namespace GreenWave.Domain.Solvers;

/// <summary>
/// Полный перебор всех комбинаций скоростей по сегментам (точное время, без дискретизации).
/// Ветка обрывается, только если она уже недопустима (не хватает длины или превышено MaxTime),
/// поэтому результат — точный оптимум на заданном наборе скоростей.
/// </summary>
public sealed class BruteForceSolver
    : ISolver
{
    private readonly double _speedStep;
    public string Name => nameof(BruteForceSolver);

    /// <param name="speedStep">Шаг сетки скоростей, м/с.</param>
    public BruteForceSolver(double speedStep)
    {
        _speedStep = speedStep;
    }

    public RouteResult? Solve(MotionModel model)
    {
        var problem = model.Problem;
        var segments = problem.Road.Segments;
        var speeds = problem.Limits.SpeedGrid(_speedStep);
        var path = new SegmentResult[segments.Length];

        SegmentResult[]? best = null;
        double bestEnergy = double.PositiveInfinity;
        double bestTime = double.PositiveInfinity;

        void Search(int index, CarState state, double energy)
        {
            if (index == segments.Length)
            {
                if (energy < bestEnergy || (energy == bestEnergy && state.Time < bestTime))
                {
                    best = (SegmentResult[])path.Clone();
                    bestEnergy = energy;
                    bestTime = state.Time;
                }
                return;
            }

            foreach (double speed in speeds)
            {
                var result = model.Drive(segments[index], state, speed);

                if (result.LengthShortage > 0) continue;
                if (result.ResultState.Time > problem.Limits.MaxTime) continue;

                path[index] = result;
                Search(index + 1, result.ResultState, energy + result.Energy);
            }
        }

        Search(0, new CarState(0, problem.InitialSpeed), 0);

        return best is null ? null : new RouteResult(best);
    }
}
