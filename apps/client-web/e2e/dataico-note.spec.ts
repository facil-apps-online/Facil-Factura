import { test, expect, type Page } from '@playwright/test';
import { ADMIN_SESSION, mockApi, signIn } from './mockApi';

// Nota crédito desde una factura que solo existe en Dataico: el servidor crea el tercero y los productos que falten y el portal abre el borrador
// con ellos; si algo falla, se muestra la causa real y no un texto genérico.

const DATAICO_SESSION = { ...ADMIN_SESSION, features: { ...ADMIN_SESSION.features, dataico: true } };

async function openLookup(page: Page) {
  await signIn(page);
  await mockApi(page, DATAICO_SESSION);
  await page.route('**/api/client/invoices/document-types', route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([{ id: 'dt1', code: '01', typeCode: 'FE-STD', name: 'Factura electrónica de venta' }, { id: 'dt-nc', code: '91', typeCode: 'NC', name: 'Nota crédito' }]) }));
  await page.route('**/api/client/invoices/dataico-lookup?*', route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ number: 'FEG1', uuid: 'u1', status: 'DIAN_ACEPTADO', issueDate: '2026-10-20', customer: 'PEPITO PEREZ (80123456)', total: '119,000.00', messages: [], hasXml: true, hasPdf: true }) }));
  await page.goto('/invoices');
  await page.waitForLoadState('networkidle');
  await page.getByRole('button', { name: 'Consultar factura' }).click();
  await page.getByPlaceholder('Ej. FEG92010').fill('FEG1');
  await page.getByRole('button', { name: 'Consultar', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Nota crédito' })).toBeEnabled();
}

test.describe('nota crédito desde una factura de Dataico', () => {
  test('crea la nota con el tercero y los productos que el servidor creó y avisa lo que hizo', async ({ page }) => {
    await openLookup(page);
    let method = '';
    await page.route('**/api/client/invoices/dataico-lookup/note-draft*', route => {
      method = route.request().method();
      return route.fulfill({
        status: 200, contentType: 'application/json',
        body: JSON.stringify({
          draft: {
            dataicoDocumentId: 'u1', number: 'FEG1', cufe: 'c', issueDate: '2026-10-20T00:00:00', currency: 'COP', paymentMeans: '10', paymentMeansType: 'CONTADO', paymentTermDays: null,
            customerTaxId: '80123456', customerName: 'PEPITO PEREZ', subtotal: 100000, taxAmount: 19000, totalAmount: 119000,
            items: [{ code: 'PRUEBA', name: 'Servicio de prueba', quantity: 1, unitPrice: 100000, unitOfMeasureCode: '94', unitOfMeasureAbbreviation: 'EA', ivaTreatment: 'Gravado', taxRate: 19, discountRate: 0, taxAmount: 19000, totalAmount: 100000 }],
          },
          customerId: 'c0', customerCreated: true, productIds: ['p0'], createdProducts: ['PRUEBA · Servicio de prueba'],
          notes: ['El adquirente no estaba en tus terceros: se creó con los datos de la factura. Completa nombres y apellidos si es persona natural.', 'Se crearon 1 producto con los datos de la factura (código, nombre, precio e IVA). Revísalos en Productos.'],
        }),
      });
    });

    await page.getByRole('button', { name: 'Nota crédito' }).click();
    // Crear el tercero y los productos es una escritura: va por POST.
    await expect.poll(() => method).toBe('POST');
    await expect(page.getByText('Generando Nota Crédito')).toBeVisible();
    await expect(page.getByText('El adquirente no estaba en tus terceros')).toBeVisible();
    await expect(page.getByText('Se crearon 1 producto')).toBeVisible();
  });

  test('si el servidor no puede armar la factura muestra su causa, no el texto genérico', async ({ page }) => {
    await openLookup(page);
    await page.route('**/api/client/invoices/dataico-lookup/note-draft*', route =>
      route.fulfill({ status: 422, contentType: 'application/json', body: JSON.stringify({ message: 'No se pudo interpretar el XML de Dataico: algo concreto.' }) }));

    await page.getByRole('button', { name: 'Nota crédito' }).click();
    await expect(page.getByText('No se pudo interpretar el XML de Dataico: algo concreto.')).toBeVisible();
    await expect(page.getByText('No se pudo reconstruir la factura consultada en Dataico')).toHaveCount(0);
  });
});
