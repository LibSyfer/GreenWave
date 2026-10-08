using GreenWave.Domain.Entities;
using GreenWave.Domain.Models;

namespace GreenWave.Domain.Motion;

public sealed class MotionModel
{
    public GreenWaveProblem Problem { get; }

    public MotionModel(GreenWaveProblem problem)
    {
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

        var pass = segment.Light.Pass(arrivalTime, Problem.SafetyMargin);
        double endSpeed = pass.Stopped ? 0 : speed;
        double brakingDistance = pass.Stopped ? Kinematics.AccelerationDistance(speed, 0, -car.Deceleration) : 0;

        return new SegmentResult(
            speed,
            arrivalTime,
            pass.Phase,
            pass.Stopped,
            new CarState(pass.PassTime, endSpeed),
            energy,
            Math.Max(0, accelerationDistance + brakingDistance - segment.Length));
    }
}
