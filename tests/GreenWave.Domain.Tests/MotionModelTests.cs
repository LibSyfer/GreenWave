using GreenWave.Domain.Entities;
using GreenWave.Domain.Models;
using GreenWave.Domain.Motion;

namespace GreenWave.Domain.Tests;

public class MotionModelTests
{
    private const double Eps = 1e-9;

    private static readonly Vehicle Car = new(Mass: 1500, Acceleration: 1.5, Deceleration: 2.5);

    private static MotionModel Model(params Segment[] segments) => new(new GreenWaveProblem(
        Road: new Road(segments),
        Vehicle: Car,
        Limits: new Limits(MinSpeed: 1, MaxSpeed: 100, MaxTime: 1000),
        InitialSpeed: 0,
        SafetyMargin: 0));

    [Fact]
    public void Drive_WithoutLight_AcceleratesAndCruises()
    {
        var segment = new Segment(300, null);
        var model = Model(segment);

        var result = model.Drive(segment, new CarState(0, 0), 10);

        // разгон 0→10 за 20/3 с на 100/3 м, затем (300 - 100/3) / 10 с
        double expectedTime = 20.0 / 3 + (300 - 100.0 / 3) / 10;
        Assert.Equal(expectedTime, result.ArrivalTime, Eps);
        Assert.Equal(new CarState(expectedTime, 10), result.ResultState);
        Assert.Equal(75_000, result.Energy, Eps);
        Assert.False(result.Stopped);
        Assert.Null(result.Phase);
        Assert.Equal(0, result.LengthShortage);
    }

    [Fact]
    public void Drive_OnGreen_KeepsSpeed()
    {
        // прибытие при 10 м/с за 30 с — зелёный [0, 40)
        var segment = new Segment(300, new TrafficLight(60, 0, 40));
        var model = Model(segment);

        var result = model.Drive(segment, new CarState(0, 10), 10);

        Assert.False(result.Stopped);
        Assert.Equal(new CarState(30, 10), result.ResultState);
        Assert.Equal(0, result.Energy);
    }

    [Fact]
    public void Drive_OnRed_BrakesStopsAndWaitsForGreen()
    {
        // на 10 м/с прибытие было бы в 30 с — красный (зелёный [0, 20), следующий с 60).
        // Торможение 10→0 при 2.5 м/с²: 20 м за 4 с вместо 2 с равномерно → у стоп-линии в 32 с.
        var segment = new Segment(300, new TrafficLight(60, 0, 20));
        var model = Model(segment);

        var result = model.Drive(segment, new CarState(0, 10), 10);

        Assert.True(result.Stopped);
        Assert.Equal(30, result.Phase!.Value, Eps);
        Assert.Equal(32, result.ArrivalTime, Eps);
        Assert.Equal(new CarState(60, 0), result.ResultState);
    }

    [Fact]
    public void Drive_GreenStartsWhileBraking_PassesFromStandstillAtStopTime()
    {
        // на 10 м/с прибытие было бы в 59 с — красный; пока тормозит, в 60 с включается зелёный,
        // у стоп-линии машина в 61 с со скоростью 0 и сразу проезжает
        var segment = new Segment(590, new TrafficLight(60, 0, 20));
        var model = Model(segment);

        var result = model.Drive(segment, new CarState(0, 10), 10);

        Assert.True(result.Stopped);
        Assert.Equal(new CarState(61, 0), result.ResultState);
    }

    [Fact]
    public void Drive_OnRed_WithMargin_WaitsForGreenStartPlusMargin()
    {
        var segment = new Segment(300, new TrafficLight(60, 0, 20));
        var model = new MotionModel(new GreenWaveProblem(
            new Road([segment]), Car, new Limits(1, 100, 1000), InitialSpeed: 0, SafetyMargin: 3));

        var result = model.Drive(segment, new CarState(0, 10), 10);

        Assert.Equal(new CarState(63, 0), result.ResultState);
    }

    [Fact]
    public void Drive_ShortSegment_ReportsLengthShortage()
    {
        // разгон 0→15 требует 75 м, сегмент 50 м
        var segment = new Segment(50, null);
        var model = Model(segment);

        var result = model.Drive(segment, new CarState(0, 0), 15);

        Assert.Equal(25, result.LengthShortage, Eps);
    }

    [Fact]
    public void Drive_StopOnRed_AddsBrakingDistanceToShortage()
    {
        // разгон 0→15: 75 м, торможение 15→0: 45 м, сегмент 100 м
        var segment = new Segment(100, new TrafficLight(60, 30, 20));
        var model = Model(segment);

        var result = model.Drive(segment, new CarState(0, 0), 15);

        Assert.True(result.Stopped);
        Assert.Equal(20, result.LengthShortage, Eps);
    }
}
