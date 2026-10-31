import { h } from '../../ui-runtime/index.js';
import { Heading } from '../../design-system/index.js';
import { totals } from '../domain/cart.js';
import { CartSummary } from '../organisms/CartSummary.js';
import { CartTable } from '../organisms/CartTable.js';
import { CartTemplate } from '../templates/CartTemplate.js';

/** Page. The cart template filled for the current cart: empty, or with lines and a summary. */
export function CartPage({ cart, onIncrement, onDecrement, onRemove }) {
  const empty = cart.lines.length === 0;
  return CartTemplate({
    header: Heading({ level: 2, text: 'Cart' }),
    content: empty
      ? h('p', { className: 'cart-empty', 'data-testid': 'cart-empty' }, 'Your cart is empty.')
      : CartTable({ lines: cart.lines, onIncrement, onDecrement, onRemove }),
    footer: empty ? null : CartSummary(totals(cart)),
  });
}
