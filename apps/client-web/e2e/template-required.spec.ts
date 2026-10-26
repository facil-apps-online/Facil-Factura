import { test, expect } from '@playwright/test';
import { mockApi, signIn } from './mockApi';

// Emitir por Dataico sin plantilla elegida: el servidor rechaza con TEMPLATE_REQUIRED y el aviso lleva al diseño de las facturas.

test('emitir sin plantilla muestra el aviso con el botón que lleva a Diseño de mis Facturas', async ({ page }) => {
  await page.setViewportSize({ width: 1800, height: 900 });
  await signIn(page);
  await mockApi(page);
  await page.route('**/api/client/payroll/*/publish', route => route.fulfill({
    status: 400,
    contentType: 'application/json',
    body: JSON.stringify({
      message: 'Falta seleccionar la plantilla.',
      detail: 'Antes de emitir debe seleccionar la plantilla de esta clase de documento en Diseño de mis Facturas.',
      code: 'TEMPLATE_REQUIRED',
    }),
  }));
  await page.goto('/payroll');
  await page.waitForLoadState('networkidle');

  await page.getByRole('button', { name: 'Emitir', exact: true }).first().click();
  const toast = page.locator('[data-sonner-toast]').filter({ hasText: 'Antes de emitir debe seleccionar la plantilla' });
  await expect(toast).toBeVisible();
  await toast.getByRole('button', { name: 'Ir a Diseño de mis Facturas' }).click();
  await expect(page).toHaveURL(/\/settings$/);
});

test('otros errores de emisión siguen mostrándose sin el botón', async ({ page }) => {
  await page.setViewportSize({ width: 1800, height: 900 });
  await signIn(page);
  await mockApi(page);
  await page.route('**/api/client/payroll/*/publish', route => route.fulfill({
    status: 400, contentType: 'application/json', body: JSON.stringify('No hay una numeración de nómina configurada.'),
  }));
  await page.goto('/payroll');
  await page.waitForLoadState('networkidle');

  await page.getByRole('button', { name: 'Emitir', exact: true }).first().click();
  const toast = page.locator('[data-sonner-toast]').filter({ hasText: 'No hay una numeración de nómina configurada.' });
  await expect(toast).toBeVisible();
  await expect(toast.getByRole('button')).toHaveCount(0);
});
