import { test, expect } from '@playwright/test';
import { document as mockDocument, mockApi, signIn } from './mockApi';

// Documento que Dataico creó pero la DIAN rechazó: queda rechazado y editable, y el cuadro del motivo muestra las reglas de la DIAN.

const RESPONSE = JSON.stringify({
  dian_status: 'DIAN_RECHAZADO', number: 'FEP29523', issue_date: '26/10/2026 14:28:47',
  dian_messages: [
    'Regla: FAD09e, Rechazo: Valida que fecha de generación de la factura sea igual a la fecha de firma',
    'Regla: RUT01, Notificación: La validación del estado del RUT próximamente estará disponible.',
    'Regla: RUT01, Notificación: La validación del estado del RUT próximamente estará disponible.',
  ],
});

test.describe('documento rechazado por la DIAN', () => {
  test('se ofrece editar y el motivo muestra las reglas, con rechazos en rojo y sin repetir notificaciones', async ({ page }) => {
    await signIn(page);
    await mockApi(page);
    // Como el servidor: la fila del listado no trae el texto de la respuesta (pesa hasta decenas de KB), solo avisa que existe.
    const { dianResponseMessage: _omitted, ...row } = { ...mockDocument(2, 'FE'), id: 'dian-rej', number: '29523', status: 'REJECTED', dianResponseMessage: '', hasDianResponse: true };
    const doc = { ...row, items: undefined, retentionsTotal: 0, netTotal: 0 };
    await page.route('**/api/client/invoices?*', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([doc]) }));
    await page.route('**/api/client/invoices', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([doc]) }));
    let responseRequests = 0;
    await page.route('**/api/client/invoices/dian-rej/dian-response', route => {
      responseRequests++;
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ dianResponseMessage: RESPONSE }) });
    });
    await page.goto('/invoices');
    await page.waitForLoadState('networkidle');

    // Rechazado: se puede editar (y el motivo está a un clic, que lo pide al servidor solo entonces).
    await expect(page.getByRole('button', { name: /Editar/ }).first()).toBeVisible();
    expect(responseRequests).toBe(0);
    await page.getByRole('button', { name: /Ver motivo del rechazo|Motivo del rechazo/ }).first().click();

    const dialog = page.getByRole('dialog');
    await expect(dialog).toContainText('Motivo del rechazo');
    await expect(dialog).toContainText('DIAN_RECHAZADO');
    const rejection = dialog.getByText('Rechazo: Valida que fecha de generación', { exact: false });
    await expect(rejection).toBeVisible();
    await expect(rejection).toHaveClass(/bg-rose-50/);
    const notification = dialog.getByText('Notificación: La validación del estado del RUT', { exact: false });
    await expect(notification).toHaveCount(1);
    await expect(notification).toHaveClass(/bg-amber-50/);
    expect(responseRequests).toBe(1);
  });

  test('el listado muestra las retenciones y el total neto que calcula el servidor, sin ítems', async ({ page }) => {
    await signIn(page);
    await mockApi(page);
    const row = { ...mockDocument(1, 'FE'), id: 'list-row', number: '777', status: 'APPROVED', items: undefined, subtotal: 100000, taxAmount: 19000, totalAmount: 119000, retentionsTotal: 2500, netTotal: 116500, hasDianResponse: false };
    await page.route('**/api/client/invoices?*', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([row]) }));
    await page.route('**/api/client/invoices', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([row]) }));
    await page.goto('/invoices');
    await page.waitForLoadState('networkidle');
    const body = page.locator('body');
    await expect(body).toContainText('$116,500.00');
    await expect(body).toContainText('-$2,500.00');
  });
});
