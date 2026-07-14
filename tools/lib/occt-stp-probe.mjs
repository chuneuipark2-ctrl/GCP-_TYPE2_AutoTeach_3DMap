/**
 * STP OCCT mesh 추출 가능 여부 (position 정점 있는 mesh 개수)
 */
import fs from 'fs';
import path from 'path';
import { createRequire } from 'module';
import { fileURLToPath } from 'url';
import { DEFAULT_STEP_PARAMS, extractMesh } from './step-mesh-utils.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const LIB_DIR = path.resolve(__dirname, '../../Viewer3D/lib');
const require = createRequire(import.meta.url);
const occtimportjs = require(path.join(LIB_DIR, 'occt-import-js.js'));

let occtReady = null;

async function loadOcct() {
  if (!occtReady) {
    occtReady = occtimportjs({ locateFile: (p) => path.join(LIB_DIR, p) });
  }
  return occtReady;
}

export function stpFileFingerprint(stpPath) {
  const st = fs.statSync(stpPath);
  return `${path.resolve(stpPath)}|${st.size}|${st.mtimeMs}`;
}

export function readProbeCache(cachePath) {
  if (!cachePath || !fs.existsSync(cachePath)) return {};
  try {
    return JSON.parse(fs.readFileSync(cachePath, 'utf8'));
  } catch {
    return {};
  }
}

export function writeProbeCache(cachePath, data) {
  if (!cachePath) return;
  fs.mkdirSync(path.dirname(cachePath), { recursive: true });
  fs.writeFileSync(cachePath, JSON.stringify(data, null, 2) + '\n', 'utf8');
}

export function clearProbeCacheEntry(stpPath, cachePath) {
  if (!cachePath || !stpPath || !fs.existsSync(stpPath)) return;
  const cache = readProbeCache(cachePath);
  const resolved = path.resolve(stpPath);
  let changed = false;
  for (const key of Object.keys(cache)) {
    if (key.startsWith(resolved + '|')) {
      delete cache[key];
      changed = true;
    }
  }
  if (changed) writeProbeCache(cachePath, cache);
}

/** @returns {{ meshTotal: number, meshWithPosition: number, parseMs: number }} */
export async function probeStpFile(stpPath, { cache = null } = {}) {
  const fp = stpFileFingerprint(stpPath);
  if (cache?.[fp]) return cache[fp];

  const buf = fs.readFileSync(stpPath);
  const t0 = Date.now();
  const occt = await loadOcct();
  const result = occt.ReadStepFile(new Uint8Array(buf), DEFAULT_STEP_PARAMS);
  const parseMs = Date.now() - t0;

  const meshes = result?.meshes || [];
  let meshWithPosition = 0;
  for (const m of meshes) {
    if (extractMesh(m)) meshWithPosition++;
  }

  const out = { meshTotal: meshes.length, meshWithPosition, parseMs };
  if (cache) cache[fp] = out;
  return out;
}

export function parseRevFromName(fileName) {
  const m = String(fileName || '').match(/_REV(\d+)/i);
  return m ? parseInt(m[1], 10) : 0;
}

/** OCCT 좌표 추출 품질 비교 — meshWithPosition 우선 */
export function compareStpCandidates(a, b) {
  if (a.meshWithPosition !== b.meshWithPosition) return a.meshWithPosition - b.meshWithPosition;
  if (a.rev !== b.rev) return a.rev - b.rev;
  return a.mtimeMs - b.mtimeMs;
}
