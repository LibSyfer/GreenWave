import { useEffect, useMemo, useState } from 'react'
import {
  fetchSamples,
  solve,
  solveDpTrace,
  solveGaTrace,
  type DpTraceResponse,
  type GaRunOptions,
  type GaTraceResponse,
  type Sample,
  type SolveResponse,
  type SolverId,
} from './api'
import { DpView } from './DpView'
import { GaView } from './GaView'
import { Field, ProblemForm } from './ProblemForm'
import { fromProblem, toProblem, type ProblemFormState } from './formModel'
import { RouteTable } from './RouteTable'
import { TimeDistanceChart } from './TimeDistanceChart'
import { fmt, plural } from './units'

const SOLVERS: { id: SolverId; title: string }[] = [
  { id: 'dp', title: 'Динамическое программирование' },
  { id: 'ga', title: 'Генетический алгоритм' },
  { id: 'bruteforce', title: 'Полный перебор' },
]

type Result =
  | { kind: 'plain'; data: SolveResponse }
  | { kind: 'dp'; data: DpTraceResponse }
  | { kind: 'ga'; data: GaTraceResponse }

const SPEED_STEPS = [1, 2, 4, 5, 10]
const TIME_STEPS = [0.1, 0.5, 1]

export default function App() {
  const [samples, setSamples] = useState<Sample[]>([])
  const [sampleId, setSampleId] = useState('')
  const [form, setForm] = useState<ProblemFormState | null>(null)
  const [solver, setSolver] = useState<SolverId>('dp')
  const [speedStepKmh, setSpeedStepKmh] = useState(2)
  const [timeStep, setTimeStep] = useState(0.1)
  const [gaOptions, setGaOptions] = useState<GaRunOptions>({ populationSize: 60, generations: 100, seed: 1 })
  // для ДП и ГА — ответ с трассой, для перебора — только маршрут
  const [result, setResult] = useState<Result | null>(null)
  const [runId, setRunId] = useState(0)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const selectSample = (sample: Sample) => {
    setSampleId(sample.id)
    setForm(fromProblem(sample.problem))
    setResult(null)
    setError(null)
  }

  useEffect(() => {
    fetchSamples()
      .then(list => {
        setSamples(list)
        if (list.length > 0) selectSample(list[0])
      })
      .catch(e => setError(e.message))
  }, [])

  // любое изменение задачи делает прошлый результат неактуальным
  const editForm = (value: ProblemFormState) => {
    setForm(value)
    setResult(null)
  }

  const problem = useMemo(() => (form ? toProblem(form) : null), [form])
  const chartReady = problem !== null && problem.road.segments.length > 0 && problem.road.segments.every(s => s.length > 0)

  const run = async () => {
    if (!problem) return
    setLoading(true)
    setError(null)
    try {
      setResult(
        solver === 'dp'
          ? { kind: 'dp', data: await solveDpTrace(problem, speedStepKmh, timeStep) }
          : solver === 'ga'
            ? { kind: 'ga', data: await solveGaTrace(problem, speedStepKmh, gaOptions) }
            : { kind: 'plain', data: await solve(problem, solver, speedStepKmh) },
      )
      setRunId(id => id + 1)
    } catch (e) {
      setResult(null)
      setError(e instanceof Error ? e.message : String(e))
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="page">
      <header>
        <h1>GreenWave</h1>
        <p className="muted">Скорости на участках дороги, чтобы проехать светофоры с минимальными затратами энергии</p>
      </header>

      <main>
        <aside className="panel">
          <label className="field">
            <span>Пример</span>
            <select
              value={sampleId}
              onChange={e => {
                const sample = samples.find(s => s.id === e.target.value)
                if (sample) selectSample(sample)
              }}
            >
              {samples.map(s => <option key={s.id} value={s.id}>{s.title}</option>)}
            </select>
          </label>

          {form && <ProblemForm value={form} onChange={editForm} />}

          <section className="fields">
            <h3>Решатель</h3>
            <label className="field">
              <span>Алгоритм</span>
              <select value={solver} onChange={e => setSolver(e.target.value as SolverId)}>
                {SOLVERS.map(s => <option key={s.id} value={s.id}>{s.title}</option>)}
              </select>
            </label>
            <label className="field">
              <span>Шаг скорости</span>
              <select value={speedStepKmh} onChange={e => setSpeedStepKmh(Number(e.target.value))}>
                {SPEED_STEPS.map(step => (
                  <option key={step} value={step}>
                    {step} км/ч{form ? ` · ${plural(gridSize(form, step), 'скорость', 'скорости', 'скоростей')}` : ''}
                  </option>
                ))}
              </select>
            </label>
            {solver === 'dp' && (
              <label className="field">
                <span title="Состояния с одной скоростью, время которых попало в одну ячейку, сливаются">Ячейка времени</span>
                <select value={timeStep} onChange={e => setTimeStep(Number(e.target.value))}>
                  {TIME_STEPS.map(step => <option key={step} value={step}>{fmt(step, 1)} с</option>)}
                </select>
              </label>
            )}
            {solver === 'ga' && (
              <>
                <Field label="Особей в поколении" value={gaOptions.populationSize} onChange={v => setGaOptions({ ...gaOptions, populationSize: v })} />
                <Field label="Поколений" value={gaOptions.generations} onChange={v => setGaOptions({ ...gaOptions, generations: v })} />
                <Field label="Зерно генератора" value={gaOptions.seed} onChange={v => setGaOptions({ ...gaOptions, seed: v })} />
              </>
            )}
            {solver === 'bruteforce' && form && (
              <p className="muted note">
                Маршрутов: {gridSize(form, speedStepKmh)}<sup>{form.segments.length}</sup> ≈{' '}
                {(gridSize(form, speedStepKmh) ** form.segments.length).toLocaleString('ru-RU', { maximumSignificantDigits: 2 })}
              </p>
            )}
          </section>

          <button className="primary" onClick={run} disabled={!form || loading}>
            {loading ? 'Считаем…' : 'Решить'}
          </button>
        </aside>

        <section className="results">
          {error && <div className="error">{error}</div>}

          {result && (
            <div className="summary">
              {result.data.route ? (
                <>
                  <Metric label="Энергия" value={`${fmt(result.data.route.energy / 1000, 2)} кДж`} />
                  <Metric label="Время" value={`${fmt(result.data.route.time)} с`} />
                  <Metric label="Остановок" value={String(result.data.route.stops)} />
                </>
              ) : (
                <Metric label="Результат" value="решения нет" />
              )}
              <Metric label="Расчёт" value={`${fmt(result.data.elapsedMs, 1)} мс`} muted />
            </div>
          )}

          {chartReady && result?.kind === 'dp' ? (
            <DpView key={runId} problem={problem} response={result.data} />
          ) : chartReady && result?.kind === 'ga' ? (
            <GaView key={runId} problem={problem} response={result.data} />
          ) : (
            <div className="panel">
              {chartReady ? (
                <TimeDistanceChart problem={problem} trajectory={result?.data.trajectory ?? null} />
              ) : (
                <p className="muted">Заполните длины сегментов, чтобы увидеть график.</p>
              )}
            </div>
          )}

          {result?.data.route && problem && (
            <div className="panel">
              <RouteTable problem={problem} route={result.data.route} />
            </div>
          )}
        </section>
      </main>
    </div>
  )
}

/** Сколько скоростей в сетке: как Limits.SpeedGrid на сервере — от минимальной с шагом, плюс максимальная. */
function gridSize(form: ProblemFormState, stepKmh: number) {
  const { minSpeedKmh: min, maxSpeedKmh: max } = form
  if (!(stepKmh > 0) || !(max >= min)) return 0
  let count = 0
  while (min + count * stepKmh < max - 1e-9) count++
  return count + 1
}

function Metric({ label, value, muted }: { label: string; value: string; muted?: boolean }) {
  return (
    <div className={muted ? 'metric muted' : 'metric'}>
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  )
}
