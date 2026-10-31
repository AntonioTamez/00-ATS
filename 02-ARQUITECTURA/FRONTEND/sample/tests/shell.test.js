import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { CONTRACT_VERSION, EVENTS, majorOf, validateEvent, validateMicroFrontend } from '../packages/contracts/index.js';
import { createEventBus } from '../packages/shell/event-bus.js';
import { createShell } from '../packages/shell/loader.js';
import { newRoot } from './support/fake-dom.js';

describe('contracts', () => {
  it('validates event payloads field by field', () => {
    assert.doesNotThrow(() => validateEvent(EVENTS.CART_CHANGED, { itemCount: 1, totalCents: 5 }));
    assert.throws(() => validateEvent(EVENTS.CART_CHANGED, { itemCount: '1', totalCents: 5 }), /'itemCount' must be a number/);
    assert.throws(() => validateEvent(EVENTS.CART_CHANGED, undefined), /'itemCount' must be a number/);
    assert.throws(() => validateEvent('nope', {}), /unknown event 'nope'/);
  });

  it('reads the major version and rejects anything that is not semver', () => {
    assert.equal(majorOf('1.2.0'), 1);
    assert.equal(majorOf('12.0.3'), 12);
    assert.throws(() => majorOf('1.2'), /invalid version/);
    assert.throws(() => majorOf('v1.2.0'), /invalid version/);
  });

  it('lists every problem of a micro-frontend that does not honor the contract', () => {
    const entry = { name: 'catalog' };

    assert.doesNotThrow(() => validateMicroFrontend({ name: 'catalog', contract: CONTRACT_VERSION, mount() {} }, entry));
    assert.throws(() => validateMicroFrontend(undefined, entry), /does not export 'mfe'/);
    assert.throws(
      () => validateMicroFrontend({ name: 'other', contract: 99 }, entry),
      /its name 'other' does not match.*speaks contract 99.*mount is not a function/,
    );
  });
});

describe('event bus', () => {
  it('delivers an event to every subscriber of that type, and only to them', () => {
    const bus = createEventBus();
    const heard = [];
    bus.subscribe(EVENTS.CART_CHANGED, (detail) => heard.push(['a', detail.itemCount]));
    bus.subscribe(EVENTS.CART_CHANGED, (detail) => heard.push(['b', detail.itemCount]));
    bus.subscribe(EVENTS.ADD_TO_CART, () => heard.push(['other']));

    bus.publish(EVENTS.CART_CHANGED, { itemCount: 2, totalCents: 10 });

    assert.deepEqual(heard, [['a', 2], ['b', 2]]);
  });

  it('stops delivering after unsubscribe', () => {
    const bus = createEventBus();
    const heard = [];
    const stop = bus.subscribe(EVENTS.CART_CHANGED, (detail) => heard.push(detail.itemCount));

    bus.publish(EVENTS.CART_CHANGED, { itemCount: 1, totalCents: 1 });
    stop();
    bus.publish(EVENTS.CART_CHANGED, { itemCount: 2, totalCents: 2 });

    assert.deepEqual(heard, [1]);
  });

  it('a handler that throws is reported and does not stop the others', () => {
    const errors = [];
    const bus = createEventBus({ onError: (error, type) => errors.push([error.message, type]) });
    const heard = [];
    bus.subscribe(EVENTS.CART_CHANGED, () => {
      throw new Error('boom');
    });
    bus.subscribe(EVENTS.CART_CHANGED, (detail) => heard.push(detail.itemCount));

    bus.publish(EVENTS.CART_CHANGED, { itemCount: 3, totalCents: 1 });

    assert.deepEqual(heard, [3]);
    assert.deepEqual(errors, [['boom', EVENTS.CART_CHANGED]]);
  });

  it('refuses to publish a payload that breaks the contract, and nobody hears it', () => {
    const bus = createEventBus();
    const heard = [];
    bus.subscribe(EVENTS.CART_CHANGED, (detail) => heard.push(detail));

    assert.throws(() => bus.publish(EVENTS.CART_CHANGED, { itemCount: 'many' }), /must be a number/);
    assert.deepEqual(heard, []);
  });
});

