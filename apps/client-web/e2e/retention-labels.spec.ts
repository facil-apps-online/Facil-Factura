import { test, expect, type Page } from '@playwright/test';
import { ADMIN_SESSION, mockApi, signIn } from './mockApi';

// Las retenciones se ven con su nombre corto ("RETE FTE 2.5%", parametrizado por la plataforma) o con el nombre completo del concepto, según el estilo del cliente:
// uno para las retenciones de cada línea y otro para las del pie.

const SHORT_NAMES = { RET_FUENTE: 'RETE FTE', RET_ICA: 'RETE ICA', RET_IVA: 'RETE IVA' };
const CONCEPTS = [
  { id: 'c1', groupKey: 'compras', groupLabel: 'Compras', name: 'Compras generales (declarantes)', taxCategory: 'RET_FUENTE', rate: 2.5, baseType: 'Subtotal', baseUvt: 0, personType: 'Ambas' },
  { id: 'c2', groupKey: 'honorarios', groupLabel: 'Honorarios', name: 'Honorarios personas jurídicas', taxCategory: 'RET_FUENTE', rate: 11, baseType: 'Subtotal', baseUvt: 0, personType: 'Ambas' },
];
const FOOTER_CATALOG = [
  { id: 'f1', category: 'RET_FUENTE', name: 'Retención en la fuente', rate: 2.5 },
  { id: 'f2', category: 'RET_ICA', name: 'Retención de ICA', rate: 0.966 },
];

const json = (route: any, body: unknown) => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });

async function setup(page: Page, styles: { line: 'Short' | 'Full'; footer: 'Short' | 'Full' }) {
  await page.setViewportSize({ width: 1800, height: 1000 });
  await signIn(page);
  await mockApi(page, { ...ADMIN_SESSION, features: { payments: true, retentionLabelStyles: styles } as any });
  await page.route('**/api/client/retention-concepts/short-names', route => json(route, SHORT_NAMES));
  await page.route('**/api/client/retention-concepts/catalog*', route => json(route, { concepts: CONCEPTS }));
  await page.route('**/api/client/tax-catalog?kind=Retention', route => json(route, FOOTER_CATALOG));
  await page.goto('/invoices');
  await page.waitForLoadState('networkidle');
  await page.getByRole('button', { name: /Nueva Factura/ }).click();
}

async function openLineRetention(page: Page) {
  await page.getByRole('button', { name: 'Agregar Ítem' }).click();
  await page.getByTitle('Retención de esta línea').filter({ visible: true }).first().click();
  const input = page.getByPlaceholder('Buscar retención...').filter({ visible: true }).first();
  await input.click();
  return input;
}

test('estilo corto en las líneas: la lista muestra el nombre corto con la tarifa y la búsqueda encuentra por el nombre completo', async ({ page }) => {
  await setup(page, { line: 'Short', footer: 'Full' });
  const input = await openLineRetention(page);

  await expect(page.getByText('RETE FTE 2.5%', { exact: true })).toBeVisible();
  await expect(page.getByText('RETE FTE 11%', { exact: true })).toBeVisible();
  await expect(page.getByText('Compras generales (declarantes)')).toHaveCount(0);

  // Buscar por el nombre completo del concepto sigue funcionando.
  await input.fill('honorarios');
  await expect(page.getByText('RETE FTE 11%', { exact: true })).toBeVisible();
  await expect(page.getByText('RETE FTE 2.5%', { exact: true })).toHaveCount(0);

  // Elegida, la línea muestra el nombre corto con su tarifa.
  await page.getByText('RETE FTE 11%', { exact: true }).click();
  await expect(page.getByTitle('Retención de esta línea').filter({ visible: true }).first()).toContainText('RETE FTE 11%');
});

test('estilo completo en las líneas: queda como siempre, con el nombre del concepto', async ({ page }) => {
  await setup(page, { line: 'Full', footer: 'Short' });
  await openLineRetention(page);

  await expect(page.getByText('Compras generales (declarantes) (RET_FUENTE 2.5%)')).toBeVisible();
  await page.getByText('Compras generales (declarantes) (RET_FUENTE 2.5%)').click();
  // Elegida, la línea muestra solo la tarifa, como siempre.
  await expect(page.getByTitle('Retención de esta línea').filter({ visible: true }).first()).toHaveText('2.5%');
});

test('el pie tiene su propio estilo: en corto muestra el nombre corto de la retención general', async ({ page }) => {
  await setup(page, { line: 'Full', footer: 'Short' });
  await page.getByRole('button', { name: /Agregar Retención/ }).click();

  // Al agregarla queda en edición: la lista usa el nombre corto, no el completo.
  await page.getByPlaceholder('Buscar retención...').filter({ visible: true }).last().click();
  await expect(page.getByText('RETE FTE 2.5%', { exact: true })).toBeVisible();
  await expect(page.getByText('RETE ICA 0.966%', { exact: true })).toBeVisible();
  await expect(page.getByText('Retención en la fuente (RET_FUENTE 2.5%)')).toHaveCount(0);
});

test('en Valores por defecto el administrador elige el estilo de cada tipo y se guarda', async ({ page }) => {
  await page.setViewportSize({ width: 1800, height: 1000 });
  await signIn(page);
  await mockApi(page, { ...ADMIN_SESSION, features: { payments: true, retentionLabelStyles: { line: 'Short', footer: 'Short' } } as any });
  let sent: any = null;
  await page.route('**/api/client/retention-concepts/label-style', route => {
    sent = route.request().postDataJSON();
    return json(route, sent);
  });
  await page.goto('/invoice-defaults');
  await page.waitForLoadState('networkidle');

  await expect(page.getByRole('heading', { name: 'Retenciones en los formularios' })).toBeVisible();
  const lineSelect = page.locator('label:has-text("Retenciones de cada línea") + div input');
  await expect(lineSelect).toHaveValue('Nombre corto (RETE FUENTE 2.5%)');
  await lineSelect.click();
  await page.getByText('Nombre completo del concepto', { exact: true }).click();

  await page.getByRole('button', { name: 'Guardar' }).click();
  await expect.poll(() => sent).toEqual({ line: 'Full', footer: 'Short' });
});
