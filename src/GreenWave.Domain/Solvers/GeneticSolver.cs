using GreenWave.Domain.Motion;

namespace GreenWave.Domain.Solvers;

public sealed record GaOptions(int PopSize, int Tournament);

public sealed class GeneticSolver
    : ISolver
{
    public string Name => nameof(GeneticSolver);

    public void Solve(MotionModel model, double delta)
    {
        throw new NotImplementedException();
    }
}
