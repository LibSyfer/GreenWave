import { useEffect, useMemo, useState } from 'react'
import { solveMonteCarlo, fetchMonteCarloExperiment, type MonteCarloSnapshot, type MonteCarloResponse, fetchSamples, fetchTrajectory, solve, solveDpTrace, solveGaTrace, type TrajectoryPoint, type GaTraceResponse, type DpTraceResponse, type Sample, type SolveResponse, type SolverId } from './api'
import { DpView } from './DpView'
import { GaView } from './GaView'
import { MonteCarloView } from './MonteCarloView'
import { MonteCarloRouteTable } from './MonteCarloRouteTable'
import { ProblemForm } from './ProblemForm'
import { fromProblem, toProblem, type ProblemFormState } from './formModel'
import { RouteTable } from './RouteTable'
import { TimeDistanceChart } from './TimeDistanceChart'
import { fmt } from './units'

const SOLVERS: { id: SolverId; title: string }[] = [
  { id: 'dp', title: 'Динамическое программирование' },
  { id: 'ga', title: 'Генетический алгоритм' },
  { id: 'bruteforce', title: 'Полный перебор' },
  { id: 'montecarlo', title: 'Монте-Карло' },
]

const SPEED_STEPS = [1, 2, 4, 5, 10]
const TIME_STEPS = [0.1, 0.5, 1]

