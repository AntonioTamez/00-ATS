import { h } from '../../ui-runtime/index.js';
import { Button } from '../atoms/Button.js';
import { TextInput } from '../atoms/TextInput.js';

/** Molecule. An input and a button that together mean "search for this". `onSearch` gets the trimmed text. */
export function SearchField({ term = '', onSearch, testId = 'search' }) {
  return h(
    'form',
    {
      className: 'ds-search',
      'data-testid': testId,
      onSubmit: (event) => onSearch(String(event.formData?.term ?? '').trim()),
    },
    TextInput({ name: 'term', value: term, placeholder: 'Search…', label: 'Search' }),
    Button({ label: 'Search', variant: 'secondary', type: 'submit' }),
  );
}
