import type { Problem, Route } from './api'
import { fmt, toKmh } from './units'

/** Маршрут по сегментам: выбранная скорость, прибытие к светофору, остановка, проезд, энергия. */
export function RouteTable({ problem, route }: { problem: Problem; route: Route }) {
  return (
    <table className="route">
      <thead>
        <tr>
          <th>#</th>
          <th>Длина, м</th>
          <th>Светофор</th>
          <th>Скорость, км/ч</th>
          <th title="Момент у стоп-линии (при остановке — с учётом торможения)">У светофора, с</th>
          <th title="Время с начала зелёного в момент прибытия на выбранной скорости">Фаза, с</th>
          <th>Остановка</th>
          <th>Проезд, с</th>
          <th>Энергия, кДж</th>
        </tr>
      </thead>
      <tbody>
        {route.segments.map((s, i) => {
          const light = problem.road.segments[i].light
          return (
            <tr key={i}>
              <td className="muted">{i + 1}</td>
              <td>{problem.road.segments[i].length}</td>
              <td className="muted">{light ? `${light.cycleDuration} / ${light.offset} / ${light.greenDuration}` : '—'}</td>
              <td>{fmt(toKmh(s.speed), 0)}</td>
              <td>{fmt(s.arrivalTime)}</td>
              <td>{s.phase === null ? '—' : fmt(s.phase)}</td>
              <td>{s.stopped ? <span className="stop">да</span> : 'нет'}</td>
              <td>{fmt(s.resultState.time)}</td>
              <td>{fmt(s.energy / 1000, 2)}</td>
            </tr>
          )
        })}
      </tbody>
    </table>
  )
}
