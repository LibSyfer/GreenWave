using GreenWave.Domain.Entities;
using GreenWave.Domain.Models;

namespace GreenWave.Domain.Tests;

/// <summary>Задачи для тестов решателей: примеры из Samples и воспроизводимые случайные.</summary>
public static class TestProblems
{
    public static double Kmh(double kmh) => kmh / 3.6;

    public static TheoryData<string> SampleNames => ["TestProblem1", "A120", "A110", "B106", "B90"];

    public static GreenWaveProblem Sample(string name) => name switch
    {
        "TestProblem1" => Samples.TestProblem1(),
        "A120" => Samples.MovingStart(),
        "A110" => Samples.MovingStart(110),
        "B106" => Samples.StopIsBetter(),
        "B90" => Samples.StopIsBetter(90),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    public static IEnumerable<int> SeedRange => Enumerable.Range(0, 200);

    public static TheoryData<int> Seeds => new(SeedRange);

    /// <summary>
    /// Случайная дорога из 2–4 сегментов; последний сегмент без светофора с вероятностью 1/2.
    /// MaxTime — от 1.2 до 2.5 минимально возможного времени на максимальной скорости.
    /// SafetyMargin — 0 или 1–5 с (зелёный не короче 12 с, так что проехать всегда можно).
    /// </summary>
    public static GreenWaveProblem Random(int seed)
    {
        var rnd = new Random(seed);
        double Uniform(double min, double max) => min + rnd.NextDouble() * (max - min);

        int count = rnd.Next(2, 5);
        var segments = new Segment[count];

        for (int i = 0; i < count; i++)
        {
            TrafficLight? light = null;
            if (i < count - 1 || rnd.Next(2) == 0)
            {
                double cycle = Math.Round(Uniform(40, 90));
                light = new TrafficLight(
                    CycleDuration: cycle,
                    Offset: Math.Round(Uniform(0, cycle)),
                    GreenDuration: Math.Round(Uniform(0.3, 0.7) * cycle));
            }

            segments[i] = new Segment(Math.Round(Uniform(100, 500)), light);
        }

        double maxSpeed = 60 / 3.6;
        double minTime = segments.Sum(s => s.Length) / maxSpeed;

        return new GreenWaveProblem(
            Road: new Road(segments),
            Vehicle: new Vehicle(Mass: 1500, Acceleration: 1.5, Deceleration: 2.5),
            Limits: new Limits(MinSpeed: 20 / 3.6, MaxSpeed: maxSpeed, MaxTime: Math.Round(minTime * Uniform(1.2, 2.5))),
            InitialSpeed: rnd.Next(2) == 0 ? 0 : Math.Round(Uniform(20, 60)) / 3.6,
            SafetyMargin: rnd.Next(2) == 0 ? 0 : Math.Round(Uniform(1, 5)));
    }
}
