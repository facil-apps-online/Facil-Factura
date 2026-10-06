import { test, expect } from '@playwright/test';
import { ADMIN_SESSION, mockApi, signIn } from './mockApi';

// Módulo de pagos: lo activa el Tenant por cliente (apagado por defecto). Apagado, no hay menú "Pagos" y la ruta devuelve al inicio.

const WITHOUT_PAYMENTS = { ...ADMIN_SESSION, features: { payments: false } };

test.describe('módulo de pagos', () => {
  test('apagado: el administrador no ve "Pagos" en el menú, pero sí el resto', async ({ page }) => {
    await signIn(page);
    await mockApi(page, WITHOUT_PAYMENTS);
    await page.goto('/');
    await page.waitForLoadState('networkidle');
    const nav = page.getByRole('navigation', { name: 'Menú principal' }).or(page.getByLabel('Menú principal'));
    await expect(nav.getByRole('link', { name: 'Pagos' })).toHaveCount(0);
    await expect(nav.getByRole('link', { name: 'Usuarios' })).toBeVisible();
    await expect(nav.getByRole('link', { name: 'Resoluciones DIAN' })).toBeVisible();
  });

  test('apagado: entrar a /payments por la URL vuelve al inicio', async ({ page }) => {
    await signIn(page);
    await mockApi(page, WITHOUT_PAYMENTS);
    await page.goto('/payments');
    await page.waitForLoadState('networkidle');
    await expect(page).toHaveURL(/\/$/);
    await expect(page.getByText('Esta sección estará disponible próximamente.')).toHaveCount(0);
  });

  test('encendido: aparece "Pagos" en el menú y la ruta abre', async ({ page }) => {
    await signIn(page);
    await mockApi(page, ADMIN_SESSION);
    await page.goto('/');
    await page.waitForLoadState('networkidle');
    const nav = page.getByRole('navigation', { name: 'Menú principal' }).or(page.getByLabel('Menú principal'));
    await expect(nav.getByRole('link', { name: 'Pagos' })).toBeVisible();
    await page.goto('/payments');
    await page.waitForLoadState('networkidle');
    await expect(page).toHaveURL(/\/payments$/);
  });
});
