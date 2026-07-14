/**
 * STEP(AP214) — 배치 이름(NEXT_ASSEMBLY_USAGE_OCCURRENCE) + 어셈블리 트리 누적 배치
 * part 파일명(PRODUCT)과 배치 이름이 다를 때 배치 이름 우선
 */

function parseEntities(stpText) {
  const entities = new Map();
  const chunks = stpText.split(/;\r?\n/);
  for (const chunk of chunks) {
    const head = chunk.match(/#(\d+)\s*=\s*([A-Z0-9_]+)?\s*\(/);
    if (!head) continue;
    const id = Number(head[1]);
    const type = head[2] || 'AGGREGATE';
    entities.set(id, { id, type, raw: chunk });
  }
  return entities;
}

function refIdsInArgs(raw) {
  const m = raw.match(/\([\s\S]*\)/);
  if (!m) return [];
  return [...m[0].matchAll(/#(\d+)/g)].map((x) => Number(x[1]));
}

function refId(raw, idx = 0) {
  const refs = refIdsInArgs(raw);
  return refs[idx] ?? null;
}

function quotedStrings(raw) {
  return [...raw.matchAll(/'([^']*)'/g)].map((m) => m[1]);
}

function parseCartesianPoint(entities, id) {
  const e = entities.get(id);
  if (!e) return null;
  const m = e.raw.match(/CARTESIAN_POINT\s*\([^,]*,\s*\(([^)]+)\)/);
  if (!m) return null;
  const nums = m[1].split(',').map((s) => Number(s.trim()));
  if (nums.length < 3 || nums.some((n) => !Number.isFinite(n))) return null;
  return { x: nums[0], y: nums[1], z: nums[2] };
}

function parseDirection(entities, id) {
  const e = entities.get(id);
  if (!e) return null;
  const m = e.raw.match(/DIRECTION\s*\([^,]*,\s*\(([^)]+)\)/);
  if (!m) return null;
  const nums = m[1].split(',').map((s) => Number(s.trim()));
  if (nums.length < 3 || nums.some((n) => !Number.isFinite(n))) return null;
  const len = Math.hypot(nums[0], nums[1], nums[2]) || 1;
  return { x: nums[0] / len, y: nums[1] / len, z: nums[2] / len };
}

/** 4×4 column-major (Three.js) — AXIS2_PLACEMENT_3D */
function parseAxis2Matrix(entities, id, depth = 0) {
  if (!id || depth > 12) return identityMat4();
  const e = entities.get(id);
  if (!e) return identityMat4();
  if (e.type === 'CARTESIAN_POINT') {
    const p = parseCartesianPoint(entities, id);
    return p ? translationMat4(p.x, p.y, p.z) : identityMat4();
  }
  if (e.type !== 'AXIS2_PLACEMENT_3D') return identityMat4();

  const refs = refIdsInArgs(e.raw);
  const loc = parseCartesianPoint(entities, refs[0]) || { x: 0, y: 0, z: 0 };
  const zAxis = parseDirection(entities, refs[1]) || { x: 0, y: 0, z: 1 };
  const xHint = parseDirection(entities, refs[2]) || { x: 1, y: 0, z: 0 };

  let zx = zAxis.x;
  let zy = zAxis.y;
  let zz = zAxis.z;
  let dot = xHint.x * zx + xHint.y * zy + xHint.z * zz;
  let xx = xHint.x - dot * zx;
  let xy = xHint.y - dot * zy;
  let xz = xHint.z - dot * zz;
  const xLen = Math.hypot(xx, xy, xz) || 1;
  xx /= xLen;
  xy /= xLen;
  xz /= xLen;

  const yx = zy * xz - zz * xy;
  const yy = zz * xx - zx * xz;
  const yz = zx * xy - zy * xx;

  return [
    xx, yx, zx, 0,
    xy, yy, zy, 0,
    xz, yz, zz, 0,
    loc.x, loc.y, loc.z, 1,
  ];
}

function identityMat4() {
  return [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];
}

function translationMat4(x, y, z) {
  return [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, x, y, z, 1];
}

function multiplyMat4(a, b) {
  const out = new Array(16).fill(0);
  for (let c = 0; c < 4; c++) {
    for (let r = 0; r < 4; r++) {
      out[c * 4 + r] =
        a[0 * 4 + r] * b[c * 4 + 0] +
        a[1 * 4 + r] * b[c * 4 + 1] +
        a[2 * 4 + r] * b[c * 4 + 2] +
        a[3 * 4 + r] * b[c * 4 + 3];
    }
  }
  return out;
}

function transformPoint(m, x, y, z) {
  return {
    x: m[0] * x + m[4] * y + m[8] * z + m[12],
    y: m[1] * x + m[5] * y + m[9] * z + m[13],
    z: m[2] * x + m[6] * y + m[10] * z + m[14],
  };
}

function parseItemDefinedMatrix(entities, id) {
  const e = entities.get(id);
  if (!e || e.type !== 'ITEM_DEFINED_TRANSFORMATION') return identityMat4();
  const refs = refIdsInArgs(e.raw);
  if (refs.length < 2) {
    const placementRef = refs[refs.length - 1];
    return parseAxis2Matrix(entities, placementRef);
  }
  // AP214: mapping_origin → mapping_target (부모 좌표계에서 자식 배치)
  const from = parseAxis2Matrix(entities, refs[refs.length - 2]);
  const to = parseAxis2Matrix(entities, refs[refs.length - 1]);
  return multiplyMat4(invertMat4(from), to);
}

function invertMat4(m) {
  const a = m;
  const b = new Array(16);
  b[0] = a[5] * a[10] * a[15] - a[5] * a[11] * a[14] - a[9] * a[6] * a[15] + a[9] * a[7] * a[14] + a[13] * a[6] * a[11] - a[13] * a[7] * a[10];
  b[4] = -a[4] * a[10] * a[15] + a[4] * a[11] * a[14] + a[8] * a[6] * a[15] - a[8] * a[7] * a[14] - a[12] * a[6] * a[11] + a[12] * a[7] * a[10];
  b[8] = a[4] * a[9] * a[15] - a[4] * a[11] * a[13] - a[8] * a[5] * a[15] + a[8] * a[7] * a[13] + a[12] * a[5] * a[11] - a[12] * a[7] * a[9];
  b[12] = -a[4] * a[9] * a[14] + a[4] * a[10] * a[13] + a[8] * a[5] * a[14] - a[8] * a[6] * a[13] - a[12] * a[5] * a[10] + a[12] * a[6] * a[9];
  b[1] = -a[1] * a[10] * a[15] + a[1] * a[11] * a[14] + a[9] * a[6] * a[15] - a[9] * a[7] * a[14] - a[13] * a[6] * a[11] + a[13] * a[7] * a[10];
  b[5] = a[0] * a[10] * a[15] - a[0] * a[11] * a[14] - a[8] * a[2] * a[15] + a[8] * a[3] * a[14] + a[12] * a[2] * a[11] - a[12] * a[3] * a[10];
  b[9] = -a[0] * a[9] * a[15] + a[0] * a[11] * a[13] + a[8] * a[1] * a[15] - a[8] * a[3] * a[13] - a[12] * a[1] * a[11] + a[12] * a[3] * a[9];
  b[13] = a[0] * a[9] * a[14] - a[0] * a[10] * a[13] - a[8] * a[1] * a[14] + a[8] * a[2] * a[13] + a[12] * a[1] * a[10] - a[12] * a[2] * a[9];
  b[2] = a[1] * a[6] * a[15] - a[1] * a[7] * a[14] - a[5] * a[2] * a[15] + a[5] * a[3] * a[14] + a[13] * a[2] * a[7] - a[13] * a[3] * a[6];
  b[6] = -a[0] * a[6] * a[15] + a[0] * a[7] * a[14] + a[4] * a[2] * a[15] - a[4] * a[3] * a[14] - a[12] * a[2] * a[7] + a[12] * a[3] * a[6];
  b[10] = a[0] * a[5] * a[15] - a[0] * a[7] * a[13] - a[4] * a[1] * a[15] + a[4] * a[3] * a[13] + a[12] * a[1] * a[7] - a[12] * a[3] * a[5];
  b[14] = -a[0] * a[5] * a[14] + a[0] * a[6] * a[13] + a[4] * a[1] * a[14] - a[4] * a[2] * a[13] - a[12] * a[1] * a[6] + a[12] * a[2] * a[5];
  b[3] = -a[1] * a[6] * a[11] + a[1] * a[7] * a[10] + a[5] * a[2] * a[11] - a[5] * a[3] * a[10] - a[9] * a[2] * a[7] + a[9] * a[3] * a[6];
  b[7] = a[0] * a[6] * a[11] - a[0] * a[7] * a[10] - a[4] * a[2] * a[11] + a[4] * a[3] * a[10] + a[8] * a[2] * a[7] - a[8] * a[3] * a[6];
  b[11] = -a[0] * a[5] * a[11] + a[0] * a[7] * a[9] + a[4] * a[1] * a[11] - a[4] * a[3] * a[9] - a[8] * a[1] * a[7] + a[8] * a[3] * a[5];
  b[15] = a[0] * a[5] * a[10] - a[0] * a[6] * a[9] - a[4] * a[1] * a[10] + a[4] * a[2] * a[9] + a[8] * a[1] * a[6] - a[8] * a[2] * a[5];
  let det = a[0] * b[0] + a[1] * b[4] + a[2] * b[8] + a[3] * b[12];
  if (Math.abs(det) < 1e-12) return identityMat4();
  det = 1 / det;
  for (let i = 0; i < 16; i++) b[i] *= det;
  return b;
}

function parseProductName(entities, productDefId) {
  const pd = entities.get(productDefId);
  if (!pd || pd.type !== 'PRODUCT_DEFINITION') return null;
  const refs = refIdsInArgs(pd.raw);
  const formationRef = refs[0];
  const formation = entities.get(formationRef);
  if (!formation) return null;
  const productRef = refIdsInArgs(formation.raw)[0];
  const product = entities.get(productRef);
  if (!product || product.type !== 'PRODUCT') return null;
  const strs = quotedStrings(product.raw);
  return strs[0] || strs[1] || null;
}

function parseRepresentationRelationMatrix(entities, id) {
  const e = entities.get(id);
  if (!e) return identityMat4();
  const m = e.raw.match(/REPRESENTATION_RELATIONSHIP_WITH_TRANSFORMATION\s*\(\s*#(\d+)\s*\)/);
  if (!m) return identityMat4();
  return parseItemDefinedMatrix(entities, Number(m[1]));
}

function buildAssemblyGraph(entities) {
  const pdsToNauo = new Map();
  const nauoInfo = new Map();
  const localMatrixByNauo = new Map();
  const hasCdsrPlacement = new Set();
  const childrenByParentPd = new Map();

  for (const e of entities.values()) {
    if (e.type === 'PRODUCT_DEFINITION_SHAPE') {
      const strs = quotedStrings(e.raw);
      if (!strs.some((s) => /NAUO/i.test(s))) continue;
      const refs = refIdsInArgs(e.raw);
      const nauoId = refs[refs.length - 1];
      if (nauoId) pdsToNauo.set(e.id, nauoId);
    }
    if (e.type === 'NEXT_ASSEMBLY_USAGE_OCCURRENCE') {
      const strs = quotedStrings(e.raw);
      const occurrenceName = strs[0] || strs[1] || '';
      const refs = refIdsInArgs(e.raw);
      const relatedPd = refs[refs.length - 2];
      const relatingPd = refs[refs.length - 1];
      const partName = parseProductName(entities, relatedPd) || occurrenceName;
      nauoInfo.set(e.id, { occurrenceName, partName, relatedPd, relatingPd });
    }
  }

  for (const e of entities.values()) {
    if (e.type !== 'CONTEXT_DEPENDENT_SHAPE_REPRESENTATION') continue;
    const relRef = refId(e.raw, 0);
    const pdsRef = refId(e.raw, 1);
    const nauoId = pdsToNauo.get(pdsRef);
    if (!nauoId) continue;
    const mat = parseRepresentationRelationMatrix(entities, relRef);
    if (!localMatrixByNauo.has(nauoId)) localMatrixByNauo.set(nauoId, mat);
    hasCdsrPlacement.add(nauoId);
  }

  for (const [nauoId, info] of nauoInfo) {
    const local = localMatrixByNauo.get(nauoId) || identityMat4();
    localMatrixByNauo.set(nauoId, local);
    if (!info.relatingPd || !info.relatedPd) continue;
    if (!childrenByParentPd.has(info.relatingPd)) childrenByParentPd.set(info.relatingPd, []);
    childrenByParentPd.get(info.relatingPd).push(nauoId);
  }

  const relatedPdSet = new Set();
  for (const info of nauoInfo.values()) {
    if (info.relatedPd) relatedPdSet.add(info.relatedPd);
  }
  const rootPds = [];
  for (const info of nauoInfo.values()) {
    if (info.relatingPd && !relatedPdSet.has(info.relatingPd) && !rootPds.includes(info.relatingPd)) {
      rootPds.push(info.relatingPd);
    }
  }
  if (!rootPds.length) {
    for (const info of nauoInfo.values()) {
      if (info.relatingPd && !rootPds.includes(info.relatingPd)) rootPds.push(info.relatingPd);
    }
  }

  return { nauoInfo, localMatrixByNauo, hasCdsrPlacement, childrenByParentPd, rootPds };
}

function walkAssemblyOccurrences(graph) {
  const { nauoInfo, localMatrixByNauo, hasCdsrPlacement, childrenByParentPd, rootPds } = graph;
  const out = [];
  const visited = new Set();

  function walk(parentPd, parentWorld) {
    const children = childrenByParentPd.get(parentPd) || [];
    for (const nauoId of children) {
      if (visited.has(nauoId)) continue;
      visited.add(nauoId);
      const info = nauoInfo.get(nauoId);
      if (!info) continue;
      const local = localMatrixByNauo.get(nauoId) || identityMat4();
      const world = multiplyMat4(parentWorld, local);
      if (hasCdsrPlacement.has(nauoId)) {
        const origin = transformPoint(world, 0, 0, 0);
        out.push({
          nauoId,
          occurrenceName: info.occurrenceName,
          partName: info.partName,
          matchName: info.occurrenceName,
          relatedPd: info.relatedPd,
          relatingPd: info.relatingPd,
          x: origin.x,
          y: origin.y,
          z: origin.z,
          matrix: world,
        });
      }
      walk(info.relatedPd, world);
    }
  }

  for (const rootPd of rootPds) {
    walk(rootPd, identityMat4());
  }

  return out;
}

/**
 * @returns {{ occurrenceName, partName, x, y, z, nauoId, matchName, matrix? }[]}
 */
export function parseStpOccurrences(stpText) {
  const entities = parseEntities(stpText);
  const graph = buildAssemblyGraph(entities);
  const walked = walkAssemblyOccurrences(graph);
  if (walked.length) return walked;

  // fallback — flat placement (구형 STP)
  const pdsToNauo = new Map();
  const nauoInfo = new Map();
  for (const e of entities.values()) {
    if (e.type === 'PRODUCT_DEFINITION_SHAPE') {
      const strs = quotedStrings(e.raw);
      if (!strs.some((s) => /NAUO/i.test(s))) continue;
      const refs = refIdsInArgs(e.raw);
      const nauoId = refs[refs.length - 1];
      if (nauoId) pdsToNauo.set(e.id, nauoId);
    }
    if (e.type === 'NEXT_ASSEMBLY_USAGE_OCCURRENCE') {
      const strs = quotedStrings(e.raw);
      const occurrenceName = strs[0] || strs[1] || '';
      const partDefRef = refId(e.raw, 1);
      const partName = parseProductName(entities, partDefRef) || occurrenceName;
      nauoInfo.set(e.id, { occurrenceName, partName });
    }
  }
  const placementByNauo = new Map();
  for (const e of entities.values()) {
    if (e.type !== 'CONTEXT_DEPENDENT_SHAPE_REPRESENTATION') continue;
    const relRef = refId(e.raw, 0);
    const pdsRef = refId(e.raw, 1);
    const nauoId = pdsToNauo.get(pdsRef);
    if (!nauoId) continue;
    const mat = parseRepresentationRelationMatrix(entities, relRef);
    if (!placementByNauo.has(nauoId)) placementByNauo.set(nauoId, mat);
  }
  const out = [];
  for (const [nauoId, info] of nauoInfo) {
    const mat = placementByNauo.get(nauoId);
    if (!mat) continue;
    const pt = transformPoint(mat, 0, 0, 0);
    out.push({
      nauoId,
      occurrenceName: info.occurrenceName,
      partName: info.partName,
      matchName: info.occurrenceName,
      x: pt.x,
      y: pt.y,
      z: pt.z,
      matrix: mat,
    });
  }
  return out;
}

export function occurrenceToMeshCenters(occurrences) {
  return occurrences.map((o) => ({
    name: o.matchName,
    occurrenceName: o.occurrenceName,
    partName: o.partName,
    cx: o.x,
    cy: o.y,
    cz: o.z,
    vol: 1,
  }));
}

/** mesh 정점에 4×4(column-major) 적용 */
export function applyMat4ToPosition(position, matrix) {
  if (!matrix) return position;
  const out = new Float32Array(position.length);
  const m = matrix;
  for (let i = 0; i < position.length; i += 3) {
    const x = position[i];
    const y = position[i + 1];
    const z = position[i + 2];
    out[i] = m[0] * x + m[4] * y + m[8] * z + m[12];
    out[i + 1] = m[1] * x + m[5] * y + m[9] * z + m[13];
    out[i + 2] = m[2] * x + m[6] * y + m[10] * z + m[14];
  }
  return out;
}
