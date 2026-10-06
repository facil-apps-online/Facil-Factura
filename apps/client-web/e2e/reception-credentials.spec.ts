import { test, expect, type Page } from '@playwright/test';
import { mockApi, signIn } from './mockApi';

// Correo de recepción por sucursal (fase 6): el formulario de credenciales arranca bloqueado (sin autocompletado del navegador),
// el lapicito lo habilita y el mensaje explica a qué se aplica lo que se guarde según la sucursal elegida.

async function open(page: Page) {
  await page.goto('/received-documents');
  await page.waitForLoadState('networkidle');
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
    await expect(page.getByRole('button', { name: 'Guardar configuración' })).toHaveCount(0);

    await page.getByRole('button', { name: 'Editar' }).click();
    await expect(host(page)).not.toHaveAttribute('readonly', '');
    await expect(password(page)).not.toHaveAttribute('readonly', '');
    await expect(page.getByRole('button', { name: 'Guardar configuración' })).toBeVisible();

    await page.getByRole('button', { name: 'Bloquear' }).click();
    await expect(host(page)).toHaveAttribute('readonly', '');
  });

  test('con la principal elegida explica que hereda el buzón del cliente', async ({ page }) => {
    await signIn(page);
    await mockApi(page);
    await open(page);
    await expect(page.getByTestId('reception-scope')).toContainText('usa el buzón del cliente');
  });

  test('con "Todas" explica que se edita el buzón por defecto del cliente', async ({ page }) => {
    await signIn(page);
    await mockApi(page);
    await open(page);
    await page.getByTestId('branch-selector').locator('input').click();
    await page.getByText('Todas las sucursales', { exact: true }).click();
    await page.waitForLoadState('networkidle');
    await expect(page.getByTestId('reception-scope')).toContainText('por defecto del cliente');
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
    await page.getByRole('button', { name: 'Guardar configuración' }).click();
    await expect.poll(() => bodies.length).toBe(1);
    expect(bodies[0].onlyAutoSend).toBe(false);

    await page.getByRole('button', { name: 'Guardar eventos automáticos' }).click();
    await expect.poll(() => bodies.length).toBe(2);
    expect(bodies[1].onlyAutoSend).toBe(true);
    expect(bodies[1].receptionEmailPassword).toBeUndefined();
  });
});
