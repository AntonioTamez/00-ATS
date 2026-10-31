import { h } from '../../ui-runtime/index.js';
import { Button } from '../atoms/Button.js';

/** Molecule. Two buttons around a number. The minus is disabled at `min`; deleting is someone else's decision. */
export function QuantityStepper({ value, min = 1, onDecrement, onIncrement, testId = 'stepper' }) {
  return h(
    'div',
    { className: 'ds-stepper', 'data-testid': testId },
    Button({ label: '−', variant: 'secondary', disabled: value <= min, onClick: onDecrement, testId: `${testId}-dec` }),
    h('span', { className: 'ds-stepper__value', 'data-testid': `${testId}-value` }, value),
    Button({ label: '+', variant: 'secondary', onClick: onIncrement, testId: `${testId}-inc` }),
  );
}
