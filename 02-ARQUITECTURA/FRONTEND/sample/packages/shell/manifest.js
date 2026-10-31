/**
 * Which micro-frontends exist, where each one is loaded from and where it is shown.
 * The shell never imports them in code: it reads this list and loads each `url` at run time.
 * `requires` declares the major version of the shared design system the micro-frontend was built against.
 */
export const manifest = Object.freeze([
  Object.freeze({ name: 'catalog', url: '../catalog/index.js', slot: 'main', requires: Object.freeze({ 'design-system': 1 }) }),
  Object.freeze({ name: 'cart', url: '../cart/index.js', slot: 'aside', requires: Object.freeze({ 'design-system': 1 }) }),
]);
