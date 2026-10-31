import { h } from '../../ui-runtime/index.js';
import { Heading } from '../atoms/Heading.js';

/** Molecule. A titled box with a body and an optional footer. It has no idea what goes inside. */
export function Card({ title, children = [], footer = null, testId }) {
  return h(
    'article',
    { className: 'ds-card', 'data-testid': testId },
    Heading({ level: 3, text: title }),
    h('div', { className: 'ds-card__body' }, children),
    footer ? h('footer', { className: 'ds-card__footer' }, footer) : null,
  );
}
