import { spawn } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const projectDirectory = path.dirname(fileURLToPath(import.meta.url));
const localNodeModulesDirectory = path.join(projectDirectory, "node_modules");
const sharedNodeModulesDirectory = path.resolve(projectDirectory, "..", "..", "..", "..", ".npm-cache", "editorweb-node_modules");
const toolScriptByName = {
  tsc: path.join("typescript", "bin", "tsc"),
  vite: path.join("vite", "bin", "vite.js"),
};

ensureNodeModules();
await runBin("tsc");
await runBin("vite", ["build", "--configLoader", "native"]);

function ensureNodeModules() {
  if (fs.existsSync(localNodeModulesDirectory)) {
    return;
  }

  if (!fs.existsSync(sharedNodeModulesDirectory)) {
    throw new Error(
      `EditorWeb dependencies were not found at ${sharedNodeModulesDirectory}. ` +
      "Install or move the cached editorweb node_modules there before building."
    );
  }

  fs.symlinkSync(sharedNodeModulesDirectory, localNodeModulesDirectory, "junction");
}

function runBin(name, args = []) {
  const executable = process.execPath;
  const scriptPath = resolveToolScript(name);

  return new Promise((resolve, reject) => {
    const child = spawn(executable, [
        "--preserve-symlinks",
        "--preserve-symlinks-main",
        scriptPath,
        ...args,
      ], {
        cwd: projectDirectory,
        stdio: "inherit"
      });

    child.on("error", reject);
    child.on("exit", (code) => {
      if (code === 0) {
        resolve();
        return;
      }

      reject(new Error(`${name} exited with code ${code ?? "unknown"}.`));
    });
  });
}

function resolveToolScript(name) {
  const scriptPath = toolScriptByName[name];
  if (!scriptPath) {
    throw new Error(`Unsupported EditorWeb build tool: ${name}.`);
  }

  return path.join(localNodeModulesDirectory, scriptPath);
}
