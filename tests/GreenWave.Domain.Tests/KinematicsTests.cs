using GreenWave.Domain.Motion;

namespace GreenWave.Domain.Tests;

public class KinematicsTests
{
    private const double Eps = 1e-9;

    [Fact]
    public void AccelerationDistance_Accelerating()
    {
        // 0 → 10 м/с при 1.5 м/с²: 100 / 3
        Assert.Equal(100.0 / 3, Kinematics.AccelerationDistance(0, 10, 1.5), Eps);
    }

    [Fact]
    public void AccelerationDistance_Braking_IsPositive()
    {
        // 20 → 10 м/с при -2.5 м/с²: (400 - 100) / 5
        Assert.Equal(60, Kinematics.AccelerationDistance(20, 10, -2.5), Eps);
    }

    [Fact]
    public void AccelerationDistance_SameSpeed_IsZero()
    {
        Assert.Equal(0, Kinematics.AccelerationDistance(10, 10, 1.5), Eps);
    }

    /// <summary>
    /// L / v + delta должно совпадать с прямым расчётом:
    /// время разгона/торможения + время равномерного движения на остатке пути.
    /// </summary>
    [Theory]
    [InlineData(0, 10, 1.5, 300)]
    [InlineData(5, 15, 1.5, 300)]
    [InlineData(15, 5, -2.5, 300)]
    [InlineData(16, 6, -2.5, 100)]
    [InlineData(10, 10, 1.5, 200)]
    public void TimeCorrection_MatchesDirectComputation(double v0, double v, double a, double length)
    {
        double rampTime = (v - v0) / a;
        double rampDistance = Kinematics.AccelerationDistance(v0, v, a);
        double expected = rampTime + (length - rampDistance) / v;

        double actual = length / v + Kinematics.TimeCorrection(v0, v, a);

        Assert.Equal(expected, actual, Eps);
    }

    [Fact]
    public void AccelerationEnergy_Accelerating_IsKineticEnergyGain()
    {
        Assert.Equal(1500 * (15.0 * 15 - 5 * 5) / 2, Kinematics.AccelerationEnergy(1500, 5, 15), Eps);
    }

    [Fact]
    public void AccelerationEnergy_Braking_IsZero()
    {
        Assert.Equal(0, Kinematics.AccelerationEnergy(1500, 15, 5));
    }
}
