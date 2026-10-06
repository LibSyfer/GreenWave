using GreenWave.Domain.Entities;

namespace GreenWave.Domain.Models;

public sealed record GreenWaveProblem(
    Road Road,
    Vehicle Vehicle,
    Limits Limits,
    double InitialSpeed,
    double SafetyMargin);
