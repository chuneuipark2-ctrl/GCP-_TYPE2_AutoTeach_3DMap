/**
 * ASSY 형체 GLB — prebuilt 검증만 (현장 변환 없음)
 *
 * node tools/ensure-assy-display.mjs --assy Carriage_Assy
 * node tools/ensure-assy-display.mjs --all
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { findHighestRevObj, resolveAssyStpPath, modelRootFromDetailRel } from './lib/rev-model-resolve.mjs';
import { isManifestStale } from './lib/manifest-stp-sync.mjs';
import { formatAssyGlbDeployHint, formatAssyObjSaveHint } from './lib/desktop-kit-paths.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '..');

function parseArgs(argv) {
  const opts = { assy: null, drawing: null, all: false };
  for (let i = 2; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--assy') opts.assy = argv[++i];
    else if (a === '--drawing') opts.drawing = argv[++i];
    else if (a === '--all') opts.all = true;
  }
  return opts;
}

function defaultDrawingRoot() {
  return path.join(ROOT, 'Viewer3D/3D_Drawing_Template');
}

function loadCatalog(drawingRoot) {
  const p = path.join(drawingRoot, 'assemblies.json');
  if (!fs.existsSync(p)) return null;
  return JSON.parse(fs.readFileSync(p, 'utf8'));
}

function isPlausibleGlb(refPath, glbPath) {
  if (!glbPath || !fs.existsSync(glbPath)) return false;
  const glbBytes = fs.statSync(glbPath).size;
  if (glbBytes < 1024) return false;
  if (!refPath || !fs.existsSync(refPath)) return true;
  const refBytes = fs.statSync(refPath).size;
  if (refBytes > 30 * 1024 * 1024 && glbBytes < 5 * 1024 * 1024) return false;
  if (refBytes > 10 * 1024 * 1024 && glbBytes < 2 * 1024 * 1024) return false;
  return true;
}

function manifestPathFor(assyDir) {
  return path.join(assyDir, 'io_manifest.json');
}

export function validateAssyDisplayGlb({ drawingRoot, assyId }) {
  const catalog = loadCatalog(drawingRoot);
  const assy = catalog?.assemblies?.find((a) => a.id === assyId);
  if (!assy?.detailModel) throw new Error('ASSY 없음: ' + assyId);

  const assyDir = path.join(drawingRoot, 'assemblies', assyId);
  const rootName = modelRootFromDetailRel(assy.detailModel);
  const objPath = findHighestRevObj(assyDir, rootName);
  const stpPath = resolveAssyStpPath(assyDir, assyId, assy.detailModel);
  const refPath = objPath && fs.existsSync(objPath) ? objPath : stpPath;
  const glbPath = path.join(assyDir, rootName + '.glb');
  const manifestPath = manifestPathFor(assyDir);

  const glbOk = isPlausibleGlb(refPath, glbPath);
  const manifestOk = fs.existsSync(manifestPath) && fs.statSync(manifestPath).size > 32;
  const manifestStale = manifestOk && stpPath && isManifestStale(manifestPath, stpPath);

  if (glbOk && manifestOk && !manifestStale) {
    const bytes = fs.statSync(glbPath).size;
    console.log(`[ok] ${assyId}: GLB ${(bytes / (1024 * 1024)).toFixed(2)} MB + manifest`);
    return { glbPath, manifestPath, ok: true };
  }

  const hints = [];
  if (!glbOk) {
    if (!objPath || !fs.existsSync(objPath)) {
      hints.push(`OBJ 없음 → Solid Edge 어셈블리 export: ${formatAssyObjSaveHint(assyId, rootName)}`);
    }
    hints.push(`GLB 없음/비정상 → ${formatAssyGlbDeployHint(assyId, rootName)}`);
    hints.push(`빌드: node tools/build-assy-artifacts.mjs --assy ${assyId}`);
  }
  if (!manifestOk) hints.push(`io_manifest.json 없음 → node tools/build-assy-artifacts.mjs --assy ${assyId}`);
  else if (manifestStale) {
    hints.push(
      `io_manifest STP 날짜 불일치 (${path.basename(stpPath)}) → node tools/stp-io-manifest.mjs --assy ${assyId} --force`
    );
  }
  throw new Error(`${assyId}: 배포 아티팩트 불완전\n  ${hints.join('\n  ')}`);
}

async function main() {
  const opts = parseArgs(process.argv);
  const drawingRoot = path.resolve(opts.drawing || defaultDrawingRoot());
  const catalog = loadCatalog(drawingRoot);
  if (!catalog?.assemblies?.length) {
    console.error('assemblies.json 없음:', drawingRoot);
    process.exit(1);
  }

  const targets = opts.all
    ? catalog.assemblies.map((a) => a.id)
    : opts.assy
      ? [opts.assy]
      : [];

  if (!targets.length) {
    console.error('사용법: node tools/ensure-assy-display.mjs --assy <AssyId> | --all [--drawing path]');
    process.exit(1);
  }

  let failed = 0;
  for (const id of targets) {
    try {
      validateAssyDisplayGlb({ drawingRoot, assyId: id });
    } catch (e) {
      failed++;
      console.error('실패:', e.message || e);
    }
  }

  if (failed > 0) process.exit(1);
  console.log(`\n=== 검증 완료 === ${targets.length - failed}/${targets.length}`);
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main();
}
