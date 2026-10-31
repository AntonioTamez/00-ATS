/**
 * What every micro-frontend and the shell agree on, and nothing else: the shape of a micro-frontend,
 * the names and payloads of the events they exchange, and how versions are compared.
 * It imports nothing and holds no behavior of any feature.
 */

export const CONTRACT_VERSION = 1;

export const EVENTS = Object.freeze({
  ADD_TO_CART: 'cart/add-item',
  CART_CHANGED: 'cart/changed',
});

const PAYLOADS = {
  [EVENTS.ADD_TO_CART]: { productId: 'string', title: 'string', priceCents: 'number' },
  [EVENTS.CART_CHANGED]: { itemCount: 'number', totalCents: 'number' },
};

/** Throws unless `detail` has exactly the fields the event promises. */
export function validateEvent(type, detail) {
  const shape = PAYLOADS[type];
  if (!shape) throw new Error(`unknown event '${type}'`);
  for (const [field, expected] of Object.entries(shape)) {
    if (typeof detail?.[field] !== expected) throw new Error(`event '${type}': '${field}' must be a ${expected}`);
  }
}

export function majorOf(version) {
  const match = /^(\d+)\.\d+\.\d+$/.exec(version);
  if (!match) throw new Error(`invalid version '${version}'`);
  return Number(match[1]);
}

/** Throws a message listing everything wrong with a module's `mfe` export. */
export function validateMicroFrontend(mfe, entry) {
  if (!mfe || typeof mfe !== 'object') throw new Error("the module does not export 'mfe'");
  const problems = [];
  if (mfe.name !== entry.name) problems.push(`its name '${mfe.name}' does not match the manifest entry '${entry.name}'`);
  if (mfe.contract !== CONTRACT_VERSION) problems.push(`it speaks contract ${mfe.contract}, the shell speaks ${CONTRACT_VERSION}`);
  if (typeof mfe.mount !== 'function') problems.push('mount is not a function');
  if (problems.length) throw new Error(problems.join('; '));
}
