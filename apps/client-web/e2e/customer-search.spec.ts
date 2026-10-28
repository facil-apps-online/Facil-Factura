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
});
