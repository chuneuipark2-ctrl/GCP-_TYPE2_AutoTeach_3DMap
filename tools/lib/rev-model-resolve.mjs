/**
 * GLB/STP _REV{n} 해석 — 확장자별 최대 REV, 없으면 plain {root}.ext
 * C# Dio3DDrawingPath.FindHighestRevModelInDir / Viewer3D/modelResolver.js 와 동일 규칙
 */
import fs from 'fs';
import path from 'path';
import { ASSY_CANONICAL_STP_ROOT } from './manifest-stp-sync.mjs';
import { loadAssyIoProfileFromDir, isStpFileExcluded } from './assy-io-profile.mjs';

function escapeRegex(s) {
  return s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

/** assemblies.json detailModel → root basename (REV 접미사 제거) */
export function modelRootFromDetailRel(detailRel) {
  if (!detailRel) return '';
  const base = path.basename(detailRel.replace(/\\/g, '/'), path.extname(detailRel));
  return base.replace(/_REV\d+$/i, '').replace(/_final$/i, '');
}

/**
 * @param {string} dir ASSY 폴더
 * @param {string} rootName 예: Carriage_Assy, LOWER_FRAME_ASSY
 * @param {string} ext 예: .glb, .stp
 */
export function findHighestRevModel(dir, rootName, ext) {
  if (!dir || !rootName || !ext || !fs.existsSync(dir)) return null;
  const extNorm = ext.startsWith('.') ? ext.toLowerCase() : `.${ext.toLowerCase()}`;
  const assyId = path.basename(dir);
  const profile = loadAssyIoProfileFromDir(dir, assyId);
  const skip = (name) => isStpFileExcluded(name, profile);
  const finalRe = new RegExp(`^${escapeRegex(rootName)}_final\\${extNorm}$`, 'i');
  const revRe = new RegExp(`^${escapeRegex(rootName)}_REV(\\d+)\\${extNorm}$`, 'i');
  const plainRe = new RegExp(`^${escapeRegex(rootName)}\\${extNorm}$`, 'i');

  for (const name of fs.readdirSync(dir)) {
    if (skip(name)) continue;
    if (finalRe.test(name)) return path.join(dir, name);
  }

  let bestRev = -1;
  let bestPath = null;

  for (const name of fs.readdirSync(dir)) {
    if (skip(name)) continue;
    const m = name.match(revRe);
    if (!m) continue;
    const rev = Number(m[1]);
    if (rev > bestRev) {
      bestRev = rev;
      bestPath = path.join(dir, name);
    }
  }

  if (bestPath) return bestPath;

  for (const name of fs.readdirSync(dir)) {
    if (skip(name)) continue;
    if (plainRe.test(name)) return path.join(dir, name);
  }

  return null;
}

export function findHighestRevGlb(dir, rootName) {
  return findHighestRevModel(dir, rootName, '.glb');
}

export function findHighestRevStp(dir, rootName) {
  return (
    findHighestRevModel(dir, rootName, '.stp') ||
    findHighestRevModel(dir, rootName, '.step')
  );
}

export function findHighestRevObj(dir, rootName) {
  return findHighestRevModel(dir, rootName, '.obj');
}

/**
 * ASSY STP — detailModel basename → canonical(Lower_Frame→LOWER_FRAME_ASSY) → 폴더 내 최신 STP
 */
export function resolveAssyStpPath(assyDir, assyId, detailRel) {
  if (!assyDir || !fs.existsSync(assyDir)) return null;
  const profile = loadAssyIoProfileFromDir(assyDir, assyId);

  const rootName = modelRootFromDetailRel(detailRel);
  const tryRoots = [profile.stpCanonicalBase, rootName, ASSY_CANONICAL_STP_ROOT[assyId]].filter(Boolean);
  const seen = new Set();

  for (const root of tryRoots) {
    const key = root.toLowerCase();
    if (seen.has(key)) continue;
    seen.add(key);
    const stp = findHighestRevStp(assyDir, root);
    if (stp && !isStpFileExcluded(stp, profile)) return stp;
  }

  const stps = fs
    .readdirSync(assyDir)
    .filter((n) => /\.(stp|step)$/i.test(n) && !isStpFileExcluded(n, profile))
    .map((n) => {
      const p = path.join(assyDir, n);
      const st = fs.statSync(p);
      return { p, mtimeMs: st.mtimeMs, size: st.size, name: n };
    })
    .sort((a, b) => b.mtimeMs - a.mtimeMs || b.size - a.size);

  return stps[0]?.p || null;
}
