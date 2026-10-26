import { test, expect, type Page } from '@playwright/test';
import { mockApi, signIn } from './mockApi';

// El producto de una línea se corrige sin salir del documento: el IVA nuevo se ve en todas las líneas que usan ese producto.

const exemptProduct = {
  id: 'px', code: 'SKU-X', name: 'Producto cargado como exento', unitPrice: 100000, ivaTreatment: 'Exento', ivaRate: 0, priceIncludesIva: false,
  taxes: [], unitOfMeasureId: '30000000-0000-0000-0000-000000000001', scope: 'Invoice',
};
const taxedProduct = { ...exemptProduct, ivaTreatment: 'Gravado', ivaRate: 19 };

// Celda de IVA de las líneas con producto del catálogo (solo lectura), en el diseño que está a la vista.
const ivaCells = (page: Page, text: RegExp) =>
  page.locator('[title="El IVA lo define el producto del catálogo"]').filter({ visible: true }).filter({ hasText: text });

async function openNewInvoiceWithProduct(page: Page) {
  let saved = false;
  let savedBody: any = null;
  await signIn(page);
  await mockApi(page);
  await page.route('**/api/client/products**', route => {
    const url = new URL(route.request().url());
    const method = route.request().method();
    if (method === 'PUT') {
      saved = true;
      savedBody = route.request().postDataJSON();
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ id: 'px' }) });
    }
    if (url.pathname.endsWith('/client/products/px')) {
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(saved ? taxedProduct : exemptProduct) });
    }
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([saved ? taxedProduct : exemptProduct]) });
  });
  await page.setViewportSize({ width: 1800, height: 1000 });
  await page.goto('/invoices');
  await page.waitForLoadState('networkidle');
  await page.getByRole('button', { name: 'Nueva Factura' }).click();

  const pickProductInLine = async (line: number) => {
    const input = page.getByPlaceholder('Buscar por código o nombre...').filter({ visible: true }).nth(line);
    await input.fill('SKU-X');
    await page.getByText('SKU-X - Producto cargado como exento').click();
  };
  return { pickProductInLine, state: () => ({ saved, savedBody }) };
}

test('corregir el IVA del producto desde la línea actualiza todas las líneas con ese producto', async ({ page }) => {
  const { pickProductInLine, state } = await openNewInvoiceWithProduct(page);

  // Dos líneas con el mismo producto, que llegó como exento.
  await page.getByRole('button', { name: 'Agregar Ítem' }).click();
  await pickProductInLine(0);
  await page.getByRole('button', { name: 'Agregar Ítem' }).click();
  await pickProductInLine(1);
  await expect(ivaCells(page, /^Exento$/)).toHaveCount(2);

  // Se abre el producto desde la primera línea.
  await page.getByRole('button', { name: 'Editar producto' }).filter({ visible: true }).first().click();
  const dialog = page.getByRole('dialog');
  await expect(dialog.getByText('Editar Producto')).toBeVisible();
  await expect(dialog.getByText('Los documentos ya emitidos no se modifican.')).toBeVisible();
  await expect(dialog.locator('input[type="text"]').first()).toHaveValue('SKU-X');

  await dialog.getByRole('button', { name: 'Gravado', exact: true }).click();
  await dialog.getByRole('button', { name: 'Guardar Producto' }).click();

  await expect(dialog).toBeHidden();
  expect(state().saved).toBe(true);
  expect(state().savedBody).toMatchObject({ ivaTreatment: 'Gravado', ivaRate: 19, code: 'SKU-X' });
  // Las dos líneas toman el IVA nuevo; el precio que traían no se toca.
  await expect(ivaCells(page, /Gravado 19%/)).toHaveCount(2);
  await expect(ivaCells(page, /^Exento$/)).toHaveCount(0);
});

test('cancelar la edición del producto no cambia nada', async ({ page }) => {
  const { pickProductInLine, state } = await openNewInvoiceWithProduct(page);
  await page.getByRole('button', { name: 'Agregar Ítem' }).click();
  await pickProductInLine(0);

  await page.getByRole('button', { name: 'Editar producto' }).filter({ visible: true }).first().click();
  const dialog = page.getByRole('dialog');
  await dialog.getByRole('button', { name: 'Gravado', exact: true }).click();
  await dialog.getByRole('button', { name: 'Cancelar' }).click();

  await expect(dialog).toBeHidden();
  expect(state().saved).toBe(false);
  await expect(ivaCells(page, /^Exento$/)).toHaveCount(1);
});
