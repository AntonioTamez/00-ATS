import { h } from '../../ui-runtime/index.js';
import { formatMoney } from '../foundations/money.js';

/** Atom. An amount in cents, shown as money. */
export function Price({ cents, currency = 'USD' }) {
  return h('span', { className: 'ds-price' }, formatMoney(cents, currency));
}
