import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { renderStatic, renderToString } from '../packages/ui-runtime/index.js';
import { EVENTS } from '../packages/contracts/index.js';
import { addItem, emptyCart, quantityOf, removeItem, setQuantity, totals } from '../packages/cart/domain/cart.js';
import { CartTable } from '../packages/cart/organisms/CartTable.js';
import { CartSummary } from '../packages/cart/organisms/CartSummary.js';
import { CartPage } from '../packages/cart/pages/CartPage.js';
import { createCartContainer } from '../packages/cart/containers/CartContainer.js';
import { createEventBus } from '../packages/shell/event-bus.js';
import { deepFreeze, newRoot } from './support/fake-dom.js';

const book = { productId: 'p1', title: 'Clean Architecture', priceCents: 3499 };
const other = { productId: 'p3', title: 'Atomic Design', priceCents: 2999 };

describe('cart domain', () => {
  it('adds a new line, or one more unit of an existing one', () => {
    const once = addItem(emptyCart, book);
    const twice = addItem(once, book);
    const mixed = addItem(twice, other);

    assert.deepEqual(once.lines, [{ ...book, quantity: 1 }]);
    assert.equal(quantityOf(twice, 'p1'), 2);
    assert.deepEqual(mixed.lines.map((l) => l.productId), ['p1', 'p3']);
  });

  it('never changes the cart it receives', () => {
    const cart = deepFreeze(addItem(emptyCart, book));

    assert.doesNotThrow(() => {
      addItem(cart, book);
      addItem(cart, other);
      setQuantity(cart, 'p1', 5);
      removeItem(cart, 'p1');
    });
    assert.equal(quantityOf(cart, 'p1'), 1);
  });

  it('sets a quantity, and a quantity of zero or less removes the line', () => {
    const cart = addItem(addItem(emptyCart, book), other);

    assert.equal(quantityOf(setQuantity(cart, 'p1', 4), 'p1'), 4);
    assert.deepEqual(setQuantity(cart, 'p1', 0).lines.map((l) => l.productId), ['p3']);
    assert.deepEqual(setQuantity(cart, 'p1', -2).lines.map((l) => l.productId), ['p3']);
  });

  it('removes a line, and removing a missing one changes nothing', () => {
    const cart = addItem(emptyCart, book);

    assert.deepEqual(removeItem(cart, 'p1').lines, []);
    assert.deepEqual(removeItem(cart, 'zzz').lines, cart.lines);
  });

  it('counts items and adds up the total in cents', () => {
    const cart = setQuantity(addItem(addItem(emptyCart, book), other), 'p1', 2);

    assert.deepEqual(totals(cart), { itemCount: 3, totalCents: 2 * 3499 + 2999 });
    assert.deepEqual(totals(emptyCart), { itemCount: 0, totalCents: 0 });
  });

  it('reports 0 for a product that is not in the cart', () => {
    assert.equal(quantityOf(emptyCart, 'p1'), 0);
  });
});

describe('cart presentation', () => {
  const lines = setQuantity(addItem(addItem(emptyCart, book), other), 'p1', 2).lines;

  it('CartTable shows each line with its quantity and its line total', () => {
    const root = newRoot();
    renderStatic(root, CartTable({ lines, onIncrement() {}, onDecrement() {}, onRemove() {} }));

    assert.match(root.byTestId('line-p1').textContent, /Clean Architecture.*2.*\$69\.98/);
    assert.match(root.byTestId('line-p3').textContent, /Atomic Design.*1.*\$29\.99/);
  });

  it('CartTable reports which product was changed or removed', () => {
    const heard = [];
    const root = newRoot();
    renderStatic(
      root,
      CartTable({
        lines,
        onIncrement: (id) => heard.push(['inc', id]),
        onDecrement: (id) => heard.push(['dec', id]),
        onRemove: (id) => heard.push(['rm', id]),
      }),
    );

    root.byTestId('qty-p1-inc').click();
    root.byTestId('qty-p1-dec').click();
    root.byTestId('remove-p3').click();
    root.byTestId('qty-p3-dec').click(); // quantity 1: disabled

    assert.deepEqual(heard, [['inc', 'p1'], ['dec', 'p1'], ['rm', 'p3']]);
  });

  it('CartSummary pluralizes and formats the total', () => {
    assert.match(renderToString(CartSummary({ itemCount: 1, totalCents: 3499 })), /1 item · <span class="ds-price">\$34\.99/);
    assert.match(renderToString(CartSummary({ itemCount: 3, totalCents: 0 })), /3 items · /);
  });

  it('CartPage shows an empty message, or the lines and the summary', () => {
    const handlers = { onIncrement() {}, onDecrement() {}, onRemove() {} };
    const empty = renderToString(CartPage({ cart: emptyCart, ...handlers }));
    const full = renderToString(CartPage({ cart: { lines }, ...handlers }));

    assert.match(empty, /Your cart is empty\./);
    assert.doesNotMatch(empty, /cart-summary/);
    assert.match(full, /cart-summary/);
    assert.doesNotMatch(full, /Your cart is empty/);
  });
});

describe('cart container', () => {
  function build() {
    const bus = createEventBus();
    const changes = [];
    bus.subscribe(EVENTS.CART_CHANGED, (detail) => changes.push(detail));
    return { bus, changes, ...createCartContainer({ bus }) };
  }

  it('adds what other micro-frontends announce, and announces the new totals', () => {
    const { bus, store, changes } = build();

    bus.publish(EVENTS.ADD_TO_CART, book);
    bus.publish(EVENTS.ADD_TO_CART, book);

    assert.equal(quantityOf(store.getState().cart, 'p1'), 2);
    assert.deepEqual(changes, [
      { itemCount: 1, totalCents: 3499 },
      { itemCount: 2, totalCents: 6998 },
    ]);
  });

  it('increments, decrements and removes, announcing each change', () => {
    const { bus, store, actions, changes } = build();
    bus.publish(EVENTS.ADD_TO_CART, book);

    actions.increment('p1');
    actions.decrement('p1');
    actions.decrement('p1'); // from 1 to 0: the line goes away
    assert.deepEqual(store.getState().cart.lines, []);

    bus.publish(EVENTS.ADD_TO_CART, other);
    actions.remove('p3');

    assert.deepEqual(changes.at(-1), { itemCount: 0, totalCents: 0 });
    assert.equal(changes.length, 6);
  });

  it('stops listening to the bus once disposed', () => {
    const { bus, store, dispose } = build();

    dispose();
    bus.publish(EVENTS.ADD_TO_CART, book);

    assert.deepEqual(store.getState().cart.lines, []);
  });

  it('rejects a malformed announcement at the bus, so the cart never sees it', () => {
    const { bus, store } = build();

    assert.throws(() => bus.publish(EVENTS.ADD_TO_CART, { productId: 'p1' }), /'title' must be a string/);
    assert.deepEqual(store.getState().cart.lines, []);
  });

  it('view() is only the page', () => {
    const { bus, view } = build();
    bus.publish(EVENTS.ADD_TO_CART, book);

    assert.match(renderToString(view()), /Clean Architecture/);
  });
});
