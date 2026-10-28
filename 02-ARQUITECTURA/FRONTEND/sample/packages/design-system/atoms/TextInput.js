import { h } from '../../ui-runtime/index.js';

/** Atom. An uncontrolled text field: it starts with `value` and the browser keeps what the user types. */
export function TextInput({ name, value = '', placeholder = '', label }) {
  return h('input', {
    type: 'text',
    className: 'ds-input',
    name,
    defaultValue: value,
    placeholder,
    'aria-label': label,
  });
}
