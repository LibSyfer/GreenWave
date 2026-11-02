import type { MonteCarloResponse } from './api'
import { fmt } from './units'

export function MonteCarloView({ comparison: c }: { comparison: MonteCarloResponse['monteCarlo'] }) {
  const value = (n: number | null | undefined, unit = '', scale = 1) => n == null ? 'нет данных' : `${fmt(n * scale, 2)}${unit}`
  return <div className="panel node-details">
    <h3>Монте-Карло — сравнение ДП и ГА</h3>
    <p className="muted note">Оба маршрута рассчитаны автоматически. Для каждого выполнено {c.experiments} экспериментов с одинаковыми случайными сдвигами фаз в пределах ±{fmt(c.phaseShift)} с. Скорости каждого маршрута фиксированы. Циклы и длительности зелёного не меняются.</p>
    <p className="muted note">На графике одновременно показаны ДП и ГА для условий, выбранных в подробной таблице. Итоги сверху относятся к {c.displayedSolver === 'dp' ? 'ДП' : c.displayedSolver === 'ga' ? 'ГА' : '(не найден)'} при номинальных фазах.</p>
    <table className="dp-children">
      <thead><tr><th>Показатель</th>{c.comparisons.map(x => <th key={x.solver}>{x.solver === 'dp' ? 'ДП' : 'ГА'}</th>)}</tr></thead>
      <tbody>
        <tr><td>Исходный маршрут</td>{c.comparisons.map(x => <td key={x.solver}>{x.route ? 'найден' : 'не найден; эксперименты не выполнялись'}</td>)}</tr>
        <tr><td>Энергия исходного маршрута, кДж</td>{c.comparisons.map(x => <td key={x.solver}>{value(x.route?.energy, '', 0.001)}</td>)}</tr>
        <tr><td>Исходное время, с</td>{c.comparisons.map(x => <td key={x.solver}>{value(x.route?.time)}</td>)}</tr>
        <tr><td>Скорости по участкам, км/ч</td>{c.comparisons.map(x => <td key={x.solver}>{x.route ? x.route.segments.map(s => fmt(s.speed * 3.6, 0)).join(' · ') : 'нет данных'}</td>)}</tr>
        <tr><td>Выполнено экспериментов</td>{c.comparisons.map(x => <td key={x.solver}>{x.statistics?.experiments ?? 0}</td>)}</tr>
        <tr><td>Физически допустимых</td>{c.comparisons.map(x => <td key={x.solver}>{x.statistics?.validExperiments ?? 'нет данных'}</td>)}</tr>
        <tr><td>Не хватает пути для манёвра</td>{c.comparisons.map(x => <td key={x.solver}>{x.statistics ? `${x.statistics.invalidExperiments} (${fmt(100 * x.statistics.invalidExperiments / c.experiments, 2)}%)` : 'нет данных'}</td>)}</tr>
        <tr><td>Хотя бы одна остановка</td>{c.comparisons.map(x => <td key={x.solver}>{value(x.statistics?.stopProbability, '%', 100)}</td>)}</tr>
        <tr><td>Превышение Tmax</td>{c.comparisons.map(x => <td key={x.solver}>{value(x.statistics?.overTimeProbability, '%', 100)}</td>)}</tr>
        <tr><td>Среднее число остановок</td>{c.comparisons.map(x => <td key={x.solver}>{value(x.statistics?.meanStops)}</td>)}</tr>
        <tr><td>Среднее время, с</td>{c.comparisons.map(x => <td key={x.solver}>{value(x.statistics?.meanTime)}</td>)}</tr>
        <tr><td>95-й процентиль времени, с</td>{c.comparisons.map(x => <td key={x.solver}>{value(x.statistics?.percentile95)}</td>)}</tr>
      </tbody>
    </table>
    <p className="muted note">Вероятности остановки и превышения Tmax, средние значения и процентиль рассчитаны отдельно по физически допустимым прогонам каждого маршрута. Если маршруты совпадают, совпадут и результаты проверки. Начальное значение генератора: {c.seed}.</p>
  </div>
}
