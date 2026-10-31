import { h } from '../ui-runtime/index.js';

/** What the user sees in a slot whose micro-frontend could not load: a message, not a broken page. */
export function FailedRemote({ name, reason }) {
  return h(
    'div',
    { className: 'shell-failed', role: 'alert', 'data-testid': `failed-${name}` },
    h('strong', null, `${name} is unavailable`),
    h('p', null, reason),
  );
}
