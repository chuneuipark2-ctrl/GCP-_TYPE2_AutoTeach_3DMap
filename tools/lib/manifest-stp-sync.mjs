/**
 * io_manifest.json ↔ STP 소스 동기화 — mtime/size·내용(stale) 기준
 */
import fs from 'fs';
import path from 'path';
import { filterReliableManifestPoints } from './assy-io-profile.mjs';

/** C# Dio3DDrawingPath canonical STP basename 과 동일 */
export const ASSY_CANONICAL_STP_ROOT = {
  SCP: 'SCP',
  Lower_Frame_assy: 'LOWER_FRAME_ASSY',
  Carriage_Assy: 'Carriage_Assy',
};

export function readStpFileMeta(stpPath) {
  if (!stpPath || !fs.existsSync(stpPath)) return null;
  const st = fs.statSync(stpPath);
  return {
    file: path.basename(stpPath),
    absPath: path.resolve(stpPath),
    mtimeMs: st.mtimeMs,
    sizeBytes: st.size,
  };
}

export function readManifestStpSource(manifest) {
  if (!manifest?.stpSource) return null;
  const s = manifest.stpSource;
  if (s.mtimeMs == null) return null;
  return {
    file: s.file || manifest.stpFile || null,
    mtimeMs: Number(s.mtimeMs),
    sizeBytes: s.sizeBytes != null ? Number(s.sizeBytes) : null,
  };
}

/** STP가 manifest보다 새로우면 true (없거나 legacy 포함) */
export function isManifestStale(manifestPath, stpPath) {
  const stpMeta = readStpFileMeta(stpPath);
  if (!stpMeta) return false;
  if (!manifestPath || !fs.existsSync(manifestPath)) return true;

  let manifest;
  try {
    manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
  } catch {
    return true;
  }

  const recorded = readManifestStpSource(manifest);
  if (!recorded) {
    const manifestMtime = fs.statSync(manifestPath).mtimeMs;
    if (stpMeta.mtimeMs > manifestMtime + 500) return true;
    if (manifest.stpFile && manifest.stpFile !== stpMeta.file) return true;
    return false;
  }

  if (recorded.file && recorded.file !== stpMeta.file) return true;
  if (Math.abs(stpMeta.mtimeMs - recorded.mtimeMs) > 0.5) return true;
  if (recorded.sizeBytes != null && recorded.sizeBytes !== stpMeta.sizeBytes) return true;
  if (isManifestContentStale(manifest)) return true;
  return false;
}

/** manifest points 중 뷰어가 신뢰하는 좌표 개수 */
export function countReliableManifestPoints(manifest) {
  const { points } = filterReliableManifestPoints(manifest?.points || {});
  return Object.keys(points).length;
}

/** STP는 있는데 points/matchCount/reliable 좌표가 비어 있으면 stale */
export function isManifestContentStale(manifest) {
  if (!manifest || typeof manifest !== 'object') return true;
  const signalCount = Number(manifest.signalCount) || 0;
  if (signalCount <= 0) return false;

  const matchCount = manifest.matchCount != null ? Number(manifest.matchCount) : -1;
  const pointCount =
    manifest.points && typeof manifest.points === 'object' ? Object.keys(manifest.points).length : 0;
  const reliableCount = countReliableManifestPoints(manifest);
  const sensorOcc = Number(manifest.sensorOccurrenceCount) || 0;

  if (reliableCount === 0) return true;
  if (pointCount === 0 || matchCount === 0) return true;
  if (sensorOcc > 0 && reliableCount === 0) return true;
  return false;
}

export function buildStpSourceField(stpPath) {
  const meta = readStpFileMeta(stpPath);
  if (!meta) return null;
  return {
    file: meta.file,
    mtimeMs: meta.mtimeMs,
    sizeBytes: meta.sizeBytes,
    checkedAt: new Date().toISOString(),
  };
}

function pointDistMm(a, b) {
  if (!a || !b || a.x == null || b.x == null) return Infinity;
  return Math.hypot(a.x - b.x, a.y - b.y, a.z - b.z);
}

/** manifest points diff — STP 재파싱 후 데이터 변경 여부 */
export function diffManifestPoints(oldPoints = {}, newPoints = {}) {
  const oldKeys = new Set(Object.keys(oldPoints));
  const newKeys = new Set(Object.keys(newPoints));
  const added = [...newKeys].filter((k) => !oldKeys.has(k));
  const removed = [...oldKeys].filter((k) => !newKeys.has(k));
  const moved = [];
  const unchanged = [];

  for (const k of [...oldKeys].filter((key) => newKeys.has(key))) {
    const dist = pointDistMm(oldPoints[k], newPoints[k]);
    if (dist > 0.5) {
      moved.push({
        key: k,
        distMm: Math.round(dist * 100) / 100,
        from: [oldPoints[k].x, oldPoints[k].y, oldPoints[k].z],
        to: [newPoints[k].x, newPoints[k].y, newPoints[k].z],
      });
    } else {
      unchanged.push(k);
    }
  }

  const changed = added.length > 0 || removed.length > 0 || moved.length > 0;
  return { added, removed, moved, unchanged, changed };
}

export function formatManifestDiffSummary(diff) {
  if (!diff?.changed) return 'I/O 좌표 변경 없음';
  const parts = [];
  if (diff.added.length) parts.push(`추가 ${diff.added.length}`);
  if (diff.removed.length) parts.push(`삭제 ${diff.removed.length}`);
  if (diff.moved.length) parts.push(`이동 ${diff.moved.length}`);
  return parts.join(', ');
}
