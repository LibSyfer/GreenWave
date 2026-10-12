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

export type GaOrigin = 'Random' | 'Elite' | 'Crossover' | 'Clone'

/**
 * Особи ГА всех поколений по колонкам: i-й элемент — i-я особь.
 * Поля «на сегмент» плоские: особь i — элементы [i·n, (i+1)·n), n = segmentCount.
 */
export interface GaIndividuals {
  generation: number[]
  genes: number[] // номера скоростей в speedGrid
  arrivalTime: number[]
  passTime: number[]
  stopped: boolean[]
  energy: number[]
  time: number[]
  shortage: number[]
  overrun: number[]
  origin: number[] // индекс в originNames
  parent1: number[] // -1 — нет
  parent2: number[]
  crossoverPoint: number[]
  tournament1: number[] // по tournamentSize на особь, -1 — турнира не было
  tournament2: number[]
  mutationStart: number[] // мутации особи i — [mutationStart[i], mutationStart[i + 1])
  mutationGene: number[]
  mutationFrom: number[]
  mutationTo: number[]
}

export interface GaTrace {
  speedGrid: number[]
  segmentCount: number
  options: {
    populationSize: number
    generations: number
    tournamentSize: number
    crossoverRate: number
    mutationRate: number
    maxCreep: number
    eliteCount: number
    seed: number
  }
  originNames: GaOrigin[]
  generationStart: number[]
  generationBest: number[]
  best: number
  individuals: GaIndividuals
}

export interface GaTraceResponse {
  route: Route | null
  trajectory: TrajectoryPoint[] | null
  elapsedMs: number
  trace: GaTrace
}

export interface GaRunOptions {
  populationSize: number
  generations: number
  seed: number
}

export async function fetchSamples(): Promise<Sample[]> {
  const response = await fetch('/api/samples')
  if (!response.ok) throw new Error(`Не удалось загрузить примеры: ${response.status}`)
  return response.json()
}

export function solve(problem: Problem, solver: SolverId, speedStepKmh: number): Promise<SolveResponse> {
  return post('/api/solve', { problem, solver, speedStepKmh })
}

export function solveDpTrace(problem: Problem, speedStepKmh: number, timeStep: number): Promise<DpTraceResponse> {
  return post('/api/solve/dp/trace', { problem, speedStepKmh, timeStep })
}

export function solveGaTrace(problem: Problem, speedStepKmh: number, options: GaRunOptions): Promise<GaTraceResponse> {
  return post('/api/solve/ga/trace', { problem, speedStepKmh, ...options })
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
