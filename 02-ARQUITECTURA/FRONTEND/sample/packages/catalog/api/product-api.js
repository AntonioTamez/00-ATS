/**
 * Where products come from. The container receives an object with `list()`; this one answers from memory.
 * A real one would call `fetch`, and nothing else in the micro-frontend would change.
 */
export const staticProducts = Object.freeze([
  Object.freeze({ id: 'p1', title: 'Clean Architecture', priceCents: 3499, stock: 5 }),
  Object.freeze({ id: 'p2', title: 'Domain-Driven Design', priceCents: 5499, stock: 0 }),
  Object.freeze({ id: 'p3', title: 'Atomic Design', priceCents: 2999, stock: 12 }),
  Object.freeze({ id: 'p4', title: 'Building Microservices', priceCents: 4599, stock: 3 }),
]);

export function createStaticProductApi(products = staticProducts) {
  return {
    async list() {
      return products.map((product) => ({ ...product }));
    },
  };
}
