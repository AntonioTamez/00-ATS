import { validateEvent } from '../contracts/index.js';

/**
 * How micro-frontends talk without knowing each other: browser-style events. Payloads are checked against the
 * contract when published, and a handler that throws is reported to `onError` and never stops the others.
 * Handlers are synchronous: a rejected promise inside a handler is not seen here.
 */
export function createEventBus({ onError = () => {} } = {}) {
  const target = new EventTarget();

  return {
    publish(type, detail) {
      validateEvent(type, detail);
      target.dispatchEvent(new CustomEvent(type, { detail }));
    },

    /** Returns the function that stops listening. */
    subscribe(type, handler) {
      const listener = (event) => {
        try {
          handler(event.detail);
        } catch (error) {
          onError(error, type);
        }
      };
      target.addEventListener(type, listener);
      return () => target.removeEventListener(type, listener);
    },
  };
}
