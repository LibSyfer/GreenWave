using GreenWave.Domain.Motion;

namespace GreenWave.Domain.Solvers;

public sealed class BruteForceSolver
    : ISolver
{
    public string Name => nameof(BruteForceSolver);

    public void Solve(MotionModel model, double delta)
    {
        throw new NotImplementedException();
    }
}
