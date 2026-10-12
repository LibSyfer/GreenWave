import { memo, useMemo, useState } from 'react'
import type { DpStatus, DpTrace, DpTraceResponse, Problem } from './api'
import { branch, buildIndex, sample, STATUS_COLORS, STATUS_HINTS, STATUS_LABELS, type DpIndex } from './dpModel'
import { TimeDistanceChart, type ChartScales, type PickPoint } from './TimeDistanceChart'
import { fmt, toKmh } from './units'

const HIGHLIGHT_LIMIT = 3000
const BRANCH_COLOR = '#2f6fd6'

interface Highlight {
  layer: number
  status: number
}

/**
 * Визуализация ДП: выжившие состояния на диаграмме «время–расстояние» (граф решений),
 * таблица отбрасываний по сегментам и разбор выбранного состояния.
 */
export function DpView({ problem, response }: { problem: Problem; response: DpTraceResponse }) {
  const trace = response.trace
  const index = useMemo(() => buildIndex(trace), [trace])
  const layerCount = trace.layers.length

  const [step, setStep] = useState(layerCount)
  const [selected, setSelected] = useState<number | null>(null)
  const [highlight, setHighlight] = useState<Highlight | null>(null)

  const c = trace.candidates
  const selectedChildren = selected === null ? [] : index.children[selected]
  const highlighted = highlight ? index.byLayerStatus[highlight.layer][highlight.status] : []
  const highlightedShown = sample(highlighted, HIGHLIGHT_LIMIT)

  // ось времени должна вмещать показанных отброшенных, но не растягиваться бесконечно
  const extent = Math.min(
    problem.limits.maxTime * 1.6,
    Math.max(0, ...selectedChildren.map(i => c.time[i]), ...highlightedShown.map(i => c.time[i])),
  )

  // выбрать можно выжившие состояния на показанных сегментах и варианты выбранного состояния
  const pickable = useMemo<PickPoint[]>(() => {
    const ends = segmentEnds(problem)
    const ids = [...index.accepted.slice(0, step).flat(), ...(selected === null ? [] : index.children[selected])]
    return ids.map(i => ({ id: i, time: c.time[i], distance: ends[c.layer[i]] }))
  }, [problem, index, step, selected, c])

  const describe = (i: number) => {
    const status = trace.statusNames[c.status[i]]
    const tail = status === 'Accepted' ? '' : ` · ${STATUS_LABELS[status]}`
    return `сегмент ${c.layer[i] + 1} · ${fmt(toKmh(c.speed[i]), 0)} км/ч · проезд ${fmt(c.time[i])} с${c.stopped[i] ? ' (стоп)' : ''} · ${fmt(c.energy[i] / 1000, 2)} кДж${tail}`
  }

  const overlay = (s: ChartScales) => (
    <DpOverlay trace={trace} index={index} scales={s} step={step} selected={selected} highlighted={highlightedShown} />
  )

  const legend = (
    <>
      <span><i className="dot" style={{ background: STATUS_COLORS.Accepted }} />состояние</span>
      <span><i className="dot ring" />после остановки</span>
      <span><i className="swatch" style={{ background: BRANCH_COLOR }} />выбранная ветка</span>
    </>
  )

  return (
    <>
      <div className="panel">
        <div className="dp-toolbar">
          <label className="field">
            <span>Сегменты на графике: 1–{step}</span>
            <input type="range" min={1} max={layerCount} value={step} onChange={e => setStep(e.target.valueAsNumber)} />
          </label>
          <span className="muted">Кликните рядом с точкой, чтобы разобрать состояние</span>
        </div>
        <TimeDistanceChart
          problem={problem}
          trajectory={step === layerCount ? response.trajectory : null}
          overlay={overlay}
          legend={legend}
          minTimeExtent={extent}
          pickable={pickable}
          onPick={setSelected}
          describe={describe}
        />
      </div>

      <div className="dp-panels">
        <div className="panel">
          <LayerTable trace={trace} highlight={highlight} onHighlight={setHighlight} />
          {highlight && highlighted.length > highlightedShown.length && (
            <p className="muted note">На графике показано {highlightedShown.length.toLocaleString('ru-RU')} из {highlighted.length.toLocaleString('ru-RU')}.</p>
          )}
        </div>
        <div className="panel">
          <div className="dp-nav">
            {trace.bestPath.length > 0 ? (
              <BranchLinks trace={trace} chain={trace.bestPath} title="Лучший маршрут" current={selected ?? undefined} onSelect={setSelected} />
            ) : (
              <p className="muted">Решения нет — лучшего маршрута нет.</p>
            )}
            <div className="dp-nav-actions">
              {trace.bestPath.length > 0 && (
                <button
                  className="secondary"
                  onClick={() => {
                    setStep(layerCount)
                    setSelected(trace.bestPath[trace.bestPath.length - 1])
                  }}
                >
                  Показать лучший маршрут
                </button>
              )}
              {selected !== null && <button className="secondary" onClick={() => setSelected(null)}>Сбросить выбор</button>}
            </div>
          </div>

          {selected === null ? (
            <p className="muted note">Состояние не выбрано. Кликните рядом с точкой на графике или по шагу лучшего маршрута.</p>
          ) : (
            <NodeDetails trace={trace} index={index} node={selected} onSelect={setSelected} />
          )}
        </div>
      </div>
    </>
  )
}

