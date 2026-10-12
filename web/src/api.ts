// Типы и запросы к GreenWave.Api. Все величины — в СИ: м, с, м/с, Дж.

export interface TrafficLight {
  cycleDuration: number
  offset: number
  greenDuration: number
}

export interface Segment {
  length: number
  light: TrafficLight | null
}

export interface Problem {
  road: { segments: Segment[] }
  vehicle: { mass: number; acceleration: number; deceleration: number }
  limits: { minSpeed: number; maxSpeed: number; maxTime: number }
  initialSpeed: number
  safetyMargin: number
}

export interface Sample {
  id: string
  title: string
  problem: Problem
}

export interface SegmentResult {
  speed: number
  arrivalTime: number
  phase: number | null
  stopped: boolean
  resultState: { time: number; speed: number }
  energy: number
  lengthShortage: number
}

export interface Route {
  segments: SegmentResult[]
  energy: number
  time: number
  stops: number
}

export interface TrajectoryPoint {
  time: number
  distance: number
  speed: number
}

export type SolverId = 'dp' | 'ga' | 'bruteforce'

export interface SolveResponse {
  solver: string
  route: Route | null
  trajectory: TrajectoryPoint[] | null
  elapsedMs: number
}

export type DpStatus = 'Accepted' | 'LengthShortage' | 'TimeLimit' | 'RemainderTimeLimit' | 'Dominated'

/** Кандидаты ДП по колонкам: i-й элемент каждого массива — i-й кандидат. */
export interface DpCandidates {
  parent: number[] // -1 — старт
  layer: number[]
  speed: number[]
  arrivalTime: number[]
  time: number[]
  endSpeed: number[]
  stopped: boolean[]
  energy: number[]
  status: number[] // индекс в statusNames
  dominatedBy: number[] // -1 — нет
}

export interface DpTrace {
  speedGrid: number[]
  timeStep: number
  statusNames: DpStatus[]
  layers: { segmentIndex: number; counts: number[] }[]
  candidates: DpCandidates
  bestPath: number[]
}

export interface DpTraceResponse {
  route: Route | null
  trajectory: TrajectoryPoint[] | null
  elapsedMs: number
  trace: DpTrace
}

export async function fetchSamples(): Promise<Sample[]> {
  const response = await fetch('/api/samples')
  if (!response.ok) throw new Error(`Не удалось загрузить примеры: ${response.status}`)
  return response.json()
}

export function solve(problem: Problem, solver: SolverId, speedStepKmh: number): Promise<SolveResponse> {
  return post('/api/solve', { problem, solver, speedStepKmh })
}

export function solveDpTrace(problem: Problem, speedStepKmh: number): Promise<DpTraceResponse> {
  return post('/api/solve/dp/trace', { problem, speedStepKmh })
}

async function post<T>(url: string, body: unknown): Promise<T> {
  const response = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })

  if (!response.ok) {
    const details = await response.json().catch(() => null)
    throw new Error(details?.detail ?? `Ошибка сервера: ${response.status}`)
  }

  return response.json()
}
