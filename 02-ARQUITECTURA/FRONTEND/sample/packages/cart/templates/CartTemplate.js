import { h } from '../../ui-runtime/index.js';

/** Template. A header, the lines and a footer, with no content of its own. */
export function CartTemplate({ header, content, footer }) {
  return h(
    'section',
    { className: 'cart-layout' },
    h('header', { className: 'cart-layout__header' }, header),
    h('div', { className: 'cart-layout__content' }, content),
    h('footer', { className: 'cart-layout__footer' }, footer),
  );
}
