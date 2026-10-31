const VOID_ELEMENTS = new Set(['input', 'br', 'hr', 'img']);
const ATTRIBUTE_ALIASES = { className: 'class', htmlFor: 'for', defaultValue: 'value' };
const ATTRIBUTE_NAME = /^[a-zA-Z_:][-a-zA-Z0-9_:.]*$/;
const ESCAPES = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' };

export function escapeHtml(text) {
  return String(text).replace(/[&<>"']/g, (c) => ESCAPES[c]);
}

/** True for props the DOM adapter turns into event listeners (onClick, onSubmit...). */
export function isEventProp(name) {
  return /^on[A-Z]/.test(name);
}

/** The attributes of an element, ready to print. Event handlers and `children` are not attributes. */
export function attributeEntries(props) {
  const entries = [];
  for (const [name, value] of Object.entries(props)) {
    if (name === 'children' || isEventProp(name) || typeof value === 'function') continue;
    if (value === false || value === null || value === undefined) continue;
    const attribute = ATTRIBUTE_ALIASES[name] ?? name;
    if (!ATTRIBUTE_NAME.test(attribute)) throw new Error(`invalid attribute name '${attribute}'`);
    entries.push([attribute, value === true ? '' : String(value)]);
  }
  return entries;
}

/** Renders a tree of elements to an HTML string. Text is always escaped. */
export function renderToString(node) {
  if (node === null || node === undefined || node === false || node === true) return '';
  if (Array.isArray(node)) return node.map(renderToString).join('');
  if (typeof node === 'string' || typeof node === 'number') return escapeHtml(node);

  const attributes = attributeEntries(node.props)
    .map(([name, value]) => (value === '' ? ` ${name}` : ` ${name}="${escapeHtml(value)}"`))
    .join('');
  const open = `<${node.type}${attributes}>`;
  if (VOID_ELEMENTS.has(node.type)) return open;
  return `${open}${node.children.map(renderToString).join('')}</${node.type}>`;
}
