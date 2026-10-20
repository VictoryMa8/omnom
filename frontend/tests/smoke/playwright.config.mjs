import { defineConfig } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { readFileSync } from 'node:fs';

const frontend = fileURLToPath(new URL('../../', import.meta.url));
const root = fileURLToPath(new URL('../../../', import.meta.url));
const port = Number(process.env.OMNOM_SMOKE_PORT || 5187);
if (!Number.isInteger(port) || port < 1024 || port > 65535) throw new Error('Invalid OMNOM_SMOKE_PORT.');
const git = (...args) => execFileSync('git', args, { cwd: root, encoding: 'utf8' }).trim();
export default defineConfig({
  forbidOnly: true, testDir: '.', testMatch: '**/*.spec.mjs', fullyParallel: false, workers: 1, retries: 0,
  timeout: 30_000, expect: { timeout: 5000 },
  outputDir: `${frontend}/.smoke/artifacts`,
  reporter: [['list'], ['json', { outputFile: `${frontend}/.smoke/results.json` }],
    ['html', { outputFolder: `${frontend}/.smoke/html`, open: 'never' }], ['./reporter.mjs']],
  metadata: {
    task: 'OM-002', startedAt: new Date().toISOString(),
    appVersion: JSON.parse(readFileSync(`${frontend}/package.json`, 'utf8')).version,
    revision: git('rev-parse', 'HEAD'), workingTree: git('status', '--short'),
    dotnetSdk: execFileSync('dotnet', ['--version'], { encoding: 'utf8' }).trim(),
    playwright: JSON.parse(readFileSync(`${frontend}/node_modules/@playwright/test/package.json`, 'utf8')).version,
    node: process.version, platform: `${process.platform}/${process.arch}`,
    command: 'npm --prefix frontend run test:smoke',
    providerMode: 'Browser API fixtures; real API checks use Exact/local references only. No live AI/USDA.',
  },
  use: { baseURL: `http://127.0.0.1:${port}`, browserName: 'chromium',
    timezoneId: 'America/Chicago', locale: 'en-US', serviceWorkers: 'block',
    screenshot: 'only-on-failure', trace: 'retain-on-failure', actionTimeout: 5000 },
  projects: [
    { name: 'desktop', use: { viewport: { width: 1280, height: 900 } } },
    { name: 'mobile', use: { viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true } },
  ],
  webServer: { command: 'node tests/smoke/server.mjs', cwd: frontend,
    url: `http://127.0.0.1:${port}`, reuseExistingServer: false, timeout: 90_000,
    env: { OMNOM_SMOKE_PORT: String(port) }, gracefulShutdown: { signal: 'SIGTERM', timeout: 5000 },
    stdout: 'pipe', stderr: 'pipe' },
});
