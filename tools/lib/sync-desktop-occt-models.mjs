/**
 * 바탕화면 GCP_3D_Debug → D드라이브 런타임 ASSY 폴더
 * OCCT mesh(position) 많은 STP/GLB 복사
 */
import fs from 'fs';
import path from 'path';
import { getDesktopKitRoot, getDesktopSearchRoots } from './desktop-kit-paths.mjs';
import { findHighestRevStp, modelRootFromDetailRel } from './rev-model-resolve.mjs';
import { loadAssyIoProfileFromDir, isStpFileExcluded } from './assy-io-profile.mjs';
import {
  compareStpCandidates,
  parseRevFromName,
  probeStpFile,
  readProbeCache,
  stpFileFingerprint,
  writeProbeCache,
} from './occt-stp-probe.mjs';

function walkStpFiles(root, out, depth = 0, maxDepth = 12) {
  if (!root || !fs.existsSync(root) || depth > maxDepth) return;
  let entries;
  try {
    entries = fs.readdirSync(root, { withFileTypes: true });
  } catch {
    return;
  }
  for (const ent of entries) {
    const full = path.join(root, ent.name);
    if (ent.isFile() && /\.(stp|step)$/i.test(ent.name)) {
      out.push(full);
    } else if (ent.isFile() && /\.%%%$/i.test(ent.name) && isStepFileByHeader(full)) {
      out.push(full);
    } else if (ent.isDirectory() && !/^\.(git|cache)$/i.test(ent.name)) {
      walkStpFiles(full, out, depth + 1, maxDepth);
    }
  }
}

function stpMatchesAssy(fileName, rootName, assyId) {
  if (/ELEC_CONVERT/i.test(fileName)) return false;
  const base = path.basename(fileName, path.extname(fileName));
  const root = rootName.replace(/_REV\d+$/i, '');
  if (new RegExp(`^${escapeRe(root)}(_final|_REV\\d+)?$`, 'i').test(base)) return true;
  if (assyId === 'Lower_Frame_assy' && /^LOWER_FRAME_ASSY(_REV\d+|_final)?$/i.test(base)) return true;
  return false;
}

