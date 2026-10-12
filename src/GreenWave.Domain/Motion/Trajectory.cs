using GreenWave.Domain.Models;

namespace GreenWave.Domain.Motion;

/// <summary>Точка траектории: время, пройденный путь от старта и скорость.</summary>
public readonly record struct TrajectoryPoint(double Time, double Distance, double Speed);

/// <summary>
/// Траектория маршрута для графика «время–расстояние»: на каждом сегменте
/// разгон/торможение до выбранной скорости, равномерное движение и, если машина
/// остановилась у светофора, торможение до нуля и ожидание проезда.
/// </summary>
public static class Trajectory
{
    /// <param name="samplesPerRamp">Точек на каждом участке разгона/торможения (это параболы).</param>
    public static IReadOnlyList<TrajectoryPoint> Build(
        GreenWaveProblem problem,
        IReadOnlyList<SegmentResult> route,
        int samplesPerRamp = 8)
    {
        var car = problem.Vehicle;
        var points = new List<TrajectoryPoint> { new(0, 0, problem.InitialSpeed) };

        double t = 0, x = 0, v0 = problem.InitialSpeed;

        for (int i = 0; i < route.Count; i++)
        {
            var segment = problem.Road.Segments[i];
            var result = route[i];
            double v = result.Speed;
            double end = x + segment.Length;

            // разгон или торможение v0 → v
            double a = v >= v0 ? car.Acceleration : -car.Deceleration;
            double rampTime = (v - v0) / a;
            Ramp(points, t, x, v0, a, rampTime, samplesPerRamp);
            x += Kinematics.AccelerationDistance(v0, v, a);
            t += rampTime;

            if (!result.Stopped)
            {
                // равномерно до конца сегмента
                t += (end - x) / v;
                x = end;
                points.Add(new(t, x, v));
                v0 = v;
                continue;
            }

            // равномерно до точки начала торможения, затем торможение до стоп-линии
            double brakingDistance = Kinematics.AccelerationDistance(v, 0, -car.Deceleration);
            t += (end - brakingDistance - x) / v;
            x = end - brakingDistance;
            points.Add(new(t, x, v));

            double brakingTime = v / car.Deceleration;
            Ramp(points, t, x, v, -car.Deceleration, brakingTime, samplesPerRamp);
            t += brakingTime;
            x = end;

            // ожидание у стоп-линии
            t = result.ResultState.Time;
            points.Add(new(t, x, 0));
            v0 = 0;
        }

        return points;
    }

    /// <summary>Точки равноускоренного движения на (0, duration], начиная из (t, x, v).</summary>
    private static void Ramp(List<TrajectoryPoint> points, double t, double x, double v, double a, double duration, int samples)
    {
        if (duration <= 0) return;

        for (int k = 1; k <= samples; k++)
        {
            double dt = duration * k / samples;
            points.Add(new(t + dt, x + v * dt + a * dt * dt / 2, v + a * dt));
        }
    }
}
