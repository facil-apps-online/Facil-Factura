import { test, expect } from '@playwright/test';
import { ADMIN_SESSION, INVOICER_SESSION, mockApi, signIn } from './mockApi';

// Valores por defecto de "Nueva factura": los configura el administrador y el formulario los aplica al abrirse.

const DEFAULTS = {
  documentTypeId: 'dt1', paymentMeans: 'TRANSFER', paymentMeansType: 'CREDITO', paymentTermDays: 60, currency: null, notes: 'Gracias por su compra',
};

test.describe('valores por defecto de factura', () => {
  test('el administrador ve el enlace, configura y guarda', async ({ page }) => {
    await signIn(page);
    await mockApi(page, ADMIN_SESSION);
    let saved: any = null;
    await page.route('**/api/client/invoice-defaults', route => {
      if (route.request().method() === 'PUT') {
        saved = route.request().postDataJSON();
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(saved) });
      }
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ documentTypeId: null, paymentMeans: null, paymentMeansType: null, paymentTermDays: null, currency: null, notes: null }) });
    });
    await page.goto('/');
    await page.waitForLoadState('networkidle');
    const nav = page.getByLabel('Menú principal');
    await nav.getByRole('link', { name: 'Valores por defecto' }).click();
    await expect(page).toHaveURL(/\/invoice-defaults$/);
    await expect(page.getByRole('heading', { name: 'Valores por defecto de factura' })).toBeVisible();
    // Sin moneda extranjera habilitada no se ofrece la moneda.
    await expect(page.getByText('Moneda', { exact: true })).toHaveCount(0);

    await page.getByPlaceholder('Contado o crédito...').click();
    await page.getByText('Crédito', { exact: true }).last().click();
    await page.getByPlaceholder('Seleccione el plazo...').click();
    await page.getByText('60 días', { exact: true }).last().click();
    await page.getByPlaceholder('Efectivo, consignación, transferencia...').click();
    await page.getByText('Transferencia', { exact: true }).last().click();
    await page.getByPlaceholder('Texto que se precarga en cada factura nueva').fill('  Gracias por su compra  ');
    await page.getByRole('button', { name: 'Guardar' }).click();
    await expect(page.getByText('Valores por defecto guardados')).toBeVisible();
    expect(saved).toEqual({ documentTypeId: null, paymentMeans: 'TRANSFER', paymentMeansType: 'CREDITO', paymentTermDays: 60, currency: null, notes: 'Gracias por su compra' });
  });

  test('contado: el plazo no se envía', async ({ page }) => {
    await signIn(page);
    await mockApi(page, ADMIN_SESSION);
    let saved: any = null;
    await page.route('**/api/client/invoice-defaults', route => {
      if (route.request().method() === 'PUT') {
        saved = route.request().postDataJSON();
        return route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
      }
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ ...DEFAULTS, paymentTermDays: 60 }) });
    });
    await page.goto('/invoice-defaults');
    await page.waitForLoadState('networkidle');
    await page.locator('input[value="Crédito"]').click();
    await page.getByText('Contado', { exact: true }).last().click();
    await expect(page.getByText('Plazo de pago')).toHaveCount(0);
    await page.getByRole('button', { name: 'Guardar' }).click();
    await expect(page.getByText('Valores por defecto guardados')).toBeVisible();
    expect(saved.paymentMeansType).toBe('DEBITO');
    expect(saved.paymentTermDays).toBeNull();
  });

  test('quien no es administrador no ve el enlace y la ruta lo devuelve al inicio', async ({ page }) => {
    await signIn(page);
    await mockApi(page, INVOICER_SESSION);
    await page.goto('/invoice-defaults');
    await page.waitForLoadState('networkidle');
    await expect(page).toHaveURL(/\/$/);
    await expect(page.getByLabel('Menú principal').getByRole('link', { name: 'Valores por defecto' })).toHaveCount(0);
  });

  test('"Nueva factura" abre con los valores definidos', async ({ page }) => {
    await signIn(page);
    await mockApi(page, ADMIN_SESSION);
    await page.route('**/api/client/invoice-defaults', route =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(DEFAULTS) }));
    await page.goto('/invoices');
    await page.waitForLoadState('networkidle');
    await page.locator('button', { hasText: /^\s*Nueva\s/ }).first().click();
    await expect(page.locator('input[value="Crédito"]')).toBeVisible();
    await expect(page.locator('input[value="60 días"]')).toBeVisible();
    await expect(page.locator('input[value="Transferencia"]')).toBeVisible();
    await expect.poll(() => page.locator('input').evaluateAll(els => els.map(e => (e as HTMLInputElement).value))).toContain('Gracias por su compra');
  });

  test('un valor que ya no existe en el catálogo se ignora', async ({ page }) => {
    await signIn(page);
    await mockApi(page, ADMIN_SESSION);
    await page.route('**/api/client/invoice-defaults', route =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ ...DEFAULTS, paymentMeans: 'YA_NO_EXISTE', documentTypeId: 'tipo-borrado' }) }));
    await page.goto('/invoices');
    await page.waitForLoadState('networkidle');
    await page.locator('button', { hasText: /^\s*Nueva\s/ }).first().click();
    await expect(page.locator('input[value="Crédito"]')).toBeVisible();
    await expect(page.locator('input[value="Transferencia"]')).toHaveCount(0);
  });
});
