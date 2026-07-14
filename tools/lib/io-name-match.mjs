/** I/O 키 ↔ CAD mesh/part 이름 매칭 (viewer.js 와 동일 규칙) */
import { partNameMatchesSensorPatterns } from './assy-io-profile.mjs';

export function normalizeIoName(name) {
  return String(name || '')
    .trim()
    .toLowerCase()
    .replace(/^io[_-]?/, '')
    .replace(/[\s-]+/g, '_');
}

export function meshNameTokens(meshName) {
  const tokens = new Set();
  const raw = String(meshName || '').trim();
  if (!raw) return tokens;
  tokens.add(normalizeIoName(raw));
  for (const part of raw.split(/[/\\|>]/)) {
    const t = normalizeIoName(part);
    if (t) tokens.add(t);
  }
  return tokens;
}

export function scoreMeshNameMatch(meshName, key) {
  const kn = normalizeIoName(key);
  if (!kn) return 0;
  const tokens = meshNameTokens(meshName);
  if (tokens.has(kn)) return 100;
  if (tokens.has(`io_${kn}`)) return 95;
  let best = 0;
  for (const t of tokens) {
    if (t === kn) best = Math.max(best, 100);
    else if (t.endsWith(`_${kn}`) || t.endsWith(kn)) best = Math.max(best, 85);
    else if (kn.length >= 12 && t.includes(kn)) best = Math.max(best, 70);
  }
  return best;
}

/** signalKeys — STP occurrence/part 이름 = PLC키 (정확 일치 우선) */
export function matchIoKeysToMeshes(signalKeys, meshes, aliasMap = null) {
  const used = new Set();
  const points = {};
  const matches = [];

  function pickMesh(target) {
    const t = normalizeIoName(target);
    for (let i = 0; i < meshes.length; i++) {
      if (used.has(i)) continue;
      const m = meshes[i];
      const names = [m.occurrenceName, m.partName, m.name].filter(Boolean).map(normalizeIoName);
      if (names.includes(t)) return { idx: i, score: 100 };
    }
    let bestIdx = -1;
    let bestScore = 0;
    for (let i = 0; i < meshes.length; i++) {
      if (used.has(i)) continue;
      const m = meshes[i];
      const score = Math.max(
        scoreMeshNameMatch(m.occurrenceName, target),
        scoreMeshNameMatch(m.partName, target),
        scoreMeshNameMatch(m.name, target)
      );
      if (score > bestScore) {
        bestScore = score;
        bestIdx = i;
      }
    }
    if (bestIdx < 0 || bestScore < 85) return null;
    return { idx: bestIdx, score: bestScore };
  }

  for (const key of signalKeys) {
    const target = aliasMap?.get(key) || key;
    const pick = pickMesh(target);
    if (!pick) continue;
    used.add(pick.idx);
    const bestMesh = meshes[pick.idx];
    points[key] = {
      x: bestMesh.cx,
      y: bestMesh.cy,
      z: bestMesh.cz,
      occurrenceName: bestMesh.occurrenceName || bestMesh.name,
      partName: bestMesh.partName || bestMesh.name,
      sourceMesh: bestMesh.occurrenceName || bestMesh.partName || bestMesh.name,
      solidEdgeName: target !== key ? target : undefined,
      matchMode: 'stp_occurrence',
    };
    matches.push({
      key,
      mesh: bestMesh.occurrenceName || bestMesh.name,
      partName: bestMesh.partName,
      score: pick.score,
      target,
      meshIndex: pick.idx,
    });
  }

  return { points, matches };
}

export function collectPlcTargetOccurrenceNames(signalKeys, seMap) {
  const targets = new Set();
  for (const key of signalKeys || []) {
    targets.add(String(key).toLowerCase());
    const se = seMap?.get?.(key);
    if (se) targets.add(String(se).toLowerCase());
  }
  return targets;
}

export function occurrenceMatchesPlcTargets(o, targets) {
  const on = String(o?.occurrenceName || '').toLowerCase();
  const pn = String(o?.partName || o?.name || '').toLowerCase();
  return targets.has(on) || targets.has(pn);
}

