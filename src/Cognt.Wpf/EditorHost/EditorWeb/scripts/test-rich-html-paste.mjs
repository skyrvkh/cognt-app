import { spawn } from 'node:child_process';
import fs from 'node:fs';
import http from 'node:http';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import * as esbuild from 'esbuild';

const projectDirectory = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const tempDirectory = fs.mkdtempSync(path.join(os.tmpdir(), 'cognt-rich-paste-test-'));
const bundlePath = path.join(tempDirectory, 'rich-html-paste.browser.js');
const profileDirectory = path.join(tempDirectory, 'edge-profile');

try {
  await esbuild.build({
    entryPoints: [path.join(projectDirectory, 'tests', 'rich-html-paste.browser.ts')],
    bundle: true,
    outfile: bundlePath,
    format: 'esm',
    platform: 'browser',
    target: 'es2022',
    logLevel: 'silent',
  });

  const server = await createServer(bundlePath);
  try {
    const browserRun = runHeadlessEdge(`http://127.0.0.1:${server.port}/`);
    const result = await Promise.race([
      server.result,
      browserRun.then(
        run => ({
          status: 'fail',
          text: `Edge exited before reporting a smoke result.\n${run.stdout}\n${run.stderr}`,
        }),
        error => ({
          status: 'fail',
          text: error instanceof Error ? error.stack || error.message : String(error),
        })
      ),
    ]);
    await browserRun.catch(() => null);
    if (result.status !== 'pass') {
      throw new Error(`Rich HTML paste browser smoke failed.\n${result.text}`);
    }

    process.stdout.write('Rich HTML paste browser smoke passed.\n');
  } finally {
    await server.close();
  }
} finally {
  fs.rmSync(tempDirectory, { force: true, recursive: true });
}

function createServer(scriptPath) {
  const html = `<!doctype html>
<html>
  <head><meta charset="utf-8"><title>Rich HTML paste smoke</title></head>
  <body data-status="pending">
    <script type="module">
      const nativeFetch = window.fetch.bind(window);
      try {
        await import('/rich-html-paste.browser.js');
        await finish('pass', 'PASS');
      } catch (error) {
        await finish(
          'fail',
          error instanceof Error ? error.stack || error.message : String(error)
        );
      }

      async function finish(status, text) {
        document.body.dataset.status = status;
        document.body.textContent = text;
        await nativeFetch('/result', {
          method: 'POST',
          headers: { 'content-type': 'application/json' },
          body: JSON.stringify({ status, text }),
        });
      }
    </script>
  </body>
</html>`;
  let resolveResult;
  const result = new Promise(resolve => {
    resolveResult = resolve;
  });

  const server = http.createServer((request, response) => {
    if (request.url === '/') {
      response.writeHead(200, { 'content-type': 'text/html; charset=utf-8' });
      response.end(html);
      return;
    }

    if (request.url === '/result' && request.method === 'POST') {
      let body = '';
      request.setEncoding('utf8');
      request.on('data', chunk => {
        body += chunk;
      });
      request.on('end', () => {
        resolveResult(parseBrowserResult(body));
        response.writeHead(204);
        response.end();
      });
      return;
    }

    if (request.url === '/rich-html-paste.browser.js') {
      response.writeHead(200, { 'content-type': 'text/javascript; charset=utf-8' });
      fs.createReadStream(scriptPath).pipe(response);
      return;
    }

    response.writeHead(404);
    response.end();
  });

  return new Promise((resolve, reject) => {
    server.on('error', reject);
    server.listen(0, '127.0.0.1', () => {
      const address = server.address();
      if (!address || typeof address === 'string') {
        reject(new Error('Could not start rich paste smoke server.'));
        return;
      }

      resolve({
        port: address.port,
        result,
        close: () => new Promise((closeResolve, closeReject) => {
          server.close(error => error ? closeReject(error) : closeResolve());
        }),
      });
    });
  });
}

function parseBrowserResult(body) {
  try {
    const result = JSON.parse(body);
    return {
      status: result.status === 'pass' ? 'pass' : 'fail',
      text: typeof result.text === 'string' ? result.text : '',
    };
  } catch (error) {
    return {
      status: 'fail',
      text: `Could not parse browser result: ${error instanceof Error ? error.message : String(error)}`,
    };
  }
}

function runHeadlessEdge(url) {
  const edgePath = resolveEdgePath();
  const args = [
    '--headless=new',
    '--disable-gpu',
    '--disable-extensions',
    '--disable-background-networking',
    '--no-first-run',
    `--user-data-dir=${profileDirectory}`,
    '--virtual-time-budget=15000',
    '--dump-dom',
    url,
  ];

  return new Promise((resolve, reject) => {
    const child = spawn(edgePath, args, {
      cwd: projectDirectory,
      stdio: ['ignore', 'pipe', 'pipe'],
      windowsHide: true,
    });
    let stdout = '';
    let stderr = '';

    child.stdout.on('data', chunk => {
      stdout += chunk;
    });
    child.stderr.on('data', chunk => {
      stderr += chunk;
    });
    child.on('error', reject);
    child.on('exit', code => {
      if (code === 0) {
        resolve({ stdout, stderr });
        return;
      }

      reject(new Error(`Headless Edge exited with code ${code ?? 'unknown'}.\n${stderr}`));
    });
  });
}

function resolveEdgePath() {
  const candidates = [
    process.env.CHROME_PATH,
    process.env.EDGE_PATH,
    'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
    'C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe',
    'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe',
    'C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe',
  ].filter(Boolean);

  for (const candidate of candidates) {
    if (fs.existsSync(candidate)) {
      return candidate;
    }
  }

  throw new Error('No Chromium-based browser executable was found. Set CHROME_PATH or EDGE_PATH to run this smoke test.');
}
