import type { DpStatus, DpTrace } from './api'

export const STATUS_LABELS: Record<DpStatus, string> = {
  Accepted: 'принят',
  LengthShortage: 'не хватает длины',
  TimeLimit: 'опоздание',
  RemainderTimeLimit: 'не успеть остаток',
  Dominated: 'проиграл в ячейке',
}

export const STATUS_HINTS: Record<DpStatus, string> = {
  Accepted: 'Лучший в своей ячейке (время, скорость) — переходит на следующий сегмент',
  LengthShortage: 'Сегмент слишком короткий, чтобы разогнаться или затормозить до этой скорости',
  TimeLimit: 'Время после сегмента уже больше Tmax',
  RemainderTimeLimit: 'Tmax пока не превышено, но остаток пути не проехать даже на максимальной скорости',
  Dominated: 'В той же ячейке (время ±шаг, та же скорость) есть кандидат с меньшей энергией',
}

export const STATUS_COLORS: Record<DpStatus, string> = {
  Accepted: '#1d1d1b',
  LengthShortage: '#8a63d2',
  TimeLimit: '#c0661c',
  RemainderTimeLimit: '#e0a83a',
  Dominated: '#a3a39d',
}

/** Индексы для быстрых выборок по трассе: дети кандидата и кандидаты сегмента по статусу. */
export interface DpIndex {
  /** Дети старта. */
  rootChildren: number[]
  /** children[i] — кандидаты, порождённые из кандидата i. */
  children: number[][]
  /** byLayerStatus[layer][status] — кандидаты сегмента с этим статусом. */
  byLayerStatus: number[][][]
  /** Принятые кандидаты по сегментам. */
  accepted: number[][]
  /** Кандидаты лучшего маршрута. */
  best: Set<number>
  acceptedStatus: number
}

export function buildIndex(trace: DpTrace): DpIndex {
  const { parent, layer, status } = trace.candidates
  const n = parent.length
  const layers = trace.layers.length
  const statuses = trace.statusNames.length
  const acceptedStatus = trace.statusNames.indexOf('Accepted')

  const rootChildren: number[] = []
  const children: number[][] = Array.from({ length: n }, () => [])
  const byLayerStatus = Array.from({ length: layers }, () => Array.from({ length: statuses }, () => [] as number[]))

  for (let i = 0; i < n; i++) {
    if (parent[i] < 0) rootChildren.push(i)
    else children[parent[i]].push(i)
    byLayerStatus[layer[i]][status[i]].push(i)
  }

  return {
    rootChildren,
    children,
    byLayerStatus,
    accepted: byLayerStatus.map(l => l[acceptedStatus]),
    best: new Set(trace.bestPath),
    acceptedStatus,
  }
}

/** Цепочка кандидатов от первого сегмента до i включительно. */
export function branch(trace: DpTrace, i: number): number[] {
  const chain: number[] = []
  for (let k = i; k >= 0; k = trace.candidates.parent[k]) chain.push(k)
  return chain.reverse()
}

/** Не больше limit элементов, равномерно по массиву. */
export function sample<T>(items: T[], limit: number): T[] {
  if (items.length <= limit) return items
  const step = items.length / limit
  return Array.from({ length: limit }, (_, k) => items[Math.floor(k * step)])
}
