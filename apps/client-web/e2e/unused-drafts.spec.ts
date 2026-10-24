import { test, expect, type Page } from '@playwright/test';
import { ADMIN_SESSION, mockApi, signIn } from './mockApi';

// Limpieza de borradores de plantilla sin uso: el cuadro lista lo que se tocaría, deja quitar casillas y pide confirmar dentro del mismo cuadro.

const DRAFTS = [
  { id: 'u1', name: 'Borrador de prueba uno', documentType: 'Factura electrónica de venta', createdAt: '2026-10-01T10:00:00Z' },
  { id: 'u2', name: 'Borrador de prueba dos', documentType: 'Nota crédito', createdAt: '2026-10-02T10:00:00Z' },
];

async function open(page: Page) {
  await signIn(page);
  await mockApi(page, ADMIN_SESSION);
  await page.route('**/api/client/templates/unused-drafts', route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(DRAFTS) }));
  await page.goto('/settings');
  await page.getByRole('button', { name: 'Limpiar borradores' }).click();
  await expect(page.getByRole('alertdialog')).toBeVisible();
}

test.describe('limpieza de borradores sin uso', () => {
  test('lista los borradores, archiva solo los marcados y avisa', async ({ page }) => {
    await open(page);
    const dialog = page.getByRole('alertdialog');
    await expect(dialog.getByText('Borrador de prueba uno')).toBeVisible();
    await expect(dialog.getByText('Borrador de prueba dos')).toBeVisible();
    await expect(dialog.getByRole('button', { name: /Archivar \(2\)/ })).toBeVisible();

    let body: any = null;
    await page.route('**/api/client/templates/unused-drafts/archive', route => {
      body = route.request().postDataJSON();
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ count: 1 }) });
    });

    await dialog.getByText('Borrador de prueba dos').click();   // quita la segunda casilla
    await dialog.getByRole('button', { name: /Archivar \(1\)/ }).click();
    await expect(dialog.getByText('no se podrán publicar de nuevo')).toBeVisible();
    await dialog.getByRole('button', { name: 'Sí, archivar' }).click();

    await expect(page.getByRole('alertdialog')).toHaveCount(0);
    await expect(page.getByText('1 plantilla archivada.')).toBeVisible();
    expect(body).toEqual({ ids: ['u1'] });
  });

  test('eliminar pide confirmar y se puede volver atrás', async ({ page }) => {
    await open(page);
    const dialog = page.getByRole('alertdialog');
    let deleted = false;
    await page.route('**/api/client/templates/unused-drafts/delete', route => {
      deleted = true;
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ count: 2 }) });
    });

    await dialog.getByRole('button', { name: /Eliminar \(2\)/ }).click();
    await expect(dialog.getByText('No se puede deshacer.')).toBeVisible();
    await dialog.getByRole('button', { name: 'Volver' }).click();
    await expect(dialog.getByRole('button', { name: /Eliminar \(2\)/ })).toBeVisible();
    expect(deleted).toBe(false);

    await dialog.getByRole('button', { name: /Eliminar \(2\)/ }).click();
    await dialog.getByRole('button', { name: 'Sí, eliminar' }).click();
    await expect(page.getByText('2 plantillas eliminadas.')).toBeVisible();
    expect(deleted).toBe(true);
  });

  test('sin casillas marcadas no se puede actuar', async ({ page }) => {
    await open(page);
    const dialog = page.getByRole('alertdialog');
    await dialog.getByText(/Seleccionar todos/).click();
    await expect(dialog.getByRole('button', { name: /^Archivar/ })).toBeDisabled();
    await expect(dialog.getByRole('button', { name: /^Eliminar/ })).toBeDisabled();
  });
});
