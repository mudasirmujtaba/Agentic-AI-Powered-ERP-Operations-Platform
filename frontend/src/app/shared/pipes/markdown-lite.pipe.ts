import { Pipe, PipeTransform } from '@angular/core';

/**
 * Renders the small markdown subset the agents produce — paragraphs, `-` bullets, **bold**, `code`, [n] citations —
 * after HTML-escaping the input, so model output can never inject markup.
 */
@Pipe({ name: 'markdownLite' })
export class MarkdownLitePipe implements PipeTransform {
  transform(text: string | null | undefined): string {
    if (!text) return '';

    const inline = (line: string) =>
      line
        .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
        .replace(/`([^`]+)`/g, '<code>$1</code>')
        .replace(/\[(\d{1,2})\]/g, '<sup class="citation">[$1]</sup>');

    const html: string[] = [];
    let list: string[] = [];
    const flushList = () => {
      if (list.length) {
        html.push(`<ul>${list.map((item) => `<li>${inline(item)}</li>`).join('')}</ul>`);
        list = [];
      }
    };

    for (const raw of escapeHtml(text).split(/\r?\n/)) {
      const line = raw.trim();
      const bullet = line.match(/^(?:[-*•]|\d+\.)\s+(.*)$/);
      if (bullet) {
        list.push(bullet[1]);
      } else if (line === '') {
        flushList();
      } else {
        flushList();
        html.push(`<p>${inline(line)}</p>`);
      }
    }
    flushList();
    return html.join('');
  }
}

function escapeHtml(value: string): string {
  return value.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
}
