import { test, expect, type Page } from '@playwright/test';
import { mockApi, signIn } from './mockApi';

// Matriz mínima de viewports del plan responsive (docs/plan-responsive-client-portal.md).
const VIEWPORTS = [
  { name: '320x568', width: 320, height: 568 },
  { name: '375x667', width: 375, height: 667 },
  { name: '390x844', width: 390, height: 844 },
  { name: '667x375', width: 667, height: 375 },
  { name: '768x1024', width: 768, height: 1024 },
  { name: '1024x768', width: 1024, height: 768 },
  { name: '1280x800', width: 1280, height: 800 },
  { name: '1440x900', width: 1440, height: 900 },
  { name: '1920x1080', width: 1920, height: 1080 },
];

const PUBLIC_ROUTES = ['/login?tenant=demo', '/forgot-password?tenant=demo', '/reset-password?token=abc'];
const PORTAL_ROUTES = ['/', '/invoices', '/support-documents', '/payroll', '/received-documents', '/customers', '/products', '/payments', '/resolutions', '/settings', '/users'];

// Falla si la página (o el contenedor con scroll del shell) es más ancha que la pantalla. El scroll
// horizontal solo se admite dentro de tablas o del lienzo del diseñador, nunca en la página.
async function expectNoHorizontalOverflow(page: Page, label: string) {
  const result = await page.evaluate(() => {
    const doc = document.documentElement;
    const vw = doc.clientWidth;
    const scroller = document.querySelector('main > div.overflow-auto') as HTMLElement | null;
    const pageOver = doc.scrollWidth > vw + 1;
    const shellOver = !!scroller && scroller.scrollWidth > scroller.clientWidth + 1;
    if (!pageOver && !shellOver) return { over: false, detail: '', offenders: [] as string[] };
    const limit = pageOver ? vw : scroller!.getBoundingClientRect().right;
    const offenders: string[] = [];
    document.querySelectorAll('body *').forEach(el => {
      const r = (el as HTMLElement).getBoundingClientRect();
      if (r.width > 0 && r.right > limit + 1) {
        const cls = (el.getAttribute('class') || '').split(/\s+/).slice(0, 4).join('.');
        offenders.push(`${el.tagName.toLowerCase()}.${cls} right=${Math.round(r.right)}`);
      }
    });
    return {
      over: true,
      detail: pageOver ? `página scrollWidth=${doc.scrollWidth} > ${vw}` : `contenedor scrollWidth=${scroller!.scrollWidth} > ${scroller!.clientWidth}`,
      offenders: offenders.slice(0, 8),
    };
  });
  expect(result.over, `${label}: ${result.detail}. Elementos: ${result.offenders.join(' | ')}`).toBe(false);
}

// Espera a que terminen las animaciones de entrada (zoom/slide): medir en pleno movimiento da falsos positivos.
async function settle(page: Page, selector: string) {
  await page.locator(selector).first().evaluate(el => Promise.all(el.getAnimations({ subtree: true }).map(a => a.finished.catch(() => undefined))));
  await page.waitForTimeout(100);
}

// Un diálogo debe quedar completo dentro de la pantalla (nada de contenido o botones fuera del viewport).
async function expectDialogInsideViewport(page: Page, label: string) {
  const dialog = page.locator('[role="dialog"]').first();
  await expect(dialog, `${label}: no se abrió el diálogo`).toBeVisible();
  await settle(page, '[role="dialog"]');
  const box = await dialog.boundingBox();
  const size = page.viewportSize()!;
  expect(box, `${label}: sin caja`).not.toBeNull();
  expect(box!.x, `${label}: diálogo se sale por la izquierda`).toBeGreaterThanOrEqual(-1);
  expect(box!.y, `${label}: diálogo se sale por arriba`).toBeGreaterThanOrEqual(-1);
  expect(box!.x + box!.width, `${label}: diálogo se sale por la derecha`).toBeLessThanOrEqual(size.width + 1);
  expect(box!.y + box!.height, `${label}: diálogo se sale por abajo`).toBeLessThanOrEqual(size.height + 1);
}

async function load(page: Page, path: string) {
  await page.goto(path);
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(250);
}

