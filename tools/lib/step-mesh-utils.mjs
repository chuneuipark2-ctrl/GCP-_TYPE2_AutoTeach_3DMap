/**
 * occt-import-js mesh 추출 / 아웃라이어 필터 (stepWorker.js와 동일 규칙)
 */

export const DEFAULT_STEP_PARAMS = {
  linearUnit: 'millimeter',
  linearDeflectionType: 'bounding_box_ratio',
  linearDeflection: 0.12,
  angularDeflection: 0.35,
};

export function toFloat32(src) {
  if (!src) return null;
  if (src instanceof Float32Array) return src;
  if (src.array) return Float32Array.from(src.array);
  if (Array.isArray(src)) return Float32Array.from(src);
  return null;
}

export function toUint32(src) {
  if (!src) return null;
  if (src instanceof Uint32Array) return src;
  if (src.array) return Uint32Array.from(src.array);
  if (Array.isArray(src)) return Uint32Array.from(src);
  return null;
}

export function extractMesh(m) {
  const position = toFloat32(m?.attributes?.position);
  if (!position || position.length < 9) return null;

  let index = toUint32(m?.index);
  if (!index) {
    index = new Uint32Array(position.length / 3);
    for (let i = 0; i < index.length; i++) index[i] = i;
  }

  return {
    name: (m.name || '').trim() || 'mesh',
    color: m.color ? [m.color[0], m.color[1], m.color[2]] : null,
    position,
    normal: toFloat32(m?.attributes?.normal),
    index,
  };
}

export function meshBounds(position) {
  let minX = Infinity;
  let minY = Infinity;
  let minZ = Infinity;
  let maxX = -Infinity;
  let maxY = -Infinity;
  let maxZ = -Infinity;
  for (let i = 0; i < position.length; i += 3) {
    const x = position[i];
    const y = position[i + 1];
    const z = position[i + 2];
    if (x < minX) minX = x;
    if (x > maxX) maxX = x;
    if (y < minY) minY = y;
    if (y > maxY) maxY = y;
    if (z < minZ) minZ = z;
    if (z > maxZ) maxZ = z;
  }
  const sx = maxX - minX;
  const sy = maxY - minY;
  const sz = maxZ - minZ;
  return {
    cx: (minX + maxX) / 2,
    cy: (minY + maxY) / 2,
    cz: (minZ + maxZ) / 2,
    sx,
    sy,
    sz,
    vol: sx * sy * sz,
    diag: Math.hypot(sx, sy, sz),
    /** 센서 구 등 — bbox 최대변의 절반 (mm) */
    radiusMm: Math.max(sx, sy, sz) / 2,
  };
}

/** 멀리 떨어진 작은 mesh 제외 (viewer stepWorker와 동일) */
export function filterMainMeshes(items) {
  if (items.length <= 1) return { kept: items, dropped: 0 };

  const tagged = items.map((m) => ({ m, b: meshBounds(m.position) }));
  tagged.sort((a, b) => b.b.vol - a.b.vol);

  let wSum = 0;
  let wx = 0;
  let wy = 0;
  let wz = 0;
  for (const t of tagged) {
    wSum += t.b.vol;
    wx += t.b.cx * t.b.vol;
    wy += t.b.cy * t.b.vol;
    wz += t.b.cz * t.b.vol;
  }
  wx /= wSum;
  wy /= wSum;
  wz /= wSum;

  const mainDiag = tagged[0].b.diag;
  const maxVol = tagged[0].b.vol;
  const maxDist = Math.max(mainDiag * 2.5, 8000);

  const kept = [];
  let dropped = 0;
  for (const t of tagged) {
    const d = Math.hypot(t.b.cx - wx, t.b.cy - wy, t.b.cz - wz);
    const tiny = t.b.vol < maxVol * 0.01;
    if (d > maxDist && tiny) {
      dropped++;
      continue;
    }
    if (d > maxDist * 5) {
      dropped++;
      continue;
    }
    kept.push(t.m);
  }

  return { kept: kept.length ? kept : [tagged[0].m], dropped };
}

export function mergeMeshes(items, name = 'merged') {
  if (!items.length) return [];
  if (items.length === 1) return items;

  let posLen = 0;
  let idxLen = 0;
  for (const m of items) {
    posLen += m.position.length;
    idxLen += m.index.length;
  }

  const position = new Float32Array(posLen);
  const index = new Uint32Array(idxLen);
  const hasNormal = items.every((m) => m.normal && m.normal.length === m.position.length);
  const normal = hasNormal ? new Float32Array(posLen) : null;

  let vOff = 0;
  let iOff = 0;
  let vBase = 0;

  for (const m of items) {
    position.set(m.position, vOff);
    if (normal && m.normal) normal.set(m.normal, vOff);
    for (let i = 0; i < m.index.length; i++) index[iOff + i] = m.index[i] + vBase;
    vOff += m.position.length;
    iOff += m.index.length;
    vBase += m.position.length / 3;
  }

  return [
    {
      name,
      color: items[0].color,
      position,
      normal,
      index,
    },
  ];
}

export function dedupeMeshNames(meshes) {
  const used = new Map();
  return meshes.map((m) => {
    const base = m.name || 'mesh';
    const n = (used.get(base) || 0) + 1;
    used.set(base, n);
    return n === 1 ? m : { ...m, name: `${base}_${n}` };
  });
}