interface OverlayProps {
  trace: DpTrace
  index: DpIndex
  scales: ChartScales
  step: number
  selected: number | null
  highlighted: number[]
}

// memo: при наведении мыши график перерисовывается, а тысячи точек ДП — нет
const DpOverlay = memo(function DpOverlay({ trace, index, scales, step, selected, highlighted }: OverlayProps) {
  const c = trace.candidates
  const { sx, sy, ends, xMax } = scales

  const x = (i: number) => sx(Math.min(c.time[i], xMax))
  const y = (i: number) => sy(ends[c.layer[i]])
  const px = (i: number) => (c.parent[i] < 0 ? sx(0) : x(c.parent[i]))
  const py = (i: number) => (c.parent[i] < 0 ? sy(0) : y(c.parent[i]))

  const visible = index.accepted.slice(0, step).flat()

  // все рёбра — одним path, иначе тысячи элементов тормозят
  const edges = visible.map(i => `M${px(i)},${py(i)}L${x(i)},${y(i)}`).join('')

  const chain = selected === null ? [] : branch(trace, selected)
  const chainPath = chain.length > 0 ? `M${sx(0)},${sy(0)}` + chain.map(i => `L${x(i)},${y(i)}`).join('') : ''
  const children = selected === null ? [] : index.children[selected]

  return (
    <g>
      <path d={edges} className="dp-edge" />

      {highlighted.map(i => (
        <circle key={`h${i}`} cx={x(i)} cy={y(i)} r={2.6} fill={STATUS_COLORS[trace.statusNames[c.status[i]]]} className="dp-point" />
      ))}

      {visible.map(i => (
        <circle
          key={i}
          cx={x(i)}
          cy={y(i)}
          r={index.best.has(i) ? 3.6 : 2.4}
          className={c.endSpeed[i] === 0 ? 'dp-node stopped' : 'dp-node'}
        />
      ))}

      {chainPath && <path d={chainPath} className="dp-branch" stroke={BRANCH_COLOR} />}

      {children.map(i => {
        const status = trace.statusNames[c.status[i]]
        return (
          <g key={`c${i}`} className="dp-child">
            <line x1={px(i)} y1={py(i)} x2={x(i)} y2={y(i)} stroke={STATUS_COLORS[status]} strokeDasharray={status === 'Accepted' ? undefined : '3 3'} />
            <circle cx={x(i)} cy={y(i)} r={3.2} fill={STATUS_COLORS[status]} className="dp-point" />
          </g>
        )
      })}

      {selected !== null && <circle cx={x(selected)} cy={y(selected)} r={6} className="dp-selected" stroke={BRANCH_COLOR} />}
    </g>
  )
})

function LayerTable({ trace, highlight, onHighlight }: { trace: DpTrace; highlight: Highlight | null; onHighlight: (h: Highlight | null) => void }) {
  return (
    <table className="dp-layers">
      <thead>
        <tr>
          <th>Сегмент</th>
          <th>Всего</th>
          {trace.statusNames.map(s => (
            <th key={s} title={STATUS_HINTS[s]}>
              <i className="dot" style={{ background: STATUS_COLORS[s] }} />
              {STATUS_LABELS[s]}
            </th>
          ))}
        </tr>
      </thead>
      <tbody>
        {trace.layers.map(layer => (
          <tr key={layer.segmentIndex}>
            <td className="muted">{layer.segmentIndex + 1}</td>
            <td>{total(layer.counts).toLocaleString('ru-RU')}</td>
            {layer.counts.map((count, status) => {
              const active = highlight?.layer === layer.segmentIndex && highlight.status === status
              return (
                <td key={status}>
                  {count === 0 ? (
                    <span className="muted">0</span>
                  ) : (
                    <button
                      className={active ? 'count active' : 'count'}
                      title="Показать на графике"
                      onClick={() => onHighlight(active ? null : { layer: layer.segmentIndex, status })}
                    >
                      {count.toLocaleString('ru-RU')}
                    </button>
                  )}
                </td>
              )
            })}
          </tr>
        ))}
      </tbody>
    </table>
  )
}

