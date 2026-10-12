export const toKmh = (ms: number) => ms * 3.6
export const toMs = (kmh: number) => kmh / 3.6

export const round = (value: number, digits = 2) => Math.round(value * 10 ** digits) / 10 ** digits

export const fmt = (value: number, digits = 1) =>
  value.toLocaleString('ru-RU', { minimumFractionDigits: digits, maximumFractionDigits: digits })
