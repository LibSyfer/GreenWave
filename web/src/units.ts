import type { Problem } from './api'

export const toKmh = (ms: number) => ms * 3.6
export const toMs = (kmh: number) => kmh / 3.6

export const round = (value: number, digits = 2) => Math.round(value * 10 ** digits) / 10 ** digits

export const fmt = (value: number, digits = 1) =>
  value.toLocaleString('ru-RU', { minimumFractionDigits: digits, maximumFractionDigits: digits })

/** «1 скорость», «3 скорости», «21 скорость», «5 скоростей». */
export function plural(n: number, one: string, few: string, many: string) {
  const mod10 = n % 10
  const mod100 = n % 100
  const word = mod10 === 1 && mod100 !== 11 ? one : mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14) ? few : many
  return `${n} ${word}`
}

/** Путь от старта до конца каждого сегмента, м. */
export function segmentEnds(problem: Problem) {
  let sum = 0
  return problem.road.segments.map(s => (sum += s.length))
}
