import { test, expect } from '@playwright/test';
import { mockApi, signIn } from './mockApi';

// Los terceros no se cargan en bloque: el selector del formulario los busca en el servidor al abrirlo y mientras se escribe.

test.describe('selector de terceros con búsqueda en el servidor', () => {
  test('la pantalla de facturas no pide terceros al cargar; el selector busca al abrir y al escribir', async ({ page }) => {
    await page.setViewportSize({ width: 1800, height: 900 });
    await signIn(page);
    await mockApi(page);
    const searches: string[] = [];
    let bulkLoads = 0;
    page.on('request', req => {
      const url = new URL(req.url());
      if (url.pathname.endsWith('/client/customers/search')) searches.push(url.searchParams.get('q') ?? '');
      if (url.pathname.endsWith('/client/customers') && req.method() === 'GET') bulkLoads++;
    });

    await page.goto('/invoices');
    await page.waitForLoadState('networkidle');
    expect(bulkLoads).toBe(0);
    expect(searches).toHaveLength(0);

    await page.getByRole('button', { name: /Nueva Factura/ }).click();
    const input = page.getByPlaceholder('Buscar cliente por nombre o identificación...');
    await input.click();
    await expect.poll(() => searches.length).toBeGreaterThan(0);
    expect(searches[0]).toBe('');

    // Escribir pide la búsqueda al servidor (con una pausa breve) y muestra solo lo que coincide.
    await input.fill('prueba 2');
    await expect.poll(() => searches.includes('prueba 2')).toBe(true);
    const option = page.getByText('Tercero de prueba 2 (900123452)');
    await expect(option).toBeVisible();
    await expect(page.getByText('Tercero de prueba 3 (900123453)')).toHaveCount(0);

    await option.click();
    await expect(input).toHaveValue('Tercero de prueba 2 (900123452)');
  });

  test('el tercero elegido se edita desde el formulario de la factura sin salir ni enviar la factura', async ({ page }) => {
    await page.setViewportSize({ width: 1800, height: 900 });
    await signIn(page);
    await mockApi(page);
    const documentWrites: string[] = [];
    page.on('request', req => {
      const url = new URL(req.url());
      if (url.pathname.includes('/client/invoices') && req.method() !== 'GET') documentWrites.push(`${req.method()} ${url.pathname}`);
    });
    let saved: any = null;
    await page.route('**/api/client/customers/c2', route => {
      if (route.request().method() !== 'PUT') return route.fallback();
      saved = route.request().postDataJSON();
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(saved) });
    });

    await page.goto('/invoices');
    await page.waitForLoadState('networkidle');
    await page.getByRole('button', { name: /Nueva Factura/ }).click();
    const input = page.getByPlaceholder('Buscar cliente por nombre o identificación...');
    await expect(page.getByRole('button', { name: 'Editar cliente' })).toHaveCount(0);

    await input.fill('prueba 2');
    await page.getByText('Tercero de prueba 2 (900123452)').click();
    await page.getByRole('button', { name: 'Editar cliente' }).click();

    const dialog = page.getByRole('dialog');
    await expect(dialog).toContainText('Editar Tercero');
    const name = dialog.locator('label:has-text("Razón Social") + input, label:has-text("Razón Social") ~ input').first();
    await expect(name).toHaveValue('Tercero de prueba 2');
    await name.fill('Tercero corregido SAS');
    await dialog.getByRole('button', { name: /^Guardar/ }).click();

    await expect(dialog).toHaveCount(0);
    expect(saved?.name).toBe('Tercero corregido SAS');
    // El selector muestra el nombre nuevo y la factura no se envió ni se guardó.
    await expect(input).toHaveValue('Tercero corregido SAS (900123452)');
    expect(documentWrites).toEqual([]);
  });

  test('en la nómina el empleado se busca, se crea y se edita desde el mismo formulario', async ({ page }) => {
    await page.setViewportSize({ width: 1800, height: 900 });
    await signIn(page);
    await mockApi(page);
    const searches: string[] = [];
    page.on('request', req => {
      const url = new URL(req.url());
      if (url.pathname.endsWith('/client/customers/search')) searches.push(`${url.searchParams.get('partyType')}|${url.searchParams.get('q') ?? ''}`);
    });
    await page.goto('/payroll');
    await page.waitForLoadState('networkidle');
    expect(searches).toHaveLength(0);

    await page.getByRole('button', { name: /Nueva Nómina/ }).click();
    await expect(page.getByRole('button', { name: 'Crear empleado rápido' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Editar empleado' })).toHaveCount(0);

    const input = page.getByPlaceholder('Buscar empleado...');
    await input.fill('prueba 1');
    await page.getByText('Tercero de prueba 1 (900123451)').click();
    expect(searches).toContain('Empleado|prueba 1');

    await page.getByRole('button', { name: 'Editar empleado' }).click();
    await expect(page.getByRole('dialog')).toContainText('Editar Tercero');
  });
});
