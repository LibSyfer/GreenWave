import { useEffect, useMemo, useState } from 'react'
import { fetchSamples, solve, type Sample, type SolveResponse, type SolverId } from './api'
import { Field, ProblemForm } from './ProblemForm'
import { fromProblem, toProblem, type ProblemFormState } from './formModel'
import { RouteTable } from './RouteTable'
import { TimeDistanceChart } from './TimeDistanceChart'
import { fmt } from './units'

const SOLVERS: { id: SolverId; title: string }[] = [
  { id: 'dp', title: 'Динамическое программирование' },
  { id: 'ga', title: 'Генетический алгоритм' },
  { id: 'bruteforce', title: 'Полный перебор' },
]

export default function App() {
  const [samples, setSamples] = useState<Sample[]>([])
  const [sampleId, setSampleId] = useState('')
  const [form, setForm] = useState<ProblemFormState | null>(null)
  const [solver, setSolver] = useState<SolverId>('dp')
  const [speedStepKmh, setSpeedStepKmh] = useState(2)
  const [result, setResult] = useState<SolveResponse | null>(null)
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
      setResult(await solve(problem, solver, speedStepKmh))
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
            <Field label="Шаг скорости, км/ч" value={speedStepKmh} onChange={setSpeedStepKmh} />
          </section>

          <button className="primary" onClick={run} disabled={!form || loading}>
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

          <div className="panel">
            {chartReady ? (
              <TimeDistanceChart problem={problem} trajectory={result?.trajectory ?? null} />
            ) : (
              <p className="muted">Заполните длины сегментов, чтобы увидеть график.</p>
            )}
          </div>

          {result?.route && problem && (
            <div className="panel">
              <RouteTable problem={problem} route={result.route} />
            </div>
          )}
        </section>
      </main>
    </div>
  )
}

function Metric({ label, value, muted }: { label: string; value: string; muted?: boolean }) {
  return (
    <div className={muted ? 'metric muted' : 'metric'}>
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  )
}
