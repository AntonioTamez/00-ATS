import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { Fragment, createStore, h, mount, renderStatic, renderToString } from '../packages/ui-runtime/index.js';
import { FakeDocument, newRoot } from './support/fake-dom.js';

describe('h and renderToString', () => {
  it('builds plain elements and prints them as HTML', () => {
    const tree = h('p', { className: 'note', id: 'a' }, 'Hello ', h('b', null, 'world'));

    assert.equal(renderToString(tree), '<p class="note" id="a">Hello <b>world</b></p>');
  });

  it('treats a component as a function from props to elements', () => {
    const Greeting = ({ name, children }) => h('p', null, `Hi ${name}`, children);

    assert.equal(renderToString(h(Greeting, { name: 'Ana' }, h('i', null, '!'))), '<p>Hi Ana<i>!</i></p>');
    assert.equal(renderToString(Greeting({ name: 'Ana', children: [] })), '<p>Hi Ana</p>');
  });

  it('escapes text, so user content cannot become markup', () => {
    const html = renderToString(h('p', { title: 'a "quote"' }, '<script>alert(1)</script> & more'));

    assert.equal(html, '<p title="a &quot;quote&quot;">&lt;script&gt;alert(1)&lt;/script&gt; &amp; more</p>');
  });

  it('prints booleans as attributes, drops false, null and handlers, and renames className, htmlFor and defaultValue', () => {
    const html = renderToString(
      h('input', { disabled: true, hidden: false, name: null, onClick: () => {}, className: 'x', htmlFor: 'y', defaultValue: 'z' }),
    );

    assert.equal(html, '<input disabled class="x" for="y" value="z">');
  });

  it('refuses attribute names that could break out of the tag', () => {
    assert.throws(() => renderToString(h('p', { 'a b=c': 1 })), /invalid attribute name/);
  });

  it('flattens arrays and fragments, and ignores null, false and true children', () => {
    const tree = h('ul', null, [h('li', null, 'a'), [h('li', null, 'b')]], null, false, true, h(Fragment, null, h('li', null, 'c')));

    assert.equal(renderToString(tree), '<ul><li>a</li><li>b</li><li>c</li></ul>');
  });

  it('prints numbers, and 0 is not dropped', () => {
    assert.equal(renderToString(h('span', null, 0)), '<span>0</span>');
  });
});

describe('createStore', () => {
  it('replaces the state instead of changing it, and freezes it', () => {
    const store = createStore({ a: 1, list: [] });
    const before = store.getState();

    store.setState({ a: 2 });

    assert.equal(before.a, 1);
    assert.equal(store.getState().a, 2);
    assert.ok(Object.isFrozen(store.getState()));
  });

  it('notifies listeners only when something changed', () => {
    const store = createStore({ a: 1 });
    const seen = [];
    store.subscribe((state) => seen.push(state.a));

    store.setState({ a: 1 });
    store.setState({ a: 2 });
    store.setState((state) => ({ a: state.a + 1 }));

    assert.deepEqual(seen, [2, 3]);
  });

  it('stops notifying after unsubscribe', () => {
    const store = createStore({ a: 1 });
    const seen = [];
    const stop = store.subscribe((state) => seen.push(state.a));

    store.setState({ a: 2 });
    stop();
    store.setState({ a: 3 });

    assert.deepEqual(seen, [2]);
  });
});

describe('the DOM adapter', () => {
  it('paints elements, attributes and text into the root', () => {
    const root = newRoot();

    renderStatic(root, h('p', { className: 'x', disabled: true }, 'Hi ', h('b', null, 'there')));

    const p = root.children[0];
    assert.equal(p.tagName, 'p');
    assert.equal(p.getAttribute('class'), 'x');
    assert.ok(p.hasAttribute('disabled'));
    assert.equal(root.textContent, 'Hi there');
  });

  it('turns handler props into listeners that receive a small normalized event', () => {
    const root = newRoot();
    const calls = [];
    renderStatic(
      root,
      h(
        'form',
        { 'data-testid': 'f', onSubmit: (event) => calls.push(['submit', event.formData]) },
        h('input', { 'data-testid': 'i', onInput: (event) => calls.push(['input', event.value]) }),
        h('button', { 'data-testid': 'b', onClick: (event) => calls.push(['click', Object.keys(event)]) }),
      ),
    );

    root.byTestId('f').submit({ term: 'x' });
    root.byTestId('i').dispatch('input', { target: { value: 'abc' } });
    root.byTestId('b').click();

    assert.deepEqual(calls, [['submit', { term: 'x' }], ['input', 'abc'], ['click', []]]);
  });

  it('refuses a handler prop it does not know how to attach', () => {
    assert.throws(() => renderStatic(newRoot(), h('p', { onWheel: () => {} })), /unsupported event prop 'onWheel'/);
  });

  it('mount paints now, repaints when the store changes, and stops on unmount', () => {
    const root = new FakeDocument().createElement('div');
    const store = createStore({ n: 0 });
    const unmount = mount(root, () => h('p', null, `n=${store.getState().n}`), store.subscribe);

    assert.equal(root.textContent, 'n=0');
    store.setState({ n: 1 });
    assert.equal(root.textContent, 'n=1');

    unmount();
    assert.equal(root.textContent, '');
    store.setState({ n: 2 });
    assert.equal(root.textContent, '');
  });
});
