/**
 * 빌드타임 3D 아티팩트 생성 — 개발/CI 전용 (현장 HMI에서 실행 금지)
 *
 * STP → io_manifest.json (센서)
 * SE OBJ → C# Assimp.Net → GLB (형체)
 *
 * node tools/build-assy-artifacts.mjs --all
 * node tools/build-assy-artifacts.mjs --assy Carriage_Assy
 *
 * Solid Edge: 형체=OBJ export (GLB export 없음), 센서=STP 유지
 */
import fs from 'fs';
import path from 'path';
import { spawnSync } from 'child_process';
import { fileURLToPath } from 'url';
import { findHighestRevObj, resolveAssyStpPath, modelRootFromDetailRel } from './lib/rev-model-resolve.mjs';
import { validateAssyDisplayGlb } from './ensure-assy-display.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '..');
const ASSY_BUILD_PROJ = path.join(__dirname, 'Dio3DAssyBuild/Dio3DAssyBuild.csproj');

function parseArgs(argv) {
  const opts = { assy: null, drawing: null, all: false, validate: false, skipGlb: false };
  for (let i = 2; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--assy') opts.assy = argv[++i];
    else if (a === '--drawing') opts.drawing = argv[++i];
    else if (a === '--all') opts.all = true;
    else if (a === '--validate') opts.validate = true;
    else if (a === '--skip-glb') opts.skipGlb = true;
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

function runNode(script, args, label) {
  const res = spawnSync('node', [script, ...args], {
    encoding: 'utf8',
    timeout: 3600000,
    maxBuffer: 64 * 1024 * 1024,
    cwd: ROOT,
  });
  if (res.stdout?.trim()) console.log(res.stdout.trim());
  if (res.stderr?.trim()) console.log(res.stderr.trim());
  if (res.status !== 0) {
    console.error(`[build] ${label} 실패 (exit ${res.status ?? '?'})`);
    return false;
  }
  return true;
}

function ensureAssyBuildTool() {
  const res = spawnSync('dotnet', ['build', ASSY_BUILD_PROJ, '-v', 'q', '-c', 'Debug'], {
    encoding: 'utf8',
    timeout: 600000,
    cwd: ROOT,
  });
  if (res.status !== 0) {
    console.error((res.stderr || res.stdout || '').trim());
    throw new Error('Dio3DAssyBuild 빌드 실패 — dotnet build tools/Dio3DAssyBuild');
  }
}

function runObjToGlb(objPath, glbPath, label) {
  ensureAssyBuildTool();
  const res = spawnSync(
    'dotnet',
    ['run', '--project', ASSY_BUILD_PROJ, '--no-build', '-c', 'Debug', '--', '--obj', objPath, '--glb', glbPath, '--force'],
    {
      encoding: 'utf8',
      timeout: 3600000,
      maxBuffer: 64 * 1024 * 1024,
      cwd: ROOT,
    }
  );
  if (res.stdout?.trim()) console.log(res.stdout.trim());
  if (res.stderr?.trim()) console.log(res.stderr.trim());
  if (res.status !== 0) {
    console.error(`[build] ${label} 실패 (exit ${res.status ?? '?'})`);
    return false;
  }
  return true;
}

function buildOneAssy(drawingRoot, assyId, { skipGlb }) {
  const catalog = loadCatalog(drawingRoot);
  const assy = catalog?.assemblies?.find((a) => a.id === assyId);
  if (!assy?.detailModel) throw new Error('ASSY 없음: ' + assyId);

  const assyDir = path.join(drawingRoot, 'assemblies', assyId);
  const rootName = modelRootFromDetailRel(assy.detailModel);
  const stpPath = resolveAssyStpPath(assyDir, assyId, assy.detailModel);
  if (!stpPath || !fs.existsSync(stpPath)) {
    throw new Error(`${assyId}: STP 없음 — ${assyDir}`);
  }

  const objPath = findHighestRevObj(assyDir, rootName);
  const manifestPath = path.join(assyDir, 'io_manifest.json');
  const glbPath = path.join(assyDir, rootName + '.glb');

  console.log(`\n[build] ${assyId}`);
  const manifestScript = path.join(__dirname, 'stp-io-manifest.mjs');
  const manifestArgs = [stpPath, manifestPath];
  if (fs.existsSync(path.join(assyDir, 'IO_STP_SENSOR_MAP.txt'))) {
    manifestArgs.push('--with-occt');
  }
  if (!runNode(manifestScript, manifestArgs, 'manifest')) {
    throw new Error(`${assyId}: manifest 생성 실패`);
  }

  if (!skipGlb) {
    if (!objPath || !fs.existsSync(objPath)) {
      throw new Error(
        `${assyId}: OBJ 없음 — Solid Edge에서 어셈블리 OBJ export 후\n` +
          `  assemblies/${assyId}/${rootName}.obj`
      );
    }
    const glbOk = runObjToGlb(objPath, glbPath, 'OBJ→GLB');
    if (!glbOk) {
      throw new Error(`${assyId}: Assimp OBJ→GLB 실패`);
    }
  }

  return validateAssyDisplayGlb({ drawingRoot, assyId });
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
    console.error(
      '사용법: node tools/build-assy-artifacts.mjs --all | --assy <Id> [--drawing path] [--validate] [--skip-glb]'
    );
    process.exit(1);
  }

  if (opts.validate) {
    let failed = 0;
    for (const id of targets) {
      try {
        validateAssyDisplayGlb({ drawingRoot, assyId: id });
      } catch (e) {
        failed++;
        console.error(e.message || e);
      }
    }
    process.exit(failed > 0 ? 1 : 0);
  }

  const t0 = Date.now();
  let ok = 0;
  let fail = 0;
  for (const id of targets) {
    try {
      buildOneAssy(drawingRoot, id, { skipGlb: opts.skipGlb });
      ok++;
    } catch (e) {
      fail++;
      console.error('[build] 실패:', e.message || e);
    }
  }

  console.log(
    `\n=== build-assy-artifacts === ok ${ok} fail ${fail} ${((Date.now() - t0) / 1000).toFixed(1)}s`
  );
  console.log('배포: Viewer3D/3D_Drawing_Template → SRM{n}/3D_Drawing (시드 시 GLB·manifest 복사)');
  if (fail > 0) process.exit(1);
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main();
}
