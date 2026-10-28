import { test, expect, type Page } from '@playwright/test';
import { document as mockDocument, mockApi, signIn } from './mockApi';

// PDF que se mandan por correo con la factura o la nota: se eligen en el borrador y se consultan desde el detalle. Solo PDF y hasta 5 MB sumados.

const pdf = (name: string, bytes = 1200) => ({ name, mimeType: 'application/pdf', buffer: Buffer.concat([Buffer.from('%PDF-1.4\n'), Buffer.alloc(bytes)]) });
const json = (route: any, body: unknown, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });

const draftRow = { ...mockDocument(0, 'FE'), id: 'dr1', number: '', status: 'DRAFT', retentionsTotal: 0, netTotal: 1000, hasDianResponse: false };
const draftFull = { ...draftRow, items: [], observations: [], customerId: null, resolutionId: null, generalRetentions: [] };

async function openInvoices(page: Page) {
  await page.setViewportSize({ width: 1800, height: 1000 });
  await signIn(page);
  await mockApi(page);
}

test('en una factura nueva se eligen PDF: solo PDF y hasta 5 MB, y quedan pendientes hasta guardar', async ({ page }) => {
  await openInvoices(page);
  await page.goto('/invoices');
  await page.waitForLoadState('networkidle');
  await page.getByRole('button', { name: /Nueva Factura/ }).click();

  const panel = page.getByTestId('document-attachments');
  await expect(panel).toContainText('Adjuntos (PDF)');
  await expect(panel).toContainText('0.0 MB de 5.0 MB');
  const input = page.getByTestId('attachments-input');

  // Un archivo que no es PDF no entra.
  await input.setInputFiles({ name: 'foto.png', mimeType: 'image/png', buffer: Buffer.from('x') });
  await expect(page.locator('[data-sonner-toast]').filter({ hasText: '«foto.png» no es un PDF.' })).toBeVisible();
  await expect(panel.getByText('foto.png')).toHaveCount(0);

  // Uno válido queda pendiente con su tamaño.
  await input.setInputFiles(pdf('orden-de-compra.pdf'));
  await expect(panel.getByText('orden-de-compra.pdf')).toBeVisible();
  await expect(panel).toContainText('se sube al guardar');

  // Sin límite de cantidad: entran más de 5 mientras el peso no pase.
  for (let i = 1; i <= 6; i++) await input.setInputFiles(pdf(`soporte-${i}.pdf`));
  await expect(panel.getByText('soporte-6.pdf')).toBeVisible();

  // Pasar de 5 MB sumados no entra.
  await input.setInputFiles(pdf('enorme.pdf', 5 * 1024 * 1024));
  await expect(page.locator('[data-sonner-toast]').filter({ hasText: /«enorme.pdf» .*máximo es 5.0 MB/ })).toBeVisible();
  await expect(panel.getByText('enorme.pdf')).toHaveCount(0);

  // Se puede quitar uno antes de guardar.
  await panel.getByRole('button', { name: 'Quitar orden-de-compra.pdf' }).click();
  await expect(panel.getByText('orden-de-compra.pdf')).toHaveCount(0);
});

test('en un borrador guardado los PDF suben al elegirlos, se listan y se pueden quitar', async ({ page }) => {
  await openInvoices(page);
  let attachments: any[] = [{ id: 'a1', fileName: 'anexo-previo.pdf', sizeBytes: 2048, createdAt: '2026-10-28T10:00:00' }];
  const uploads: string[] = [];
  const deletes: string[] = [];
  await page.route('**/api/client/invoices?*', route => json(route, [draftRow]));
  await page.route('**/api/client/invoices/dr1', route => json(route, draftFull));
  await page.route('**/api/client/invoices/dr1/attachments', route => {
    if (route.request().method() === 'POST') {
      uploads.push('POST');
      attachments = [...attachments, { id: `a${attachments.length + 1}`, fileName: 'nuevo.pdf', sizeBytes: 1209, createdAt: '2026-10-28T11:00:00' }];
      return json(route, attachments[attachments.length - 1]);
    }
    return json(route, attachments);
  });
  await page.route('**/api/client/invoices/dr1/attachments/*', route => {
    deletes.push(route.request().method());
    attachments = attachments.filter(a => !route.request().url().endsWith(a.id));
    return route.fulfill({ status: 204 });
  });

  await page.goto('/invoices');
  await page.waitForLoadState('networkidle');
  await page.getByRole('button', { name: /Editar/ }).first().click();

  const panel = page.getByTestId('document-attachments');
  await expect(panel.getByText('anexo-previo.pdf')).toBeVisible();

  await page.getByTestId('attachments-input').setInputFiles(pdf('nuevo.pdf'));
  await expect(panel.getByText('nuevo.pdf')).toBeVisible();
  expect(uploads).toHaveLength(1);

  await panel.getByRole('button', { name: 'Quitar anexo-previo.pdf' }).click();
  await expect(panel.getByText('anexo-previo.pdf')).toHaveCount(0);
  expect(deletes).toEqual(['DELETE']);
});

test('el detalle de un documento emitido lista los adjuntos para descargar, sin poder cambiarlos', async ({ page }) => {
  await openInvoices(page);
  const approvedRow = { ...draftRow, id: 'ap1', number: '55', status: 'APPROVED' };
  await page.route('**/api/client/invoices?*', route => json(route, [approvedRow]));
  await page.route('**/api/client/invoices/ap1', route => json(route, { ...draftFull, id: 'ap1', number: '55', status: 'APPROVED', customer: { name: 'Cliente', identificationNumber: '1' } }));
  let downloads = 0;
  await page.route('**/api/client/invoices/ap1/attachments', route => json(route, [{ id: 'z1', fileName: 'orden.pdf', sizeBytes: 4096, createdAt: '2026-10-28T10:00:00' }]));
  await page.route('**/api/client/invoices/ap1/attachments/z1', route => {
    downloads++;
    return route.fulfill({ status: 200, contentType: 'application/pdf', body: '%PDF-1.4 contenido' });
  });

  await page.goto('/invoices');
  await page.waitForLoadState('networkidle');
  await page.getByRole('button', { name: /Ver detalle/ }).first().click();

  const panel = page.getByTestId('document-attachments');
  await expect(panel.getByText('orden.pdf')).toBeVisible();
  await expect(panel.getByRole('button', { name: 'Quitar orden.pdf' })).toHaveCount(0);
  await expect(panel.getByRole('button', { name: 'Agregar PDF' })).toHaveCount(0);

  await panel.getByRole('button', { name: 'Descargar orden.pdf' }).click();
  await expect.poll(() => downloads).toBe(1);
});
