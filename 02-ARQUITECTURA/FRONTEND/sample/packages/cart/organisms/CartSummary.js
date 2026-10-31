import { h } from '../../ui-runtime/index.js';
import { Price } from '../../design-system/index.js';

/** Organism. "2 items · $69.98". */
export function CartSummary({ itemCount, totalCents }) {
  return h(
    'p',
    { className: 'cart-summary', 'data-testid': 'summary' },
    `${itemCount} ${itemCount === 1 ? 'item' : 'items'} · `,
    Price({ cents: totalCents }),
  );
}
