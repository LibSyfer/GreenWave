namespace GreenWave.Domain.Motion;

/// <summary>
/// Формулы равноускоренного движения.
/// acceleration — ускорение со знаком: разгон > 0, торможение < 0.
/// </summary>
public static class Kinematics
{
    // D_j
    public static double AccelerationDistance(double startSpeed, double speed, double acceleration)
        => (speed * speed - startSpeed * startSpeed) / (2 * acceleration);

    // delta_j
    public static double TimeCorrection(double startSpeed, double speed, double acceleration)
        => Math.Pow(speed - startSpeed, 2) / (2 * acceleration * speed);

    // e_j
    public static double AccelerationEnergy(double mass, double startSpeed, double speed)
        => mass * Math.Max(0, speed * speed - startSpeed * startSpeed) / 2;
}
