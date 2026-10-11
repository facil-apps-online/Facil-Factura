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

test.describe('estados de nómina', () => {
  test('la lista muestra los estados en español, como facturas y documentos soporte', async ({ page }) => {
    await openPayroll(page);
    const table = page.locator('table').first();
    await expect(table.getByText('Borrador').first()).toBeVisible();
    await expect(table.getByText('Emitida').first()).toBeVisible();
    await expect(table.getByText('Rechazada').first()).toBeVisible();
    for (const english of ['DRAFT', 'APPROVED', 'REJECTED']) await expect(table.getByText(english, { exact: true })).toHaveCount(0);
  });

  test('un borrador no muestra consecutivo en la lista', async ({ page }) => {
    await openPayroll(page);
    // El mock trae consecutivos "NE0"... para todas; el servidor real los entrega solo en las emitidas, y la pantalla muestra "-" si no hay.
    await page.route('**/api/client/payroll?*', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([
      { id: 'x1', status: 'DRAFT', consecutiveNumber: null, typeCode: 'NE', issueDate: '2026-10-01T00:00:00', totalAmount: 1, customer: { name: 'Empleado Borrador' }, initialSettlementDate: '2026-09-01T00:00:00', finalSettlementDate: '2026-09-30T00:00:00' },
    ]) }));
    await page.reload();
    await page.waitForLoadState('networkidle');
    const row = page.locator('table').first().locator('tr', { hasText: 'Empleado Borrador' });
    await expect(row.locator('td').nth(1)).toHaveText('-');
  });
});

test.describe('nómina igual que facturas', () => {
  const entries = [
    { id: 'p1', status: 'DRAFT', consecutiveNumber: null, typeCode: 'NE', issueDate: '2026-10-01T00:00:00', totalAmount: 800, totalAccruals: 1000, totalDeductions: 200, customer: { name: 'Ana Borrador' }, initialSettlementDate: '2026-09-01T00:00:00', finalSettlementDate: '2026-09-30T00:00:00' },
    { id: 'p2', status: 'REJECTED', consecutiveNumber: null, typeCode: 'NE', issueDate: '2026-10-02T00:00:00', totalAmount: 1500, totalAccruals: 1700, totalDeductions: 200, customer: { name: 'Beto Rechazado' }, initialSettlementDate: '2026-09-01T00:00:00', finalSettlementDate: '2026-09-30T00:00:00',
      dianResponseMessage: JSON.stringify({ errors: [{ path: ['employee', 'address', 'city'], error: 'Ciudad requerida' }] }) },
    { id: 'p3', status: 'APPROVED', consecutiveNumber: 'NE-7', typeCode: 'NE', issueDate: '2026-10-03T00:00:00', totalAmount: 2000, totalAccruals: 2300, totalDeductions: 300, customer: { name: 'Cata Emitida' }, initialSettlementDate: '2026-09-01T00:00:00', finalSettlementDate: '2026-09-30T00:00:00' },
  ];

  async function open(page: Page) {
    await page.setViewportSize({ width: 1800, height: 900 });
    await signIn(page);
    await mockApi(page);
    await page.route('**/api/client/payroll?*', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(entries) }));
    await page.goto('/payroll');
    await page.waitForLoadState('networkidle');
  }

  test('el filtro por estado deja solo las nóminas de ese estado', async ({ page }) => {
    await open(page);
    const table = page.locator('table').first();
    await expect(table.getByText('Ana Borrador')).toBeVisible();
    await expect(table.getByText('Cata Emitida')).toBeVisible();

    const estado = page.locator('label', { hasText: 'Estado' }).locator('xpath=..');
    await estado.locator('input').click();
    await page.locator('div[style*="position: fixed"]').getByText('Rechazada', { exact: true }).click();
    await expect(table.getByText('Beto Rechazado')).toBeVisible();
    await expect(table.getByText('Ana Borrador')).toHaveCount(0);
    await expect(table.getByText('Cata Emitida')).toHaveCount(0);
  });

  test('el motivo del rechazo se abre en el mismo diálogo que en facturas', async ({ page }) => {
    await open(page);
    await page.getByRole('button', { name: 'Ver motivo del rechazo' }).click();
    const dialog = page.getByRole('dialog');
    await expect(dialog.getByText('Motivo del rechazo')).toBeVisible();
    await expect(dialog.getByText('employee → address → city')).toBeVisible();
    await expect(dialog.getByText('Ciudad requerida')).toBeVisible();
  });

  test('la vista previa de impresión pide el PDF de la nómina', async ({ page }) => {
    await open(page);
    let requested = '';
    await page.route('**/api/client/payroll/p3/preview', route => { requested = route.request().url(); return route.fulfill({ status: 200, contentType: 'application/pdf', body: '%PDF-1.4' }); });
    await page.context().route('blob:**', route => route.abort());
    await page.locator('table').first().locator('tr', { hasText: 'Cata Emitida' }).getByRole('button', { name: 'Vista previa de impresión' }).click();
    await expect.poll(() => requested).toContain('/payroll/p3/preview');
  });

  test('el total del período desglosa devengado, deducciones y neto', async ({ page }) => {
    await open(page);
    const totals = page.getByText('Total del período').locator('xpath=..');
    await expect(totals).toContainText('Devengado');
    await expect(totals).toContainText('Deducciones');
    await expect(totals).toContainText('Neto');
  });
});

