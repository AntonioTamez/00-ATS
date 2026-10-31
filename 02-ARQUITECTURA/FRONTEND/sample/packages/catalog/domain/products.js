/** Pure catalog rules. No UI, no I/O. */

export function filterProducts(products, term) {
  const wanted = term.trim().toLowerCase();
  return wanted === '' ? products : products.filter((product) => product.title.toLowerCase().includes(wanted));
}

/** How a product's stock is announced, and whether it can be bought. */
export function stockStatus(product) {
  if (product.stock <= 0) return { label: 'Out of stock', tone: 'danger', canBuy: false };
  if (product.stock <= 3) return { label: 'Low stock', tone: 'warning', canBuy: true };
  return { label: 'In stock', tone: 'success', canBuy: true };
}
