import { h } from '../../ui-runtime/index.js';

/** Template. Where things go, with no content of its own: a header, a toolbar and the main area. */
export function CatalogTemplate({ header, toolbar, content }) {
  return h(
    'section',
    { className: 'catalog-layout' },
    h('header', { className: 'catalog-layout__header' }, header),
    h('div', { className: 'catalog-layout__toolbar' }, toolbar),
    h('div', { className: 'catalog-layout__content' }, content),
  );
}
