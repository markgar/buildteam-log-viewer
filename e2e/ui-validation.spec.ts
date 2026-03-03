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

test('Swagger UI shows /health endpoint', async ({ page }) => {
  await page.goto('/swagger/index.html');
  await page.waitForLoadState('networkidle');
  const healthPath = page.locator('.swagger-ui .opblock-summary-path', { hasText: '/health' });
  await expect(healthPath).toBeVisible();
});

test('Health endpoint returns ok status via API', async ({ request }) => {
  const response = await request.get('/health');
  expect(response.status()).toBe(200);
  const body = await response.json();
  expect(body.status).toBe('ok');
});

test('Swagger UI shows /projects endpoint', async ({ page }) => {
  await page.goto('/swagger/index.html');
  await page.waitForLoadState('networkidle');
  const projectsPath = page.locator('.swagger-ui .opblock-summary-path', { hasText: '/projects' });
  await expect(projectsPath.first()).toBeVisible();
});

test('Swagger UI shows /projects/{projectId}/runs endpoint', async ({ page }) => {
  await page.goto('/swagger/index.html');
  await page.waitForLoadState('networkidle');
  const runsPath = page.locator('.swagger-ui .opblock-summary-path[data-path="/projects/{projectId}/runs"]');
  await expect(runsPath).toBeVisible();
});

test('GET /projects returns JSON with error or projects key', async ({ request }) => {
  const response = await request.get('/projects');
  const body = await response.json();
  const hasProjects = 'projects' in body;
  const hasError = 'error' in body;
  expect(hasProjects || hasError).toBeTruthy();
});

test('GET /projects/nonexistent/runs returns 404 or 500 with JSON', async ({ request }) => {
  const response = await request.get('/projects/nonexistent-project-xyz/runs');
  expect([404, 500]).toContain(response.status());
  const body = await response.json();
  expect('error' in body || 'project_id' in body).toBeTruthy();
});

test('OpenAPI spec contains /projects path', async ({ request }) => {
  const response = await request.get('/openapi/v1.json');
  expect(response.status()).toBe(200);
  const body = await response.json();
  expect(body.paths).toHaveProperty('/projects');
});

test('OpenAPI spec contains /projects/{projectId}/runs path', async ({ request }) => {
  const response = await request.get('/openapi/v1.json');
  expect(response.status()).toBe(200);
  const body = await response.json();
  expect(body.paths).toHaveProperty('/projects/{projectId}/runs');
});

test('OpenAPI spec contains /projects/{projectId}/runs/{runId}/logs path', async ({ request }) => {
  const response = await request.get('/openapi/v1.json');
  expect(response.status()).toBe(200);
  const body = await response.json();
  expect(body.paths).toHaveProperty('/projects/{projectId}/runs/{runId}/logs');
});

test('Swagger UI shows /projects/{projectId}/runs/{runId}/logs endpoint', async ({ page }) => {
  await page.goto('/swagger/index.html');
  await page.waitForLoadState('networkidle');
  const logsPath = page.locator('.swagger-ui .opblock-summary-path[data-path="/projects/{projectId}/runs/{runId}/logs"]');
  await expect(logsPath).toBeVisible();
});

test('GET /projects/nonexistent/runs/some-run/logs returns 404 or 500 with JSON error', async ({ request }) => {
  const response = await request.get('/projects/nonexistent-project/runs/20260302-211501/logs');
  expect([404, 500]).toContain(response.status());
  const body = await response.json();
  expect(body.error).toBeDefined();
});

test('GET /projects/some-project/runs/nonexistent-run/logs returns 404 or 500 with JSON error', async ({ request }) => {
  const response = await request.get('/projects/some-project/runs/nonexistent-run/logs');
  expect([404, 500]).toContain(response.status());
  const body = await response.json();
  expect(body.error).toBeDefined();
});

// Milestone 04b: Log content and tail endpoints

test('OpenAPI spec contains log content path /projects/{projectId}/runs/{runId}/logs/{fileName}', async ({ request }) => {
  const response = await request.get('/openapi/v1.json');
  expect(response.status()).toBe(200);
  const body = await response.json();
  expect(body.paths).toHaveProperty('/projects/{projectId}/runs/{runId}/logs/{fileName}');
});

test('OpenAPI spec contains tail path /projects/{projectId}/runs/{runId}/logs/{fileName}/tail', async ({ request }) => {
  const response = await request.get('/openapi/v1.json');
  expect(response.status()).toBe(200);
  const body = await response.json();
  expect(body.paths).toHaveProperty('/projects/{projectId}/runs/{runId}/logs/{fileName}/tail');
});

test('Swagger UI shows log content endpoint', async ({ page }) => {
  await page.goto('/swagger/index.html');
  await page.waitForLoadState('networkidle');
  const contentPath = page.locator('.swagger-ui .opblock-summary-path[data-path="/projects/{projectId}/runs/{runId}/logs/{fileName}"]');
  await expect(contentPath).toBeVisible();
});

test('Swagger UI shows tail endpoint', async ({ page }) => {
  await page.goto('/swagger/index.html');
  await page.waitForLoadState('networkidle');
  const tailPath = page.locator('.swagger-ui .opblock-summary-path[data-path="/projects/{projectId}/runs/{runId}/logs/{fileName}/tail"]');
  await expect(tailPath).toBeVisible();
});

test('GET /projects/nonexistent/runs/some-run/logs/builder-1.log returns 404 or 500 with JSON error', async ({ request }) => {
  const response = await request.get('/projects/nonexistent/runs/20260302-211501/logs/builder-1.log');
  expect([404, 500]).toContain(response.status());
  const body = await response.json();
  expect(body.error).toBeDefined();
});

test('GET log content with raw=true for nonexistent returns 404 or 500', async ({ request }) => {
  const response = await request.get('/projects/some-project/runs/20260302-211501/logs/nonexistent.log?raw=true');
  expect([404, 500]).toContain(response.status());
});

test('GET /projects/nonexistent/runs/some-run/logs/builder-1.log/tail returns 404 or 500', async ({ request }) => {
  const response = await request.get('/projects/nonexistent/runs/20260302-211501/logs/builder-1.log/tail');
  expect([404, 500]).toContain(response.status());
  const body = await response.json();
  expect(body.error).toBeDefined();
});

test('GET tail with lines param for nonexistent returns 404 or 500', async ({ request }) => {
  const response = await request.get('/projects/some-project/runs/20260302-211501/logs/builder-1.log/tail?lines=50');
  expect([404, 500]).toContain(response.status());
  const body = await response.json();
  expect(body.error).toBeDefined();
});
