using GreenWave.Domain.Entities;

namespace GreenWave.Domain.Models;

public sealed record GreenWaveProblem(
    Road Road,
    Vehicle Vehicle,
    Limits Limits,
    double InitialSpeed,
    double SafetyMargin)
{
    public void Validate()
    {
        Require(Road.Segments.Length > 0, "дорога должна содержать хотя бы один сегмент");
        Require(Vehicle.Mass > 0, "масса должна быть > 0");
        Require(Vehicle.Acceleration > 0, "ускорение должно быть > 0");
        Require(Vehicle.Deceleration > 0, "замедление должно быть > 0");
        Require(Limits.MinSpeed > 0, "минимальная скорость должна быть > 0");
        Require(Limits.MaxSpeed >= Limits.MinSpeed, "максимальная скорость должна быть >= минимальной");
        Require(Limits.MaxTime > 0, "MaxTime должно быть > 0");
        Require(InitialSpeed >= 0, "начальная скорость должна быть >= 0");
        Require(SafetyMargin >= 0, "SafetyMargin должен быть >= 0");

        for (int i = 0; i < Road.Segments.Length; i++)
        {
            var segment = Road.Segments[i];
            Require(segment.Length > 0, $"сегмент {i}: длина должна быть > 0");

            if (segment.Light is { } light)
            {
                Require(light.CycleDuration > 0, $"сегмент {i}: длительность цикла должна быть > 0");
                Require(light.GreenDuration > 0 && light.GreenDuration <= light.CycleDuration,
                    $"сегмент {i}: зелёный должен быть в (0, CycleDuration]");
                Require(light.GreenDuration > 2 * SafetyMargin,
                    $"сегмент {i}: зелёный короче двух SafetyMargin — проехать нельзя");
            }
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new ArgumentException($"Некорректная задача: {message}");
    }
}
