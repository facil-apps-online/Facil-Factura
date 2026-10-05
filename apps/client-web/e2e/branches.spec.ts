import { test, expect, type Page } from '@playwright/test';
import { ADMIN_SESSION, BRANCH_MAIN, INVOICER_SESSION, mockApi, signIn } from './mockApi';

const ADMIN_ONLY_LINKS = ['Documentos recibidos', 'Pagos', 'Resoluciones DIAN', 'Diseño y Ajustes', 'Usuarios'];
const INVOICER_LINKS = ['Inicio', 'Mis Facturas', 'Documentos Soporte', 'Nómina electrónica', 'Terceros', 'Productos y servicios'];

async function open(page: Page, path: string) {
  await page.goto(path);
  await page.waitForLoadState('networkidle');
}

test.describe('selector de sucursal', () => {
  test('con dos sucursales se muestra y envía la elegida en cada petición', async ({ page }) => {
    await signIn(page);
    await mockApi(page);
    const sent: Array<string | undefined> = [];
    page.on('request', req => {
      if (new URL(req.url()).pathname.endsWith('/client/customers')) sent.push(req.headers()['x-branch-id']);
    });

    await open(page, '/customers');
    await expect(page.getByTestId('branch-selector')).toBeVisible();
    // Arranca en la principal (la primera de la lista).
    expect(sent.length).toBeGreaterThan(0);
    expect(sent.every(branch => branch === BRANCH_MAIN.id)).toBe(true);
    expect(await page.evaluate(() => localStorage.getItem('fel_client_branch'))).toBe(BRANCH_MAIN.id);
  });

  test('quien ve todas las sucursales puede elegir "Todas" y queda guardado', async ({ page }) => {
    await signIn(page);
    await mockApi(page);
    await open(page, '/customers');

    await page.getByTestId('branch-selector').locator('input').click();
    await page.getByText('Todas las sucursales', { exact: true }).click();
    await expect.poll(() => page.evaluate(() => localStorage.getItem('fel_client_branch'))).toBe('all');

    // La elección sobrevive a una recarga.
    await page.reload();
    await page.waitForLoadState('networkidle');
    await expect(page.getByTestId('branch-selector').locator('input')).toHaveValue('Todas las sucursales');
  });

  test('con una sola sucursal no se muestra', async ({ page }) => {
    await signIn(page);
    await mockApi(page, { ...ADMIN_SESSION, branches: [BRANCH_MAIN] });
    await open(page, '/customers');
    await expect(page.getByTestId('branch-selector')).toHaveCount(0);
  });

  test('una sucursal guardada que ya no está permitida se descarta', async ({ page }) => {
    await signIn(page);
    await mockApi(page, INVOICER_SESSION);
    await page.addInitScript(() => localStorage.setItem('fel_client_branch', 'sucursal-que-ya-no-existe'));
    await open(page, '/customers');
    expect(await page.evaluate(() => localStorage.getItem('fel_client_branch'))).toBe(INVOICER_SESSION.branches[0].id);
  });
});

test.describe('rol Facturador', () => {
  test.beforeEach(async ({ page }) => {
    await signIn(page);
    await mockApi(page, INVOICER_SESSION);
  });

  test('el menú no ofrece las secciones de administración', async ({ page }) => {
    await open(page, '/');
    const nav = page.locator('aside[aria-label="Menú principal"]');
    for (const label of INVOICER_LINKS) await expect(nav.getByRole('link', { name: label })).toBeVisible();
    for (const label of ADMIN_ONLY_LINKS) await expect(nav.getByRole('link', { name: label })).toHaveCount(0);
  });

  test('entrar por la URL a una pantalla de administración devuelve al inicio', async ({ page }) => {
    for (const path of ['/users', '/resolutions', '/settings', '/received-documents', '/payments']) {
      await open(page, path);
      await expect(page, path).toHaveURL(/\/$/);
    }
  });

  test('el inicio no le ofrece configurar la resolución', async ({ page }) => {
    await open(page, '/');
    await expect(page.getByText('Configura una resolución para empezar a facturar.')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Configurar resolución' })).toHaveCount(0);
  });

  test('con una sola sucursal permitida no ve el selector y sigue pudiendo emitir', async ({ page }) => {
    await open(page, '/invoices');
    await expect(page.getByTestId('branch-selector')).toHaveCount(0);
    await expect(page.locator('button', { hasText: /^\s*Nueva\s/ }).first()).toBeVisible();
  });
});

test.describe('rol Administrador', () => {
  test.beforeEach(async ({ page }) => {
    await signIn(page);
    await mockApi(page);
  });

  test('el menú incluye las secciones de administración', async ({ page }) => {
    await open(page, '/');
    const nav = page.locator('aside[aria-label="Menú principal"]');
    for (const label of [...INVOICER_LINKS, ...ADMIN_ONLY_LINKS]) await expect(nav.getByRole('link', { name: label })).toBeVisible();
  });

  test('pantalla de usuarios: lista con rol, sucursales y estado', async ({ page }) => {
    await open(page, '/users');
    await expect(page.getByRole('heading', { name: 'Usuarios' })).toBeVisible();
    await expect(page.getByText('Administradora Principal').first()).toBeVisible();
    await expect(page.getByText('(tú)').first()).toBeVisible();
    await expect(page.getByText('Desactivado').first()).toBeVisible();
    // Sin botón de desactivar para uno mismo; los demás sí tienen su ciclo completo.
    await expect(page.getByRole('button', { name: 'Desactivar a Administradora Principal' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Reactivar a Usuario desactivado' }).first()).toBeVisible();
    await expect(page.getByRole('button', { name: 'Reenviar invitación a Usuario desactivado' })).toHaveCount(0);
  });

  test('modal de nuevo usuario: el rol sale del catálogo y las sucursales se eligen', async ({ page }) => {
    await open(page, '/users');
    await page.getByRole('button', { name: 'Nuevo usuario' }).click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();

    await dialog.getByRole('textbox', { name: 'Nombre' }).fill('Nueva Persona');
    await dialog.getByRole('textbox', { name: 'Correo' }).fill('nueva@empresa.com');

    await dialog.locator('input[placeholder="Elige un rol"]').click();
    await expect(page.getByText('Administrador', { exact: true }).last()).toBeVisible();
    await page.getByText('Facturador', { exact: true }).last().click();

    // Con "Todas" marcado no se listan sucursales; al desmarcar aparecen para elegir.
    const all = dialog.getByLabel('Todas las sucursales');
    await expect(all).toBeChecked();
    await all.uncheck();
    await expect(dialog.getByLabel('Principal')).toBeVisible();
  });

  test('resoluciones: cada una muestra dónde se usa y numeración propia por sucursal', async ({ page }) => {
    await page.setViewportSize({ width: 1920, height: 1080 });
    await open(page, '/resolutions');
    await expect(page.getByRole('columnheader', { name: 'Sucursales' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Numeración propia por sucursal' })).toBeVisible();
    await expect(page.getByText('NCN').first()).toBeVisible();
    await expect(page.getByRole('button', { name: /Sucursales de la resolución/ }).first()).toBeVisible();
  });

  test('con una sola sucursal las resoluciones no muestran nada de sucursales', async ({ page }) => {
    await mockApi(page, { ...ADMIN_SESSION, branches: [BRANCH_MAIN] });
    await page.setViewportSize({ width: 1920, height: 1080 });
    await open(page, '/resolutions');
    await expect(page.getByRole('columnheader', { name: 'Sucursales' })).toHaveCount(0);
    await expect(page.getByRole('heading', { name: 'Numeración propia por sucursal' })).toHaveCount(0);
  });
});
