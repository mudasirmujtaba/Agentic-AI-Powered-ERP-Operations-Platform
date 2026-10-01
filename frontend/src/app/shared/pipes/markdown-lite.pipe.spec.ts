import { MarkdownLitePipe } from './markdown-lite.pipe';

describe('MarkdownLitePipe', () => {
  const pipe = new MarkdownLitePipe();

  it('renders paragraphs, bullets, bold and citations', () => {
    const html = pipe.transform('Verdict: **late**.\n\n- Stock short [1]\n- PO due soon');
    expect(html).toBe('<p>Verdict: <strong>late</strong>.</p><ul><li>Stock short <sup class="citation">[1]</sup></li><li>PO due soon</li></ul>');
  });

  it('escapes HTML from model output', () => {
    const html = pipe.transform('<img src=x onerror=alert(1)> **ok**');
    expect(html).not.toContain('<img');
    expect(html).toContain('&lt;img');
    expect(html).toContain('<strong>ok</strong>');
  });
});
