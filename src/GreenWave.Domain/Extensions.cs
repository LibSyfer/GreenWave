using System.Numerics;

namespace GreenWave.Domain;

public static class Extensions
{
    public static T Mod<T>(T value, T divisor) where T : INumber<T>
    {
        T r = value % divisor;
        return r < T.Zero ? r + T.Abs(divisor) : r;
    }
}