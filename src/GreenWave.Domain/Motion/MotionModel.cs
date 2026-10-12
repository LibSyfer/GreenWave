using GreenWave.Domain.Entities;
using GreenWave.Domain.Models;

namespace GreenWave.Domain.Motion;

public sealed class MotionModel
{
    public GreenWaveProblem Problem { get; }

    public MotionModel(GreenWaveProblem problem)
    {
        problem.Validate();
        Problem = problem;
    }

    public SegmentResult Drive(Segment segment, CarState start, double speed)
    {
        var car = Problem.Vehicle;

        double a = speed >= start.Speed ? car.Acceleration : -car.Deceleration;
        double accelerationDistance = Kinematics.AccelerationDistance(start.Speed, speed, a); // D_j
        double timeCorrection = Kinematics.TimeCorrection(start.Speed, speed, a);

        double arrivalTime = start.Time + segment.Length / speed + timeCorrection;

        double energy = Kinematics.AccelerationEnergy(car.Mass, start.Speed, speed);

        if (segment.Light is null)
        {
            return new SegmentResult(
                speed,
                arrivalTime,
                null,
                false,
                new CarState(arrivalTime, speed),
                energy,
                Math.Max(0, accelerationDistance - segment.Length));
        }

        var light = segment.Light;
        double phase = light.Phase(arrivalTime);

        if (!light.Pass(arrivalTime, Problem.SafetyMargin).Stopped)
        {
            return new SegmentResult(
                speed,
                arrivalTime,
                phase,
                false,
                new CarState(arrivalTime, speed),
                energy,
                Math.Max(0, accelerationDistance - segment.Length));
        }

        // На скорости speed машина не проезжает — тормозит до остановки у стоп-линии и ждёт.
        double brakingDistance = Kinematics.AccelerationDistance(speed, 0, -car.Deceleration);
        double stopTime = arrivalTime + Kinematics.StopDelay(speed, car.Deceleration);
        double passTime = light.NextPassTime(stopTime, Problem.SafetyMargin);

        return new SegmentResult(
            speed,
            stopTime,
            phase,
            true,
            new CarState(passTime, 0),
            energy,
            Math.Max(0, accelerationDistance + brakingDistance - segment.Length));
    }
}
