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
    const doc = { ...mockDocument(2, 'FE'), id: 'dian-rej', number: '29523', status: 'REJECTED', dianResponseMessage: RESPONSE };
    await page.route('**/api/client/invoices?*', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([doc]) }));
    await page.route('**/api/client/invoices', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([doc]) }));
    await page.goto('/invoices');
    await page.waitForLoadState('networkidle');

    // Rechazado: se puede editar (y el motivo está a un clic).
    await expect(page.getByRole('button', { name: /Editar/ }).first()).toBeVisible();
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
  });
});
