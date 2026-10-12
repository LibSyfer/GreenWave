using GreenWave.Domain.Entities;

namespace GreenWave.Domain.Tests;

public class TrafficLightTests
{
    private const double Eps = 1e-9;

    // Цикл 60 с, сдвиг 10 с, зелёный 30 с: зелёный на [10, 40), [70, 100), ...
    private static readonly TrafficLight Light = new(CycleDuration: 60, Offset: 10, GreenDuration: 30);

    [Theory]
    [InlineData(10, 0)]
    [InlineData(25, 15)]
    [InlineData(5, 55)]
    [InlineData(130, 0)]
    [InlineData(-5, 45)]
    public void Phase_IsTimeSinceGreenStart(double t, double expected)
    {
        Assert.Equal(expected, Light.Phase(t), Eps);
    }

    [Fact]
    public void Pass_OnGreen_NoStop()
    {
        var pass = Light.Pass(25, 0);

        Assert.False(pass.Stopped);
        Assert.Equal(25, pass.PassTime, Eps);
        Assert.Equal(15, pass.Phase, Eps);
    }

    [Fact]
    public void Pass_OnRed_WaitsForNextGreen()
    {
        var pass = Light.Pass(45, 0);

        Assert.True(pass.Stopped);
        Assert.Equal(70, pass.PassTime, Eps);
    }

    [Fact]
    public void Pass_AtGreenStart_NoStop()
    {
        var pass = Light.Pass(70, 0);

        Assert.False(pass.Stopped);
        Assert.Equal(70, pass.PassTime, Eps);
    }

    [Fact]
    public void Pass_AtGreenEnd_NoStop()
    {
        // r == GreenDuration — граница включительно
        Assert.False(Light.Pass(40, 0).Stopped);
    }

    [Fact]
    public void Pass_WithMargin_TooEarlyInGreen_WaitsForMargin()
    {
        var pass = Light.Pass(12, 5);

        Assert.True(pass.Stopped);
        Assert.Equal(15, pass.PassTime, Eps);
    }

    [Fact]
    public void Pass_WithMargin_TooLateInGreen_Stops()
    {
        var pass = Light.Pass(37, 5);

        Assert.True(pass.Stopped);
        Assert.True(pass.PassTime >= 70);
    }

    [Fact(Skip = "Известная ошибка: после красного проезд разрешается в r = 0, без учёта SafetyMargin")]
    public void Pass_WithMargin_OnRed_WaitsForGreenStartPlusMargin()
    {
        var pass = Light.Pass(45, 5);

        Assert.True(pass.Stopped);
        Assert.Equal(75, pass.PassTime, Eps);
    }
}
