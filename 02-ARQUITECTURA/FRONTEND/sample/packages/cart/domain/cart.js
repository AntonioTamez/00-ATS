/** Pure cart rules. Every function returns a NEW cart and leaves the one it received untouched. */

export const emptyCart = Object.freeze({ lines: Object.freeze([]) });

export function quantityOf(cart, productId) {
  return cart.lines.find((line) => line.productId === productId)?.quantity ?? 0;
}

export function addItem(cart, item) {
  const existing = cart.lines.find((line) => line.productId === item.productId);
  const lines = existing
    ? cart.lines.map((line) => (line === existing ? { ...line, quantity: line.quantity + 1 } : line))
    : [...cart.lines, { productId: item.productId, title: item.title, priceCents: item.priceCents, quantity: 1 }];
  return { lines };
}

export function setQuantity(cart, productId, quantity) {
  if (quantity <= 0) return removeItem(cart, productId);
  return { lines: cart.lines.map((line) => (line.productId === productId ? { ...line, quantity } : line)) };
}

export function removeItem(cart, productId) {
  return { lines: cart.lines.filter((line) => line.productId !== productId) };
}

export function totals(cart) {
  return {
    itemCount: cart.lines.reduce((sum, line) => sum + line.quantity, 0),
    totalCents: cart.lines.reduce((sum, line) => sum + line.quantity * line.priceCents, 0),
  };
}