export default function App() {
  const [samples, setSamples] = useState<Sample[]>([])
  const [sampleId, setSampleId] = useState('')
  const [form, setForm] = useState<ProblemFormState | null>(null)
  const [solver, setSolver] = useState<SolverId>('dp')
  const [speedStepKmh, setSpeedStepKmh] = useState(2)
  const [timeStep, setTimeStep] = useState(0.1)
  const [experiments, setExperiments] = useState(1000)
  const [phaseShift, setPhaseShift] = useState(3)
  // ДП и ГА возвращают историю поиска вместе с маршрутом.
  const [result, setResult] = useState<SolveResponse | DpTraceResponse | GaTraceResponse | MonteCarloResponse | null>(null)
  const [runId, setRunId] = useState(0)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [mcNumber, setMcNumber] = useState<number | null>(null)
  const [mcSnapshot, setMcSnapshot] = useState<MonteCarloSnapshot | null>(null)
  const [gaSelectedId, setGaSelectedId] = useState<number | null>(null)
  const [gaChart, setGaChart] = useState<{ individualId: number; trajectory: TrajectoryPoint[] | null; error?: string } | null>(null)

  const selectSample = (sample: Sample) => {
    setSampleId(sample.id)
    setForm(fromProblem(sample.problem))
    if (sample.settings) {
      setSpeedStepKmh(sample.settings.speedStepKmh)
      setExperiments(sample.settings.experiments)
      setPhaseShift(sample.settings.phaseShift)
    }
    setMcNumber(null)
    setMcSnapshot(null)
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
  const gaTrace = result && 'gaTrace' in result ? result.gaTrace : null
  const mc = result && 'monteCarlo' in result ? result.monteCarlo : null
  const mcCurrent = mcSnapshot?.comparison === mc && mcSnapshot?.number === mcNumber ? mcSnapshot : null
  const mcProblem = mcNumber === null ? problem : mcCurrent?.results.dp?.problem ?? mcCurrent?.results.ga?.problem ?? problem
  const dpTrajectory = mcNumber !== null ? mcCurrent?.results.dp?.trajectory ?? null : mc?.comparisons.find(x => x.solver === 'dp')?.trajectory ?? null
  const mcGaTrajectory = mcNumber !== null ? mcCurrent?.results.ga?.trajectory ?? null : mc?.comparisons.find(x => x.solver === 'ga')?.trajectory ?? null
  const sameTrajectories = !!dpTrajectory && !!mcGaTrajectory && JSON.stringify(dpTrajectory) === JSON.stringify(mcGaTrajectory)
  useEffect(() => {
    if (!mc || !problem || mcNumber === null) return
    const controller = new AbortController()
    const number = mcNumber
    Promise.all(mc.comparisons.filter(x => x.route).map(async x => {
      const replay = await fetchMonteCarloExperiment(problem, x.route!.segments.map(s => s.speed), mc, number, controller.signal)
      return [x.solver, replay] as const
    })).then(entries => {
      if (!controller.signal.aborted) setMcSnapshot({ comparison: mc, number, results: Object.fromEntries(entries) })
    }).catch(e => {
      if (!controller.signal.aborted) setMcSnapshot({ comparison: mc, number, results: {}, error: e instanceof Error ? e.message : String(e) })
    })
    return () => controller.abort()
  }, [mc, problem, mcNumber])
  const gaSelection = useMemo(() => {
    if (!gaTrace) return null
    const id = gaSelectedId ?? gaTrace.generations[0].bestId
    for (const generation of gaTrace.generations) {
      const individual = generation.population.find(x => x.id === id)
      if (individual) return { generation: generation.index, individual }
    }
    return null
  }, [gaTrace, gaSelectedId])

  useEffect(() => {
    if (!problem || !gaTrace || !gaSelection) return
    const controller = new AbortController()
    const individual = gaSelection.individual
    setGaChart(null)
    if (individual.shortage > 0) {
      setGaChart({ individualId: individual.id, trajectory: null })
      return () => controller.abort()
    }
    fetchTrajectory(problem, individual.genes.map(gene => gaTrace.speedGrid[gene]), controller.signal)
      .then(data => {
        if (!controller.signal.aborted) setGaChart({ individualId: individual.id, trajectory: data.trajectory })
      })
      .catch(e => {
        if (!controller.signal.aborted) setGaChart({ individualId: individual.id, trajectory: null, error: e instanceof Error ? e.message : String(e) })
      })
    return () => controller.abort()
  }, [problem, gaTrace, gaSelection])

  const run = async (selectedSolver: SolverId = solver) => {
    if (!problem) return
    setLoading(true)
    setMcNumber(null)
    setMcSnapshot(null)
    setGaSelectedId(null)
    setGaChart(null)
    setError(null)
    try {
      setResult(
        selectedSolver === 'montecarlo' ? await solveMonteCarlo(problem, speedStepKmh, experiments, phaseShift) : selectedSolver === 'dp' ? await solveDpTrace(problem, speedStepKmh, timeStep) : selectedSolver === 'ga' ? await solveGaTrace(problem, speedStepKmh) : await solve(problem, selectedSolver, speedStepKmh),
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
              <select value={solver} disabled={loading} onChange={e => { const next = e.target.value as SolverId; setSolver(next); setResult(null); if (next === 'montecarlo') void run(next) }}>
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
            {solver === 'montecarlo' && <>
              <label className="field">
                <span>Количество экспериментов</span>
                <input type="number" min={1} max={100000} step={1} value={Number.isNaN(experiments) ? '' : experiments}
                  onChange={e => { setExperiments(e.target.valueAsNumber); setResult(null) }} />
              </label>
              <label className="field">
                <span>Сдвиг фаз, ±с</span>
                <input type="number" min={0} max={60} step={0.1} value={Number.isNaN(phaseShift) ? '' : phaseShift}
                  onChange={e => { setPhaseShift(e.target.valueAsNumber); setResult(null) }} />
              </label>
              <p className="muted note">Автоматическое сравнение маршрутов ДП и ГА при одинаковых сдвигах фаз.</p>
            </>}
            {solver === 'dp' && (
              <label className="field">
                <span title="Состояния с одной скоростью, время которых попало в одну ячейку, сливаются">Ячейка времени</span>
                <select value={timeStep} onChange={e => setTimeStep(Number(e.target.value))}>
                  {TIME_STEPS.map(step => <option key={step} value={step}>{fmt(step, 1)} с</option>)}
                </select>
              </label>
            )}
            {solver === 'bruteforce' && form && (
              <p className="muted note">
                Маршрутов: {gridSize(form, speedStepKmh)}<sup>{form.segments.length}</sup> ≈{' '}
                {(gridSize(form, speedStepKmh) ** form.segments.length).toLocaleString('ru-RU', { maximumSignificantDigits: 2 })}
              </p>
            )}
          </section>

          <button className="primary" onClick={() => run()} disabled={!form || loading || (solver === 'montecarlo' &&
            (!Number.isInteger(experiments) || experiments < 1 || experiments > 100000 || !Number.isFinite(phaseShift) || phaseShift < 0 || phaseShift > 60))}>
            {loading ? 'Считаем…' : 'Решить'}
          </button>
        </aside>

        <section className="results">
          {error && <div className="error">{error}</div>}

          {result && (
            <div className="summary">
              {result.route ? (
                <>
                  <Metric label="Энергия" value={`${fmt(result.route.energy / 1000, 2)} кДж`} />
                  <Metric label="Время" value={`${fmt(result.route.time)} с`} />
                  <Metric label="Остановок" value={String(result.route.stops)} />
                </>
              ) : (
                <Metric label="Результат" value="решения нет" />
              )}
              <Metric label="Расчёт" value={`${fmt(result.elapsedMs, 1)} мс`} muted />
            </div>
          )}

          {chartReady && result && 'trace' in result ? (
            <DpView key={runId} problem={problem} response={result} />
          ) : (
            <div className="panel">
              {chartReady ? (
                <>
                  {gaSelection && <p className="muted note">
                    На графике: поколение {gaSelection.generation}, особь #{gaSelection.individual.id}.
                    {' '}Итоги сверху и таблица маршрута — лучший результат за весь запуск.
                    {gaSelection.individual.shortage > 0 ? ' Траектория не строится: не хватает длины для разгона или торможения.'
                      : gaSelection.individual.overrun > 0 ? ' Превышено допустимое время движения.' : ''}
                    {gaChart?.individualId === gaSelection.individual.id ? gaChart.error && ` Ошибка: ${gaChart.error}` : ' Загрузка траектории…'}
                  </p>}
                  {mc && <p className="muted note">
                    {mcNumber === null ? 'Исходные маршруты при номинальных фазах.' : `Эксперимент №${mcNumber}: сдвинутые фазы и проезд ДП/ГА.`} ДП — сплошная линия, ГА — пунктир.
                    {sameTrajectories && ' Маршруты ДП и ГА совпадают: линии наложены друг на друга.'}
                    {mcNumber !== null && !mcCurrent ? ' Загрузка эксперимента…' : <>
                      {!dpTrajectory && ' Траектория ДП отсутствует: маршрут не найден или прогон недопустим.'}
                      {!mcGaTrajectory && ' Траектория ГА отсутствует: маршрут не найден или прогон недопустим.'}
                    </>}
                    {mcCurrent?.error && ` Ошибка: ${mcCurrent.error}`}
                  </p>}
                  <TimeDistanceChart problem={mc ? mcProblem! : problem} trajectory={mc ? dpTrajectory : gaSelection
                    ? gaChart?.individualId === gaSelection.individual.id ? gaChart.trajectory : null
                    : result?.trajectory ?? null}
                    secondaryTrajectory={mc ? mcGaTrajectory : undefined} trajectoryLabel={mc ? 'ДП' : undefined} />
                </>
              ) : (
                <p className="muted">Заполните длины сегментов, чтобы увидеть график.</p>
              )}
            </div>
          )}

          {result && 'gaTrace' in result && <GaView key={runId} response={result} onSelect={setGaSelectedId} />}

          {result && 'monteCarlo' in result && <MonteCarloView comparison={result.monteCarlo} />}

          {mc && problem && <MonteCarloRouteTable key={runId} problem={problem} comparison={mc} snapshot={mcSnapshot} onExperiment={setMcNumber} />}

          {!mc && result?.route && problem && (
            <div className="panel">
              <RouteTable problem={problem} route={result.route} />
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

function plural(n: number, one: string, few: string, many: string) {
  const mod10 = n % 10
  const mod100 = n % 100
  const word = mod10 === 1 && mod100 !== 11 ? one : mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14) ? few : many
  return `${n} ${word}`
}

function Metric({ label, value, muted }: { label: string; value: string; muted?: boolean }) {
  return (
    <div className={muted ? 'metric muted' : 'metric'}>
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  )
}
