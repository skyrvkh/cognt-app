import { spawn } from 'node:child_process';
import fs from 'node:fs';
import http from 'node:http';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const projectDirectory = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const distDirectory = path.join(projectDirectory, 'dist');
const tempDirectory = fs.mkdtempSync(path.join(os.tmpdir(), 'cognt-rich-paste-e2e-'));
const profileDirectory = path.join(tempDirectory, 'edge-profile');

try {
  assertFile(path.join(distDirectory, 'assets', 'blazor-blocksuite.js'));

  const server = await createServer(distDirectory);
  try {
    const browser = launchHeadlessBrowser(`http://127.0.0.1:${server.port}/`);
    let timeoutId;
    const timeout = new Promise(resolve => {
      timeoutId = setTimeout(
        () => resolve({
          status: 'fail',
          text: 'Timed out waiting for rich HTML paste E2E result.',
        }),
        30000
      );
    });
    const result = await Promise.race([
      server.result,
      browser.exit.then(
        run => ({
          status: 'fail',
          text: `Browser exited before reporting an E2E result.\nexit=${run.code ?? 'unknown'}\n${run.stdout}\n${run.stderr}`,
        }),
        error => ({
          status: 'fail',
          text: error instanceof Error ? error.stack || error.message : String(error),
        })
      ),
      timeout,
    ]);
    clearTimeout(timeoutId);
    browser.kill();
    await browser.exit.catch(() => null);
    if (result.status !== 'pass') {
      throw new Error(`Rich HTML paste E2E failed.\n${result.text}`);
    }

    process.stdout.write('Rich HTML paste editor E2E passed.\n');
  } finally {
    await server.close();
  }
} finally {
  fs.rmSync(tempDirectory, { force: true, recursive: true });
}

