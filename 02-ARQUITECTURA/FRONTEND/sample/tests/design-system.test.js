import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { renderToString } from '../packages/ui-runtime/index.js';
import {
  Badge,
  Button,
  Card,
  Heading,
  Price,
  QuantityStepper,
  SearchField,
  TextInput,
  VERSION,
  formatMoney,
} from '../packages/design-system/index.js';
import { deepFreeze, newRoot } from './support/fake-dom.js';
import { renderStatic } from '../packages/ui-runtime/index.js';

describe('foundations', () => {
  it('formats integer cents as money', () => {
    assert.equal(formatMoney(1250), '$12.50');
    assert.equal(formatMoney(5), '$0.05');
    assert.equal(formatMoney(0), '$0.00');
    assert.equal(formatMoney(-1999, 'EUR'), '-€19.99');
    assert.equal(formatMoney(100, 'CLP'), 'CLP 1.00');
  });

  it('refuses amounts that are not integer cents', () => {
    assert.throws(() => formatMoney(12.5), TypeError);
    assert.throws(() => formatMoney('1250'), TypeError);
  });

  it('is versioned as semver, because other packages check its major', () => {
    assert.match(VERSION, /^\d+\.\d+\.\d+$/);
  });
});

describe('atoms', () => {
  it('Button shows its label, variant and disabled state', () => {
    assert.equal(
      renderToString(Button({ label: 'Save', variant: 'secondary', disabled: true, testId: 'save' })),
      '<button type="button" class="ds-button ds-button--secondary" disabled data-testid="save">Save</button>',
    );
  });

  it('Button keeps the handler it was given out of the markup, and a type override', () => {
    const html = renderToString(Button({ label: 'Go', type: 'submit', onClick: () => {} }));

    assert.equal(html, '<button type="submit" class="ds-button ds-button--primary">Go</button>');
  });

  it('Badge, Heading, Price and TextInput render their props', () => {
    assert.equal(renderToString(Badge({ text: 'New', tone: 'success' })), '<span class="ds-badge ds-badge--success">New</span>');
    assert.equal(renderToString(Heading({ level: 3, text: 'Title' })), '<h3 class="ds-heading">Title</h3>');
    assert.equal(renderToString(Price({ cents: 2999 })), '<span class="ds-price">$29.99</span>');
    assert.equal(
      renderToString(TextInput({ name: 'q', value: 'abc', placeholder: 'Search', label: 'Search' })),
      '<input type="text" class="ds-input" name="q" value="abc" placeholder="Search" aria-label="Search">',
    );
  });

  it('Heading keeps its level between 1 and 6', () => {
    assert.match(renderToString(Heading({ level: 0, text: 'x' })), /^<h1/);
    assert.match(renderToString(Heading({ level: 9, text: 'x' })), /^<h6/);
  });

  it('Badge has a neutral tone by default', () => {
    assert.match(renderToString(Badge({ text: 'x' })), /ds-badge--neutral/);
  });
});

describe('molecules', () => {
  it('SearchField gives the trimmed text to onSearch when the form is submitted', () => {
    const heard = [];
    const root = newRoot();
    renderStatic(root, SearchField({ term: 'old', onSearch: (term) => heard.push(term) }));

    root.byTestId('search').submit({ term: '  design  ' });
    root.byTestId('search').submit({});

    assert.deepEqual(heard, ['design', '']);
  });

  it('SearchField starts with the current term in the input', () => {
    assert.match(renderToString(SearchField({ term: 'atomic', onSearch() {} })), /name="term" value="atomic"/);
  });

  it('QuantityStepper steps up and down, and cannot go below its minimum', () => {
    const calls = [];
    const root = newRoot();
    renderStatic(root, QuantityStepper({ value: 1, onDecrement: () => calls.push('-'), onIncrement: () => calls.push('+') }));

    root.byTestId('stepper-inc').click();
    const decremented = root.byTestId('stepper-dec').click();

    assert.deepEqual(calls, ['+']);
    assert.equal(decremented, false);
    assert.equal(root.byTestId('stepper-value').textContent, '1');
  });

  it('QuantityStepper enables the minus above the minimum', () => {
    const calls = [];
    const root = newRoot();
    renderStatic(root, QuantityStepper({ value: 3, onDecrement: () => calls.push('-'), onIncrement() {} }));

    root.byTestId('stepper-dec').click();

    assert.deepEqual(calls, ['-']);
  });

  it('Card shows a title, its content and an optional footer', () => {
    const html = renderToString(Card({ title: 'T', children: 'body', footer: 'foot', testId: 'c' }));

    assert.equal(
      html,
      '<article class="ds-card" data-testid="c"><h3 class="ds-heading">T</h3><div class="ds-card__body">body</div><footer class="ds-card__footer">foot</footer></article>',
    );
    assert.doesNotMatch(renderToString(Card({ title: 'T' })), /<footer/);
  });
});

describe('purity: a presentational component is a function of its props', () => {
  const cases = {
    Button: () => Button({ label: 'x', onClick() {} }),
    Badge: () => Badge({ text: 'x', tone: 'danger' }),
    Heading: () => Heading({ text: 'x' }),
    Price: () => Price({ cents: 100 }),
    TextInput: () => TextInput({ name: 'n', value: 'v' }),
    SearchField: () => SearchField({ term: 't', onSearch() {} }),
    QuantityStepper: () => QuantityStepper({ value: 2, onDecrement() {}, onIncrement() {} }),
    Card: () => Card({ title: 't', children: [Badge({ text: 'a' })] }),
  };

  for (const [name, render] of Object.entries(cases)) {
    it(`${name} renders the same thing every time`, () => {
      assert.equal(renderToString(render()), renderToString(render()));
    });
  }

  it('none of them modifies the props it receives', () => {
    const props = deepFreeze({ label: 'x', cents: 5, text: 'y', tone: 'success', value: 2, term: 'z', title: 't', children: ['c'] });

    assert.doesNotThrow(() => {
      Button({ ...props });
      Price({ ...props });
      Card({ ...props });
      QuantityStepper({ ...props, onDecrement() {}, onIncrement() {} });
    });
  });
});
