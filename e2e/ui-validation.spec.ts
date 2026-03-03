import { test, expect } from '@playwright/test';

test('Swagger UI page loads without JS errors', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', (err) => errors.push(err.message));
  await page.goto('/swagger/index.html');
  await page.waitForLoadState('networkidle');
  expect(errors).toEqual([]);
});

test('Swagger UI shows page title', async ({ page }) => {
  await page.goto('/swagger/index.html');
  await page.waitForLoadState('networkidle');
  const title = await page.title();
  expect(title).toContain('Swagger');
});

test('Swagger UI renders API info section', async ({ page }) => {
  await page.goto('/swagger/index.html');
  await page.waitForLoadState('networkidle');
  const infoContainer = page.locator('.swagger-ui .info');
  await expect(infoContainer).toBeVisible();
});

test('OpenAPI JSON endpoint returns valid JSON', async ({ page }) => {
  const response = await page.goto('/openapi/v1.json');
  expect(response?.status()).toBe(200);
  const body = await response?.json();
  expect(body.openapi).toMatch(/^3/);
});

test('Swagger UI loads OpenAPI spec successfully', async ({ page }) => {
  await page.goto('/swagger/index.html');
  await page.waitForLoadState('networkidle');
  // The swagger-ui should not show an error banner
  const errorWrapper = page.locator('.swagger-ui .errors-wrapper');
  const errorCount = await errorWrapper.count();
  if (errorCount > 0) {
    await expect(errorWrapper).not.toBeVisible();
  }
});
