/**
 * 뷰어용 ASSY I/O 프로필 — 각 ASSY 폴더 ASSY_IO_PROFILE.json 과 동기화
 * (tools/lib/assy-io-profile.mjs 와 동일 규칙)
 */
export const ASSY_IO_PROFILE_DEFAULTS = {
  SCP: {
    stpCanonicalBase: 'SCP',
    excludeStpNamePatterns: [],
    viewer: { skipSurfaceSnapPatterns: ['^Tower_Lamp_'], deoverlapGapMm: 6, surfaceSnapMaxMm: 0 },
  },
  Lower_Frame_assy: {
    stpCanonicalBase: 'LOWER_FRAME_ASSY',
    excludeStpNamePatterns: ['ELEC_CONVERT'],
    viewer: {
      skipSurfaceSnapPatterns: ['^Travel_', '^Modem_Fault$', '^Station_(Ready|Stop)_\\d+$'],
      deoverlapGapMm: 6,
      surfaceSnapMaxMm: 0,
    },
  },
  Carriage_Assy: {
    stpCanonicalBase: 'Carriage_Assy',
    excludeStpNamePatterns: [],
    viewer: { skipSurfaceSnapPatterns: ['^LiDAR[12]_'], deoverlapGapMm: 6, surfaceSnapMaxMm: 0 },
  },
};

export function mergeAssyIoProfile(assyId, fileJson) {
  const base = ASSY_IO_PROFILE_DEFAULTS[assyId] || {
    stpCanonicalBase: assyId,
    excludeStpNamePatterns: [],
    viewer: { skipSurfaceSnapPatterns: [], deoverlapGapMm: 6, surfaceSnapMaxMm: 0 },
  };
  if (!fileJson) return { assyId, ...base, viewer: { ...base.viewer } };
  return {
    assyId,
    ...base,
    ...fileJson,
    excludeStpNamePatterns: fileJson.excludeStpNamePatterns ?? base.excludeStpNamePatterns,
    viewer: { ...base.viewer, ...(fileJson.viewer || {}) },
  };
}

export function shouldSkipIoSurfaceSnap(signalKey, profile) {
  const k = signalKey || '';
  for (const pat of profile?.viewer?.skipSurfaceSnapPatterns || []) {
    try {
      if (new RegExp(pat, 'i').test(k)) return true;
    } catch {
      if (k.toLowerCase().includes(String(pat).toLowerCase())) return true;
    }
  }
  return false;
}

export function getDeoverlapGapMm(profile) {
  return profile?.viewer?.deoverlapGapMm > 0 ? profile.viewer.deoverlapGapMm : 6;
}

/** 전 ASSY 동일 — manifest·뷰어 표시 허용 matchMode */
export const RELIABLE_MANIFEST_MATCH_MODES = new Set([
  'stp_occurrence',
  'stp_occurrence_mesh_center',
]);

const UNRELIABLE_SOURCE_MESH_RE = /GDFL|8BIT/i;

export function isReliableManifestPoint(pt) {
  if (!pt || pt.x == null || pt.y == null || pt.z == null) return false;
  const mode = String(pt.matchMode || '');
  if (!RELIABLE_MANIFEST_MATCH_MODES.has(mode)) return false;
  if (UNRELIABLE_SOURCE_MESH_RE.test(String(pt.sourceMesh || ''))) return false;
  return true;
}

export function filterReliableManifestPoints(points = {}) {
  const out = {};
  let dropped = 0;
  for (const [key, pt] of Object.entries(points || {})) {
    if (isReliableManifestPoint(pt)) out[key] = pt;
    else dropped++;
  }
  return { points: out, dropped };
}

export function isExcludedStpFileName(fileName, profile) {
  const base = String(fileName || '');
  for (const pat of profile?.excludeStpNamePatterns || []) {
    if (base.toLowerCase().includes(String(pat).toLowerCase())) return true;
  }
  return false;
}
