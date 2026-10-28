import { attributeEntries, isEventProp } from './render-string.js';

const EVENT_TYPES = { onClick: 'click', onInput: 'input', onChange: 'change', onSubmit: 'submit' };

/** Builds real DOM nodes from elements. Handlers receive a small normalized event, never the browser's. */
export function toDom(node, doc) {
  if (node === null || node === undefined || node === false || node === true) return [];
  if (Array.isArray(node)) return node.flatMap((child) => toDom(child, doc));
  if (typeof node === 'string' || typeof node === 'number') return [doc.createTextNode(String(node))];

  const element = doc.createElement(node.type);
  for (const [name, value] of attributeEntries(node.props)) element.setAttribute(name, value);
  for (const [name, handler] of Object.entries(node.props)) {
    if (!isEventProp(name) || typeof handler !== 'function') continue;
    const type = EVENT_TYPES[name];
    if (!type) throw new Error(`unsupported event prop '${name}'`);
    element.addEventListener(type, (event) => handler(normalize(name, event, element)));
  }
  element.append(...node.children.flatMap((child) => toDom(child, doc)));
  return [element];
}

function normalize(name, event, element) {
  if (name === 'onSubmit') {
    event.preventDefault?.();
    return { formData: event.formData ?? Object.fromEntries(new FormData(element)) };
  }
  if (name === 'onInput' || name === 'onChange') return { value: event.target?.value };
  return {};
}

/**
 * Paints `view()` into `root` now, and again every time `subscribe`'s listener fires.
 * It replaces the whole content on every change (no diffing): simple, and enough to teach the architecture.
 * Returns the function that stops updating and empties the root.
 */
export function mount(root, view, subscribe) {
  const doc = root.ownerDocument;
  const paint = () => root.replaceChildren(...toDom(view(), doc));
  paint();
  const stop = subscribe(paint);
  return () => {
    stop();
    root.replaceChildren();
  };
}

/** Paints a fixed element tree once. */
export function renderStatic(root, node) {
  root.replaceChildren(...toDom(node, root.ownerDocument));
}