function NodeDetails({ trace, index, node, onSelect }: { trace: DpTrace; index: DpIndex; node: number; onSelect: (i: number) => void }) {
  const c = trace.candidates
  const status = trace.statusNames[c.status[node]]
  const children = [...index.children[node]].sort((a, b) => c.speed[a] - c.speed[b])
  const isLast = c.layer[node] === trace.layers.length - 1
  const winner = c.dominatedBy[node]

  return (
    <div className="node-details">
      <div className="node-title">
        <strong>Сегмент {c.layer[node] + 1} · кандидат #{node}</strong>
        <StatusBadge status={status} />
        {index.best.has(node) && <span className="badge best">лучший маршрут</span>}
      </div>

      <dl>
        <dt>Скорость на сегменте</dt><dd>{fmt(toKmh(c.speed[node]), 0)} км/ч</dd>
        <dt>У светофора</dt><dd>{fmt(c.arrivalTime[node])} с</dd>
        <dt>Остановка</dt><dd>{c.stopped[node] ? 'да' : 'нет'}</dd>
        <dt>Проезд</dt><dd>{fmt(c.time[node])} с, {fmt(toKmh(c.endSpeed[node]), 0)} км/ч</dd>
        <dt>Энергия с начала</dt><dd>{fmt(c.energy[node] / 1000, 2)} кДж</dd>
      </dl>

      <p className="muted note">{STATUS_HINTS[status]}.</p>

      {status === 'Dominated' && winner >= 0 && (
        <p className="note">
          Победил <button className="link-inline" onClick={() => onSelect(winner)}>#{winner}</button>:{' '}
          {fmt(c.energy[winner] / 1000, 2)} кДж против {fmt(c.energy[node] / 1000, 2)} кДж, проезд {fmt(c.time[winner])} с.
        </p>
      )}

      <BranchLinks trace={trace} chain={branch(trace, node)} title="Ветка от старта" current={node} onSelect={onSelect} />

      {status === 'Accepted' && isLast && <p className="note">Финиш. {index.best.has(node) ? 'Это лучший маршрут.' : 'Не лучший по энергии.'}</p>}

      {children.length > 0 && (
        <>
          <h3>Варианты на сегменте {c.layer[node] + 2}</h3>
          <table className="dp-children">
            <thead>
              <tr>
                <th>км/ч</th>
                <th>проезд, с</th>
                <th>стоп</th>
                <th>энергия, кДж</th>
                <th>итог</th>
              </tr>
            </thead>
            <tbody>
              {children.map(i => {
                const s = trace.statusNames[c.status[i]]
                return (
                  <tr key={i} onClick={() => onSelect(i)}>
                    <td>{fmt(toKmh(c.speed[i]), 0)}</td>
                    <td>{fmt(c.time[i])}</td>
                    <td>{c.stopped[i] ? 'да' : ''}</td>
                    <td>{fmt(c.energy[i] / 1000, 2)}</td>
                    <td style={{ color: STATUS_COLORS[s] }}>
                      {STATUS_LABELS[s]}
                      {s === 'Dominated' && ` → #${c.dominatedBy[i]}`}
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </>
      )}
    </div>
  )
}

function BranchLinks({ trace, chain, title, current, onSelect }: { trace: DpTrace; chain: number[]; title: string; current?: number; onSelect: (i: number) => void }) {
  const c = trace.candidates
  return (
    <div className="branch">
      <h3>{title}</h3>
      <div className="branch-steps">
        <span className="muted">старт</span>
        {chain.map(i => (
          <span key={i}>
            <span className="muted"> → </span>
            <button className={i === current ? 'link-inline current' : 'link-inline'} onClick={() => onSelect(i)}>
              {fmt(toKmh(c.speed[i]), 0)} км/ч
            </button>
            <span className="muted"> ({fmt(c.time[i], 0)} с)</span>
          </span>
        ))}
      </div>
    </div>
  )
}

function StatusBadge({ status }: { status: DpStatus }) {
  return (
    <span className="badge" style={{ color: STATUS_COLORS[status], borderColor: STATUS_COLORS[status] }}>
      {STATUS_LABELS[status]}
    </span>
  )
}

const total = (counts: number[]) => counts.reduce((a, b) => a + b, 0)

/** Путь от старта до конца каждого сегмента, м. */
const segmentEnds = (problem: Problem) => {
  let sum = 0
  return problem.road.segments.map(s => (sum += s.length))
}