function escapeRe(s) {
  return s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function warnCadFolderWithoutStp(assyId) {
  const kit = getDesktopKitRoot();
  if (!kit) return;
  const cadDir = path.join(kit, '0_CAD원본_보관', assyId);
  if (!fs.existsSync(cadDir)) return;
  const names = fs.readdirSync(cadDir);
  const hasAsm = names.some((n) => /\.(asm|par)$/i.test(n));
  const hasStp =
    names.some((n) => /\.(stp|step)$/i.test(n)) ||
    names.some((n) => /\.%%%$/i.test(n) && isStepFileByHeader(path.join(cadDir, n)));
  if (hasAsm && !hasStp) {
    console.warn(
      `  ⚠ ${cadDir}`,
      '— .par/.asm 만 있음. Solid Edge에서 STP export 후 이 폴더(또는 STP_수정용)에 .stp 저장 필요'
    );
  }
}

function glbSibling(stpPath) {
  const dir = path.dirname(stpPath);
  const stem = path.basename(stpPath, path.extname(stpPath));
  for (const ext of ['.glb', '.gltf']) {
    const p = path.join(dir, stem + ext);
    if (fs.existsSync(p)) return p;
  }
  return null;
}

function copyFileSafe(src, dest, dryRun) {
  if (path.resolve(src) === path.resolve(dest)) return false;
  const st = fs.statSync(src);
  if (fs.existsSync(dest)) {
    const dt = fs.statSync(dest);
    if (dt.size === st.size && dt.mtimeMs >= st.mtimeMs) return false;
  }
  if (dryRun) {
    console.log(`  [dry-run] 복사 ${path.basename(src)} -> ${dest}`);
    return true;
  }
  fs.mkdirSync(path.dirname(dest), { recursive: true });
  fs.copyFileSync(src, dest);
  fs.utimesSync(dest, st.atime, st.mtime);
  console.log(`  복사 ${path.basename(src)} -> ${dest}`);
  return true;
}

async function describeStp(stpPath, cache) {
  const st = fs.statSync(stpPath);
  const probe = await probeStpFile(stpPath, { cache });
  return {
    path: stpPath,
    name: stpBaseName(stpPath),
    rev: parseRevFromName(stpBaseName(stpPath)),
    mtimeMs: st.mtimeMs,
    size: st.size,
    ...probe,
  };
}

/**
 * @param {object} opts
 * @param {string} opts.drawingRoot bin/.../SRM0/3D_Drawing
 * @param {boolean} [opts.dryRun]
 * @param {string} [opts.assyFilter]
 */
export async function syncDesktopOcctModels({ drawingRoot, dryRun = false, assyFilter = null }) {
  const catalogPath = path.join(drawingRoot, 'assemblies.json');
  if (!fs.existsSync(catalogPath)) {
    console.warn('assemblies.json 없음:', drawingRoot);
    return { copied: 0 };
  }

  const catalog = JSON.parse(fs.readFileSync(catalogPath, 'utf8'));
  const searchRoots = getDesktopSearchRoots();
  if (!searchRoots.length) {
    console.warn('바탕화면 GCP_3D_Debug 검색 경로 없음');
    return { copied: 0 };
  }

  const cachePath = path.join(drawingRoot, '.occt-probe-cache.json');
  const cache = readProbeCache(cachePath);
  const desktopStps = [];
  for (const root of searchRoots) {
    walkStpFiles(root, desktopStps);
  }
  console.log(`바탕화면 STP 검색: ${desktopStps.length}개 (${searchRoots.join(' | ')})`);

  let copied = 0;

  for (const assy of catalog.assemblies || []) {
    if (assyFilter && assy.id !== assyFilter) continue;
    const detailRel = assy.detailModel?.replace(/\\/g, '/');
    if (!detailRel) continue;

    const rootName = modelRootFromDetailRel(detailRel);
    const assyDir = path.join(drawingRoot, path.dirname(detailRel));
    if (!fs.existsSync(assyDir)) fs.mkdirSync(assyDir, { recursive: true });
    const profile = loadAssyIoProfileFromDir(assyDir, assy.id);

    const runtimeStp = findHighestRevStp(assyDir, rootName);
    const candidates = desktopStps.filter(
      (p) => stpMatchesAssy(p, rootName, assy.id) && !isStpFileExcluded(path.basename(p), profile)
    );
    if (!candidates.length) {
      console.log(`[${assy.id}] 바탕화면 후보 STP 없음`);
      warnCadFolderWithoutStp(assy.id);
      continue;
    }

    let runtimeDesc = null;
    if (runtimeStp && fs.existsSync(runtimeStp)) {
      runtimeDesc = await describeStp(runtimeStp, cache);
    }

    let best = null;
    for (const c of candidates) {
      if (runtimeStp && path.resolve(c) === path.resolve(runtimeStp)) continue;
      const desc = await describeStp(c, cache);
      if (!best || compareStpCandidates(desc, best) > 0) best = desc;
    }

    if (!best) {
      console.log(`[${assy.id}] 바탕화면 후보 ${candidates.length}개 — 런타임과 동일/비교 불가`);
      continue;
    }

    const rtPos = runtimeDesc?.meshWithPosition ?? 0;
    console.log(
      `[${assy.id}] 런타임 ${runtimeDesc ? `${path.basename(runtimeDesc.path)} mesh=${rtPos}` : '없음'}` +
        ` | 바탕 최고 ${best.name} mesh=${best.meshWithPosition} (${(best.parseMs / 1000).toFixed(1)}s)`
    );

    if (best.meshWithPosition <= 0) {
      console.log(`  → OCCT mesh 없음 — 복사 안 함 (STP export 설정 확인)`);
      continue;
    }
    if (runtimeDesc && compareStpCandidates(best, runtimeDesc) <= 0) {
      console.log(`  → 런타임이 동등/우수 — 복사 안 함`);
      continue;
    }

    const destStp = path.join(assyDir, best.name);
    if (copyFileSafe(best.path, destStp, dryRun)) copied++;

    const glb = glbSibling(best.path);
    if (glb && !isStpFileExcluded(path.basename(glb), profile)) {
      const destGlb = path.join(assyDir, path.basename(glb));
      if (copyFileSafe(glb, destGlb, dryRun)) copied++;
    }
  }

  writeProbeCache(cachePath, cache);
  console.log(dryRun ? `dry-run 완료 (복사 예정 ${copied}파일)` : `동기화 완료 — ${copied}파일 복사`);
  return { copied };
}
