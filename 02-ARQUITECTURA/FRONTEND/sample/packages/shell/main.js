import { EVENTS } from '../contracts/index.js';
import { VERSION as designSystemVersion } from '../design-system/index.js';
import { createEventBus } from './event-bus.js';
import { createShell } from './loader.js';
import { manifest } from './manifest.js';

// Browser entry point (loaded by index.html). It wires the page and starts the shell.
const bus = createEventBus({ onError: (error, type) => console.error(`a handler of '${type}' failed`, error) });

const cartCount = document.querySelector('#cart-count');
bus.subscribe(EVENTS.CART_CHANGED, ({ itemCount }) => {
  cartCount.textContent = String(itemCount);
});

const shell = createShell({ manifest, designSystemVersion, bus });
const report = await shell.start({
  main: document.querySelector('#main'),
  aside: document.querySelector('#aside'),
});
console.info('micro-frontends', report);
