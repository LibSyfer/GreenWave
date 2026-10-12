import type { Problem } from './api'
import { round, toKmh, toMs } from './units'

// Состояние формы — в единицах, удобных человеку (км/ч), в задачу переводится при запуске.

export interface SegmentForm {
  length: number
  hasLight: boolean
  cycle: number
  offset: number
  green: number
}

export interface ProblemFormState {
  segments: SegmentForm[]
  mass: number
  acceleration: number
  deceleration: number
  minSpeedKmh: number
  maxSpeedKmh: number
  maxTime: number
  initialSpeedKmh: number
  safetyMargin: number
}

export function fromProblem(p: Problem): ProblemFormState {
  return {
    segments: p.road.segments.map(s => ({
      length: s.length,
      hasLight: s.light !== null,
      cycle: s.light?.cycleDuration ?? 60,
      offset: s.light?.offset ?? 0,
      green: s.light?.greenDuration ?? 30,
    })),
    mass: p.vehicle.mass,
    acceleration: p.vehicle.acceleration,
    deceleration: p.vehicle.deceleration,
    minSpeedKmh: round(toKmh(p.limits.minSpeed)),
    maxSpeedKmh: round(toKmh(p.limits.maxSpeed)),
    maxTime: p.limits.maxTime,
    initialSpeedKmh: round(toKmh(p.initialSpeed)),
    safetyMargin: p.safetyMargin,
  }
}

export function toProblem(f: ProblemFormState): Problem {
  return {
    road: {
      segments: f.segments.map(s => ({
        length: s.length,
        light: s.hasLight ? { cycleDuration: s.cycle, offset: s.offset, greenDuration: s.green } : null,
      })),
    },
    vehicle: { mass: f.mass, acceleration: f.acceleration, deceleration: f.deceleration },
    limits: { minSpeed: toMs(f.minSpeedKmh), maxSpeed: toMs(f.maxSpeedKmh), maxTime: f.maxTime },
    initialSpeed: toMs(f.initialSpeedKmh),
    safetyMargin: f.safetyMargin,
  }
}
