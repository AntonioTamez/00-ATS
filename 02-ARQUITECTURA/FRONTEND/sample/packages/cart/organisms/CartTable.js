import { h } from '../../ui-runtime/index.js';
import { Button, Price, QuantityStepper } from '../../design-system/index.js';

/** Organism. The lines of the cart, each with its quantity stepper, line total and remove button. */
export function CartTable({ lines, onIncrement, onDecrement, onRemove }) {
  return h(
    'ul',
    { className: 'cart-lines' },
    lines.map((line) =>
      h(
        'li',
        { className: 'cart-line', 'data-testid': `line-${line.productId}` },
        h('span', { className: 'cart-line__title' }, line.title),
        QuantityStepper({
          value: line.quantity,
          testId: `qty-${line.productId}`,
          onDecrement: () => onDecrement(line.productId),
          onIncrement: () => onIncrement(line.productId),
        }),
        Price({ cents: line.quantity * line.priceCents }),
        Button({
          label: 'Remove',
          variant: 'secondary',
          onClick: () => onRemove(line.productId),
          testId: `remove-${line.productId}`,
        }),
      ),
    ),
  );
}
