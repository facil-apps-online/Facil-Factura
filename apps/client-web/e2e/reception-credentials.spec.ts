import { test, expect, type Page } from '@playwright/test';
import { mockApi, signIn } from './mockApi';

// Correo de recepción por sucursal (fase 6): el formulario de credenciales arranca bloqueado (sin autocompletado del navegador),
// el lapicito lo habilita y el mensaje explica a qué se aplica lo que se guarde según la sucursal elegida.

// El buzón se configura en Diseño y Ajustes, pestaña "Recepción electrónica".
async function openReceptionTab(page: Page) {
  await page.getByRole('button', { name: 'Recepción electrónica' }).click();
  await page.waitForLoadState('networkidle');
}

async function open(page: Page) {
  await page.goto('/settings');
  await page.waitForLoadState('networkidle');
  await openReceptionTab(page);
}

const host = (page: Page) => page.getByPlaceholder('imap.gmail.com');
const password = (page: Page) => page.locator('input[type="password"]').first();

test.describe('buzón de recepción', () => {
  test('arranca bloqueado y sin autocompletado; el lapicito lo habilita', async ({ page }) => {
    await signIn(page);
    await mockApi(page);
    await open(page);

    await expect(host(page)).toHaveAttribute('readonly', '');
    await expect(password(page)).toHaveAttribute('readonly', '');
    // Chrome no rellena con claves guardadas: new-password en los campos de credenciales.
    await expect(password(page)).toHaveAttribute('autocomplete', 'new-password');
    await expect(page.getByRole('button', { name: 'Guardar buzón' })).toBeDisabled();

    await page.getByRole('button', { name: 'Editar' }).click();
    await expect(host(page)).not.toHaveAttribute('readonly', '');
    await expect(password(page)).not.toHaveAttribute('readonly', '');
    await expect(page.getByRole('button', { name: 'Guardar buzón' })).toBeEnabled();

    await page.getByRole('button', { name: 'Bloquear' }).click();
    await expect(host(page)).toHaveAttribute('readonly', '');
  });

  test('con la principal elegida explica que es el valor por defecto de las demás', async ({ page }) => {
    await signIn(page);
    await mockApi(page);
    await open(page);
    await expect(page.getByText('Estás editando el buzón de la sucursal principal')).toBeVisible();
    await expect(page.getByText(/valor por defecto de las demás/)).toBeVisible();
  });

  test('una sucursal sin buzón ni eventos propios dice que usa los de la principal', async ({ page }) => {
    await signIn(page);
    await mockApi(page);
    await open(page);
    await page.getByTestId('branch-selector').locator('input').click();
    await page.getByText('Sucursal Comercializadora', { exact: false }).first().click();
    await page.waitForLoadState('networkidle');
    await expect(page.getByText('Esta sucursal usa el buzón de la principal.')).toBeVisible();
    await expect(page.getByText('Esta sucursal usa los eventos de la principal.')).toBeVisible();
  });

  test('con "Todas" se trabaja sobre la principal y lo dice', async ({ page }) => {
    await signIn(page);
    await mockApi(page);
    await open(page);
    await page.getByTestId('branch-selector').locator('input').click();
    await page.getByText('Todas las sucursales', { exact: true }).click();
    await page.waitForLoadState('networkidle');
    await expect(page.getByText(/se trabaja sobre la principal/)).toBeVisible();
  });

  test('guardar el buzón no envía solo-eventos; guardar eventos automáticos sí', async ({ page }) => {
    await signIn(page);
    await mockApi(page);
    const bodies: any[] = [];
    await page.route('**/client/reception-settings', route => {
      if (route.request().method() === 'PUT') {
        bodies.push(route.request().postDataJSON());
        return route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
      }
      return route.fallback();
    });
    await open(page);

    await page.getByRole('button', { name: 'Editar' }).click();
    await page.getByRole('button', { name: 'Guardar buzón' }).click();
    await expect.poll(() => bodies.length).toBe(1);
    expect(bodies[0].onlyAutoSend).toBe(false);

    await page.getByRole('button', { name: 'Guardar eventos automáticos' }).click();
    await expect.poll(() => bodies.length).toBe(2);
    expect(bodies[1].onlyAutoSend).toBe(true);
    expect(bodies[1].receptionEmailPassword).toBeUndefined();
  });

  test('quitar el buzón propio pide confirmar con un cuadro del sitio y se puede volver atrás', async ({ page }) => {
    await signIn(page);
    await mockApi(page);
    const deletes: string[] = [];
    await page.route('**/client/reception-settings', route => {
      const method = route.request().method();
      if (method === 'DELETE') { deletes.push(route.request().url()); return route.fulfill({ status: 200, contentType: 'application/json', body: '{}' }); }
      if (method === 'GET') {
        return route.fulfill({
          status: 200, contentType: 'application/json',
          body: JSON.stringify({ receptionEmailEnabled: true, receptionEmailHost: 'imap.gmail.com', receptionEmailPort: 993, receptionEmailUseSsl: true, receptionEmailUser: 'a@b.co', hasPassword: true, autoSendAcuseRecibo: false, autoSendReciboBien: false, autoSendAceptacion: false, autoSendReclamo: false, isMain: false, isBranchOwn: true, isEventsOwn: false }),
        });
      }
      return route.fallback();
    });
    await open(page);

    await page.getByRole('button', { name: 'Volver al buzón principal' }).click();
    const dialog = page.getByRole('alertdialog');
    await expect(dialog).toContainText('Volverá a usar el de la principal');
    await dialog.getByRole('button', { name: 'Cancelar' }).click();
    await expect(dialog).toBeHidden();
    expect(deletes).toHaveLength(0);

    await page.getByRole('button', { name: 'Volver al buzón principal' }).click();
    await page.getByRole('alertdialog').getByRole('button', { name: 'Quitar' }).click();
    await expect.poll(() => deletes.length).toBe(1);
  });
});
