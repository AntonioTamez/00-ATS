/**
 * A tiny element factory. `h('button', { onClick }, 'Save')` returns a plain object describing the element.
 * Components are plain functions from props to elements: calling `Button({ label })` or `h(Button, { label })` is the same.
 */

/** Groups children without adding an element. */
export function Fragment({ children }) {
  return children;
}

export function h(type, props, ...children) {
  const kids = flatten(children);
  if (typeof type === 'function') return type({ ...(props ?? {}), children: kids });
  return { type, props: { ...(props ?? {}) }, children: kids };
}

function flatten(list) {
  const out = [];
  for (const child of list) {
    if (Array.isArray(child)) out.push(...flatten(child));
    else if (child !== null && child !== undefined && child !== false && child !== true) out.push(child);
  }
  return out;
}
