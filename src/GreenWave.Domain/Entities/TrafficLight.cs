namespace GreenWave.Domain.Entities;

public sealed record TrafficLight(double CycleDuration, double Offset, double GreenDuration)
{
    public double Phase(double t) => Extensions.Mod(t - Offset, CycleDuration);
    public PassResult Pass(double arrivalTime, double safetyMargin)
    {
        double r = Phase(arrivalTime);

        if (r > GreenDuration - safetyMargin)
            return new PassResult(arrivalTime + (CycleDuration - r), true, r);

        if (r < safetyMargin)
            return new PassResult(arrivalTime + (safetyMargin - r), true, r);

        return new PassResult(arrivalTime, false, r);
    }
}

public record class PassResult(double PassTime, bool Stopped, double Phase);
