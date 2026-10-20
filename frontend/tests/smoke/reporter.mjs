import { mkdir, writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
export default class EvidenceReporter {
  rows = [];
  onBegin(config) { this.metadata = config.metadata; }
  onTestEnd(test, result) {
    this.rows.push({ test: test.titlePath().filter(Boolean).join(' / '), status: result.status,
      durationMs: result.duration, annotations: test.annotations, errors: result.errors.map(error => error.message) });
  }
  async onEnd(result) {
    const directory = fileURLToPath(new URL('../../.smoke/', import.meta.url));
    await mkdir(directory, { recursive: true });
    const status = result.status === 'passed' && this.rows.some(row => row.status !== 'passed') ? 'failed' : result.status;
    const evidence = { ...this.metadata, completedAt: new Date().toISOString(), status,
      durationMs: result.duration, tests: this.rows,
      limitations: ['Not a live model-quality evaluation.', 'Chromium only; viewport coverage is not real-device coverage.',
        'Storage/parse/search failures are injected into disposable test contexts.'] };
    await writeFile(`${directory}/summary.json`, JSON.stringify(evidence, null, 2));
    await writeFile(`${directory}/summary.md`, `# OM-002 smoke run\n\nStatus: **${status}**\n\n`
      + `Version: ${this.metadata.appVersion}\n\nRevision: ${this.metadata.revision}\n\n`
      + `Started: ${this.metadata.startedAt}; completed: ${evidence.completedAt}\n\n`
      + `Environment: ${this.metadata.node}, ${this.metadata.platform}; .NET SDK ${this.metadata.dotnetSdk}; Playwright ${this.metadata.playwright}\n\n`
      + `Command: \`${this.metadata.command}\`\n\n${this.metadata.providerMode}\n\n`
      + '| Scenario | Result | Duration (ms) |\n|---|---|---|\n'
      + this.rows.map(row => `| ${row.test} | ${row.status} | ${row.durationMs} |`).join('\n')
      + '\n\nSkipped checks: ' + this.rows.filter(row => row.status === 'skipped').map(row => row.test).join(', ')
      + '\n\nWorking tree:\n```text\n' + this.metadata.workingTree + '\n```\n\n'
      + evidence.limitations.join('\n\n') + '\n');
    return { status };
  }
}
