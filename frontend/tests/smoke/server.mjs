// Dedicated loopback server with a disposable database; never inherits live credentials.
import { spawn } from 'node:child_process';
import { mkdtemp, mkdir, rm } from 'node:fs/promises';
import { createWriteStream } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

const backend = fileURLToPath(new URL('../../../backend/', import.meta.url));
const reports = fileURLToPath(new URL('../../.smoke/', import.meta.url));
const data = await mkdtemp(join(tmpdir(), 'omnom-smoke-'));
await mkdir(reports, { recursive: true });
const log = createWriteStream(join(reports, 'server.log'), { flags: 'w' });
const port = Number(process.env.OMNOM_SMOKE_PORT || 5187);
if (!Number.isInteger(port) || port < 1024 || port > 65535) throw new Error('Invalid smoke port.');
const child = spawn('dotnet', [join(backend, 'bin/Debug/net8.0/Omnom.Api.dll'),
  '--urls', `http://127.0.0.1:${port}`], {
  cwd: backend, stdio: ['ignore', 'pipe', 'pipe'], env: { ...process.env,
    ASPNETCORE_ENVIRONMENT: 'Development', ASPNETCORE_URLS: `http://127.0.0.1:${port}`,
    OPENROUTER_API_KEY: '', USDA_API_KEY: 'omnom-smoke-disabled',
    DATABASE_URL: '', DATABASE_URL_UNPOOLED: '', APP_PASSCODE: '',
    REQUIRE_PASSCODE: 'false', PUBLIC_DIARY: 'true', DATA_DIR: data,
  },
});
console.log(`OM-002: isolated SQLite at ${data}; providers disabled; loopback port ${port}.`);
child.stdout.pipe(log, { end: false }); child.stderr.pipe(log, { end: false });
child.stderr.pipe(process.stderr);
let stopping = false;
for (const signal of ['SIGINT', 'SIGTERM']) process.on(signal, () => {
  stopping = true; child.kill('SIGTERM');
});
child.on('error', error => { console.error(error.message); process.exitCode = 1; });
child.on('close', async code => {
  log.end(); await rm(data, { recursive: true, force: true });
  process.exit(stopping ? 0 : code ?? 1);
});
