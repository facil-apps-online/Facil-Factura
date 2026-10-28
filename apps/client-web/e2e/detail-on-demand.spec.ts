import { test, expect } from '@playwright/test';
import { mockApi, signIn } from './mockApi';

// Las listas traen solo lo que se ve; la respuesta de la DIAN de una nómina y el historial técnico de un evento se piden al abrirlos.

const RESPONSE = JSON.stringify({ dian_status: 'DIAN_RECHAZADO', dian_messages: ['Regla: NIAE01, Rechazo: El valor es inválido'] });

test('la nómina avisa que hay respuesta y la pide al abrir el motivo', async ({ page }) => {
  await page.setViewportSize({ width: 1800, height: 900 });
  await signIn(page);
  await mockApi(page);
  const row = {
    id: 'nx', consecutiveNumber: 'NE-9', status: 'REJECTED', issueDate: '2026-10-01T00:00:00', initialSettlementDate: '2026-09-01T00:00:00', finalSettlementDate: '2026-09-30T00:00:00',
    totalAmount: 1000000, totalAccruals: 1100000, totalDeductions: 100000, typeCode: 'NE', customer: { id: 'e1', name: 'Empleado Uno', identificationNumber: '1001' }, hasDianResponse: true,
  };
  await page.route('**/api/client/payroll?*', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([row]) }));
  let responseRequests = 0;
  await page.route('**/api/client/payroll/nx/dian-response', route => {
    responseRequests++;
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ dianResponseMessage: RESPONSE }) });
  });
  await page.goto('/payroll');
  await page.waitForLoadState('networkidle');
  expect(responseRequests).toBe(0);

  await page.getByRole('button', { name: /Ver motivo del rechazo|Motivo del rechazo/ }).first().click();
  const dialog = page.getByRole('dialog');
  await expect(dialog).toContainText('Motivo del rechazo');
  await expect(dialog).toContainText('NIAE01');
  expect(responseRequests).toBe(1);
});

test('en documentos recibidos el historial de un evento se pide al abrirlo y trae la respuesta técnica', async ({ page }) => {
  await page.setViewportSize({ width: 1800, height: 900 });
  await signIn(page);
  await mockApi(page);
  const doc = {
    id: 'rdx', issuerName: 'Proveedor Uno', issuerTaxId: '900111222', documentId: 'FE-1', issueDate: '2026-10-01T00:00:00', totalAmount: 1000, sourceType: 'Email', isCredit: true, isSupportDocument: false,
    // Como el servidor: en la lista el evento no trae la respuesta técnica ni los enlaces.
    events: [{ id: 'ev1', eventCode: '030', status: 'SENT', sentAt: '2026-10-02T10:00:00', eventNumber: 5, dianResponseMessage: 'Recibido' }],
  };
  await page.route('**/api/client/received-documents?*', route => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ items: [doc], total: 1 }) }));
  let historyRequests = 0;
  await page.route('**/api/client/received-documents/rdx/events', route => {
    historyRequests++;
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([{ ...doc.events[0], cude: 'k', providerUid: 'u', providerResponse: '{"detalle":"respuesta técnica completa"}', providerXmlUrl: 'https://x/xml', providerPdfUrl: null }]) });
  });
  await page.goto('/received-documents');
  await page.waitForLoadState('networkidle');
  expect(historyRequests).toBe(0);

  await page.getByRole('button', { name: /Ver historial de/ }).first().click();
  await expect(page.getByRole('dialog')).toContainText('Respuesta técnica de Dataico');
  expect(historyRequests).toBe(1);
});

test('documentos recibidos se piden por páginas: búsqueda y página siguiente van al servidor', async ({ page }) => {
  await page.setViewportSize({ width: 1800, height: 900 });
  await signIn(page);
  await mockApi(page);
  const requests: URLSearchParams[] = [];
  const TOTAL = 60;
  await page.route('**/api/client/received-documents?*', route => {
    const params = new URL(route.request().url()).searchParams;
    requests.push(params);
    const q = (params.get('q') ?? '').toLowerCase();
    const size = Number(params.get('pageSize'));
    const pageNumber = Number(params.get('page'));
    const all = Array.from({ length: TOTAL }, (_, i) => ({
      id: `r${i}`, issuerName: `Proveedor ${String(i + 1).padStart(2, '0')}`, issuerTaxId: '900111222', documentId: `FE-${i + 1}`, issueDate: '2026-10-01T00:00:00', totalAmount: 1000, sourceType: 'Email', isCredit: false, isSupportDocument: false, events: [],
    })).filter(d => !q || d.issuerName.toLowerCase().includes(q));
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ items: all.slice((pageNumber - 1) * size, pageNumber * size), total: all.length }) });
  });
  await page.goto('/received-documents');
  await page.waitForLoadState('networkidle');
  await expect(page.getByText(/Mostrando 1–25 de 60/)).toBeVisible();
  expect(requests[requests.length - 1].get('page')).toBe('1');

  await page.getByRole('button', { name: 'Página siguiente' }).click();
  await expect(page.getByText(/Mostrando 26–50 de 60/)).toBeVisible();
  expect(requests[requests.length - 1].get('page')).toBe('2');

  await page.getByPlaceholder(/Buscar por proveedor/).fill('Proveedor 07');
  await expect.poll(() => requests.some(r => r.get('q') === 'Proveedor 07')).toBe(true);
  await expect(page.getByText(/de 1\b/)).toBeVisible();
});
