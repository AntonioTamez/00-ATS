import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { VERSION } from '../packages/design-system/index.js';
import { EVENTS } from '../packages/contracts/index.js';
import { createEventBus } from '../packages/shell/event-bus.js';
import { createShell } from '../packages/shell/loader.js';
import { manifest } from '../packages/shell/manifest.js';
import { createStaticProductApi, staticProducts } from '../packages/catalog/api/product-api.js';
import { newRoot, settle } from './support/fake-dom.js';

/** The real shell, loading the REAL micro-frontends from the manifest with the real dynamic import. */
async function startShop({ services } = {}) {
  const bus = createEventBus();
  const slots = { main: newRoot(), aside: newRoot() };
  const header = { count: '0' };
  bus.subscribe(EVENTS.CART_CHANGED, ({ itemCount }) => {
    header.count = String(itemCount);
  });
  const shell = createShell({ manifest, designSystemVersion: VERSION, bus, services });
  const report = await shell.start(slots);
  await settle();
  return { bus, slots, header, shell, report };
}

describe('the shop: shell + catalog + cart, loaded from the manifest', () => {
  it('loads both micro-frontends with the real dynamic import', async () => {
    const { report, slots } = await startShop();

    assert.deepEqual(report, { mounted: ['catalog', 'cart'], failed: [] });
    assert.match(slots.main.textContent, /Catalog/);
    assert.match(slots.main.textContent, /Clean Architecture/);
    assert.match(slots.aside.textContent, /Your cart is empty\./);
  });

  it('adding a book in the catalog shows it in the cart and in the header, with no direct link between them', async () => {
    const { slots, header } = await startShop();

    slots.main.byTestId('add-p1').click();
    slots.main.byTestId('add-p1').click();
    slots.main.byTestId('add-p3').click();

    assert.match(slots.aside.byTestId('line-p1').textContent, /Clean Architecture.*2.*\$69\.98/);
    assert.match(slots.aside.byTestId('line-p3').textContent, /Atomic Design/);
    assert.match(slots.aside.byTestId('summary').textContent, /3 items · \$99\.97/);
    assert.equal(header.count, '3');
  });

  it('a book that is out of stock cannot be added', async () => {
    const { slots, header } = await startShop();

    const clicked = slots.main.byTestId('add-p2').click();

    assert.equal(clicked, false);
    assert.equal(header.count, '0');
    assert.match(slots.aside.textContent, /Your cart is empty\./);
  });

  it('searching the catalog does not touch the cart', async () => {
    const { slots, header } = await startShop();
    slots.main.byTestId('add-p1').click();

    slots.main.byTestId('search').submit({ term: 'design' });

    assert.equal(slots.main.findAll((e) => e.tagName === 'article').length, 2);
    assert.equal(header.count, '1');
    assert.match(slots.aside.byTestId('line-p1').textContent, /Clean Architecture/);
  });

  it('the cart can step, shrink to nothing and remove, and the header follows', async () => {
    const { slots, header } = await startShop();
    slots.main.byTestId('add-p1').click();

    slots.aside.byTestId('qty-p1-inc').click();
    assert.equal(header.count, '2');
    slots.aside.byTestId('qty-p1-dec').click();
    assert.equal(header.count, '1');
    slots.aside.byTestId('remove-p1').click();

    assert.equal(header.count, '0');
    assert.match(slots.aside.textContent, /Your cart is empty\./);
  });

  it('a catalog that fails to load shows its error and a retry; the cart keeps working', async () => {
    let online = false;
    const productApi = {
      async list() {
        if (!online) throw new Error('offline');
        return createStaticProductApi().list();
      },
    };
    const { slots, bus, header } = await startShop({ services: { productApi } });

    assert.match(slots.main.byTestId('error').textContent, /Could not load the catalog: offline/);
    bus.publish(EVENTS.ADD_TO_CART, { productId: 'p1', title: 'Clean Architecture', priceCents: 3499 });
    assert.equal(header.count, '1');

    online = true;
    slots.main.byTestId('retry').click();
    await settle();

    assert.match(slots.main.textContent, /Clean Architecture/);
    assert.equal(slots.main.byTestId('error'), null);
  });

  it('stopping the shell empties the slots and nobody reacts to the bus afterwards', async () => {
    const { slots, shell, bus, header } = await startShop();
    slots.main.byTestId('add-p1').click();

    await shell.stop();
    bus.publish(EVENTS.ADD_TO_CART, { productId: 'p3', title: 'Atomic Design', priceCents: 2999 });

    assert.equal(slots.main.textContent, '');
    assert.equal(slots.aside.textContent, '');
    assert.equal(header.count, '1');
  });

  it('shows every product of the static API', async () => {
    const { slots } = await startShop();

    assert.equal(slots.main.findAll((e) => e.tagName === 'article').length, staticProducts.length);
  });
});
