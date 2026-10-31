/**
 * The design system's public face. Other packages import ONLY this file, never the folders behind it.
 * It holds atoms and molecules, and no knowledge of products, carts or any business.
 */
export const VERSION = '1.2.0';

export { formatMoney } from './foundations/money.js';
export { Button } from './atoms/Button.js';
export { Badge } from './atoms/Badge.js';
export { Heading } from './atoms/Heading.js';
export { Price } from './atoms/Price.js';
export { TextInput } from './atoms/TextInput.js';
export { SearchField } from './molecules/SearchField.js';
export { QuantityStepper } from './molecules/QuantityStepper.js';
export { Card } from './molecules/Card.js';
