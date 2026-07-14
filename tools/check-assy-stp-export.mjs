/**
 * 전 ASSY 공통 — STP export에 PLC 배치 이름 들어갔는지 검증
 *
 * node tools/check-assy-stp-export.mjs --assy Carriage_Assy
 * node tools/check-assy-stp-export.mjs --all
 * node tools/check-assy-stp-export.mjs --assy SCP --drawing "D:\...\SRM0\3D_Drawing"
 * node tools/check-assy-stp-export.mjs path/to/model.stp   (signalKeys는 assemblies.json)
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { resolveAssyStpPath, modelRootFromDetailRel } from './lib/rev-model-resolve.mjs';
import { getDefaultRuntimeDrawingRoot } from './lib/desktop-kit-paths.mjs';
import { isManifestContentStale, readManifestStpSource, readStpFileMeta } from './lib/manifest-stp-sync.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '..');

function parseArgs(argv) {
  const opts = { assy: null, all: false, drawing: null, stp: null };
  for (let i = 2; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--assy') opts.assy = argv[++i];
    else if (a === '--all') opts.all = true;
    else if (a === '--drawing') opts.drawing = path.resolve(argv[++i]);
    else if (!a.startsWith('-')) opts.stp = path.resolve(a);
  }
  return opts;
}

function plcKeysInStp(stpText, keys) {
  const found = [];
  const missing = [];
  for (const k of keys) {
    if (stpText.includes(`'${k}'`)) found.push(k);
    else missing.push(k);
  }
  return { found, missing };
}

function checkStpFile(stpPath, signalKeys, label) {
  if (!fs.existsSync(stpPath)) {
    console.error(`[${label}] STP 없음:`, stpPath);
    return { ok: false, exit: 2 };
  }

  const stp = fs.readFileSync(stpPath, 'latin1');
  const st = fs.statSync(stpPath);
  const { found, missing } = plcKeysInStp(stp, signalKeys);
  const occ = [...new Set([...stp.matchAll(/NEXT_ASSEMBLY_USAGE_OCCURRENCE\('([^']+)'/g)].map((m) => m[1]))];

  console.log(`\n=== ${label} STP export 검증 ===`);
  console.log('파일:', stpPath);
  console.log('mtime:', st.mtime.toISOString(), '· size:', (st.size / 1024 / 1024).toFixed(1), 'MB');
  console.log('배치(occurrence) 고유 이름:', occ.length, '개');
  console.log('PLC 이름 매칭:', found.length, '/', signalKeys.length);
  if (found.length) console.log('  OK:', found.slice(0, 12).join(', ') + (found.length > 12 ? '...' : ''));
  if (missing.length) console.log('  없음:', missing.join(', '));

  const manifestPath = path.join(path.dirname(stpPath), 'io_manifest.json');
  if (fs.existsSync(manifestPath)) {
    let manifest;
    try {
      manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
    } catch {
      manifest = null;
    }
    if (manifest) {
      const stpMeta = readStpFileMeta(stpPath);
      const rec = readManifestStpSource(manifest);
      const contentStale = isManifestContentStale(manifest);
      const fileStale =
        !rec ||
        rec.file !== stpMeta?.file ||
        Math.abs((rec.mtimeMs || 0) - (stpMeta?.mtimeMs || 0)) > 0.5 ||
        (rec.sizeBytes != null && rec.sizeBytes !== stpMeta?.sizeBytes);
      console.log(
        'io_manifest:',
        `match ${manifest.matchCount ?? '?'}/${manifest.signalCount ?? '?'}`,
        contentStale ? '· ⚠ 내용 stale (points 비어있음)' : '· OK',
        fileStale ? '· ⚠ STP 파일과 동기화 필요' : ''
      );
      if (found.length > 0 && contentStale) {
        console.log('  → STP에는 이름 있음 — manifest만 재생성하면 됨:');
        console.log(`     node tools/stp-io-manifest.mjs --assy ${label} --force`);
      }
    }
  }

  if (found.length === signalKeys.length) {
    console.log('\n✅ STP export OK');
    return { ok: true, exit: 0 };
  }
  if (found.length > 0) {
    console.log('\n⚠ 일부만 매칭 — PathFinder 배치 이름 확인');
    return { ok: false, exit: 1 };
  }
  console.log('\n❌ PLC 배치 이름 0개 — PathFinder 배치명=PLC signalKey 후 STP 재export');
  return { ok: false, exit: 1 };
}

function main() {
  const opts = parseArgs(process.argv);
  const drawingRoot = opts.drawing || getDefaultRuntimeDrawingRoot(ROOT);
  const catalogPath = path.join(drawingRoot, 'assemblies.json');

  if (opts.stp) {
    if (!fs.existsSync(catalogPath)) {
      console.error('assemblies.json 필요:', catalogPath);
      process.exit(2);
    }
    const catalog = JSON.parse(fs.readFileSync(catalogPath, 'utf8'));
    const assyId = opts.assy || path.basename(path.dirname(opts.stp));
    const assy = catalog.assemblies?.find((a) => a.id === assyId);
    const keys = assy?.signalKeys || [];
    if (!keys.length) {
      console.error('signalKeys 없음 — --assy 지정');
      process.exit(2);
    }
    const r = checkStpFile(opts.stp, keys, assyId);
    process.exit(r.exit);
  }

  if (!fs.existsSync(catalogPath)) {
    console.error('assemblies.json 없음:', catalogPath);
    process.exit(2);
  }

  const catalog = JSON.parse(fs.readFileSync(catalogPath, 'utf8'));
  const targets = opts.all
    ? catalog.assemblies || []
    : opts.assy
      ? catalog.assemblies?.filter((a) => a.id === opts.assy) || []
      : [];

  if (!targets.length) {
    console.error(
      '사용법:\n' +
        '  node tools/check-assy-stp-export.mjs --assy <AssyId>\n' +
        '  node tools/check-assy-stp-export.mjs --all [--drawing path]\n' +
        '  node tools/check-assy-stp-export.mjs <file.stp> --assy <AssyId>'
    );
    process.exit(2);
  }

  let worst = 0;
  for (const assy of targets) {
    const rel = assy.detailModel?.replace(/\\/g, '/');
    if (!rel) continue;
    const assyDir = path.join(drawingRoot, path.dirname(rel));
    const stp = resolveAssyStpPath(assyDir, assy.id, rel);
    const keys = assy.signalKeys || [];
    if (!keys.length) {
      console.warn(`[${assy.id}] signalKeys 없음 — skip`);
      continue;
    }
    const r = checkStpFile(stp, keys, assy.id);
    if (r.exit > worst) worst = r.exit;
  }

  console.log('\n=== 검증 완료 ===');
  process.exit(worst);
}

main();
