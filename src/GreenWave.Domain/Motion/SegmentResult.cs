namespace GreenWave.Domain.Motion;

public sealed record SegmentResult(
    double Speed,
    double ArrivalTime,
    double? Phase,
    bool Stopped,
    CarState ResultState,
    double Energy,
    double LengthShortage);
