import { createStore } from '../../ui-runtime/index.js';
import { EVENTS } from '../../contracts/index.js';
import { CatalogPage } from '../pages/CatalogPage.js';

/**
 * Container. It owns the state and the side effects (loading, searching, telling the rest of the app about a purchase)
 * and produces no markup of its own: `view()` only hands state and handlers to the page.
 */
export function createCatalogContainer({ productApi, bus }) {
  const store = createStore({ status: 'loading', products: [], term: '', error: '' });
  let latestLoad = 0;

  const actions = {
    async load() {
      const id = ++latestLoad;
      store.setState({ status: 'loading', error: '' });
      try {
        const products = await productApi.list();
        if (id === latestLoad) store.setState({ status: 'ready', products });
      } catch (error) {
        if (id === latestLoad) store.setState({ status: 'error', error: `Could not load the catalog: ${error.message}` });
      }
    },

    search(term) {
      store.setState({ term });
    },

    add(productId) {
      const product = store.getState().products.find((candidate) => candidate.id === productId);
      if (!product || product.stock <= 0) return;
      bus.publish(EVENTS.ADD_TO_CART, { productId: product.id, title: product.title, priceCents: product.priceCents });
    },
  };

  const view = () =>
    CatalogPage({ state: store.getState(), onSearch: actions.search, onAdd: actions.add, onRetry: actions.load });

  return { store, actions, view };
}
