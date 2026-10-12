namespace GreenWave.Domain.Entities;

public sealed record Limits(double MinSpeed, double MaxSpeed, double MaxTime)
{
    /// <summary>
    /// Дискретный набор скоростей: MinSpeed, MinSpeed + step, ..., и MaxSpeed (всегда включается).
    /// </summary>
    public double[] SpeedGrid(double step)
    {
        if (step <= 0)
            throw new ArgumentOutOfRangeException(nameof(step), "шаг скорости должен быть > 0");

        const double eps = 1e-9;
        var speeds = new List<double>();

        for (int k = 0; MinSpeed + k * step < MaxSpeed - eps; k++)
            speeds.Add(MinSpeed + k * step);

        speeds.Add(MaxSpeed);
        return speeds.ToArray();
    }
}
