/**
 * A minimal observable state holder. State is replaced, never mutated: every `setState` builds a new frozen object,
 * and listeners are told only when something actually changed.
 */
export function createStore(initial) {
  let state = Object.freeze({ ...initial });
  const listeners = new Set();

  return {
    getState: () => state,

    setState(update) {
      const patch = typeof update === 'function' ? update(state) : update;
      const next = { ...state, ...patch };
      if (Object.keys(next).every((key) => Object.is(next[key], state[key]))) return;
      state = Object.freeze(next);
      for (const listener of [...listeners]) listener(state);
    },

    /** Returns the function that stops listening. */
    subscribe(listener) {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
  };
}
