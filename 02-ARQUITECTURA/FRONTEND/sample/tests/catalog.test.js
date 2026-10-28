import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { renderStatic, renderToString } from '../packages/ui-runtime/index.js';
import { EVENTS } from '../packages/contracts/index.js';
import { createStaticProductApi, staticProducts } from '../packages/catalog/api/product-api.js';
import { filterProducts, stockStatus } from '../packages/catalog/domain/products.js';
import { ProductCard } from '../packages/catalog/organisms/ProductCard.js';
import { ProductGrid } from '../packages/catalog/organisms/ProductGrid.js';
import { CatalogPage } from '../packages/catalog/pages/CatalogPage.js';
import { createCatalogContainer } from '../packages/catalog/containers/CatalogContainer.js';
import { createEventBus } from '../packages/shell/event-bus.js';
import { deepFreeze, newRoot } from './support/fake-dom.js';

const [cleanArch, ddd] = staticProducts;

describe('catalog domain', () => {
  it('filters by title, ignoring case and surrounding spaces', () => {
    assert.deepEqual(filterProducts(staticProducts, '  DESIGN ').map((p) => p.id), ['p2', 'p3']);
    assert.deepEqual(filterProducts(staticProducts, 'zzz'), []);
  });

  it('shows everything for an empty term', () => {
    assert.equal(filterProducts(staticProducts, '   ').length, staticProducts.length);
  });

  it('announces stock: none, low (3 or fewer) and plenty', () => {
    assert.deepEqual(stockStatus({ stock: 0 }), { label: 'Out of stock', tone: 'danger', canBuy: false });
    assert.deepEqual(stockStatus({ stock: 3 }), { label: 'Low stock', tone: 'warning', canBuy: true });
    assert.deepEqual(stockStatus({ stock: 4 }), { label: 'In stock', tone: 'success', canBuy: true });
  });
});

describe('catalog presentation (organisms, page)', () => {
  it('ProductCard shows title, stock badge and price, and adds by id', () => {
    const added = [];
    const root = newRoot();
    renderStatic(root, ProductCard({ product: cleanArch, onAdd: (id) => added.push(id) }));

    root.byTestId('add-p1').click();

    assert.match(root.textContent, /Clean Architecture/);
    assert.match(root.textContent, /In stock/);
    assert.match(root.textContent, /\$34\.99/);
    assert.deepEqual(added, ['p1']);
  });

  it('ProductCard cannot add a product that is out of stock', () => {
    const added = [];
    const root = newRoot();
    renderStatic(root, ProductCard({ product: ddd, onAdd: (id) => added.push(id) }));

    const clicked = root.byTestId('add-p2').click();

    assert.equal(clicked, false);
    assert.deepEqual(added, []);
    assert.match(root.textContent, /Out of stock/);
  });

  it('ProductGrid shows one card per product, or an empty message', () => {
    assert.equal((renderToString(ProductGrid({ products: staticProducts, onAdd() {} })).match(/<article/g) ?? []).length, 4);
    assert.match(renderToString(ProductGrid({ products: [], onAdd() {} })), /No products match your search\./);
  });

  const handlers = { onSearch() {}, onAdd() {}, onRetry() {} };

  it('CatalogPage shows a loading message while loading', () => {
    const html = renderToString(CatalogPage({ state: { status: 'loading', products: [], term: '', error: '' }, ...handlers }));

    assert.match(html, /data-testid="loading"/);
    assert.doesNotMatch(html, /<article/);
  });

  it('CatalogPage shows the error and a retry button', () => {
    const root = newRoot();
    const retried = [];
    renderStatic(root, CatalogPage({ state: { status: 'error', products: [], term: '', error: 'boom' }, ...handlers, onRetry: () => retried.push(1) }));

    root.byTestId('retry').click();

    assert.match(root.byTestId('error').textContent, /boom/);
    assert.deepEqual(retried, [1]);
  });

  it('CatalogPage filters the products by the term in its state', () => {
    const html = renderToString(CatalogPage({ state: { status: 'ready', products: staticProducts, term: 'atomic', error: '' }, ...handlers }));

    assert.equal((html.match(/<article/g) ?? []).length, 1);
    assert.match(html, /Atomic Design/);
  });

  it('presentational components do not modify their props', () => {
    const state = deepFreeze({ status: 'ready', products: structuredClone(staticProducts), term: 'a', error: '' });

    assert.doesNotThrow(() => CatalogPage({ state, ...handlers }));
  });
});

describe('catalog container', () => {
  const productsApi = (list) => ({ list });

  function build(api, bus = createEventBus()) {
    return { bus, ...createCatalogContainer({ productApi: api, bus }) };
  }

  it('starts loading, then shows the products', async () => {
    const container = build(createStaticProductApi());
    assert.equal(container.store.getState().status, 'loading');

    await container.actions.load();

    assert.equal(container.store.getState().status, 'ready');
    assert.equal(container.store.getState().products.length, 4);
  });

  it('turns a failing API into an error state, and a retry can recover', async () => {
    let fail = true;
    const container = build(productsApi(async () => {
      if (fail) throw new Error('offline');
      return structuredClone(staticProducts);
    }));

    await container.actions.load();
    assert.deepEqual(
      [container.store.getState().status, container.store.getState().error],
      ['error', 'Could not load the catalog: offline'],
    );

    fail = false;
    await container.actions.load();
    assert.equal(container.store.getState().status, 'ready');
    assert.equal(container.store.getState().error, '');
  });

  it('keeps the answer of the LATEST load when an older one finishes after it', async () => {
    const resolvers = [];
    const container = build(productsApi(() => new Promise((resolve) => resolvers.push(resolve))));

    const first = container.actions.load();
    const second = container.actions.load();
    resolvers[1]([{ id: 'new', title: 'New', priceCents: 1, stock: 1 }]);
    await second;
    resolvers[0]([{ id: 'old', title: 'Old', priceCents: 1, stock: 1 }]);
    await first;

    assert.deepEqual(container.store.getState().products.map((p) => p.id), ['new']);
  });

  it('keeps the search term in its state', async () => {
    const container = build(createStaticProductApi());
    await container.actions.load();

    container.actions.search('micro');

    assert.equal(container.store.getState().term, 'micro');
    assert.match(renderToString(container.view()), /Building Microservices/);
    assert.doesNotMatch(renderToString(container.view()), /Atomic Design/);
  });

  it('announces a purchase on the bus with exactly the contract payload', async () => {
    const container = build(createStaticProductApi());
    const heard = [];
    container.bus.subscribe(EVENTS.ADD_TO_CART, (detail) => heard.push(detail));
    await container.actions.load();

    container.actions.add('p3');

    assert.deepEqual(heard, [{ productId: 'p3', title: 'Atomic Design', priceCents: 2999 }]);
  });

  it('does not announce an out-of-stock or unknown product', async () => {
    const container = build(createStaticProductApi());
    const heard = [];
    container.bus.subscribe(EVENTS.ADD_TO_CART, (detail) => heard.push(detail));
    await container.actions.load();

    container.actions.add('p2');
    container.actions.add('nope');

    assert.deepEqual(heard, []);
  });

  it('view() is only the page: the container writes no markup of its own', async () => {
    const container = build(createStaticProductApi());
    await container.actions.load();

    assert.equal(renderToString(container.view()), renderToString(CatalogPage({
      state: container.store.getState(),
      onSearch() {},
      onAdd() {},
      onRetry() {},
    })));
  });
});
