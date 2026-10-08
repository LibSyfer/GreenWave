using GreenWave.Domain.Entities;
using GreenWave.Domain.Models;

namespace GreenWave.Domain;

public static class Samples
{
    private static double Kmh(double kmh) => kmh / 3.6;

    private static readonly Vehicle Car = new Vehicle(Mass: 1500, Acceleration: 1.5, Deceleration: 2.5);

    public static GreenWaveProblem TestProblem1() => new(
        Road: new Road([
            new Segment(400, new TrafficLight(60, 10, 30)),
            new Segment(300, new TrafficLight(60, 40, 25)),
            new Segment(500, new TrafficLight(70, 15, 35)),
            new Segment(350, new TrafficLight(60, 30, 30)),
            new Segment(200, null)
            ]),
        Vehicle: Car,
        Limits: new Limits(MinSpeed: Kmh(20), MaxSpeed: Kmh(60), MaxTime: 150),
        InitialSpeed: 0,
        SafetyMargin: 0
        );

    public static GreenWaveProblem MovingStart(double maxTime = 120) => new(
        Road: new Road([
            new Segment(250, new TrafficLight(60, 20, 30)),
            new Segment(150, new TrafficLight(60, 45, 25)),
            new Segment(450, new TrafficLight(80, 10, 40)),
            new Segment(300, null),
        ]),
        Vehicle: Car,
        Limits: new Limits(MinSpeed: Kmh(20), MaxSpeed: Kmh(60), MaxTime: maxTime),
        InitialSpeed: Kmh(50),
        SafetyMargin: 0);

    public static GreenWaveProblem StopIsBetter(double maxTime = 106) => new(
        Road: new Road([
            new Segment(300, new TrafficLight(60, 0, 30)),
            new Segment(250, new TrafficLight(70, 15, 20)),
            new Segment(250, null),
        ]),
        Vehicle: Car,
        Limits: new Limits(MinSpeed: Kmh(20), MaxSpeed: Kmh(60), MaxTime: maxTime),
        InitialSpeed: 0,
        SafetyMargin: 0);
}
