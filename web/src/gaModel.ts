import type { GaOrigin, GaTrace } from './api'

export const ORIGIN_LABELS: Record<GaOrigin, string> = {
  Random: 'случайная',
  Elite: 'элита',
  Crossover: 'скрещивание',
  Clone: 'копия',
}

export const ORIGIN_HINTS: Record<GaOrigin, string> = {
  Random: 'Случайная особь начального поколения',
  Elite: 'Одна из лучших особей прошлого поколения, перенесена без изменений',
  Crossover: 'Голова хромосомы от первого родителя, хвост — от второго; затем возможны мутации',
  Clone: 'Скрещивание не выпало: копия родителя, затем возможны мутации',
}

export const originOf = (t: GaTrace, i: number): GaOrigin => t.originNames[t.individuals.origin[i]]

export const genesOf = (t: GaTrace, i: number): number[] =>
  t.individuals.genes.slice(i * t.segmentCount, (i + 1) * t.segmentCount)

export const violation = (t: GaTrace, i: number) => t.individuals.shortage[i] + t.individuals.overrun[i]

export const feasible = (t: GaTrace, i: number) => violation(t, i) === 0

/** Как GeneticSolver.Comparer: меньше нарушение, затем энергия, затем время. */
export const compare = (t: GaTrace, a: number, b: number) =>
  violation(t, a) - violation(t, b) || t.individuals.energy[a] - t.individuals.energy[b] || t.individuals.time[a] - t.individuals.time[b]

export const generationCount = (t: GaTrace) => t.generationStart.length - 1

export function generationIds(t: GaTrace, g: number): number[] {
  const ids: number[] = []
  for (let i = t.generationStart[g]; i < t.generationStart[g + 1]; i++) ids.push(i)
  return ids
}

/** Особи поколения от лучшей к худшей. */
export const sortedGeneration = (t: GaTrace, g: number) => generationIds(t, g).sort((a, b) => compare(t, a, b))

export interface GenerationStats {
  best: number | null // энергия лучшей допустимой, Дж
  average: number | null // средняя энергия допустимых, Дж
  feasible: number
}

export function generationStats(t: GaTrace): GenerationStats[] {
  return Array.from({ length: generationCount(t) }, (_, g) => {
    const energies = generationIds(t, g).filter(i => feasible(t, i)).map(i => t.individuals.energy[i])
    return {
      best: energies.length > 0 ? Math.min(...energies) : null,
      average: energies.length > 0 ? energies.reduce((a, b) => a + b, 0) / energies.length : null,
      feasible: energies.length,
    }
  })
}

export interface Mutation {
  gene: number
  from: number
  to: number
}

export function mutationsOf(t: GaTrace, i: number): Mutation[] {
  const m = t.individuals
  const result: Mutation[] = []
  for (let k = m.mutationStart[i]; k < m.mutationStart[i + 1]; k++)
    result.push({ gene: m.mutationGene[k], from: m.mutationFrom[k], to: m.mutationTo[k] })
  return result
}

/** Участники турнира за первого (1) или второго (2) родителя; null — турнира не было. */
export function tournamentOf(t: GaTrace, i: number, which: 1 | 2): number[] | null {
  const k = t.options.tournamentSize
  const all = which === 1 ? t.individuals.tournament1 : t.individuals.tournament2
  const ids = all.slice(i * k, (i + 1) * k)
  return ids[0] < 0 ? null : ids
}

/** От какого родителя пришёл ген j (до мутаций); -1 — случайный ген начального поколения. */
export function geneParent(t: GaTrace, i: number, j: number): number {
  const m = t.individuals
  switch (originOf(t, i)) {
    case 'Random':
      return -1
    case 'Crossover':
      return j < m.crossoverPoint[i] ? m.parent1[i] : m.parent2[i]
    default:
      return m.parent1[i]
  }
}

export interface GeneHistory {
  /** Особь начального поколения, где ген появился случайно. */
  origin: number
  originValue: number
  /** Мутации по пути к особи — от ранних к поздним. */
  mutations: { individual: number; from: number; to: number }[]
  /** Сколько поколений ген передавался. */
  generations: number
}

/** Откуда у особи i ген j: идём назад по родителям, собирая мутации этого гена. */
export function geneHistory(t: GaTrace, i: number, j: number): GeneHistory {
  const mutations: GeneHistory['mutations'] = []
  let current = i

  for (;;) {
    const m = mutationsOf(t, current).find(x => x.gene === j)
    if (m) mutations.push({ individual: current, from: m.from, to: m.to })

    const parent = geneParent(t, current, j)
    if (parent < 0) break
    current = parent
  }

  mutations.reverse()
  const originValue = mutations.length > 0 ? mutations[0].from : t.individuals.genes[i * t.segmentCount + j]
  return {
    origin: current,
    originValue,
    mutations,
    generations: t.individuals.generation[i] - t.individuals.generation[current],
  }
}

/** Схематичная траектория особи: старт, прибытие к каждому светофору и, при остановке, момент проезда. */
export function schematicPoints(t: GaTrace, i: number, ends: number[]): [number, number][] {
  const m = t.individuals
  const points: [number, number][] = [[0, 0]]
  for (let j = 0; j < t.segmentCount; j++) {
    const k = i * t.segmentCount + j
    points.push([m.arrivalTime[k], ends[j]])
    if (m.stopped[k]) points.push([m.passTime[k], ends[j]])
  }
  return points
}

/** Кратко, почему особь недопустима; пустая строка — допустима. */
export function violationText(t: GaTrace, i: number, fmt: (v: number, digits?: number) => string): string {
  const parts: string[] = []
  if (t.individuals.overrun[i] > 0) parts.push(`опоздание ${fmt(t.individuals.overrun[i])} с`)
  if (t.individuals.shortage[i] > 0) parts.push(`не хватает ${fmt(t.individuals.shortage[i], 0)} м`)
  return parts.join(', ')
}