test.describe('anular y modificar nómina', () => {
  const period = { initialSettlementDate: '2026-09-01T00:00:00', finalSettlementDate: '2026-09-30T00:00:00' };
  const entries = [
    { id: 'v1', status: 'APPROVED', consecutiveNumber: 'NE-7', typeCode: 'NE', issueDate: '2026-10-03T00:00:00', totalAmount: 2000, totalAccruals: 2300, totalDeductions: 300, customer: { name: 'Cata Vigente' }, ...period },
    { id: 'a1', status: 'APPROVED', consecutiveNumber: 'NE-8', typeCode: 'NE', issueDate: '2026-10-04T00:00:00', totalAmount: 5000, totalAccruals: 5500, totalDeductions: 500, annulledBy: 'NE-ELIMINACION', customer: { name: 'Dani Anulada' }, ...period },
    { id: 'a2', status: 'APPROVED', consecutiveNumber: 'NE-9', typeCode: 'NE', issueDate: '2026-10-05T00:00:00', totalAmount: 3000, totalAccruals: 3300, totalDeductions: 300, annulledBy: 'NE-REEMPLAZO', customer: { name: 'Eva Reemplazada' }, ...period },
    { id: 'n1', status: 'APPROVED', consecutiveNumber: 'NE-10', typeCode: 'NE-ELIMINACION', issueDate: '2026-10-06T00:00:00', totalAmount: 5000, totalAccruals: 5500, totalDeductions: 500, customer: { name: 'Dani Anulada' }, ...period },
    { id: 'd1', status: 'DRAFT', consecutiveNumber: null, typeCode: 'NE', issueDate: '2026-10-07T00:00:00', totalAmount: 100, totalAccruals: 100, totalDeductions: 0, customer: { name: 'Fabio Borrador' }, ...period },
  ];
  const detail = (e: any) => ({ ...e, number: e.consecutiveNumber, employeeId: 'emp1', prefix: 'NE', accruals: [{ description: 'Salario', amount: 1000 }], deductions: [], referenceDocumentId: null, referenceConcept: '' });

  async function open(page: Page) {
    await page.setViewportSize({ width: 1800, height: 900 });
    await signIn(page);
    await mockApi(page);
    await page.route('**/api/client/payroll?*', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(entries) }));
    for (const e of entries) {
      await page.route(`**/api/client/payroll/${e.id}`, route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(detail(e)) }));
      await page.route(`**/api/client/payroll/${e.id}/related`, route => route.fulfill({ status: 200, contentType: 'application/json', body: '[]' }));
    }
    await page.goto('/payroll');
    await page.waitForLoadState('networkidle');
  }

  test('lo anulado se ve como Anulada o Reemplazada y no suma al total del periodo', async ({ page }) => {
    await open(page);
    const table = page.locator('table').first();
    await expect(table.locator('tr', { hasText: 'Dani Anulada' }).first().getByText('Anulada', { exact: true })).toBeVisible();
    await expect(table.locator('tr', { hasText: 'Eva Reemplazada' }).getByText('Reemplazada', { exact: true })).toBeVisible();
    await expect(table.locator('tr', { hasText: 'Cata Vigente' }).getByText('Emitida', { exact: true })).toBeVisible();

    // Solo suman la vigente (2000) y el borrador (100): ni las notas ni lo anulado (5000 y 3000).
    const totals = page.getByText('Total del período').locator('xpath=..');
    await expect(totals).toContainText('Devengado');
    await expect(totals).toContainText(/\$2[.,]400/);
    await expect(totals).toContainText(/\$2[.,]100/);
    await expect(totals).not.toContainText(/\$10[.,]/);
  });

  test('una nómina vigente ofrece anular y modificar; una anulada no', async ({ page }) => {
    await open(page);
    await page.getByRole('button', { name: /Ver detalle de nómina NE-7/ }).click();
    await expect(page.getByRole('button', { name: 'Generar Nota de Reemplazo' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Generar Nota de Eliminación' })).toBeVisible();

    await page.getByRole('button', { name: /Volver/ }).click();
    await page.getByRole('button', { name: /Ver detalle de nómina NE-8/ }).first().click();
    await expect(page.getByText('Anulada', { exact: true }).first()).toBeVisible();
    await expect(page.getByRole('button', { name: 'Generar Nota de Reemplazo' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Generar Nota de Eliminación' })).toHaveCount(0);
  });

  test('la nota de reemplazo pide solo el motivo y explica que se creará un borrador nuevo', async ({ page }) => {
    await open(page);
    await page.getByRole('button', { name: /Ver detalle de nómina NE-7/ }).click();
    await page.getByRole('button', { name: 'Generar Nota de Reemplazo' }).click();
    await expect(page.getByRole('heading', { name: 'Nota de Reemplazo de Nómina' })).toBeVisible();
    await expect(page.getByText('se crea un borrador nuevo con los mismos datos')).toBeVisible();
    await expect(page.getByText('Motivo', { exact: true })).toBeVisible();
    await expect(page.getByText('Devengado:')).toHaveCount(0);
  });

  test('al emitir una nota de reemplazo se abre el borrador nuevo para corregirlo', async ({ page }) => {
    await open(page);
    const nuevo = { id: 'nuevo1', status: 'DRAFT', typeCode: 'NE', employeeId: 'emp1', prefix: 'NE', totalAmount: 1000, accruals: [{ description: 'Salario', amount: 1000 }], deductions: [], referenceDocumentId: 'v1', referenceConcept: '', ...period };
    await page.route('**/api/client/payroll/d1/publish', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ newDraftId: 'nuevo1' }) }));
    await page.route('**/api/client/payroll/nuevo1', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(nuevo) }));
    await page.locator('table').first().locator('tr', { hasText: 'Fabio Borrador' }).getByRole('button', { name: /Emitir/ }).click();
    await expect(page.getByRole('heading', { name: 'Editar Nómina Electrónica' })).toBeVisible();
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

  test('la lista de empleados muestra contrato, salario y municipio, y avisa cuando falta el DANE', async ({ page }) => {
    // La tabla de terceros aparece desde 1400 px; por debajo se ven tarjetas.
    await page.setViewportSize({ width: 1500, height: 900 });
    await signIn(page);
    await mockApi(page);
    await page.route('**/api/client/customers?partyType=Empleado', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([
      { id: 'e1', name: 'Ana Con Dane', identificationType: '13', identificationNumber: '1001', partyType: 'Empleado', contractType: 'TERMINO_INDEFINIDO', workerType: 'DEPENDIENTE', baseSalary: 1300000, cityName: 'Bogotá', cityCode: '11001' },
      { id: 'e2', name: 'Beto Sin Dane', identificationType: '13', identificationNumber: '1002', partyType: 'Empleado', contractType: 'TERMINO_INDEFINIDO', workerType: 'DEPENDIENTE', baseSalary: 1300000, cityName: '', cityCode: '' }
    ]) }));
    await page.goto('/customers');
    await page.waitForLoadState('networkidle');
    await page.getByRole('button', { name: 'Empleados', exact: true }).click();

    const table = page.locator('table').first();
    await expect(table.getByText('Salario base')).toBeVisible();
    await expect(table.getByText('Régimen')).toHaveCount(0);
    await expect(table.locator('tr', { hasText: 'Ana Con Dane' }).getByText('11001')).toBeVisible();
    await expect(table.locator('tr', { hasText: 'Beto Sin Dane' }).getByText('Falta código DANE')).toBeVisible();
    await expect(table.locator('tr', { hasText: 'Ana Con Dane' }).getByText('Falta código DANE')).toHaveCount(0);
  });

  test('el código DANE es obligatorio para un empleado y para un cliente no', async ({ page }) => {
    await signIn(page);
    await mockApi(page);
    await page.goto('/customers');
    await page.waitForLoadState('networkidle');

    await page.getByRole('button', { name: 'Nuevo Cliente' }).click();
    await expect(page.getByPlaceholder('Ej. 11001')).not.toHaveAttribute('required', '');
    await expect(page.getByText('Obligatorio para empleados')).toHaveCount(0);
    await page.getByLabel('Cerrar').click();
    await expect(page.getByRole('dialog')).toBeHidden();

    await page.getByRole('button', { name: 'Empleados', exact: true }).click();
    await page.getByRole('button', { name: 'Nuevo Empleado' }).click();
    await expect(page.getByPlaceholder('Ej. 11001')).toHaveAttribute('required', '');
    await expect(page.getByText('Obligatorio para empleados')).toBeVisible();
    await page.getByPlaceholder('Ej. 11001').fill('11001');
    await expect(page.getByText('Obligatorio para empleados')).toHaveCount(0);
  });
});