for (const vp of VIEWPORTS) {
  test.describe(vp.name, () => {
    test.use({ viewport: { width: vp.width, height: vp.height } });

    for (const route of PUBLIC_ROUTES) {
      test(`público ${route}`, async ({ page }) => {
        await mockApi(page);
        await load(page, route);
        await expectNoHorizontalOverflow(page, `${vp.name} ${route}`);
      });
    }

    test.describe('portal', () => {
      test.beforeEach(async ({ page }) => {
        await signIn(page);
        await mockApi(page);
      });

      for (const route of PORTAL_ROUTES) {
        test(`ruta ${route}`, async ({ page }) => {
          await load(page, route);
          await expectNoHorizontalOverflow(page, `${vp.name} ${route}`);
        });
      }

      test('formulario de factura con ítems', async ({ page }) => {
        await load(page, '/invoices');
        await page.locator('button', { hasText: /^\s*Nueva\s/ }).first().click();
        await page.getByRole('button', { name: /Agregar Ítem/ }).click();
        await page.getByRole('button', { name: /Agregar Ítem/ }).click();
        await expectNoHorizontalOverflow(page, `${vp.name} factura (crear)`);
      });

      test('formulario de documento soporte con ítems', async ({ page }) => {
        await load(page, '/support-documents');
        await page.locator('button', { hasText: /^\s*Nuevo\s/ }).first().click();
        await page.getByRole('button', { name: /Agregar Ítem/ }).click();
        await expectNoHorizontalOverflow(page, `${vp.name} documento soporte (crear)`);
      });

      test('detalle de factura', async ({ page }) => {
        await load(page, '/invoices');
        await page.getByRole('button', { name: /Ver detalle/ }).first().click();
        await page.waitForTimeout(300);
        await expectNoHorizontalOverflow(page, `${vp.name} factura (detalle)`);
      });

      test('modal de tercero', async ({ page }) => {
        await load(page, '/customers');
        await page.locator('button', { hasText: /^\s*Nuevo\s/ }).first().click();
        await expectDialogInsideViewport(page, `${vp.name} modal de tercero`);
        await expectNoHorizontalOverflow(page, `${vp.name} modal de tercero`);
      });

      test('modal de producto', async ({ page }) => {
        await load(page, '/products');
        await page.locator('button', { hasText: /^\s*Nuevo\s/ }).first().click();
        await expectDialogInsideViewport(page, `${vp.name} modal de producto`);
        await expectNoHorizontalOverflow(page, `${vp.name} modal de producto`);
      });

      test('modal de usuario', async ({ page }) => {
        await load(page, '/users');
        await page.getByRole('button', { name: 'Nuevo usuario' }).click();
        await page.getByLabel('Todas las sucursales').uncheck();
        await expectDialogInsideViewport(page, `${vp.name} modal de usuario`);
        await expectNoHorizontalOverflow(page, `${vp.name} modal de usuario`);
      });

      test('modal de sucursales de una resolución', async ({ page }) => {
        await load(page, '/resolutions');
        await page.getByRole('button', { name: /Sucursales de la resolución/ }).first().click();
        await expectDialogInsideViewport(page, `${vp.name} modal de sucursales de la resolución`);
        await expectNoHorizontalOverflow(page, `${vp.name} modal de sucursales de la resolución`);
      });

      test('modal de numeración propia por sucursal', async ({ page }) => {
        await load(page, '/resolutions');
        await page.getByRole('button', { name: 'Agregar numeración' }).click();
        await expectDialogInsideViewport(page, `${vp.name} modal de numeración propia`);
        await expectNoHorizontalOverflow(page, `${vp.name} modal de numeración propia`);
      });

      test('modal de resolución', async ({ page }) => {
        await load(page, '/resolutions');
        await page.locator('button', { hasText: /^\s*Nueva\s/ }).first().click();
        await expectDialogInsideViewport(page, `${vp.name} modal de resolución`);
        await expectNoHorizontalOverflow(page, `${vp.name} modal de resolución`);
      });

      if (vp.width < 1024) {
        test('menú lateral como drawer', async ({ page }) => {
          await load(page, '/');
          const aside = page.locator('aside[aria-label="Menú principal"]');
          await expect(aside).toBeHidden();
          await page.getByRole('button', { name: 'Abrir menú' }).click();
          await expect(aside).toBeVisible();
          await settle(page, 'aside[aria-label="Menú principal"]');
          const box = await aside.boundingBox();
          expect(box!.x).toBeGreaterThanOrEqual(-1);
          expect(box!.x + box!.width).toBeLessThanOrEqual(vp.width + 1);
          await aside.getByRole('link', { name: 'Terceros' }).click();
          await expect(aside).toBeHidden();
        });
      }
    });
  });
}
