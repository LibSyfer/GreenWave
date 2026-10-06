namespace GreenWave.Domain.Entities;

public sealed record Vehicle(double Mass, double Acceleration, double Deceleration)
{
    public static Vehicle Default => new(1500, 1.5, 2.5);
}