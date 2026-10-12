import { memo, useEffect, useMemo, useState } from 'react'
import type { GaTrace, GaTraceResponse, Problem } from './api'
import {
  feasible,
  generationCount,
  generationIds,
  generationStats,
  geneHistory,
  geneParent,
  genesOf,
  mutationsOf,
  ORIGIN_HINTS,
  ORIGIN_LABELS,
  originOf,
  schematicPoints,
  sortedGeneration,
  tournamentOf,
  violationText,
  type GenerationStats,
} from './gaModel'
import { TimeDistanceChart, type ChartScales, type PickPoint } from './TimeDistanceChart'
import { fmt, plural, segmentEnds, toKmh } from './units'

const SELECTED_COLOR = '#2f6fd6'
const PLAY_INTERVAL_MS = 120

/**
 * Визуализация ГА: проигрыватель поколений, сходимость, популяция на диаграмме
 * «время–расстояние», хромосомы поколения и происхождение выбранной особи.
 */
export function GaView({ problem, response }: { problem: Problem; response: GaTraceResponse }) {
  const trace = response.trace
  const last = generationCount(trace) - 1
  const stats = useMemo(() => generationStats(trace), [trace])

  const [gen, setGen] = useState(last)
  const [selected, setSelected] = useState<number | null>(null)
  const [playing, setPlaying] = useState(false)

  useEffect(() => {
    if (!playing) return
    const timer = setInterval(() => {
      setGen(g => {
        if (g >= last) {
          setPlaying(false)
          return g
        }
        return g + 1
      })
    }, PLAY_INTERVAL_MS)
    return () => clearInterval(timer)
  }, [playing, last])

  const play = () => {
    if (gen >= last) setGen(0)
    setPlaying(p => !p)
  }

  // выбор особи переносит к её поколению
  const select = (i: number | null) => {
    setSelected(i)
    if (i !== null) {
      setPlaying(false)
      setGen(trace.individuals.generation[i])
    }
  }

  const ends = useMemo(() => segmentEnds(problem), [problem])
  const ids = useMemo(() => generationIds(trace, gen), [trace, gen])
  const generationBest = trace.generationBest[gen]

  const pickable = useMemo<PickPoint[]>(
    () => ids.flatMap(i => schematicPoints(trace, i, ends).slice(1).map(([time, distance]) => ({ id: i, time, distance }))),
    [ids, trace, ends],
  )

  const describe = (i: number) => {
    const speeds = genesOf(trace, i).map(g => fmt(toKmh(trace.speedGrid[g]), 0)).join(', ')
    const status = violationText(trace, i, fmt) || 'допустима'
    return `#${i} · [${speeds}] км/ч · ${fmt(trace.individuals.energy[i] / 1000, 2)} кДж · ${status}`
  }

  const overlay = (s: ChartScales) => <GaOverlay trace={trace} ids={ids} best={generationBest} selected={selected} ends={ends} scales={s} />

  const legend = (
    <>
      <span><i className="swatch" style={{ background: '#7d7d78' }} />допустимая</span>
      <span><i className="swatch" style={{ background: '#e6a39c' }} />недопустимая</span>
      <span><i className="swatch" style={{ background: SELECTED_COLOR }} />выбранная</span>
    </>
  )

  const s = stats[gen]
  const bestOverall = trace.best

  return (
    <>
      <div className="panel">
        <div className="ga-toolbar">
          <button className="secondary play" onClick={play}>{playing ? '❚❚ Пауза' : '▶ Проиграть'}</button>
          <input type="range" min={0} max={last} value={gen} onChange={e => { setPlaying(false); setGen(e.target.valueAsNumber) }} />
          <span className="ga-gen">
            Поколение <strong>{gen}</strong> из {last}
          </span>
          <span className="muted">
            допустимых {s.feasible} из {ids.length}
            {s.best !== null && <> · лучшая {fmt(s.best / 1000, 2)} кДж</>}
          </span>
        </div>
        <ConvergenceChart stats={stats} population={trace.options.populationSize} gen={gen} onGen={g => { setPlaying(false); setGen(g) }} />
      </div>

      <div className="panel">
        <TimeDistanceChart
          problem={problem}
          trajectory={gen === last ? response.trajectory : null}
          overlay={overlay}
          legend={legend}
          pickable={pickable}
          onPick={select}
          describe={describe}
        />
      </div>

      <div className="panel">
        <div className="dp-nav">
          <div>
            <h3>Лучшая особь запуска</h3>
            <span>
              <button className="link-inline" onClick={() => select(bestOverall)}>#{bestOverall}</button>
              <span className="muted"> · поколение {trace.individuals.generation[bestOverall]} · </span>
              <GeneSpeeds trace={trace} i={bestOverall} />
              {!feasible(trace, bestOverall) && <span className="stop"> · недопустима: {violationText(trace, bestOverall, fmt)}</span>}
            </span>
          </div>
          <div className="dp-nav-actions">
            <button className="secondary" onClick={() => select(bestOverall)}>Показать лучшую особь</button>
            {selected !== null && <button className="secondary" onClick={() => setSelected(null)}>Сбросить выбор</button>}
          </div>
        </div>

        {selected === null ? (
          <p className="muted note">Особь не выбрана. Кликните рядом с траекторией на графике или по строке таблицы ниже.</p>
        ) : (
          <IndividualDetails trace={trace} i={selected} onSelect={select} />
        )}
      </div>

      <div className="panel">
        <h3>Хромосомы поколения {gen} — от лучшей к худшей</h3>
        <GenerationTable trace={trace} gen={gen} selected={selected} onSelect={select} />
      </div>
    </>
  )
}

