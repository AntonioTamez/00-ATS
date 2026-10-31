import { mount } from '../ui-runtime/index.js';
import { CONTRACT_VERSION } from '../contracts/index.js';
import { createStaticProductApi } from './api/product-api.js';
import { createCatalogContainer } from './containers/CatalogContainer.js';

/** The catalog micro-frontend: what the shell loads. It talks to the rest of the app only through `bus`. */
export const mfe = Object.freeze({
  name: 'catalog',
  version: '1.0.0',
  contract: CONTRACT_VERSION,

  mount(root, { bus, services = {} }) {
    const container = createCatalogContainer({ productApi: services.productApi ?? createStaticProductApi(), bus });
    const unmount = mount(root, container.view, container.store.subscribe);
    container.actions.load();
    return unmount;
  },
});
