namespace GreenWave.Domain.Models;

public readonly record struct StepResult(
    double ArrivalTime,
    double? Phase,
    bool Stoped,
    double PassTime,
    double PassSpeed,
    double Energy,
    double LengthShortage);
