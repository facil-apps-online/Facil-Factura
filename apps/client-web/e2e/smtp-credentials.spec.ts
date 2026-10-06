import { test, expect } from '@playwright/test';
import { mockApi, signIn } from './mockApi';

// SMTP de reenvío (Diseño y Ajustes): es un formulario de credenciales, así que arranca bloqueado y sin autocompletado, y el lapicito lo habilita.

test.describe('SMTP de reenvío', () => {
  test('arranca bloqueado y sin autocompletado; el lapicito lo habilita y recién ahí aparece Guardar', async ({ page }) => {
    await signIn(page);
    await mockApi(page);
    await page.goto('/settings');
    await page.waitForLoadState('networkidle');

    const host = page.getByPlaceholder('smtp.gmail.com');
    const password = page.locator('input[type="password"]').first();
    await expect(host).toHaveAttribute('readonly', '');
    await expect(password).toHaveAttribute('readonly', '');
    await expect(password).toHaveAttribute('autocomplete', 'new-password');
    await expect(page.getByRole('button', { name: 'Guardar configuración' })).toHaveCount(0);
    // Probar la conexión no modifica nada: sigue disponible con el formulario bloqueado.
    await expect(page.getByRole('button', { name: 'Probar conexión' })).toBeVisible();

    await page.getByRole('button', { name: 'Editar' }).click();
    await expect(host).not.toHaveAttribute('readonly', '');
    await expect(password).not.toHaveAttribute('readonly', '');
    await expect(page.getByRole('button', { name: 'Guardar configuración' })).toBeVisible();

    await page.getByRole('button', { name: 'Bloquear' }).click();
    await expect(host).toHaveAttribute('readonly', '');
    await expect(page.getByRole('button', { name: 'Guardar configuración' })).toHaveCount(0);
  });
});
