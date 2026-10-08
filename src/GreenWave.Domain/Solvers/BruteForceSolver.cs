using GreenWave.Domain.Motion;

namespace GreenWave.Domain.Solvers;

public sealed class BruteForceSolver
    : ISolver
{
    public string Name => nameof(BruteForceSolver);

    public RouteResult? Solve(MotionModel model)
    {
        throw new NotImplementedException();
    }
}
