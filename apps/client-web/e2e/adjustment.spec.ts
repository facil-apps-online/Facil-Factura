import { test, expect } from '@playwright/test';
import { mockApi, signIn } from './mockApi';

// La nota de ajuste del documento soporte debe abrir como el documento que ajusta: con sus retenciones
// (por ítem y generales) y sus observaciones, igual que la nota crédito con la factura.
test('la nota de ajuste copia las retenciones y observaciones del soporte original', async ({ page }) => {
  await signIn(page);
  await mockApi(page);
  await page.goto('/support-documents');
  await page.waitForLoadState('networkidle');

  await page.getByRole('button', { name: /Ver detalle de .* 501/ }).first().click();
  await page.getByRole('button', { name: /Generar Nota de Ajuste/ }).click();

  // Retención del ítem (11 %) y retención general (0.414 %), ya cargadas en el formulario.
  await expect(page.getByText('11%').first()).toBeVisible();
  await expect(page.getByText('0.414%').first()).toBeVisible();
  // Observaciones del documento original.
  await expect(page.getByPlaceholder('Notas adicionales para este documento...')).toHaveValue('Observación del documento original');
});
