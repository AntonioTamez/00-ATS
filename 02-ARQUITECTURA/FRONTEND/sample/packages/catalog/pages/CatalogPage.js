import { h } from '../../ui-runtime/index.js';
import { Button, Heading, SearchField } from '../../design-system/index.js';
import { filterProducts } from '../domain/products.js';
import { ProductGrid } from '../organisms/ProductGrid.js';
import { CatalogTemplate } from '../templates/CatalogTemplate.js';

/**
 * Page. The template filled with real content for one state of the screen.
 * It only reads `state` and calls the handlers it was given: it never fetches or stores anything.
 */
export function CatalogPage({ state, onSearch, onAdd, onRetry }) {
  return CatalogTemplate({
    header: Heading({ level: 2, text: 'Catalog' }),
    toolbar: SearchField({ term: state.term, onSearch }),
    content: body(state, onAdd, onRetry),
  });
}

function body(state, onAdd, onRetry) {
  if (state.status === 'loading') {
    return h('p', { className: 'catalog-status', 'data-testid': 'loading' }, 'Loading…');
  }
  if (state.status === 'error') {
    return h(
      'div',
      { className: 'catalog-status catalog-status--error', role: 'alert', 'data-testid': 'error' },
      h('p', null, state.error),
      Button({ label: 'Retry', onClick: onRetry, testId: 'retry' }),
    );
  }
  return ProductGrid({ products: filterProducts(state.products, state.term), onAdd });
}
