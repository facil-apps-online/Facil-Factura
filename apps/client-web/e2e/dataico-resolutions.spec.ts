import { test, expect } from '@playwright/test';
import { ADMIN_SESSION, mockApi, signIn } from './mockApi';

// Vista "Resoluciones en Dataico": lo que Dataico tiene vinculado con su estado, para cargar o recargar una resolución en vez de rechazarla.

const DATAICO_SESSION = { ...ADMIN_SESSION, features: { ...ADMIN_SESSION.features, dataico: true } };

test.describe('resoluciones en Dataico', () => {
  test('no se ofrece a quien no factura por Dataico', async ({ page }) => {
    await signIn(page);
    await mockApi(page);
    await page.goto('/resolutions');
    await page.waitForLoadState('networkidle');
    await expect(page.getByRole('button', { name: 'Resoluciones en Dataico' })).toHaveCount(0);
  });

  test('lista cada resolución con su estado y solo ofrece acción donde corresponde', async ({ page }) => {
    await signIn(page);
    await mockApi(page, DATAICO_SESSION);
    await page.goto('/resolutions');
    await page.waitForLoadState('networkidle');
    await page.getByRole('button', { name: 'Resoluciones en Dataico' }).click();

    const dialog = page.getByRole('alertdialog');
    await expect(dialog.getByTestId('dataico-resolution-FELP-111')).toContainText('Cargada');
    await expect(dialog.getByTestId('dataico-resolution-DS-222')).toContainText('Eliminada');
    await expect(dialog.getByTestId('dataico-resolution-NUEVA-333')).toContainText('Nueva');
    await expect(dialog.getByTestId('dataico-resolution-VIEJA-444')).toContainText('Vencida');

    await expect(dialog.getByRole('button', { name: 'Recargar la resolución DS 222' })).toBeVisible();
    await expect(dialog.getByRole('button', { name: 'Cargar la resolución NUEVA 333' })).toBeVisible();
    // Cargada y vencida no tienen botón.
    await expect(dialog.getByRole('button', { name: /resolución FELP 111/ })).toHaveCount(0);
    await expect(dialog.getByRole('button', { name: /resolución VIEJA 444/ })).toHaveCount(0);
  });

  test('recargar una eliminada envía su tipo, prefijo y número y avisa lo hecho', async ({ page }) => {
    await signIn(page);
    await mockApi(page, DATAICO_SESSION);
    let sent: any = null;
    await page.route('**/api/client/resolutions/dataico-load', route => {
      sent = route.request().postDataJSON();
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ message: 'Resolución DS 222 recargada.' }) });
    });
    await page.goto('/resolutions');
    await page.waitForLoadState('networkidle');
    await page.getByRole('button', { name: 'Resoluciones en Dataico' }).click();

    await page.getByRole('alertdialog').getByRole('button', { name: 'Recargar la resolución DS 222' }).click();
    await expect.poll(() => sent).not.toBeNull();
    expect(sent).toEqual({ documentType: 'DS', prefix: 'DS', number: '222' });
    await expect(page.getByRole('alertdialog').getByRole('status')).toContainText('Resolución DS 222 recargada.');
  });

  test('si no se puede recargar muestra el motivo', async ({ page }) => {
    await signIn(page);
    await mockApi(page, DATAICO_SESSION);
    await page.route('**/api/client/resolutions/dataico-load', route =>
      route.fulfill({ status: 400, contentType: 'application/json', body: JSON.stringify({ message: 'La resolución venció el 01/01/2025.' }) }));
    await page.goto('/resolutions');
    await page.waitForLoadState('networkidle');
    await page.getByRole('button', { name: 'Resoluciones en Dataico' }).click();
    await page.getByRole('alertdialog').getByRole('button', { name: 'Cargar la resolución NUEVA 333' }).click();
    await expect(page.getByRole('alertdialog').getByRole('alert')).toContainText('venció');
  });
});
