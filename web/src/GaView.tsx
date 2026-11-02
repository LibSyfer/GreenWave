import { useMemo, useState } from 'react'
import type { GaIndividual, GaSelection, GaTraceResponse } from './api'
import { fmt, toKmh } from './units'

const ORIGIN = { Random: 'Случайная', Elite: 'Элита', Crossover: 'Скрещивание', Clone: 'Копирование' }

/** История ГА использует только существующие классы оформления. */
export function GaView({ response, onSelect }: { response: GaTraceResponse; onSelect: (id: number) => void }) {
  const trace = response.gaTrace
  const [generationIndex, setGenerationIndex] = useState(0)
  const [selectedId, setSelectedId] = useState(trace.generations[0].bestId)
  const byId = useMemo(() => new Map(trace.generations.flatMap(g =>
    g.population.map(x => [x.id, { individual: x, generation: g.index }] as const))), [trace])
  const generation = trace.generations[generationIndex]
  const selected = byId.get(selectedId)!.individual
  const origin = selected.origin
  const speed = (gene: number) => fmt(toKmh(trace.speedGrid[gene]), 0)
  const genes = (x: GaIndividual) => x.genes.map(speed).join(' · ')
  const select = (id: number) => {
    const entry = byId.get(id)
    if (!entry) return
    setGenerationIndex(entry.generation)
    setSelectedId(id)
    onSelect(id)
  }
  const changeGeneration = (index: number) => {
    setGenerationIndex(index)
    setSelectedId(trace.generations[index].bestId)
    onSelect(trace.generations[index].bestId)
  }
  const parentLink = (id: number) => <button className="link-inline" onClick={() => select(id)}>#{id}</button>
  const selection = (label: string, tournament: GaSelection | null) => tournament && (
    <p className="note">{label}: {tournament.contestantIds.map((id, i) => (
      <span key={i}>{i > 0 && ', '}{parentLink(id)}</span>
    ))}. Победитель: {parentLink(tournament.winnerId)}.</p>
  )
  const parent1 = origin.parent1Id === null ? null : byId.get(origin.parent1Id)!.individual
  const parent2 = origin.parent2Id === null ? null : byId.get(origin.parent2Id)!.individual

  return (
    <div className="panel">
      <h3>Поколения генетического алгоритма</h3>
      <div className="dp-toolbar">
        <label className="field">
          <span>Поколение</span>
          <select value={generationIndex} onChange={e => changeGeneration(Number(e.target.value))}>
            {trace.generations.map(g => <option key={g.index} value={g.index}>{g.index === 0 ? '0 — начальное' : g.index}</option>)}
          </select>
        </label>
        <div className="dp-nav-actions">
          <button className="secondary" disabled={generationIndex === 0} onClick={() => changeGeneration(generationIndex - 1)}>Назад</button>
          <button className="secondary" disabled={generationIndex === trace.generations.length - 1} onClick={() => changeGeneration(generationIndex + 1)}>Далее</button>
          <button className="secondary" onClick={() => select(trace.bestId)}>Лучшая особь</button>
        </div>
      </div>
      <p className="muted note">
        Поколение {generation.index} из {trace.generations.length - 1} · особей {generation.population.length} ·
        допустимых {generation.population.filter(x => x.feasible).length} ·
        скрещиваний {generation.population.filter(x => x.origin.kind === 'Crossover').length} потомков ·
        мутаций {generation.population.reduce((sum, x) => sum + x.origin.mutations.length, 0)} генов.
      </p>
      <details>
        <summary>Особи поколения</summary>
        <table className="dp-children">
          <thead><tr><th>Особь</th><th>Энергия, кДж</th><th>Время, с</th><th>Происхождение / мутации</th></tr></thead>
          <tbody>{generation.population.map(x => (
            <tr key={x.id}>
              <td><button className={x.id === selectedId ? 'link-inline current' : 'link-inline'} onClick={() => select(x.id)}>#{x.id}</button>{x.id === generation.bestId && ' ★'}</td>
              <td>{fmt(x.energy / 1000, 2)}</td><td>{fmt(x.time)}</td>
              <td>{ORIGIN[x.origin.kind]} / {x.origin.mutations.length}{!x.feasible && ' · недопустима'}</td>
            </tr>
          ))}</tbody>
        </table>
      </details>
      <div className="node-details">
        <h3>Особь #{selected.id} · {ORIGIN[origin.kind]}</h3>
        <p className="note">Скорости по участкам: {genes(selected)} км/ч.</p>
        <p className="note">Энергия {fmt(selected.energy / 1000, 2)} кДж · время {fmt(selected.time)} с · {selected.feasible ? 'допустима' : `недопустима: нехватка длины ${fmt(selected.shortage)} м, опоздание ${fmt(selected.overrun)} с`}.</p>
        {selected.id === trace.bestId && <p className="note">Лучшая особь за весь запуск{!selected.feasible && ' — допустимое решение не найдено'}.</p>}
        {origin.kind === 'Random' && <p className="muted note">Начальная хромосома создана случайно, родителей и скрещивания нет.</p>}
        {parent1 && <p className="note">Родитель 1: {parentLink(parent1.id)} — {genes(parent1)} км/ч.</p>}
        {parent2 && <p className="note">Родитель 2: {parentLink(parent2.id)} — {genes(parent2)} км/ч.</p>}
        {selection('Турнир родителя 1', origin.selection1)}
        {selection('Турнир родителя 2', origin.selection2)}
        {origin.kind === 'Elite' && <p className="muted note">Перенос из прошлого поколения без скрещивания и мутаций.</p>}
        {origin.kind === 'Clone' && <p className="muted note">Копия родителя 1 с возможными последующими мутациями.</p>}
        {parent1 && parent2 && origin.crossoverPoint !== null && (
          <>
            <h3>Скрещивание</h3>
            <p className="note">Разрез после участка {origin.crossoverPoint}. Слева — гены родителя 1, справа — родителя 2.</p>
            <p className="note">До мутаций: {parent1.genes.slice(0, origin.crossoverPoint).map(speed).join(' · ')} | {parent2.genes.slice(origin.crossoverPoint).map(speed).join(' · ')} км/ч.</p>
          </>
        )}
        <h3>Мутации</h3>
        {origin.mutations.length === 0 ? <p className="muted note">Мутаций нет.</p> : (
          <table className="dp-children">
            <thead><tr><th>Участок</th><th>До, км/ч</th><th>После, км/ч</th></tr></thead>
            <tbody>{origin.mutations.map(m => <tr key={m.gene}><td>{m.gene + 1}</td><td>{speed(m.from)}</td><td>{speed(m.to)}</td></tr>)}</tbody>
          </table>
        )}
      </div>
    </div>
  )
}
