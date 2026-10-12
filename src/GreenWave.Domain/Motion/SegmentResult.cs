namespace GreenWave.Domain.Motion;

/// <param name="Speed">Выбранная скорость движения на сегменте.</param>
/// <param name="ArrivalTime">Момент, когда машина у стоп-линии (при остановке — уже с учётом торможения).</param>
/// <param name="Phase">Фаза светофора в момент прибытия на скорости Speed — по ней принимается решение об остановке.</param>
/// <param name="Stopped">Машина остановилась у светофора.</param>
/// <param name="ResultState">Состояние после проезда светофора (или конца сегмента без светофора).</param>
/// <param name="Energy">Затраты энергии на разгон.</param>
/// <param name="LengthShortage">На сколько метров сегмент короче, чем нужно для разгона/торможения; 0 — манёвр выполним.</param>
public sealed record SegmentResult(
    double Speed,
    double ArrivalTime,
    double? Phase,
    bool Stopped,
    CarState ResultState,
    double Energy,
    double LengthShortage);
