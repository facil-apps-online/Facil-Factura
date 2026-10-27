import { test, expect, type Page } from '@playwright/test';
import { mockApi, signIn } from './mockApi';

// Numeraciones de notas crédito y débito: agrupadas por prefijo, con creación de numeraciones con otros prefijos, predeterminada,
// verificación contra Dataico (su API no deja crear la numeración, solo avisar) y cuadros del sitio en lugar del confirm del navegador.

async function open(page: Page) {
  await page.goto('/resolutions');
  await page.waitForLoadState('networkidle');
}

const group = (page: Page, prefix: string) => page.getByTestId(`note-group-${prefix}`);

test.describe('numeraciones de notas', () => {
  test('se agrupan por prefijo e indican origen, predeterminada y uso', async ({ page }) => {
    await signIn(page);
    await mockApi(page);
    await open(page);

    for (const prefix of ['FEV', 'NC', 'NCX', 'ND', 'NCN']) await expect(group(page, prefix)).toBeVisible();

    // Un prefijo agrupa lo que numera: FEV es el de la factura y numera crédito y débito.
    await expect(group(page, 'FEV')).toContainText('Nota crédito');
    await expect(group(page, 'FEV')).toContainText('Nota débito');
    await expect(group(page, 'FEV')).toContainText('Prefijo de la factura');
    // NC y ND son las predeterminadas, así que el prefijo de la factura ya no es el de uso por defecto.
    await expect(group(page, 'NC')).toContainText('Predeterminada');
    await expect(group(page, 'ND')).toContainText('Predeterminada');
    await expect(group(page, 'FEV')).not.toContainText('En uso por defecto');
    await expect(group(page, 'NC')).toContainText('Con notas emitidas');
    await expect(group(page, 'NCN')).toContainText('Sucursal Sucursal Norte');
  });

  test('una numeración con notas emitidas no se puede eliminar; una sin ellas sí, con el cuadro del sitio', async ({ page }) => {
    await signIn(page);
    await mockApi(page);
    await open(page);

    await expect(page.getByRole('button', { name: 'Eliminar numeración NC de Nota crédito' })).toHaveCount(0);

    let deleted = false;
    page.on('dialog', () => { throw new Error('Se usó un diálogo del navegador'); });
    await page.route('**/api/client/note-numberings/own/nn2', route => {
      if (route.request().method() === 'DELETE') { deleted = true; return route.fulfill({ status: 204 }); }
      return route.fallback();
    });
    await page.getByRole('button', { name: 'Eliminar numeración NCX de Nota crédito' }).click();
    const dialog = page.getByRole('alertdialog');
    await expect(dialog).toContainText('NCX');
    await dialog.getByRole('button', { name: 'Cancelar' }).click();
    expect(deleted).toBe(false);

    await page.getByRole('button', { name: 'Eliminar numeración NCX de Nota crédito' }).click();
    await page.getByRole('alertdialog').getByRole('button', { name: /Eliminar|Aceptar|Confirmar/ }).click();
    await expect.poll(() => deleted).toBe(true);
  });

  test('agregar una numeración envía el prefijo en mayúsculas y muestra el aviso de Dataico si no existe allá', async ({ page }) => {
    await signIn(page);
    await mockApi(page);
    await open(page);

    let sent: any = null;
    await page.route('**/api/client/note-numberings/own', route => {
      if (route.request().method() !== 'POST') return route.fallback();
      sent = route.request().postDataJSON();
      return route.fulfill({
        status: 200, contentType: 'application/json',
        body: JSON.stringify({ id: 'nn9', kind: 'DebitNote', prefix: 'NDX', nextNumber: 5, isDefault: false, dataico: { checked: true, exists: false, message: 'Dataico no tiene una numeración de notas débito con el prefijo NDX. Créala en Dataico.' } }),
      });
    });

    await page.getByRole('button', { name: 'Nueva numeración' }).click();
    await page.getByPlaceholder('Elige el tipo de nota').click();
    await page.getByText('Nota débito', { exact: true }).last().click();
    await page.locator('#note-numbering-prefix').fill('ndx');
    await page.locator('#note-numbering-next').fill('5');
    await page.getByRole('dialog').getByRole('button', { name: 'Guardar' }).click();

    await expect.poll(() => sent).not.toBeNull();
    expect(sent).toEqual({ kind: 'DebitNote', prefix: 'NDX', nextNumber: 5 });
    await expect(page.getByRole('status').filter({ hasText: 'Dataico no tiene una numeración' })).toBeVisible();
  });

  test('"Traer de Dataico" lista los prefijos que faltan y precarga el formulario', async ({ page }) => {
    await signIn(page);
    await mockApi(page);
    await open(page);

    await page.getByRole('button', { name: 'Traer de Dataico' }).click();
    await expect(page.getByText('NCAY')).toBeVisible();
    await page.getByRole('button', { name: 'Agregar', exact: true }).click();
    await expect(page.locator('#note-numbering-prefix')).toHaveValue('NCAY');
    await expect(page.getByPlaceholder('Elige el tipo de nota')).toHaveValue('Nota crédito');
  });
});
