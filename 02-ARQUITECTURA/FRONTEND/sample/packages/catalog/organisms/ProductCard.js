import { h } from '../../ui-runtime/index.js';
import { Badge, Button, Card, Price } from '../../design-system/index.js';
import { stockStatus } from '../domain/products.js';

/** Organism. One product: its title, price, stock badge and the button to buy it. */
export function ProductCard({ product, onAdd }) {
  const status = stockStatus(product);
  return Card({
    title: product.title,
    testId: `product-${product.id}`,
    children: h(
      'div',
      { className: 'catalog-product' },
      Badge({ text: status.label, tone: status.tone }),
      Price({ cents: product.priceCents }),
    ),
    footer: Button({
      label: 'Add to cart',
      disabled: !status.canBuy,
      onClick: () => onAdd(product.id),
      testId: `add-${product.id}`,
    }),
  });
}
