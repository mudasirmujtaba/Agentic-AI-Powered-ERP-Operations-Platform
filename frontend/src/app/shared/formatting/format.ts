/** Value formats shared by charts, KPI tiles and report tables (mirrors the API's ValueFormat). */
export type ValueFormat = 'Number' | 'Currency' | 'Percent' | 'Days' | 'Text' | 'Date';
export type ChartFormat = 'currency' | 'number';

const currency = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 });
const currencyCents = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', minimumFractionDigits: 2 });
const number = new Intl.NumberFormat('en-US', { maximumFractionDigits: 1 });
const percent = new Intl.NumberFormat('en-US', { style: 'percent', maximumFractionDigits: 1 });
const date = new Intl.DateTimeFormat('en-US', { year: 'numeric', month: 'short', day: 'numeric' });

export function formatValue(value: number, format: ChartFormat): string {
  return format === 'currency' ? currency.format(value) : number.format(value);
}

export function formatCompact(value: number, format: ChartFormat): string {
  return new Intl.NumberFormat('en-US', {
    ...(format === 'currency' ? { style: 'currency', currency: 'USD' } : {}),
    notation: 'compact',
    maximumFractionDigits: 1,
  }).format(value);
}

/** Formats a report cell or KPI; `null`/missing shows an em dash rather than a misleading zero. */
export function formatReportValue(value: unknown, format: ValueFormat, precise = false): string {
  if (value === null || value === undefined || value === '') return '—';
  switch (format) {
    case 'Currency':
      return (precise ? currencyCents : currency).format(Number(value));
    case 'Number':
      return number.format(Number(value));
    case 'Percent':
      return percent.format(Number(value));
    case 'Days':
      return `${number.format(Number(value))} d`;
    case 'Date':
      return date.format(new Date(String(value)));
    default:
      return String(value);
  }
}

/** Right-align numeric columns. */
export function isNumericFormat(format: ValueFormat): boolean {
  return format !== 'Text' && format !== 'Date';
}
