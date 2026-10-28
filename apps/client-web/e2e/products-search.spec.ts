import { test, expect } from '@playwright/test';
import { mockApi, signIn } from './mockApi';

// Los productos no se cargan en bloque: la lista de Productos se pide por páginas y el selector de las líneas de un documento los busca al escribir.

const TOTAL = 90;
const make = (i: number) => ({
  id: `q${i}`, code: `SKU-${String(i).padStart(3, '0')}`, name: `Producto ${String(i).padStart(3, '0')}`, unitPrice: 1000, ivaTreatment: 'Gravado', ivaRate: 19,
  taxes: [], unitOfMeasureId: '30000000-0000-0000-0000-000000000001', scope: 'Invoice', taxesPending: i === 3,
});

test('la lista de Productos pide al servidor cada página y búsqueda, y avisa los pendientes', async ({ page }) => {
  await page.setViewportSize({ width: 1800, height: 900 });
  await signIn(page);
  await mockApi(page);
  const requests: URLSearchParams[] = [];
  await page.route('**/api/client/products?*', route => {
    const params = new URL(route.request().url()).searchParams;
    requests.push(params);
    const q = (params.get('q') ?? '').toLowerCase();
    const size = Number(params.get('pageSize'));
    const pageNumber = Number(params.get('page'));
    const all = Array.from({ length: TOTAL }, (_, i) => make(i + 1)).filter(p => !q || p.name.toLowerCase().includes(q) || p.code.toLowerCase().includes(q));
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ total: all.length, pendingCount: 1, items: all.slice((pageNumber - 1) * size, pageNumber * size) }) });
  });

  await page.goto('/products');
  await page.waitForLoadState('networkidle');
  await expect(page.getByText(/Mostrando 1–25 de 90/)).toBeVisible();
  await expect(page.locator('table tbody tr')).toHaveCount(25);
  expect(requests[requests.length - 1].get('scope')).toBe('Invoice');
  await expect(page.getByText('1 producto con impuestos por completar.')).toBeVisible();

  await page.getByRole('button', { name: 'Página siguiente' }).click();
  await expect(page.getByText(/Mostrando 26–50 de 90/)).toBeVisible();
  expect(requests[requests.length - 1].get('page')).toBe('2');

  await page.getByPlaceholder('Buscar por código o nombre...').fill('SKU-07');
  await expect.poll(() => requests.some(r => r.get('q') === 'SKU-07')).toBe(true);
  await expect(page.getByText(/Mostrando 1–10 de 10/)).toBeVisible();

  await page.getByRole('button', { name: 'Ver solo esos' }).click();
  await expect.poll(() => requests.some(r => r.get('onlyPending') === 'true')).toBe(true);
});

test('el formulario de factura no baja el catálogo: el selector de la línea busca al escribir', async ({ page }) => {
  await page.setViewportSize({ width: 1800, height: 1000 });
  await signIn(page);
  await mockApi(page);
  const searches: string[] = [];
  let catalogLoads = 0;
  page.on('request', req => {
    const url = new URL(req.url());
    if (url.pathname.endsWith('/client/products/search')) searches.push(`${url.searchParams.get('scope')}|${url.searchParams.get('q') ?? ''}`);
    if (url.pathname.endsWith('/client/products') && req.method() === 'GET') catalogLoads++;
  });
  await page.goto('/invoices');
  await page.waitForLoadState('networkidle');
  expect(catalogLoads).toBe(0);

  await page.getByRole('button', { name: /Nueva Factura/ }).click();
  await page.getByRole('button', { name: 'Agregar Ítem' }).click();
  const input = page.getByPlaceholder('Buscar por código o nombre...').filter({ visible: true }).first();
  await input.fill('producto 2');
  await expect.poll(() => searches.includes('Invoice|producto 2')).toBe(true);
  await page.getByText('SKU-2 - Producto 2').click();
  await expect(input).toHaveValue('SKU-2');
});
