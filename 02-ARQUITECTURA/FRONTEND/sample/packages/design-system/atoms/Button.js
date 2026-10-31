import { h } from '../../ui-runtime/index.js';

/** Atom. A button that knows nothing about what it does: the caller decides via `onClick`. */
export function Button({ label, onClick, variant = 'primary', type = 'button', disabled = false, testId }) {
  return h(
    'button',
    { type, className: `ds-button ds-button--${variant}`, disabled, onClick, 'data-testid': testId },
    label,
  );
}
