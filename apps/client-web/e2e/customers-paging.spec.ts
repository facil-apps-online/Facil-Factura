import { test, expect } from '@playwright/test';
import { mockApi, signIn } from './mockApi';

// La lista de Terceros se pide por páginas al servidor: busca, ordena y corta allá, y solo trae las filas que se ven.

const TOTAL = 120;
const person = (i: number) => ({
  id: `p${i}`, name: `Tercero ${String(i).padStart(3, '0')}`, identificationType: '31', identificationNumber: `9001${String(i).padStart(5, '0')}`, verificationDigit: '7',
  partyType: 'Cliente', email: `t${i}@correo.com`, phone: '300', taxRegime: '48', countryCode: 'CO', cityCode: '11001', cityName: 'Bogotá', address: 'Calle 1',
});

test('la página de Terceros pide al servidor cada página, búsqueda y orden, y no baja todos los terceros', async ({ page }) => {
  await page.setViewportSize({ width: 1800, height: 900 });
  await signIn(page);
  await mockApi(page);
  const requests: URLSearchParams[] = [];
  await page.route('**/api/client/customers?*', route => {
    const params = new URL(route.request().url()).searchParams;
    requests.push(params);
    const q = (params.get('q') ?? '').toLowerCase();
    const size = Number(params.get('pageSize'));
    const pageNumber = Number(params.get('page'));
    const all = Array.from({ length: TOTAL }, (_, i) => person(i + 1)).filter(p => !q || p.name.toLowerCase().includes(q));
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ total: all.length, items: all.slice((pageNumber - 1) * size, pageNumber * size) }) });
  });

  await page.goto('/customers');
  await page.waitForLoadState('networkidle');

  // Primera página: solo las filas de la página, con el total del servidor.
  await expect(page.getByText(/Mostrando 1–25 de 120/)).toBeVisible();
  await expect(page.locator('table tbody tr')).toHaveCount(25);
  expect(requests.length).toBeLessThanOrEqual(2);
  expect(requests[requests.length - 1].get('page')).toBe('1');
  expect(requests[requests.length - 1].get('partyType')).toBe('Cliente');

  // Página siguiente: pide la 2.
  await page.getByRole('button', { name: 'Página siguiente' }).click();
  await expect(page.getByText(/Mostrando 26–50 de 120/)).toBeVisible();
  expect(requests[requests.length - 1].get('page')).toBe('2');

  // Buscar: vuelve a la página 1 y manda el texto al servidor (con una pausa breve).
  await page.getByPlaceholder(/Buscar por nombre/).fill('Tercero 01');
  await expect.poll(() => requests.some(r => r.get('q') === 'Tercero 01')).toBe(true);
  const searched = requests.filter(r => r.get('q') === 'Tercero 01').pop()!;
  expect(searched.get('page')).toBe('1');
  await expect(page.getByText(/Mostrando 1–\d+ de 10/)).toBeVisible();

  // Limpiar y ordenar por nombre descendente: el orden lo hace el servidor.
  await page.getByRole('button', { name: 'Limpiar búsqueda' }).click();
  await expect(page.getByText(/de 120/)).toBeVisible();
  await page.getByRole('button', { name: /Razón Social/ }).click();
  await expect.poll(() => requests.some(r => r.get('sort') === 'name' && r.get('dir') === 'desc')).toBe(true);
});
