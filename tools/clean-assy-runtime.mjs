/**
 * ASSY 런타임 폴더 정리 — junk·ELEC_CONVERT 만 삭제
 * STP/GLB 파일명·확장자 변경 금지 (사용자가 넣은 이름 그대로 유지)
 *
 * 반드시 --drawing 지정 (기본 bin/SRM0 자동 타격 금지 — 디버그 시 사용자 GLB 삭제 방지)
 *
 * node tools/clean-assy-runtime.mjs --drawing "D:\...\SRM0\3D_Drawing"
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { loadAssyIoProfileFromDir, isStpFileExcluded } from './lib/assy-io-profile.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const DELETE_GLOB = [
  /\.obj$/i,
  /\.mtl$/i,
  /\.log$/i,
  /\.jt$/i,
  /^io_glb_mesh_map\./i,
  /^\.jt-occurrence-placer\.json$/i,
  / \(2\)\.glb$/i,
  /_from_obj\.glb$/i,
  /\.gltf2$/i,
  /\.bin$/i,
];

function loadCatalog(drawingRoot) {
  return JSON.parse(fs.readFileSync(path.join(drawingRoot, 'assemblies.json'), 'utf8'));
}

function shouldDeleteJunk(name) {
  if (/\.(stp|step|glb|gltf|json|txt)$/i.test(name)) return false;
  if (/\.%%%$/i.test(name)) return false;
  return DELETE_GLOB.some((re) => re.test(name));
}

function cleanAssyFolder(assyDir, assyId) {
  if (!fs.existsSync(assyDir)) {
    console.warn('  skip (없음):', assyDir);
    return;
  }

  const profile = loadAssyIoProfileFromDir(assyDir, assyId);
  let removed = 0;

  for (const name of fs.readdirSync(assyDir)) {
    const full = path.join(assyDir, name);
    const st = fs.statSync(full);
    if (st.isDirectory()) {
      fs.rmSync(full, { recursive: true, force: true });
      console.log(`  [${assyId}] 폴더 삭제: ${name}/`);
      removed++;
      continue;
    }
    if (isStpFileExcluded(name, profile)) {
      fs.unlinkSync(full);
      console.log(`  [${assyId}] ELEC_CONVERT 삭제: ${name}`);
      removed++;
      continue;
    }
    if (shouldDeleteJunk(name)) {
      fs.unlinkSync(full);
      console.log(`  [${assyId}] junk 삭제: ${name}`);
      removed++;
    }
  }

  console.log(`  [${assyId}] 완료 — junk/ELEC ${removed}건 (STP·GLB 이름 변경 없음)`);
}

function main() {
  const args = process.argv.slice(2);
  let drawingRoot = null;
  for (let i = 0; i < args.length; i++) {
    if (args[i] === '--drawing' && args[i + 1]) drawingRoot = path.resolve(args[++i]);
  }

  if (!drawingRoot) {
    console.error(
      '사용법: node tools/clean-assy-runtime.mjs --drawing <3D_Drawing폴더>\n' +
        '  예: node tools/clean-assy-runtime.mjs --drawing bin/Debug/net6.0-windows/SRM0/3D_Drawing\n' +
        '  ※ --drawing 없이 실행 금지 (사용자 STP/GLB 보호)'
    );
    process.exit(1);
  }

  const catalog = loadCatalog(drawingRoot);
  console.log('ASSY 런타임 정리:', drawingRoot);

  for (const assy of catalog.assemblies || []) {
    const rel = assy.detailModel?.replace(/\\/g, '/');
    if (!rel) continue;
    const folder = path.basename(path.dirname(rel));
    cleanAssyFolder(path.join(drawingRoot, 'assemblies', folder), assy.id);
  }

  console.log('\n=== 정리 완료 ===');
}

main();
