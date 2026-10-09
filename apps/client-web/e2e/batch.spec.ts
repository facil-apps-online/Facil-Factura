import { test, expect, type Page } from '@playwright/test';
import { mockApi, signIn } from './mockApi';

// Acciones masivas (emitir o eliminar varios a la vez) y formulario de empleados. La API se simula: el servidor real responde una línea
// JSON por documento (ndjson); aquí se entrega completa porque lo que se prueba es la pantalla.

const ndjson = (lines: object[]) => lines.map(l => JSON.stringify(l)).join('\n') + '\n';

async function openPayroll(page: Page) {
  await page.setViewportSize({ width: 1800, height: 900 });
  await signIn(page);
  await mockApi(page);
  await page.goto('/payroll');
  await page.waitForLoadState('networkidle');
}

const box = (page: Page, number: string) => page.getByRole('checkbox', { name: `Seleccionar nómina ${number}` });

test.describe('acciones masivas en nómina', () => {
  test('sin selección no hay barra; al marcar borradores se habilitan Emitir y Eliminar', async ({ page }) => {
    await openPayroll(page);
    await expect(page.getByRole('region', { name: 'Acciones masivas' })).toHaveCount(0);

    await box(page, 'NE0').check();
    await box(page, 'NE3').check();
    const bar = page.getByRole('region', { name: 'Acciones masivas' });
    await expect(bar).toContainText('2 seleccionados');
    await expect(bar.getByRole('button', { name: 'Emitir (2)' })).toBeEnabled();
    await expect(bar.getByRole('button', { name: 'Eliminar (2)' })).toBeEnabled();
  });

  test('si hay una emitida entre las marcadas, los botones se deshabilitan y explican por qué', async ({ page }) => {
    await openPayroll(page);
    await box(page, 'NE0').check();
    await box(page, 'NE1').check();
    const bar = page.getByRole('region', { name: 'Acciones masivas' });
    await expect(bar.getByRole('button', { name: 'Emitir (2)' })).toBeDisabled();
    await expect(bar.getByRole('button', { name: 'Eliminar (2)' })).toBeDisabled();
    await expect(bar).toContainText('1 de los seleccionados ya fueron emitidos o están en proceso');

    await box(page, 'NE1').uncheck();
    await expect(bar.getByRole('button', { name: 'Emitir (1)' })).toBeEnabled();
  });

  test('"seleccionar todas" marca las filas de la página y "Quitar selección" las limpia', async ({ page }) => {
    await openPayroll(page);
    await page.getByRole('checkbox', { name: 'Seleccionar todas las filas de la página' }).check();
    await expect(page.getByRole('region', { name: 'Acciones masivas' })).toContainText('5 seleccionados');
    await page.getByRole('button', { name: 'Quitar selección' }).click();
    await expect(page.getByRole('region', { name: 'Acciones masivas' })).toHaveCount(0);
  });

  test('emitir en lote: confirma, envía los ids y muestra el resultado de cada documento', async ({ page }) => {
    await openPayroll(page);
    let sentIds: string[] = [];
    await page.route('**/api/client/payroll/batch/publish', route => {
      sentIds = route.request().postDataJSON().ids;
      return route.fulfill({
        status: 200,
        contentType: 'application/x-ndjson',
        body: ndjson([
          { type: 'start', total: 2 },
          { type: 'item', id: 'n0', ok: true, message: 'Nómina electrónica emitida correctamente.' },
          { type: 'item', id: 'n3', ok: false, message: 'Dataico rechazó la nómina.', detail: 'invalid-key medical-leave-type' },
          { type: 'done', succeeded: 1, failed: 1 },
        ]),
      });
    });

    await box(page, 'NE0').check();
    await box(page, 'NE3').check();
    await page.getByRole('button', { name: 'Emitir (2)' }).click();
    await page.getByRole('alertdialog').getByRole('button', { name: 'Emitir' }).click();

    const dialog = page.getByRole('dialog');
    await expect(dialog).toContainText('2 de 2');
    await expect(dialog).toContainText('Nómina electrónica emitida correctamente.');
    await expect(dialog).toContainText('Dataico rechazó la nómina.');
    await expect(dialog).toContainText('invalid-key medical-leave-type');
    await expect(dialog).toContainText('1 emitido');
    expect(sentIds.sort()).toEqual(['n0', 'n3']);

    await dialog.getByText('Cerrar', { exact: true }).click();
    // El que falló queda seleccionado para corregirlo y reintentar.
    await expect(page.getByRole('region', { name: 'Acciones masivas' })).toContainText('1 seleccionado');
    await expect(box(page, 'NE3')).toBeChecked();
  });

  test('eliminar en lote pide confirmación y no envía nada si se cancela', async ({ page }) => {
    await openPayroll(page);
    let called = false;
    await page.route('**/api/client/payroll/batch/delete', route => { called = true; return route.fulfill({ status: 200, contentType: 'application/x-ndjson', body: ndjson([{ type: 'done', succeeded: 0, failed: 0 }]) }); });

    await box(page, 'NE0').check();
    await page.getByRole('button', { name: 'Eliminar (1)' }).click();
    const confirm = page.getByRole('alertdialog');
    await expect(confirm).toContainText('no se puede deshacer');
    await confirm.getByRole('button', { name: 'Cancelar' }).click();
    expect(called).toBe(false);
  });
});

test.describe('formulario de empleados', () => {
  test('un empleado no muestra régimen fiscal ni nivel tributario; un cliente sí', async ({ page }) => {
    await signIn(page);
    await mockApi(page);
    await page.goto('/customers');
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: 'Nuevo Cliente' }).click();
    await expect(page.getByText('Régimen Fiscal')).toBeVisible();
    await expect(page.getByText('Nivel Tributario')).toBeVisible();
    await page.getByLabel('Cerrar').click();
    await expect(page.getByRole('dialog')).toBeHidden();

    await page.getByRole('button', { name: 'Empleados', exact: true }).click();
    await page.getByRole('button', { name: 'Nuevo Empleado' }).click();
    await expect(page.getByRole('dialog')).toBeVisible();
    await expect(page.getByText('Régimen Fiscal')).toHaveCount(0);
    await expect(page.getByText('Nivel Tributario')).toHaveCount(0);
  });
});
