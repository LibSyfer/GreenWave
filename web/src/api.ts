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
  settings?: { speedStepKmh: number; experiments: number; phaseShift: number } | null
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

export type SolverId = 'dp' | 'ga' | 'bruteforce' | 'montecarlo'

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

export interface GaSelection {
  contestantIds: number[]
  winnerId: number
}

export interface GaIndividual {
  id: number
  genes: number[]
  energy: number
  time: number
  shortage: number
  overrun: number
  feasible: boolean
  origin: {
    kind: 'Random' | 'Elite' | 'Crossover' | 'Clone'
    parent1Id: number | null
    parent2Id: number | null
    selection1: GaSelection | null
    selection2: GaSelection | null
    crossoverPoint: number | null
    mutations: { gene: number; from: number; to: number }[]
  }
}

export interface GaTraceResponse {
  route: Route | null
  trajectory: TrajectoryPoint[] | null
  elapsedMs: number
  gaTrace: {
    speedGrid: number[]
    bestId: number
    generations: { index: number; bestId: number; population: GaIndividual[] }[]
  }
}

export function solveGaTrace(problem: Problem, speedStepKmh: number): Promise<GaTraceResponse> {
  return post('/api/solve/ga/trace', { problem, speedStepKmh })
}

export interface MonteCarloStatistics {
  experiments: number
  phaseShift: number
  seed: number
  validExperiments: number
  invalidExperiments: number
  withStops: number
  overTime: number
  stopProbability: number | null
  overTimeProbability: number | null
  meanStops: number | null
  meanTime: number | null
  percentile95: number | null
}

export interface MonteCarloResponse extends SolveResponse {
  monteCarlo: {
    experiments: number
    phaseShift: number
    seed: number
    displayedSolver: 'dp' | 'ga' | null
    comparisons: { solver: 'dp' | 'ga'; title: string; route: Route | null; trajectory: TrajectoryPoint[] | null; statistics: MonteCarloStatistics | null; experiments: MonteCarloSummary[] }[]
  }
}

export function solveMonteCarlo(problem: Problem, speedStepKmh: number, experiments: number, phaseShift: number): Promise<MonteCarloResponse> {
  return post('/api/solve/monte-carlo', { problem, speedStepKmh, experiments, phaseShift })
}

export interface MonteCarloExperiment {
  problem: Problem
  route: Route | null
  invalidSegment: number | null
  trajectory: TrajectoryPoint[] | null
}

export interface MonteCarloSummary {
  number: number
  stops: number | null
  time: number | null
  newStops: boolean
  overTime: boolean
  invalidSegment: number | null
}

export interface MonteCarloSnapshot {
  comparison: MonteCarloResponse['monteCarlo']
  number: number
  results: Partial<Record<'dp' | 'ga', MonteCarloExperiment>>
  error?: string
}

export function fetchMonteCarloExperiment(problem: Problem, speeds: number[], comparison: MonteCarloResponse['monteCarlo'], experiment: number, signal: AbortSignal): Promise<MonteCarloExperiment> {
  return post('/api/solve/monte-carlo/experiment', {
    problem, speeds, experiments: comparison.experiments, phaseShift: comparison.phaseShift, seed: comparison.seed, experiment,
  }, signal)
}

export function solve(problem: Problem, solver: SolverId, speedStepKmh: number): Promise<SolveResponse> {
  return post('/api/solve', { problem, solver, speedStepKmh })
}

export function solveDpTrace(problem: Problem, speedStepKmh: number, timeStep: number): Promise<DpTraceResponse> {
  return post('/api/solve/dp/trace', { problem, speedStepKmh, timeStep })
}

export function fetchTrajectory(problem: Problem, speeds: number[], signal: AbortSignal): Promise<{ trajectory: TrajectoryPoint[] | null }> {
  return post('/api/trajectory', { problem, speeds }, signal)
}

async function post<T>(url: string, body: unknown, signal?: AbortSignal): Promise<T> {
  const response = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
    signal,
  })

  if (!response.ok) {
    const details = await response.json().catch(() => null)
    throw new Error(details?.detail ?? `Ошибка сервера: ${response.status}`)
  }

  return response.json()
}
