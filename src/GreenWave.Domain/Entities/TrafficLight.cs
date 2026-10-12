namespace GreenWave.Domain.Entities;

public sealed record TrafficLight(double CycleDuration, double Offset, double GreenDuration)
{
    /// <summary>Время с начала последнего зелёного, в [0, CycleDuration).</summary>
    public double Phase(double t) => Extensions.Mod(t - Offset, CycleDuration);

    /// <summary>
    /// Ближайший момент не раньше <paramref name="t"/>, когда разрешён проезд:
    /// фаза в [safetyMargin, GreenDuration - safetyMargin].
    /// </summary>
    public double NextPassTime(double t, double safetyMargin)
    {
        double r = Phase(t);

        if (r < safetyMargin)
            return t + (safetyMargin - r);

        if (r > GreenDuration - safetyMargin)
            return t + (CycleDuration - r) + safetyMargin;

        return t;
    }

    public PassResult Pass(double arrivalTime, double safetyMargin)
    {
        double passTime = NextPassTime(arrivalTime, safetyMargin);
        return new PassResult(passTime, passTime > arrivalTime, Phase(arrivalTime));
    }
}

public record class PassResult(double PassTime, bool Stopped, double Phase);