function createServer(staticRoot) {
  const html = `<!doctype html>
<html>
  <head>
    <meta charset="utf-8">
    <title>Cognt rich HTML paste E2E</title>
    <link rel="stylesheet" href="/assets/style.css">
    <style>
      html, body { margin: 0; width: 100%; height: 100%; }
      #host { width: 900px; height: 700px; }
      #status { position: fixed; bottom: 0; left: 0; }
    </style>
  </head>
  <body data-status="pending">
    <div id="host"></div>
    <div id="links"></div>
    <div id="status"></div>
    <script type="module">
      import {
        activateEditor,
        exportContent,
        focusEditor,
        loadNote,
        mountBlockSuite,
        pasteRichHtmlForAutomation
      } from '/assets/blazor-blocksuite.js';

      const imageBase64 = 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mO8efMmAAX3Akk8sS2VAAAAAElFTkSuQmCC';
      const hostMessages = [];
      const hostAttachments = new Map();
      const editorBlobs = new Map();
      const pasteObservations = [];
      let latestContent = '';
      const keepAliveTimer = setInterval(() => {}, 50);
      const nativeFetch = window.fetch.bind(window);

      window.__cogntEnableInfoDiagnostics = true;
      globalThis.fetch = async input => {
        const url = typeof input === 'string' ? input : input.url;
        if (url.includes('/blocked-by-webview.png')) {
          throw new TypeError('Failed to fetch');
        }

        return new Response(base64ToBlob(imageBase64, 'image/png'), {
          status: 200,
          headers: { 'content-type': 'image/png' },
        });
      };

      const dotNet = {
        async invokeMethodAsync(method, ...args) {
          switch (method) {
            case 'HandleHostMessage':
              hostMessages.push(args[0]);
              return null;
            case 'HandleDocumentChanged':
              return null;
            case 'HandleContentChanged':
              latestContent = String(args[0] ?? '');
              return null;
            case 'HandleAttachImageSource': {
              const fallbackName = String(args[2] ?? 'pasted-image.png');
              const relativePath = 'attachments/images/' + fallbackName.replace(/[^a-z0-9._-]+/gi, '-');
              hostAttachments.set(relativePath, {
                fileName: fallbackName,
                contentType: 'image/png',
                contentBase64: imageBase64,
              });
              return {
                ok: true,
                displayName: fallbackName,
                relativePath,
                kind: 'Image',
              };
            }
            case 'HandleReadAttachment': {
              const relativePath = String(args[1] ?? '');
              const attachment = hostAttachments.get(relativePath);
              return attachment
                ? { ok: true, relativePath, ...attachment }
                : { ok: false, error: 'missing attachment' };
            }
            case 'HandleReadNativeClipboardImage':
              return null;
            case 'HandleSearchMentionTargets':
              return [];
            case 'HandleCreateLinkedNote':
              return { ok: false, error: 'not used in paste E2E' };
            case 'HandleStoreEditorBlob': {
              const blobId = String(args[1] ?? '');
              editorBlobs.set(blobId, {
                contentType: String(args[2] ?? 'application/octet-stream'),
                contentBase64: String(args[3] ?? ''),
              });
              return { ok: true };
            }
            case 'HandleReadEditorBlob': {
              const blob = editorBlobs.get(String(args[1] ?? ''));
              return blob ? { ok: true, ...blob } : { ok: false, error: 'missing blob' };
            }
            case 'HandleListEditorBlobs':
              return { ok: true, blobIds: Array.from(editorBlobs.keys()) };
            case 'HandleCreateExtract':
            case 'HandleSaveCloze':
            case 'HandleOpenImageOcclusion':
            case 'HandleOpenMention':
            case 'HandleRequestMentionPreview':
              return null;
            default:
              throw new Error('Unhandled host method: ' + method);
          }
        }
      };

      try {
        const host = document.getElementById('host');
        const links = document.getElementById('links');
        const status = document.getElementById('status');
        const instanceId = mountBlockSuite(host, links, status, dotNet);
        await loadNote(instanceId, 'automation.blocksuite.json', 'Automation', '', 'C:/CogntAutomation');
        await activateEditor(instanceId);
        await focusEditor(instanceId);
        await waitForEditor(host);

        let pasteError = null;
        const pastePromise = pasteRichHtmlForAutomation(
          instanceId,
          createPasteHtml(),
          'SAP HANA Cloud Value Proposition\\nSAP HANA Cloud is a fully managed in-memory multitenant cloud database.'
        ).catch(error => {
          pasteError = error;
        });
        await waitFor(() => pasteError || countExportedImages(latestContent) >= 2, 12000);
        if (pasteError) {
          throw pasteError;
        }
        void pastePromise;

        const exported = latestContent;
        assert(exported.trim().length > 0, 'paste automation did not produce saved content');
        const payload = JSON.parse(exported);
        const blocks = flattenSnapshotBlocks(payload.snapshot);
        const imageBlocks = blocks.filter(block => block.flavour === 'affine:image');

        assert(exported.includes('SAP HANA Cloud Value Proposition'), 'formatted pasted heading text was not exported');
        assert(exported.includes('managed in-memory multitenant'), 'pasted paragraph text was not exported');
        assert(
          exported.includes('[[cognt-paste-image:') === false,
          'temporary paste image markers leaked into exported content'
        );
        assert(imageBlocks.length >= 2, 'expected at least two persisted image blocks');
        assert(
          imageBlocks.every(block => typeof block.props?.sourceId === 'string' && block.props.sourceId.startsWith('attachments/images/')),
          'image blocks did not export with persisted attachment source ids'
        );
        assert(
          hostMessages.some(message => message?.message?.includes?.('Rich HTML paste inserted')),
          'paste image insertion diagnostic was not emitted'
        );

        await finish('pass', 'PASS images=' + imageBlocks.length);
      } catch (error) {
        const diagnostics = hostMessages
          .map(message => message?.message)
          .filter(Boolean)
          .slice(-100);
        await finish(
          'fail',
          JSON.stringify({
            error: error instanceof Error ? error.stack || error.message : String(error),
            latestContentLength: latestContent.length,
            latestImageCount: countExportedImages(latestContent),
            pasteObservations,
            diagnostics,
          }, null, 2)
        );
      }

      function createPasteHtml() {
        return createCfHtml(
          '<h2>SAP HANA Cloud Value Proposition</h2>' +
          '<p><strong>SAP HANA Cloud</strong> is a fully managed in-memory multitenant cloud database.</p>' +
          '<img alt="Challenges" src="https://learning.sap.com/blocked-by-webview.png">' +
          '<p>The cloud-based data foundation enables faster decisions based on live data.</p>' +
          '<img alt="Strategic Direction" src="//learning.sap.com/also-blocked-by-webview.png">' +
          '<img src="https://0.0.0.1/icons/expand-image.svg">',
          'https://learning.sap.com/course/sap-hana-cloud'
        );
      }

      function createCfHtml(fragment, sourceUrl) {
        const body = '<!DOCTYPE html><html><body><!--StartFragment-->' + fragment + '<!--EndFragment--></body></html>';
        const headerTemplate =
          'Version:0.9\\r\\n' +
          'StartHTML:0000000000\\r\\n' +
          'EndHTML:0000000000\\r\\n' +
          'StartFragment:0000000000\\r\\n' +
          'EndFragment:0000000000\\r\\n' +
          'SourceURL:' + sourceUrl + '\\r\\n';
        const startHtml = getUtf8Length(headerTemplate);
        const endHtml = startHtml + getUtf8Length(body);
        const startFragment = startHtml + getUtf8Length('<!DOCTYPE html><html><body><!--StartFragment-->');
        const endFragment = startFragment + getUtf8Length(fragment);
        return headerTemplate
          .replace('StartHTML:0000000000', 'StartHTML:' + String(startHtml).padStart(10, '0'))
          .replace('EndHTML:0000000000', 'EndHTML:' + String(endHtml).padStart(10, '0'))
          .replace('StartFragment:0000000000', 'StartFragment:' + String(startFragment).padStart(10, '0'))
          .replace('EndFragment:0000000000', 'EndFragment:' + String(endFragment).padStart(10, '0')) + body;
      }

      async function waitForEditor(host) {
        await waitFor(() => findPasteTarget(host), 8000);
      }

      function findPasteTarget(host) {
        for (const root of walkRoots(host)) {
          const target = root.querySelector('[contenteditable="true"], .inline-editor, rich-text');
          if (target) {
            return target;
          }
        }

        return null;
      }

      function* walkRoots(root) {
        yield root;
        for (const element of root.querySelectorAll('*')) {
          if (element.shadowRoot) {
            yield* walkRoots(element.shadowRoot);
          }
        }
      }

      function countExportedImages(content) {
        if (!content.trim()) {
          return 0;
        }

        try {
          const payload = JSON.parse(content);
          return flattenSnapshotBlocks(payload.snapshot)
            .filter(block => block.flavour === 'affine:image')
            .length;
        } catch {
          return 0;
        }
      }

      function flattenSnapshotBlocks(value) {
        const blocks = [];
        const visit = candidate => {
          if (!candidate || typeof candidate !== 'object') {
            return;
          }

          if (typeof candidate.flavour === 'string') {
            blocks.push(candidate);
          }

          if (Array.isArray(candidate)) {
            for (const item of candidate) {
              visit(item);
            }
            return;
          }

          if (Array.isArray(candidate.children)) {
            for (const child of candidate.children) {
              visit(child);
            }
          }

          if (candidate.blocks && typeof candidate.blocks === 'object') {
            for (const child of Object.values(candidate.blocks)) {
              visit(child);
            }
          }
        };

        visit(value);
        return blocks;
      }

      function waitFor(predicate, timeoutMs) {
        const startedAt = performance.now();
        return new Promise((resolve, reject) => {
          const tick = () => {
            try {
              if (predicate()) {
                resolve();
                return;
              }
            } catch (error) {
              reject(error);
              return;
            }

            if (performance.now() - startedAt > timeoutMs) {
              reject(new Error('Timed out waiting for condition.'));
              return;
            }

            setTimeout(tick, 50);
          };
          tick();
        });
      }

      function base64ToBlob(base64, contentType) {
        const binary = atob(base64);
        const bytes = new Uint8Array(binary.length);
        for (let index = 0; index < binary.length; index++) {
          bytes[index] = binary.charCodeAt(index);
        }
        return new Blob([bytes], { type: contentType });
      }

      function getUtf8Length(value) {
        return new TextEncoder().encode(value).length;
      }

      function assert(condition, message) {
        if (!condition) {
          throw new Error(message);
        }
      }

      function observePaste(label, target) {
        target.addEventListener('paste', event => {
          pasteObservations.push({
            label,
            target: getEventTargetLabel(event.target),
            currentTarget: getEventTargetLabel(event.currentTarget),
            hasClipboardData: Boolean(event.clipboardData),
            types: Array.from(event.clipboardData?.types ?? []),
            defaultPrevented: event.defaultPrevented,
            path: event.composedPath().slice(0, 8).map(getEventTargetLabel),
          });
        }, { capture: true });
      }

      function getEventTargetLabel(value) {
        if (value === document) {
          return '#document';
        }
        if (value === window) {
          return '#window';
        }
        if (value instanceof ShadowRoot) {
          return '#shadow-root';
        }
        if (value instanceof Element) {
          return value.localName + (value.id ? '#' + value.id : '');
        }
        return String(value);
      }

      async function finish(status, text) {
        clearInterval(keepAliveTimer);
        document.documentElement.innerHTML =
          '<head></head><body data-status="' + status + '"><pre></pre></body>';
        document.body.dataset.status = status;
        document.body.querySelector('pre').textContent = text;
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
    const requestedPath = request.url === '/'
      ? null
      : path.normalize(decodeURIComponent(request.url.split('?', 1)[0]).replace(/^[/\\]+/, ''));
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

    if (requestedPath === null) {
      response.writeHead(200, { 'content-type': 'text/html; charset=utf-8' });
      response.end(html);
      return;
    }

    const filePath = path.join(staticRoot, requestedPath);
    if (!filePath.startsWith(staticRoot) || !fs.existsSync(filePath) || !fs.statSync(filePath).isFile()) {
      response.writeHead(404);
      response.end();
      return;
    }

    response.writeHead(200, { 'content-type': getContentType(filePath) });
    fs.createReadStream(filePath).pipe(response);
  });

  return new Promise((resolve, reject) => {
    server.on('error', reject);
    server.listen(0, '127.0.0.1', () => {
      const address = server.address();
      if (!address || typeof address === 'string') {
        reject(new Error('Could not start rich paste E2E server.'));
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

function launchHeadlessBrowser(url) {
  const edgePath = resolveEdgePath();
  const args = [
    '--headless=new',
    '--disable-gpu',
    '--disable-extensions',
    '--disable-background-networking',
    '--no-first-run',
    `--user-data-dir=${profileDirectory}`,
    url,
  ];

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

  return {
    exit: new Promise((resolve, reject) => {
      child.on('error', reject);
      child.on('exit', code => {
        resolve({ code, stdout, stderr });
      });
    }),
    kill() {
      if (!child.killed && child.exitCode === null) {
        child.kill();
      }
    },
  };
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

  throw new Error('No Chromium-based browser executable was found. Set CHROME_PATH or EDGE_PATH to run this E2E test.');
}

function getContentType(filePath) {
  const extension = path.extname(filePath).toLowerCase();
  return extension === '.js'
    ? 'text/javascript; charset=utf-8'
    : extension === '.css'
      ? 'text/css; charset=utf-8'
      : extension === '.ttf'
        ? 'font/ttf'
        : 'application/octet-stream';
}

function assertFile(filePath) {
  if (!fs.existsSync(filePath)) {
    throw new Error(`Missing built editor asset: ${filePath}. Run npm run build first.`);
  }
}
