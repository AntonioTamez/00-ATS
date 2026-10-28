// A just-enough DOM for the tests: elements, text, attributes, listeners and a way to fire events.
// It lets the real micro-frontends mount and react to clicks without a browser.

export class FakeText {
  constructor(text) {
    this.text = text;
  }

  get textContent() {
    return this.text;
  }
}

export class FakeElement {
  constructor(tagName, ownerDocument) {
    this.tagName = tagName;
    this.ownerDocument = ownerDocument;
    this.attributes = new Map();
    this.children = [];
    this.listeners = new Map();
  }

  setAttribute(name, value) {
    this.attributes.set(name, String(value));
  }

  getAttribute(name) {
    return this.attributes.get(name) ?? null;
  }

  hasAttribute(name) {
    return this.attributes.has(name);
  }

  addEventListener(type, listener) {
    this.listeners.set(type, [...(this.listeners.get(type) ?? []), listener]);
  }

  append(...nodes) {
    this.children.push(...nodes);
  }

  replaceChildren(...nodes) {
    this.children = [...nodes];
  }

  get textContent() {
    return this.children.map((child) => child.textContent).join('');
  }

  /** Every element below this one (depth first) for which `predicate` is true. */
  findAll(predicate) {
    const found = [];
    for (const child of this.children) {
      if (!(child instanceof FakeElement)) continue;
      if (predicate(child)) found.push(child);
      found.push(...child.findAll(predicate));
    }
    return found;
  }

  byTestId(id) {
    return this.findAll((element) => element.getAttribute('data-testid') === id)[0] ?? null;
  }

  /** Fires an event like a browser would: a disabled element ignores it. Returns whether any listener ran. */
  dispatch(type, event = {}) {
    if (this.hasAttribute('disabled')) return false;
    const listeners = this.listeners.get(type) ?? [];
    for (const listener of listeners) listener({ preventDefault() {}, target: this, ...event });
    return listeners.length > 0;
  }

  click() {
    return this.dispatch('click');
  }

  submit(formData) {
    return this.dispatch('submit', { formData });
  }
}

export class FakeDocument {
  createElement(tagName) {
    return new FakeElement(tagName, this);
  }

  createTextNode(text) {
    return new FakeText(text);
  }
}

/** A new empty element to mount into. */
export function newRoot() {
  return new FakeDocument().createElement('div');
}

/** Lets pending promises and zero-delay timers run. */
export async function settle() {
  for (let i = 0; i < 5; i += 1) await new Promise((resolve) => setTimeout(resolve, 0));
}

/** Freezes a value and everything inside it, so a component that mutates its props throws. */
export function deepFreeze(value) {
  if (value && typeof value === 'object' && !Object.isFrozen(value)) {
    Object.freeze(value);
    for (const inner of Object.values(value)) deepFreeze(inner);
  }
  return value;
}