interface OverlayProps {
  trace: GaTrace
  ids: number[]
  best: number
  selected: number | null
  ends: number[]
  scales: ChartScales
}

// memo: при наведении мыши график перерисовывается, а траектории популяции — нет
const GaOverlay = memo(function GaOverlay({ trace, ids, best, selected, ends, scales }: OverlayProps) {
  const { sx, sy, xMax } = scales
  const path = (i: number) =>
    schematicPoints(trace, i, ends)
      .map(([t, d], k) => `${k === 0 ? 'M' : 'L'}${sx(Math.min(t, xMax))},${sy(d)}`)
      .join('')

  const valid = ids.filter(i => feasible(trace, i))
  const invalid = ids.filter(i => !feasible(trace, i))

  return (
    <g>
      <path d={invalid.map(path).join('')} className="ga-path invalid" />
      <path d={valid.map(path).join('')} className="ga-path valid" />
      <path d={path(best)} className="ga-path best" />
      {selected !== null && ids.includes(selected) && <path d={path(selected)} className="ga-path selected" stroke={SELECTED_COLOR} />}
    </g>
  )
})

function ConvergenceChart({ stats, population, gen, onGen }: { stats: GenerationStats[]; population: number; gen: number; onGen: (g: number) => void }) {
  const W = 900
  const H = 170
  const M = { left: 60, right: 16, top: 20, bottom: 26 }
  const plotW = W - M.left - M.right
  const plotH = H - M.top - M.bottom

  const [hover, setHover] = useState<number | null>(null)

  const values = stats.flatMap(s => [s.best, s.average]).filter((v): v is number => v !== null)
  const yMin = values.length > 0 ? Math.min(...values) * 0.95 : 0
  const yMax = values.length > 0 ? Math.max(...values) * 1.03 : 1
  const last = Math.max(stats.length - 1, 1)

  const sx = (g: number) => M.left + (g / last) * plotW
  const sy = (e: number) => M.top + plotH - ((e - yMin) / (yMax - yMin || 1)) * plotH

  const line = (pick: (s: GenerationStats) => number | null) =>
    stats
      .map((s, g) => [g, pick(s)] as const)
      .filter(([, v]) => v !== null)
      .map(([g, v], k) => `${k === 0 ? 'M' : 'L'}${sx(g)},${sy(v!)}`)
      .join('')

  const genAt = (e: React.MouseEvent<SVGSVGElement>) => {
    const rect = e.currentTarget.getBoundingClientRect()
    const x = ((e.clientX - rect.left) / rect.width) * W
    return Math.max(0, Math.min(stats.length - 1, Math.round(((x - M.left) / plotW) * last)))
  }

  const shown = hover ?? gen
  const s = stats[shown]
  const yTicks = [0, 0.5, 1].map(k => yMin + k * (yMax - yMin))
  const barW = Math.max(1, plotW / stats.length - 1)

  return (
    <div className="chart">
      <div className="chart-header">
        <div className="legend">
          <span><i className="swatch line" />лучшая энергия</span>
          <span><i className="swatch" style={{ background: '#a3a39d' }} />средняя (допустимые)</span>
          <span><i className="swatch" style={{ background: '#e4ece4' }} />доля допустимых</span>
        </div>
        <div className="readout">
          поколение {shown}: {s.best !== null ? `лучшая ${fmt(s.best / 1000, 2)} · средняя ${fmt(s.average! / 1000, 2)} кДж` : 'допустимых нет'} · допустимых {s.feasible}
        </div>
      </div>
      <svg
        viewBox={`0 0 ${W} ${H}`}
        className="picking"
        onMouseMove={e => setHover(genAt(e))}
        onMouseLeave={() => setHover(null)}
        onClick={e => onGen(genAt(e))}
      >
        {stats.map((st, g) => (
          <rect key={g} x={sx(g) - barW / 2} width={barW} y={M.top + plotH * (1 - st.feasible / population)} height={(plotH * st.feasible) / population} className="ga-feasible-bar" />
        ))}
        {yTicks.map(v => (
          <g key={v}>
            <line x1={M.left} x2={M.left + plotW} y1={sy(v)} y2={sy(v)} className="grid" />
            <text x={M.left - 8} y={sy(v) + 4} className="tick" textAnchor="end">{fmt(v / 1000, 0)}</text>
          </g>
        ))}
        <text x={M.left - 8} y={10} className="axis-label" textAnchor="end">кДж</text>
        {[0, Math.round(last / 2), last].map(g => (
          <text key={g} x={sx(g)} y={H - 8} className="tick" textAnchor="middle">{g}</text>
        ))}
        <path d={line(st => st.average)} className="ga-line average" />
        <path d={line(st => st.best)} className="ga-line best" />
        <line x1={sx(gen)} x2={sx(gen)} y1={M.top} y2={M.top + plotH} className="ga-marker" />
        {hover !== null && hover !== gen && <line x1={sx(hover)} x2={sx(hover)} y1={M.top} y2={M.top + plotH} className="cursor" />}
      </svg>
    </div>
  )
}

