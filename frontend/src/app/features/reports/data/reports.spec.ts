import { formatReportValue } from '../../../shared/formatting/format';
import { ReportTable, toCsv } from './reports';

describe('report helpers', () => {
  it('exports CSV with quoting and raw numbers', () => {
    const table: ReportTable = {
      title: 'Revenue by customer',
      columns: [
        { key: 'customer', label: 'Customer', format: 'Text' },
        { key: 'revenue', label: 'Revenue', format: 'Currency' },
      ],
      rows: [
        { customer: 'Apex, Inc.', revenue: 1234.5 },
        { customer: 'Say "hi"', revenue: null },
      ],
      emptyText: null,
    };

    expect(toCsv(table)).toBe('Customer,Revenue\r\n"Apex, Inc.",1234.5\r\n"Say ""hi""",');
  });

  it('formats report values and shows a dash for missing ones', () => {
    expect(formatReportValue(1234.5, 'Currency')).toBe('$1,235');
    expect(formatReportValue(1234.5, 'Currency', true)).toBe('$1,234.50');
    expect(formatReportValue(0.4357, 'Percent')).toBe('43.6%');
    expect(formatReportValue(46, 'Days')).toBe('46 d');
    expect(formatReportValue(null, 'Number')).toBe('—');
  });
});
