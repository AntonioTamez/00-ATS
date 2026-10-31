import { h } from '../../ui-runtime/index.js';

/** Atom. A heading of level 1 to 6. */
export function Heading({ level = 2, text }) {
  const clamped = Math.min(6, Math.max(1, Math.trunc(level)));
  return h(`h${clamped}`, { className: 'ds-heading' }, text);
}
