import { h } from '../../ui-runtime/index.js';
import { ProductCard } from './ProductCard.js';

/** Organism. A grid of product cards, or a message when there is nothing to show. */
export function ProductGrid({ products, onAdd, emptyText = 'No products match your search.' }) {
  if (products.length === 0) return h('p', { className: 'catalog-empty', 'data-testid': 'empty' }, emptyText);
  return h('div', { className: 'catalog-grid' }, products.map((product) => ProductCard({ product, onAdd })));
}
