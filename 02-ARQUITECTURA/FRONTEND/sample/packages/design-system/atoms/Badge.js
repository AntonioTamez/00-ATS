import { h } from '../../ui-runtime/index.js';

/** Atom. A short label with a tone: neutral, success, warning or danger. */
export function Badge({ text, tone = 'neutral' }) {
  return h('span', { className: `ds-badge ds-badge--${tone}` }, text);
}
