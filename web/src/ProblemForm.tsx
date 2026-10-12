import type { ProblemFormState, SegmentForm } from './formModel'

interface Props {
  value: ProblemFormState
  onChange: (value: ProblemFormState) => void
}

export function ProblemForm({ value, onChange }: Props) {
  const set = <K extends keyof ProblemFormState>(key: K, v: ProblemFormState[K]) => onChange({ ...value, [key]: v })

  const setSegment = (index: number, patch: Partial<SegmentForm>) =>
    set('segments', value.segments.map((s, i) => (i === index ? { ...s, ...patch } : s)))

  const addSegment = () =>
    set('segments', [...value.segments, { length: 300, hasLight: true, cycle: 60, offset: 0, green: 30 }])

  const removeSegment = (index: number) => set('segments', value.segments.filter((_, i) => i !== index))

  return (
    <div className="form">
      <section>
        <h3>Дорога</h3>
        <table className="segments">
          <thead>
            <tr>
              <th>#</th>
              <th>Длина, м</th>
              <th title="Светофор в конце сегмента">Св.</th>
              <th title="Длительность цикла, с">Цикл</th>
              <th title="Начало зелёного от t = 0, с">Сдвиг</th>
              <th title="Длительность зелёного, с">Зел.</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {value.segments.map((s, i) => (
              <tr key={i}>
                <td className="muted">{i + 1}</td>
                <td><NumberInput value={s.length} onChange={v => setSegment(i, { length: v })} /></td>
                <td>
                  <input type="checkbox" checked={s.hasLight} onChange={e => setSegment(i, { hasLight: e.target.checked })} />
                </td>
                <td><NumberInput value={s.cycle} disabled={!s.hasLight} onChange={v => setSegment(i, { cycle: v })} /></td>
                <td><NumberInput value={s.offset} disabled={!s.hasLight} onChange={v => setSegment(i, { offset: v })} /></td>
                <td><NumberInput value={s.green} disabled={!s.hasLight} onChange={v => setSegment(i, { green: v })} /></td>
                <td>
                  <button className="icon" title="Удалить сегмент" onClick={() => removeSegment(i)}>×</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        <button className="link" onClick={addSegment}>+ сегмент</button>
      </section>

      <section className="fields">
        <h3>Ограничения</h3>
        <Field label="Мин. скорость, км/ч" value={value.minSpeedKmh} onChange={v => set('minSpeedKmh', v)} />
        <Field label="Макс. скорость, км/ч" value={value.maxSpeedKmh} onChange={v => set('maxSpeedKmh', v)} />
        <Field label="Tmax, с" value={value.maxTime} onChange={v => set('maxTime', v)} />
        <Field label="Начальная скорость, км/ч" value={value.initialSpeedKmh} onChange={v => set('initialSpeedKmh', v)} />
        <Field label="Запас у светофора, с" value={value.safetyMargin} onChange={v => set('safetyMargin', v)} />
      </section>

      <section className="fields">
        <h3>Автомобиль</h3>
        <Field label="Масса, кг" value={value.mass} onChange={v => set('mass', v)} />
        <Field label="Ускорение, м/с²" value={value.acceleration} onChange={v => set('acceleration', v)} />
        <Field label="Замедление, м/с²" value={value.deceleration} onChange={v => set('deceleration', v)} />
      </section>
    </div>
  )
}

function NumberInput({ value, onChange, disabled }: { value: number; onChange: (v: number) => void; disabled?: boolean }) {
  return (
    <input
      type="number"
      value={Number.isNaN(value) ? '' : value}
      disabled={disabled}
      onChange={e => onChange(e.target.valueAsNumber)}
    />
  )
}

export function Field({ label, value, onChange }: { label: string; value: number; onChange: (v: number) => void }) {
  return (
    <label className="field">
      <span>{label}</span>
      <NumberInput value={value} onChange={onChange} />
    </label>
  )
}