function GenerationTable({ trace, gen, selected, onSelect }: { trace: GaTrace; gen: number; selected: number | null; onSelect: (i: number) => void }) {
  const ids = useMemo(() => sortedGeneration(trace, gen), [trace, gen])

  return (
    <div className="ga-table-wrap">
      <table className="ga-table">
        <thead>
          <tr>
            <th>#</th>
            <th>особь</th>
            {Array.from({ length: trace.segmentCount }, (_, j) => <th key={j} className="gene-col">с{j + 1}</th>)}
            <th>энергия, кДж</th>
            <th>время, с</th>
            <th>статус</th>
            <th>происхождение</th>
          </tr>
        </thead>
        <tbody>
          {ids.map((i, rank) => {
            const mutated = new Set(mutationsOf(trace, i).map(m => m.gene))
            const origin = originOf(trace, i)
            return (
              <tr key={i} className={i === selected ? 'selected' : undefined} onClick={() => onSelect(i)}>
                <td className="muted">{rank + 1}</td>
                <td className="muted">#{i}</td>
                {genesOf(trace, i).map((g, j) => (
                  <td key={j} className="gene-col">
                    <GeneCell trace={trace} gene={g} mutated={mutated.has(j)} />
                  </td>
                ))}
                <td>{fmt(trace.individuals.energy[i] / 1000, 2)}</td>
                <td>{fmt(trace.individuals.time[i])}</td>
                <td>{feasible(trace, i) ? <span className="muted">допустима</span> : <span className="stop">{violationText(trace, i, fmt)}</span>}</td>
                <td className="muted" title={ORIGIN_HINTS[origin]}>
                  {ORIGIN_LABELS[origin]}
                  {mutated.size > 0 && ' + мутация'}
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}

function IndividualDetails({ trace, i, onSelect }: { trace: GaTrace; i: number; onSelect: (i: number) => void }) {
  const m = trace.individuals
  const origin = originOf(trace, i)
  const p1 = m.parent1[i]
  const p2 = m.parent2[i]
  const cut = m.crossoverPoint[i]
  const mutations = mutationsOf(trace, i)
  const rank = sortedGeneration(trace, m.generation[i]).indexOf(i) + 1

  return (
    <div className="node-details">
      <div className="node-title">
        <strong>Особь #{i} · поколение {m.generation[i]} · место {rank}</strong>
        <span className="badge" title={ORIGIN_HINTS[origin]}>{ORIGIN_LABELS[origin]}</span>
        {i === trace.best && <span className="badge best">лучшая особь запуска</span>}
        {!feasible(trace, i) && <span className="badge invalid">недопустима</span>}
      </div>

      <dl>
        <dt>Энергия</dt><dd>{fmt(m.energy[i] / 1000, 2)} кДж</dd>
        <dt>Время</dt><dd>{fmt(m.time[i])} с</dd>
        {!feasible(trace, i) && <><dt>Нарушение</dt><dd className="stop">{violationText(trace, i, fmt)}</dd></>}
      </dl>

      <h3>Происхождение</h3>
      <p className="muted note">{ORIGIN_HINTS[origin]}.</p>

      {origin !== 'Random' && (
        <div className="lineage">
          <ChromosomeRow
            trace={trace}
            label={origin === 'Crossover' ? 'родитель 1' : 'родитель'}
            i={p1}
            highlight={j => (origin === 'Crossover' ? j < cut : true)}
            source="a"
            cut={origin === 'Crossover' ? cut : -1}
            onSelect={onSelect}
          />
          {origin === 'Crossover' && (
            <ChromosomeRow trace={trace} label="родитель 2" i={p2} highlight={j => j >= cut} source="b" cut={cut} onSelect={onSelect} />
          )}
          <ChromosomeRow
            trace={trace}
            label="особь"
            i={i}
            highlight={() => true}
            source={j => (origin === 'Crossover' && j >= cut ? 'b' : 'a')}
            cut={origin === 'Crossover' ? cut : -1}
            mutations={mutations}
          />
          {mutations.length > 0 && (
            <p className="note">
              Мутации:{' '}
              {mutations.map((mu, k) => (
                <span key={k}>
                  {k > 0 && ', '}сегмент {mu.gene + 1}: {speedKmh(trace, mu.from)} → {speedKmh(trace, mu.to)} км/ч
                </span>
              ))}
            </p>
          )}
        </div>
      )}

      {(origin === 'Crossover' || origin === 'Clone') && (
        <div className="tournaments">
          <Tournament trace={trace} title={origin === 'Crossover' ? 'Турнир за родителя 1' : 'Турнир за родителя'} ids={tournamentOf(trace, i, 1)} winner={p1} onSelect={onSelect} />
          {origin === 'Crossover' && <Tournament trace={trace} title="Турнир за родителя 2" ids={tournamentOf(trace, i, 2)} winner={p2} onSelect={onSelect} />}
        </div>
      )}

      <h3>История генов</h3>
      <GeneHistoryList trace={trace} i={i} onSelect={onSelect} />
    </div>
  )
}

interface ChromosomeRowProps {
  trace: GaTrace
  label: string
  i: number
  highlight: (j: number) => boolean
  source: 'a' | 'b' | ((j: number) => 'a' | 'b')
  cut: number
  mutations?: { gene: number; from: number; to: number }[]
  onSelect?: (i: number) => void
}

/** Хромосома в разборе происхождения: подсвечены гены, ушедшие потомку, отмечены разрез и мутации. */
function ChromosomeRow({ trace, label, i, highlight, source, cut, mutations = [], onSelect }: ChromosomeRowProps) {
  const genes = genesOf(trace, i)
  return (
    <div className="chromosome-row">
      <span className="chromosome-label">
        {label}{' '}
        {onSelect ? <button className="link-inline" onClick={() => onSelect(i)}>#{i}</button> : <span>#{i}</span>}
        <span className="muted"> · {fmt(trace.individuals.energy[i] / 1000, 2)} кДж</span>
      </span>
      <span className="chromosome">
        {genes.map((g, j) => {
          const mutation = mutations.find(mu => mu.gene === j)
          const src = typeof source === 'function' ? source(j) : source
          return (
            <span key={j} className="chromosome-gene">
              {j === cut && <span className="cut" title="точка разреза" />}
              <span className={`gene-box src-${src}${highlight(j) ? '' : ' faded'}${mutation ? ' mutated' : ''}`}>
                {speedKmh(trace, g)}
              </span>
              {mutation && <span className="mutation-note">было {speedKmh(trace, mutation.from)}</span>}
            </span>
          )
        })}
      </span>
    </div>
  )
}

function Tournament({ trace, title, ids, winner, onSelect }: { trace: GaTrace; title: string; ids: number[] | null; winner: number; onSelect: (i: number) => void }) {
  if (!ids) return null
  return (
    <div className="tournament">
      <h3>{title}</h3>
      {ids.map((id, k) => (
        <div key={k} className={id === winner ? 'contestant winner' : 'contestant'}>
          <button className="link-inline" onClick={() => onSelect(id)}>#{id}</button>{' '}
          <GeneSpeeds trace={trace} i={id} />{' '}
          <span className="muted">
            · {fmt(trace.individuals.energy[id] / 1000, 2)} кДж
            {!feasible(trace, id) && ` · ${violationText(trace, id, fmt)}`}
          </span>
          {id === winner && <span className="winner-mark"> — победитель</span>}
        </div>
      ))}
    </div>
  )
}

function GeneHistoryList({ trace, i, onSelect }: { trace: GaTrace; i: number; onSelect: (i: number) => void }) {
  const m = trace.individuals
  return (
    <ul className="gene-history">
      {genesOf(trace, i).map((g, j) => {
        const h = geneHistory(trace, i, j)
        return (
          <li key={j}>
            <span className="gene-history-head">
              Сегмент {j + 1}: <strong>{speedKmh(trace, g)} км/ч</strong>
            </span>
            <span className="muted">
              {' '}— случайно {speedKmh(trace, h.originValue)} км/ч в поколении {m.generation[h.origin]} (
              <button className="link-inline" onClick={() => onSelect(h.origin)}>#{h.origin}</button>)
              {h.mutations.map((mu, k) => (
                <span key={k}>
                  {' '}→ мутация {speedKmh(trace, mu.from)}→{speedKmh(trace, mu.to)} в поколении {m.generation[mu.individual]} (
                  <button className="link-inline" onClick={() => onSelect(mu.individual)}>#{mu.individual}</button>)
                </span>
              ))}
              {h.generations > 0 && <>; передаётся {plural(h.generations, 'поколение', 'поколения', 'поколений')}</>}
              {geneParent(trace, i, j) < 0 && h.mutations.length === 0 && ' — эта особь и есть источник'}
            </span>
          </li>
        )
      })}
    </ul>
  )
}

function GeneCell({ trace, gene, mutated }: { trace: GaTrace; gene: number; mutated: boolean }) {
  const f = trace.speedGrid.length > 1 ? gene / (trace.speedGrid.length - 1) : 0
  return (
    <span className={mutated ? 'gene-cell mutated' : 'gene-cell'} style={geneStyle(f)} title={mutated ? 'мутировал' : undefined}>
      {speedKmh(trace, gene)}
    </span>
  )
}

function GeneSpeeds({ trace, i }: { trace: GaTrace; i: number }) {
  return <span>[{genesOf(trace, i).map(g => speedKmh(trace, g)).join(', ')}] км/ч</span>
}

/** Цвет гена по скорости: светлее — медленнее, темнее — быстрее. */
function geneStyle(f: number) {
  return {
    background: `hsl(215 30% ${92 - f * 52}%)`,
    color: f > 0.55 ? '#fff' : 'var(--text)',
  }
}

const speedKmh = (trace: GaTrace, gene: number) => fmt(toKmh(trace.speedGrid[gene]), 0)
