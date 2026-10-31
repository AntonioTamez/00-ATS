import { mount } from '../ui-runtime/index.js';
import { CONTRACT_VERSION } from '../contracts/index.js';
import { createCartContainer } from './containers/CartContainer.js';

/** The cart micro-frontend: what the shell loads. It talks to the rest of the app only through `bus`. */
export const mfe = Object.freeze({
  name: 'cart',
  version: '1.0.0',
  contract: CONTRACT_VERSION,

  mount(root, { bus }) {
    const container = createCartContainer({ bus });
    const unmount = mount(root, container.view, container.store.subscribe);
    return () => {
      container.dispose();
      unmount();
    };
  },
});