/** ASSY 프로필 기준 STP occurrence/mesh 후보 */
export function filterSensorOccurrences(allOccurrences, profile, signalKeys, seMap) {
  const targets = collectPlcTargetOccurrenceNames(signalKeys, seMap);
  const patterns = profile?.manifest?.sensorPartNamePatterns || [];

  return allOccurrences.filter((o) => {
    if (occurrenceMatchesPlcTargets(o, targets)) return true;
    if (patterns.length) {
      return (
        partNameMatchesSensorPatterns(o.occurrenceName, patterns) ||
        partNameMatchesSensorPatterns(o.partName, patterns)
      );
    }
    return false;
  });
}

/** IO_STP_SENSOR_MAP 번호 목록용 — ASSY 프로필 전용 */
export function buildStpIndexSensorMeshes(profile, occCenters, meshCenters, seMap, signalKeys) {
  const targets = collectPlcTargetOccurrenceNames(signalKeys, seMap);
  const patterns = profile?.manifest?.sensorPartNamePatterns || [];
  const sensorMeshes = [];

  occCenters.forEach((m, idx) => {
    const label = m.partName || m.name || m.occurrenceName;
    if (occurrenceMatchesPlcTargets(m, targets)) {
      sensorMeshes.push({ ...m, idx, name: m.occurrenceName || label });
      return;
    }
    if (patterns.length && partNameMatchesSensorPatterns(label, patterns)) {
      sensorMeshes.push({ ...m, idx, name: label });
    }
  });

  if (!sensorMeshes.length && meshCenters?.length) {
    meshCenters.forEach((m, idx) => {
      if (occurrenceMatchesPlcTargets({ occurrenceName: m.name, partName: m.name }, targets)) {
        sensorMeshes.push({ ...m, idx });
        return;
      }
      if (patterns.length && partNameMatchesSensorPatterns(m.name, patterns)) {
        sensorMeshes.push({ ...m, idx });
      }
    });
  }

  sensorMeshes.sort((a, b) => a.cz - b.cz || a.cy - b.cy || a.cx - b.cx);
  return sensorMeshes;
}

/** 프로필 allowSensorPartSeq + sensorPartNamePatterns 있을 때만 */
export function matchSensorPartsSequential(signalKeys, meshes, existingPoints = {}, profile = null) {
  const points = { ...existingPoints };
  const matches = [];
  if (!profile?.manifest?.allowSensorPartSeq) return { points, matches };

  const patterns = profile.manifest.sensorPartNamePatterns || [];
  if (!patterns.length) return { points, matches };

  const usedIdx = new Set();
  for (const m of Object.values(existingPoints)) {
    const idx = meshes.findIndex(
      (x) =>
        x.name === m.sourceMesh &&
        Math.abs(x.cx - m.x) < 0.01 &&
        Math.abs(x.cy - m.y) < 0.01 &&
        Math.abs(x.cz - m.z) < 0.01
    );
    if (idx >= 0) usedIdx.add(idx);
  }

  const sensorMeshes = [];
  meshes.forEach((m, idx) => {
    if (usedIdx.has(idx)) return;
    if (!partNameMatchesSensorPatterns(m.name, patterns)) return;
    sensorMeshes.push({ ...m, idx });
  });
  sensorMeshes.sort((a, b) => a.cz - b.cz || a.cy - b.cy || a.cx - b.cx);

  const unmatchedKeys = signalKeys.filter((k) => !points[k]);
  for (let i = 0; i < unmatchedKeys.length && i < sensorMeshes.length; i++) {
    const key = unmatchedKeys[i];
    const m = sensorMeshes[i];
    usedIdx.add(m.idx);
    points[key] = {
      x: m.cx,
      y: m.cy,
      z: m.cz,
      sourceMesh: m.name,
      matchMode: 'sensor_part_seq',
      radiusMm: m.radiusMm,
      bboxSizeMm: m.sx != null ? [m.sx, m.sy, m.sz] : undefined,
    };
    matches.push({ key, mesh: m.name, meshIndex: m.idx, mode: 'sensor_part_seq' });
  }

  return { points, matches };
}
