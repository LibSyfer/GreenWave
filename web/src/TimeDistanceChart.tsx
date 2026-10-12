import { useState } from 'react'
import type { Problem, TrajectoryPoint } from './api'
import { fmt, toKmh } from './units'

const W = 900
const H = 420
const M = { left: 60, right: 16, top: 12, bottom: 44 }
const PLOT_W = W - M.left - M.right
const PLOT_H = H - M.top - M.bottom
const LIGHT_BAR = 6

interface Props {
  problem: Problem
  trajectory: TrajectoryPoint[] | null
}

/** График «время–расстояние»: фазы светофоров на их позициях и траектория машины. */
export function TimeDistanceChart({ problem, trajectory }: Props) {
  const [hoverTime, setHoverTime] = useState<number | null>(null)

  const segments = problem.road.segments
  const ends = segments.map((_, i) => segments.slice(0, i + 1).reduce((sum, s) => sum + s.length, 0))
  const totalLength = ends[ends.length - 1] ?? 0
  const lastTime = trajectory?.[trajectory.length - 1]?.time ?? 0
  const xMax = niceCeil(Math.max(problem.limits.maxTime, lastTime) * 1.03)

  const sx = (t: number) => M.left + (t / xMax) * PLOT_W
  const sy = (d: number) => M.top + PLOT_H - (d / totalLength) * PLOT_H

  const hover = hoverTime !== null && trajectory ? pointAt(trajectory, hoverTime) : null

  const onMove = (e: React.MouseEvent<SVGSVGElement>) => {
    const rect = e.currentTarget.getBoundingClientRect()
    const x = ((e.clientX - rect.left) / rect.width) * W
    const t = ((x - M.left) / PLOT_W) * xMax
    setHoverTime(t >= 0 && t <= xMax ? t : null)
  }

  return (
    <div className="chart">
      <div className="chart-header">
        <div className="legend">
          <span><i className="swatch green" />зелёный</span>
          <span><i className="swatch red" />красный</span>
          <span><i className="swatch line" />траектория</span>
        </div>
        <div className="readout">
          {hover
            ? `t = ${fmt(hover.time)} с · путь ${fmt(hover.distance, 0)} м · ${fmt(toKmh(hover.speed), 0)} км/ч`
            : 'наведите на график'}
        </div>
      </div>

      <svg viewBox={`0 0 ${W} ${H}`} onMouseMove={onMove} onMouseLeave={() => setHoverTime(null)}>
        {/* сетка и оси */}
        {timeTicks(xMax).map(t => (
          <g key={`t${t}`}>
            <line x1={sx(t)} x2={sx(t)} y1={M.top} y2={M.top + PLOT_H} className="grid" />
            <text x={sx(t)} y={M.top + PLOT_H + 16} className="tick" textAnchor="middle">{t}</text>
          </g>
        ))}
        {[0, ...ends].map((d, i) => (
          <g key={`d${i}`}>
            <line x1={M.left} x2={M.left + PLOT_W} y1={sy(d)} y2={sy(d)} className="grid" />
            <text x={M.left - 8} y={sy(d) + 4} className="tick" textAnchor="end">{d}</text>
          </g>
        ))}
        <text x={M.left + PLOT_W} y={H - 4} className="axis-label" textAnchor="end">время, с</text>
        <text x={12} y={M.top + 4} className="axis-label">м</text>

        {/* фазы светофоров */}
        {segments.map((s, i) =>
          s.light ? (
            <g key={`l${i}`}>
              {lightIntervals(s.light, xMax).map(([from, to, green], k) => (
                <rect
                  key={k}
                  x={sx(from)}
                  width={sx(to) - sx(from)}
                  y={sy(ends[i]) - LIGHT_BAR / 2}
                  height={LIGHT_BAR}
                  className={green ? 'phase-green' : 'phase-red'}
                />
              ))}
            </g>
          ) : null,
        )}

        {/* Tmax */}
        <line x1={sx(problem.limits.maxTime)} x2={sx(problem.limits.maxTime)} y1={M.top} y2={M.top + PLOT_H} className="tmax" />
        <text x={sx(problem.limits.maxTime) - 4} y={M.top + 12} className="tick" textAnchor="end">Tmax</text>

        {trajectory && (
          <polyline points={trajectory.map(p => `${sx(p.time)},${sy(p.distance)}`).join(' ')} className="trajectory" />
        )}

        {hover && (
          <g>
            <line x1={sx(hover.time)} x2={sx(hover.time)} y1={M.top} y2={M.top + PLOT_H} className="cursor" />
            <circle cx={sx(hover.time)} cy={sy(hover.distance)} r={3.5} className="cursor-dot" />
          </g>
        )}
      </svg>
    </div>
  )
}

/** Интервалы [от, до, зелёный?] светофора на отрезке [0, xMax]. */
function lightIntervals(light: { cycleDuration: number; offset: number; greenDuration: number }, xMax: number) {
  const { cycleDuration: c, offset, greenDuration: g } = light
  const result: [number, number, boolean][] = []
  if (!(c > 0)) return result

  const clip = (a: number, b: number, green: boolean) => {
    const from = Math.max(a, 0)
    const to = Math.min(b, xMax)
    if (to > from) result.push([from, to, green])
  }

  // первый цикл начинается не позже 0 — так покрывается и хвост красного до первого зелёного
  for (let start = offset - Math.ceil(offset / c) * c; start < xMax; start += c) {
    clip(start, start + g, true)
    clip(start + g, start + c, false)
  }

  return result
}

function pointAt(points: TrajectoryPoint[], t: number): TrajectoryPoint | null {
  if (points.length === 0 || t < points[0].time || t > points[points.length - 1].time) return null

  for (let i = 1; i < points.length; i++) {
    const a = points[i - 1]
    const b = points[i]
    if (t <= b.time) {
      const k = b.time > a.time ? (t - a.time) / (b.time - a.time) : 1
      return { time: t, distance: a.distance + k * (b.distance - a.distance), speed: a.speed + k * (b.speed - a.speed) }
    }
  }
  return points[points.length - 1]
}

function niceCeil(value: number) {
  const step = value > 200 ? 50 : value > 50 ? 10 : 5
  return Math.ceil(value / step) * step
}

function timeTicks(xMax: number) {
  const step = [5, 10, 20, 25, 50, 100, 200].find(s => xMax / s <= 12) ?? 500
  const ticks: number[] = []
  for (let t = 0; t <= xMax + 1e-9; t += step) ticks.push(t)
  return ticks
}
