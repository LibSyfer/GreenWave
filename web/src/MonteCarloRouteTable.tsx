import { Fragment, useEffect, useMemo, useState } from 'react'
import type { MonteCarloResponse, MonteCarloSnapshot, Problem } from './api'
import { fmt, toKmh } from './units'

export function MonteCarloRouteTable({ problem, comparison, snapshot, onExperiment }: {
  problem: Problem; comparison: MonteCarloResponse['monteCarlo']; snapshot: MonteCarloSnapshot | null; onExperiment: (number: number | null) => void
}) {
  const [solver, setSolver] = useState(comparison.displayedSolver ?? 'dp')
  const [filter, setFilter] = useState('newStops')
  const [picked, setPicked] = useState<number | null>(null)
  const [original, setOriginal] = useState(false)
  const selected = comparison.comparisons.find(x => x.solver === solver)!
  const matches = useMemo(() => selected.experiments.filter(x => filter === 'all' ||
    (filter === 'newStops' && x.newStops) || (filter === 'overTime' && x.overTime) ||
    (filter === 'invalid' && x.invalidSegment !== null)), [selected, filter])
  const position = Math.max(0, matches.findIndex(x => x.number === picked))
  const number = original ? null : matches[position]?.number ?? null
  useEffect(() => { onExperiment(number) }, [number, onExperiment])
  const current = snapshot?.comparison === comparison && snapshot.number === number ? snapshot : null
  const replay = current?.results[solver]
  const loading = number !== null && !current
  const newStops = replay?.route?.segments.flatMap((s, i) => s.stopped && !selected.route?.segments[i].stopped ? [i + 1] : []) ?? []
  const choose = (value: number) => { setOriginal(false); setPicked(value) }
  const reset = () => { setOriginal(false); setPicked(null) }
  const page = matches.slice(Math.floor(position / 20) * 20, Math.floor(position / 20) * 20 + 20)
  return <div className="panel">
    <h3>Исходный проезд и эксперимент — сравнение по участкам</h3>
    <div className="fields">
      <label className="field"><span>Маршрут в таблице</span>
        <select aria-label="Маршрут в таблице" value={solver} onChange={e => { setSolver(e.target.value as 'dp' | 'ga'); reset() }}>
          <option value="dp">ДП</option><option value="ga">ГА</option>
        </select>
      </label>
      <label className="field"><span>Показать эксперименты</span>
        <select aria-label="Показать эксперименты" value={filter} onChange={e => { setFilter(e.target.value); reset() }}>
          <option value="newStops">Появились остановки</option><option value="overTime">Превышено Tmax</option>
          <option value="invalid">Физически недопустимые</option><option value="all">Все эксперименты</option>
        </select>
      </label>
      <p className="muted note">Найдено: {matches.length} из {selected.experiments.length}. Фильтр «Появились остановки» ищет остановки на участках, где исходный маршрут проезжал без остановки. Недопустимые прогоны учитываются отдельно.</p>
      <div className="dp-nav-actions" style={{ flexWrap: 'wrap' }}>
        <button className="secondary" disabled={!matches.length || position === 0} onClick={() => choose(matches[position - 1].number)}>Предыдущий подходящий</button>
        <button className="secondary" disabled={!matches.length || position === matches.length - 1} onClick={() => choose(matches[position + 1].number)}>Следующий подходящий</button>
        <button className="secondary" onClick={() => setOriginal(true)}>Показать исходные фазы</button>
        {original && matches.length > 0 && <button className="secondary" onClick={() => setOriginal(false)}>Вернуться к эксперименту</button>}
      </div>
      {page.length > 0 && <div className="branch-steps">Эксперименты: {page.map(x => <Fragment key={x.number}>
        {' '}<button className={x.number === number ? 'count active' : 'count'} onClick={() => choose(x.number)}>№{x.number}</button>
      </Fragment>)}<span className="muted"> · {matches.length ? position + 1 : 0} из {matches.length}</span></div>}
    </div>
    {!selected.route ? <p className="muted note">Маршрут не найден. Эксперименты для него не выполнялись.</p> : <>
      {number === null ? <p className="muted note">{original ? 'Показан исходный маршрут.' : 'Подходящих экспериментов нет. Показан исходный маршрут.'}</p>
        : loading ? <p className="muted note">Загрузка эксперимента №{number}…</p>
        : current?.error ? <p className="error">{current.error}</p>
        : replay?.invalidSegment ? <p className="muted note">Эксперимент №{number}: на участке {replay.invalidSegment} не хватает пути для манёвра. Проезд эксперимента и его траектория не отображаются.</p>
        : replay?.route && <p className="note">Эксперимент №{number}: {newStops.length ? `новые остановки на участках ${newStops.join(', ')}` : 'новых остановок нет'}.
          {' '}Изменение времени: {fmt(replay.route.time - selected.route.time)} с. Остановок: {selected.route.stops} → {replay.route.stops}.
          {replay.route.time > problem.limits.maxTime && ' Превышено Tmax.'}</p>}
      <p className="muted note">Для каждого участка: исходный проезд и выбранный эксперимент рядом. Светофор: цикл / сдвиг / зелёный, с. На графике — фазы и обе траектории выбранного эксперимента; итоги сверху относятся к исходному маршруту.</p>
      <div style={{ overflowX: 'auto' }}><table className="route">
        <thead><tr><th>#</th><th>Условия</th><th>Длина, м</th><th>Светофор</th><th>Скорость, км/ч</th><th>У светофора, с</th><th title="Время с начала зелёного в момент прибытия на выбранной скорости">Фаза, с</th><th>Остановка</th><th>Проезд, с</th><th>Энергия, кДж</th></tr></thead>
        <tbody>{selected.route.segments.map((s, i) => <Fragment key={i}>
          {[{ segment: s, p: problem, label: 'Исходные' }, ...(replay?.route ? [{ segment: replay.route.segments[i], p: replay.problem, label: `№${number}` }] : [])].map(({ segment: item, p, label }) => {
            const light = p.road.segments[i].light
            const addedStop = label !== 'Исходные' && item.stopped && !s.stopped
            return <tr key={label}><td className="muted">{i + 1}</td><td>{label}</td><td>{p.road.segments[i].length}</td>
              <td className="muted">{light ? `${fmt(light.cycleDuration)} / ${fmt(light.offset, 2)} / ${fmt(light.greenDuration)}` : '—'}</td>
              <td>{fmt(toKmh(item.speed), 0)}</td><td>{fmt(item.arrivalTime)}</td>
              <td>{item.phase === null ? '—' : fmt(item.phase)}</td>
              <td>{item.stopped ? <span className="stop">{addedStop ? 'да — новая' : 'да'}</span> : 'нет'}</td>
              <td>{fmt(item.resultState.time)}</td><td>{fmt(item.energy / 1000, 2)}</td></tr>
          })}
        </Fragment>)}</tbody>
      </table></div>
    </>}
  </div>
}