describe('shell loader', () => {
  const designSystemVersion = '1.2.0';
  const entry = (name, slot, extra = {}) => ({ name, url: `./${name}.js`, slot, requires: { 'design-system': 1 }, ...extra });

  function remote(name, log) {
    return {
      mfe: {
        name,
        version: '1.0.0',
        contract: CONTRACT_VERSION,
        mount(root) {
          log.push(`mount ${name}`);
          root.append(root.ownerDocument.createTextNode(`${name} here`));
          return () => log.push(`unmount ${name}`);
        },
      },
    };
  }

  function setup(modules, manifest) {
    const log = [];
    const slots = { main: newRoot(), aside: newRoot() };
    const importer = async (url) => {
      const name = url.slice(2, -3);
      if (!(name in modules)) throw new Error(`cannot find ${url}`);
      return typeof modules[name] === 'function' ? modules[name](log) : modules[name];
    };
    const shell = createShell({ manifest, importer, designSystemVersion, bus: createEventBus() });
    return { log, slots, shell };
  }

  const both = [entry('catalog', 'main'), entry('cart', 'aside')];

  it('mounts every micro-frontend in its slot and reports them in manifest order', async () => {
    const { slots, shell, log } = setup({ catalog: (l) => remote('catalog', l), cart: (l) => remote('cart', l) }, both);

    const report = await shell.start(slots);

    assert.deepEqual(report, { mounted: ['catalog', 'cart'], failed: [] });
    assert.equal(slots.main.textContent, 'catalog here');
    assert.equal(slots.aside.textContent, 'cart here');
    assert.deepEqual(log.sort(), ['mount cart', 'mount catalog']);
  });

  it('shows a message in the slot of a micro-frontend that cannot be loaded, and the others still work', async () => {
    const { slots, shell } = setup({ cart: (l) => remote('cart', l) }, both);

    const report = await shell.start(slots);

    assert.deepEqual(report.mounted, ['cart']);
    assert.deepEqual(report.failed, [{ name: 'catalog', reason: 'cannot find ./catalog.js' }]);
    assert.match(slots.main.byTestId('failed-catalog').textContent, /catalog is unavailable.*cannot find/);
    assert.equal(slots.aside.textContent, 'cart here');
  });

  it('refuses a module that does not honor the contract', async () => {
    const bad = { mfe: { name: 'catalog', contract: 99, mount() {} } };
    const { slots, shell } = setup({ catalog: bad, cart: (l) => remote('cart', l) }, both);

    const report = await shell.start(slots);

    assert.match(report.failed[0].reason, /speaks contract 99/);
    assert.deepEqual(report.mounted, ['cart']);
  });

  it('refuses a micro-frontend built for another major version of the design system', async () => {
    const { slots, shell, log } = setup(
      { catalog: (l) => remote('catalog', l), cart: (l) => remote('cart', l) },
      [entry('catalog', 'main', { requires: { 'design-system': 2 } }), entry('cart', 'aside')],
    );

    const report = await shell.start(slots);

    assert.equal(report.failed[0].reason, 'it needs design-system 2.x but the shell provides 1.2.0');
    assert.deepEqual(log, ['mount cart']);
  });

  it('shows a message when mounting throws, without breaking the other micro-frontend', async () => {
    const exploding = { mfe: { name: 'catalog', contract: CONTRACT_VERSION, mount() { throw new Error('kaboom'); } } };
    const { slots, shell } = setup({ catalog: exploding, cart: (l) => remote('cart', l) }, both);

    const report = await shell.start(slots);

    assert.deepEqual(report.failed, [{ name: 'catalog', reason: 'kaboom' }]);
    assert.equal(slots.aside.textContent, 'cart here');
  });

  it('reports a slot that does not exist', async () => {
    const { slots, shell } = setup({ cart: (l) => remote('cart', l) }, [entry('cart', 'footer')]);

    const report = await shell.start(slots);

    assert.deepEqual(report.failed, [{ name: 'cart', reason: "the slot 'footer' does not exist" }]);
  });

  it('unmounts in reverse order, and one that throws does not stop the rest', async () => {
    const log = [];
    const throwing = { mfe: { name: 'cart', contract: CONTRACT_VERSION, mount: () => () => { throw new Error('no'); } } };
    const { slots, shell } = setup({ catalog: () => remote('catalog', log), cart: throwing }, both);
    await shell.start(slots);

    await shell.stop();

    assert.deepEqual(log, ['mount catalog', 'unmount catalog']);
  });

  it('a micro-frontend that mounts and returns nothing can still be stopped', async () => {
    const quiet = { mfe: { name: 'catalog', contract: CONTRACT_VERSION, mount() {} } };
    const { slots, shell } = setup({ catalog: quiet }, [entry('catalog', 'main')]);

    await shell.start(slots);

    await assert.doesNotReject(shell.stop());
  });

  it('can be started again after stopping', async () => {
    const { slots, shell, log } = setup({ catalog: (l) => remote('catalog', l) }, [entry('catalog', 'main')]);

    await shell.start(slots);
    await shell.stop();
    await shell.start(slots);
    await shell.stop();

    assert.deepEqual(log, ['mount catalog', 'unmount catalog', 'mount catalog', 'unmount catalog']);
  });
});
