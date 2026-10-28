const SYMBOLS = { USD: '$', EUR: '€', ARS: 'AR$' };

/** 1250 -> "$12.50". Amounts travel as integer cents so no rounding error can sneak in. */
export function formatMoney(cents, currency = 'USD') {
  if (!Number.isInteger(cents)) throw new TypeError('cents must be an integer');
  const sign = cents < 0 ? '-' : '';
  const abs = Math.abs(cents);
  const symbol = SYMBOLS[currency] ?? `${currency} `;
  return `${sign}${symbol}${Math.floor(abs / 100)}.${String(abs % 100).padStart(2, '0')}`;
}
