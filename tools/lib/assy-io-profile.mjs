/**
 * ASSY별 I/O·STP·뷰어 규칙 — SCP / Lower_Frame_assy / Carriage_Assy 전용 분리
 * 각 ASSY 폴더 ASSY_IO_PROFILE.json 과 동기화
 */
import fs from 'fs';
import path from 'path';

/** @typedef {'stp_occurrence'|'solid_edge_io_define'|'io_stp_sensor_map'} ManifestMatchPriority */

/** 뷰어·manifest에 허용 — 전 ASSY 동일 (fallback 금지) */
export const RELIABLE_MANIFEST_MATCH_MODES = new Set([
  'stp_occurrence',
  'stp_occurrence_mesh_center',
]);

/** GDFL/8BIT 등 범용 센서 part — occurrence 이름 매칭 아님 */
export const UNRELIABLE_SOURCE_MESH_RE = /GDFL|8BIT/i;

export function isReliableManifestPoint(pt) {
  if (!pt || pt.x == null || pt.y == null || pt.z == null) return false;
  const mode = String(pt.matchMode || '');
  if (!RELIABLE_MANIFEST_MATCH_MODES.has(mode)) return false;
  if (UNRELIABLE_SOURCE_MESH_RE.test(String(pt.sourceMesh || ''))) return false;
  return true;
}

/** fallback·추측 좌표 제거 — stp_occurrence 만 유지 */
export function filterReliableManifestPoints(points = {}) {
  const out = {};
  let dropped = 0;
  for (const [key, pt] of Object.entries(points || {})) {
    if (isReliableManifestPoint(pt)) out[key] = pt;
    else dropped++;
  }
  return { points: out, dropped };
}

/** @type {Record<string, object>} */
export const ASSY_IO_PROFILE_DEFAULTS = {
  SCP: {
    assyId: 'SCP',
    label: 'Standing_Control_Panel',
    stpCanonicalBase: 'SCP',
    excludeStpNamePatterns: [],
    manifest: {
      matchPriority: ['stp_occurrence', 'solid_edge_io_define'],
      sensorPartNamePatterns: [],
      useIoStpSensorMap: false,
      allowSensorPartSeq: false,
      defaultRadiusMm: 20,
    },
    viewer: {
      skipSurfaceSnapPatterns: ['^Tower_Lamp_'],
      deoverlapGapMm: 6,
      surfaceSnapMaxMm: 0,
    },
  },
  Lower_Frame_assy: {
    assyId: 'Lower_Frame_assy',
    label: 'Lower_Frame_assy',
    stpCanonicalBase: 'LOWER_FRAME_ASSY',
    excludeStpNamePatterns: ['ELEC_CONVERT'],
    manifest: {
      matchPriority: ['stp_occurrence', 'solid_edge_io_define'],
      /** GDFL/8BIT 없음 — STP 배치명=PLC키 직접 읽기만 */
      sensorPartNamePatterns: [],
      useIoStpSensorMap: false,
      allowSensorPartSeq: false,
      defaultRadiusMm: 20,
    },
    viewer: {
      skipSurfaceSnapPatterns: [
        '^Travel_',
        '^Modem_Fault$',
      ],
      deoverlapGapMm: 6,
      surfaceSnapMaxMm: 0,
    },
  },
  Carriage_Assy: {
    assyId: 'Carriage_Assy',
    label: 'Carriage_Assy',
    stpCanonicalBase: 'Carriage_Assy',
    excludeStpNamePatterns: [],
    manifest: {
      matchPriority: ['stp_occurrence', 'solid_edge_io_define'],
      sensorPartNamePatterns: [],
      useIoStpSensorMap: false,
      allowSensorPartSeq: false,
      defaultRadiusMm: 20,
    },
    viewer: {
      skipSurfaceSnapPatterns: ['^LiDAR[12]_'],
      deoverlapGapMm: 6,
      surfaceSnapMaxMm: 0,
    },
  },
  '8bit_sensor': {
    assyId: '8bit_sensor',
    label: '8bit_sensor',
    stpCanonicalBase: '8bit_part_assy',
    excludeStpNamePatterns: ['ELEC_CONVERT'],
    manifest: {
      matchPriority: ['stp_occurrence', 'solid_edge_io_define'],
      sensorPartNamePatterns: [],
      useIoStpSensorMap: false,
      allowSensorPartSeq: false,
      defaultRadiusMm: 5,
    },
    viewer: {
      markerShape: 'box',
      markerBoxMm: [4, 4, 4],
      skipSurfaceSnapPatterns: ['^Station_(Ready|Stop)_\\d+$'],
      deoverlapGapMm: 0.5,
      surfaceSnapMaxMm: 0,
    },
  },
  Lidar: {
    assyId: 'Lidar',
    label: 'Lidar',
    stpCanonicalBase: 'Carriage_Assy',
    excludeStpNamePatterns: [],
    manifest: {
      matchPriority: ['stp_occurrence', 'solid_edge_io_define'],
      sensorPartNamePatterns: [],
      useIoStpSensorMap: false,
      allowSensorPartSeq: false,
      defaultRadiusMm: 20,
    },
    viewer: {
      skipSurfaceSnapPatterns: ['^LiDAR[12]_'],
      deoverlapGapMm: 6,
      surfaceSnapMaxMm: 0,
    },
  },
};

export function getAssyIoProfile(assyId) {
  const base = ASSY_IO_PROFILE_DEFAULTS[assyId];
  if (!base) {
    return {
      assyId,
      stpCanonicalBase: assyId,
      excludeStpNamePatterns: [],
      manifest: {
        matchPriority: ['stp_occurrence', 'solid_edge_io_define'],
        sensorPartNamePatterns: [],
        useIoStpSensorMap: false,
        allowSensorPartSeq: false,
        defaultRadiusMm: 20,
      },
      viewer: { skipSurfaceSnapPatterns: [], deoverlapGapMm: 6 },
    };
  }
  return JSON.parse(JSON.stringify(base));
}

export function loadAssyIoProfileFromDir(assyDir, assyId) {
  const fallback = getAssyIoProfile(assyId);
  const jsonPath = path.join(assyDir, 'ASSY_IO_PROFILE.json');
  if (!fs.existsSync(jsonPath)) return fallback;
  try {
    const file = JSON.parse(fs.readFileSync(jsonPath, 'utf8'));
    return {
      ...fallback,
      ...file,
      manifest: { ...fallback.manifest, ...(file.manifest || {}) },
      viewer: { ...fallback.viewer, ...(file.viewer || {}) },
    };
  } catch {
    return fallback;
  }
}

export function isStpFileExcluded(fileName, profile) {
  const base = path.basename(fileName || '');
  for (const pat of profile?.excludeStpNamePatterns || []) {
    if (base.toLowerCase().includes(String(pat).toLowerCase())) return true;
  }
  return false;
}

export function partNameMatchesSensorPatterns(name, patterns) {
  if (!patterns?.length) return false;
  const n = String(name || '');
  return patterns.some((pat) => {
    try {
      return new RegExp(pat, 'i').test(n);
    } catch {
      return n.toLowerCase().includes(String(pat).toLowerCase());
    }
  });
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
