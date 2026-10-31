import { createStore } from '../../ui-runtime/index.js';
import { EVENTS } from '../../contracts/index.js';
import { addItem, emptyCart, quantityOf, removeItem, setQuantity, totals } from '../domain/cart.js';
import { CartPage } from '../pages/CartPage.js';

/**
 * Container. It listens for "add to cart" announcements from other micro-frontends, keeps the cart,
 * and announces the new totals. It has no markup: `view()` hands the cart and the handlers to the page.
 */
export function createCartContainer({ bus }) {
  const store = createStore({ cart: emptyCart });

  const update = (change) => {
    store.setState({ cart: change(store.getState().cart) });
    bus.publish(EVENTS.CART_CHANGED, totals(store.getState().cart));
  };

  const stopListening = bus.subscribe(EVENTS.ADD_TO_CART, (item) => update((cart) => addItem(cart, item)));

  const actions = {
    increment: (productId) => update((cart) => setQuantity(cart, productId, quantityOf(cart, productId) + 1)),
    decrement: (productId) => update((cart) => setQuantity(cart, productId, quantityOf(cart, productId) - 1)),
    remove: (productId) => update((cart) => removeItem(cart, productId)),
  };

  const view = () =>
    CartPage({
      cart: store.getState().cart,
      onIncrement: actions.increment,
      onDecrement: actions.decrement,
      onRemove: actions.remove,
    });

  return { store, actions, view, dispose: stopListening };
}
