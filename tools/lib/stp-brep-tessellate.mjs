/**
 * STEP BREP → triangle mesh (STP only, OCCT 실패 ASSY)
 */
import { parseEntities } from './stp-extract-solid.mjs';
import { applyMat4ToPosition } from './stp-occurrence-parser.mjs';
import { buildItemColourMap } from './stp-colour-map.mjs';

const DEFAULT_COLOR = [0.69, 0.69, 0.72];
const ARC_SEG = 16;

function refIds(raw) {
  const m = raw.match(/\([\s\S]*\)/);
  if (!m) return [];
  return [...m[0].matchAll(/#(\d+)/g)].map((x) => Number(x[1]));
}

function refId(raw, i = 0) {
  return refIds(raw)[i] ?? null;
}

function strs(raw) {
  return [...raw.matchAll(/'([^']*)'/g)].map((m) => m[1]);
}

function pt(entities, id) {
  const e = entities.get(id);
  if (!e) return null;
  const m = e.raw.match(/CARTESIAN_POINT\s*\([^,]*,\s*\(([^)]+)\)/);
  if (!m) return null;
  const n = m[1].split(',').map((s) => Number(s.trim()));
  if (n.length < 3 || n.some((v) => !Number.isFinite(v))) return null;
  return { x: n[0], y: n[1], z: n[2] };
}

function dir(entities, id) {
  const e = entities.get(id);
  if (!e) return null;
  const m = e.raw.match(/DIRECTION\s*\([^,]*,\s*\(([^)]+)\)/);
  if (!m) return null;
  const n = m[1].split(',').map((s) => Number(s.trim()));
  const l = Math.hypot(n[0], n[1], n[2]) || 1;
  return { x: n[0] / l, y: n[1] / l, z: n[2] / l };
}

function axis2(entities, id) {
  const e = entities.get(id);
  if (!e || e.type !== 'AXIS2_PLACEMENT_3D') return { o: { x: 0, y: 0, z: 0 }, z: { x: 0, y: 0, z: 1 }, x: { x: 1, y: 0, z: 0 }, y: { x: 0, y: 1, z: 0 } };
  const r = refIds(e.raw);
  const o = pt(entities, r[0]) || { x: 0, y: 0, z: 0 };
  const z = dir(entities, r[1]) || { x: 0, y: 0, z: 1 };
  let xh = dir(entities, r[2]) || { x: 1, y: 0, z: 0 };
  const dot = xh.x * z.x + xh.y * z.y + xh.z * z.z;
  xh = { x: xh.x - dot * z.x, y: xh.y - dot * z.y, z: xh.z - dot * z.z };
  const xl = Math.hypot(xh.x, xh.y, xh.z) || 1;
  xh = { x: xh.x / xl, y: xh.y / xl, z: xh.z / xl };
  const y = { x: z.y * xh.z - z.z * xh.y, y: z.z * xh.x - z.x * xh.z, z: z.x * xh.y - z.y * xh.x };
  return { o, z, x: xh, y };
}

function add3(a, b) {
  return { x: a.x + b.x, y: a.y + b.y, z: a.z + b.z };
}
function sub3(a, b) {
  return { x: a.x - b.x, y: a.y - b.y, z: a.z - b.z };
}
function mul3(a, s) {
  return { x: a.x * s, y: a.y * s, z: a.z * s };
}
function dot3(a, b) {
  return a.x * b.x + a.y * b.y + a.z * b.z;
}
function cross3(a, b) {
  return { x: a.y * b.z - a.z * b.y, y: a.z * b.x - a.x * b.z, z: a.x * b.y - a.y * b.x };
}
function len3(a) {
  return Math.hypot(a.x, a.y, a.z);
}

function discretizeCircle(entities, curveId, v1, v2) {
  const e = entities.get(curveId);
  if (!e || e.type !== 'CIRCLE') return [v1, v2];
  const r = refIds(e.raw);
  const ax = axis2(entities, r[0]);
  const radius = Number(e.raw.match(/CIRCLE\s*\([^,]*,[^,]*,\s*([^)]+)\)/)?.[1] || 0);
  if (!radius) return [v1, v2];
  const out = [];
  const a1 = Math.atan2(dot3(sub3(v1, ax.o), ax.y), dot3(sub3(v1, ax.o), ax.x));
  let a2 = Math.atan2(dot3(sub3(v2, ax.o), ax.y), dot3(sub3(v2, ax.o), ax.x));
  if (a2 < a1) a2 += Math.PI * 2;
  if (a2 - a1 < 1e-6) a2 += Math.PI * 2;
  const n = Math.max(4, Math.ceil(((a2 - a1) / (Math.PI * 2)) * ARC_SEG));
  for (let i = 0; i <= n; i++) {
    const t = a1 + ((a2 - a1) * i) / n;
    out.push(add3(add3(ax.o, mul3(ax.x, radius * Math.cos(t))), mul3(ax.y, radius * Math.sin(t))));
  }
  return out;
}

function vertexPoint(entities, vertexId) {
  const v = entities.get(vertexId);
  if (!v || v.type !== 'VERTEX_POINT') return null;
  return pt(entities, refId(v.raw, 0));
}

function edgePoints(entities, edgeId) {
  const e = entities.get(edgeId);
  if (!e || e.type !== 'EDGE_CURVE') return [];
  const r = refIds(e.raw);
  const v1 = vertexPoint(entities, r[0]);
  const v2 = vertexPoint(entities, r[1]);
  const curve = entities.get(r[2]);
  if (!v1 || !v2) return [];
  if (!curve) return [v1, v2];
  if (curve.type === 'LINE') return [v1, v2];
  if (curve.type === 'CIRCLE') return discretizeCircle(entities, r[2], v1, v2);
  return [v1, v2];
}

function loopPoints(entities, loopId) {
  const e = entities.get(loopId);
  if (!e || e.type !== 'EDGE_LOOP') return [];
  const pts = [];
  for (const oeId of refIds(e.raw)) {
    const oe = entities.get(oeId);
    if (!oe || oe.type !== 'ORIENTED_EDGE') continue;
    const ep = edgePoints(entities, refId(oe.raw, 0));
    const rev = /\.F\.\s*\)/.test(oe.raw);
    const seq = rev ? [...ep].reverse() : ep;
    for (const p of seq) {
      if (!pts.length) pts.push(p);
      else {
        const last = pts[pts.length - 1];
        if (Math.hypot(p.x - last.x, p.y - last.y, p.z - last.z) > 1e-6) pts.push(p);
      }
    }
  }
  return pts;
}

function pushTri(outPos, outIdx, a, b, c) {
  const base = outPos.length / 3;
  outPos.push(a.x, a.y, a.z, b.x, b.y, b.z, c.x, c.y, c.z);
  outIdx.push(base, base + 1, base + 2);
}

function triangulatePlane(pts, normal) {
  if (pts.length < 3) return { pos: [], idx: [] };
  const pos = [];
  const idx = [];
  const n = normal || { x: 0, y: 0, z: 1 };
  const ref = Math.abs(n.x) < 0.9 ? { x: 1, y: 0, z: 0 } : { x: 0, y: 1, z: 0 };
  const u = cross3(n, ref);
  const ul = len3(u) || 1;
  u.x /= ul;
  u.y /= ul;
  u.z /= ul;
  const v = cross3(n, u);
  const poly2 = pts.map((p) => ({ x: dot3(p, u), y: dot3(p, v), p }));
  const cx = poly2.reduce((s, q) => s + q.x, 0) / poly2.length;
  const cy = poly2.reduce((s, q) => s + q.y, 0) / poly2.length;
  poly2.sort((a, b) => Math.atan2(a.y - cy, a.x - cx) - Math.atan2(b.y - cy, b.x - cx));
  for (let i = 1; i < poly2.length - 1; i++) pushTri(pos, idx, poly2[0].p, poly2[i].p, poly2[i + 1].p);
  return { pos, idx };
}

function cylUV(ax, p) {
  const rel = sub3(p, ax.o);
  const v = dot3(rel, ax.z);
  const rx = dot3(rel, ax.x);
  const ry = dot3(rel, ax.y);
  return { u: Math.atan2(ry, rx), v };
}

function cylPoint(ax, radius, u, v) {
  return add3(ax.o, add3(mul3(ax.z, v), add3(mul3(ax.x, radius * Math.cos(u)), mul3(ax.y, radius * Math.sin(u)))));
}

function unwrapTheta(uvs) {
  if (uvs.length < 2) return uvs;
  const out = [{ u: uvs[0].u, v: uvs[0].v }];
  for (let i = 1; i < uvs.length; i++) {
    let u = uvs[i].u;
    while (u - out[i - 1].u > Math.PI) u -= Math.PI * 2;
    while (u - out[i - 1].u < -Math.PI) u += Math.PI * 2;
    out.push({ u, v: uvs[i].v });
  }
  return out;
}

function meshSurfaceFan(outer, rev, uvAt, pointAt) {
  if (outer.length < 3) return { pos: [], idx: [] };
  const uvs = unwrapTheta(outer.map(uvAt));
  let su = 0;
  let sv = 0;
  for (const q of uvs) {
    su += q.u;
    sv += q.v;
  }
  su /= uvs.length;
  sv /= uvs.length;
  const center = pointAt(su, sv);
  const pos = [];
  const idx = [];
  for (let i = 0; i < outer.length; i++) {
    const b = outer[i];
    const c = outer[(i + 1) % outer.length];
    if (rev) pushTri(pos, idx, center, c, b);
    else pushTri(pos, idx, center, b, c);
  }
  return { pos, idx };
}

function parseSurface(entities, surfId) {
  const e = entities.get(surfId);
  if (!e) return { type: 'PLANE', axis: axis2(entities, null), radius: 1, semi: 1 };
  const axis = axis2(entities, refId(e.raw, 0));
  if (e.type === 'CYLINDRICAL_SURFACE') {
    const radius = Number(e.raw.match(/CYLINDRICAL_SURFACE\s*\([^,]*,[^,]*,\s*([^)]+)\)/)?.[1] || 1);
    return { type: 'CYLINDRICAL', axis, radius };
  }
  if (e.type === 'CONICAL_SURFACE') {
    const m = e.raw.match(/CONICAL_SURFACE\s*\([^,]*,[^,]*,\s*([^,]+),\s*([^)]+)\)/);
    return { type: 'CONICAL', axis, radius: Number(m?.[1] || 1), semi: Number(m?.[2] || 0.5) };
  }
  if (e.type === 'SPHERICAL_SURFACE') {
    const radius = Number(e.raw.match(/SPHERICAL_SURFACE\s*\([^,]*,[^,]*,\s*([^)]+)\)/)?.[1] || 1);
    return { type: 'SPHERICAL', axis, radius };
  }
  if (e.type === 'TOROIDAL_SURFACE') {
    const m = e.raw.match(/TOROIDAL_SURFACE\s*\([^,]*,[^,]*,\s*([^,]+),\s*([^)]+)\)/);
    return { type: 'TOROIDAL', axis, radius: Number(m?.[1] || 1), minor: Number(m?.[2] || 0.2) };
  }
  return { type: 'PLANE', axis };
}

function meshFace(entities, faceId) {
  const e = entities.get(faceId);
  if (!e || e.type !== 'ADVANCED_FACE') return { pos: [], idx: [] };
  const r = refIds(e.raw);
  const surf = parseSurface(entities, r[2]);
  const loops = [];
  for (const fbId of r.slice(0, 2)) {
    const fb = entities.get(fbId);
    if (!fb || fb.type !== 'FACE_BOUND') continue;
    loops.push(loopPoints(entities, refId(fb.raw, 0)));
  }
  const outer = loops[0] || [];
  if (outer.length < 3) return { pos: [], idx: [] };
  const rev = /\.F\.\s*\)/.test(e.raw);

  if (surf.type === 'CYLINDRICAL') {
    return meshSurfaceFan(
      outer,
      rev,
      (p) => cylUV(surf.axis, p),
      (u, v) => cylPoint(surf.axis, surf.radius, u, v)
    );
  }
  if (surf.type === 'CONICAL') {
    return meshSurfaceFan(
      outer,
      rev,
      (p) => cylUV(surf.axis, p),
      (u, v) => {
        const rAt = Math.max(0.001, surf.radius + v * surf.semi);
        return cylPoint(surf.axis, rAt, u, v);
      }
    );
  }
  if (surf.type === 'SPHERICAL') {
    const sphUV = (p) => {
      const rel = sub3(p, surf.axis.o);
      const r = len3(rel) || surf.radius;
      const zen = Math.acos(Math.min(1, Math.max(-1, dot3(rel, surf.axis.z) / r)));
      const azi = Math.atan2(dot3(rel, surf.axis.y), dot3(rel, surf.axis.x));
      return { u: azi, v: zen };
    };
    return meshSurfaceFan(outer, rev, sphUV, (u, v) => {
      const r = surf.radius;
      const sinZ = Math.sin(v);
      return add3(
        surf.axis.o,
        add3(
          add3(mul3(surf.axis.x, r * sinZ * Math.cos(u)), mul3(surf.axis.y, r * sinZ * Math.sin(u))),
          mul3(surf.axis.z, r * Math.cos(v))
        )
      );
    });
  }
  if (surf.type === 'TOROIDAL') {
    const R = surf.radius;
    const r = surf.minor;
    const torUV = (p) => {
      const rel = sub3(p, surf.axis.o);
      const z = dot3(rel, surf.axis.z);
      const rx = dot3(rel, surf.axis.x);
      const ry = dot3(rel, surf.axis.y);
      const rho = Math.hypot(rx, ry);
      return { u: Math.atan2(ry, rx), v: Math.atan2(z, rho - R) };
    };
    return meshSurfaceFan(outer, rev, torUV, (u, v) => {
      const cu = Math.cos(u);
      const su = Math.sin(u);
      const cv = Math.cos(v);
      const sv = Math.sin(v);
      const rho = R + r * cv;
      return add3(
        surf.axis.o,
        add3(
          add3(mul3(surf.axis.x, rho * cu), mul3(surf.axis.y, rho * su)),
          mul3(surf.axis.z, r * sv)
        )
      );
    });
  }
  const n = surf.axis.z;
  const nn = rev ? mul3(n, -1) : n;
  return triangulatePlane(outer, nn);
}

function meshSolid(entities, solidId, color) {
  const e = entities.get(solidId);
  if (!e || e.type !== 'MANIFOLD_SOLID_BREP') return null;
  const shell = entities.get(refId(e.raw, 0));
  if (!shell || shell.type !== 'CLOSED_SHELL') return null;
  const pos = [];
  const idx = [];
  for (const faceId of refIds(shell.raw)) {
    const f = meshFace(entities, faceId);
    const base = pos.length / 3;
    pos.push(...f.pos);
    for (const i of f.idx) idx.push(i + base);
  }
  if (pos.length < 9) return null;
  return { position: new Float32Array(pos), index: new Uint32Array(idx), color: color || DEFAULT_COLOR };
}

function contextNameMap(entities) {
  const map = new Map();
  for (const e of entities.values()) {
    if (!e.raw.includes('REPRESENTATION_CONTEXT')) continue;
    const names = strs(e.raw);
    if (names.length) map.set(e.id, names[0]);
  }
  return map;
}

/** @returns {Map<string, { position, index, color }[]>} */
export function buildPartMeshMap(stpText) {
  const entities = parseEntities(stpText);
  const colours = buildSolidColourMap(stpText);
  const ctxNames = contextNameMap(entities);
  const partMeshes = new Map();

  for (const e of entities.values()) {
    if (e.type !== 'ADVANCED_BREP_SHAPE_REPRESENTATION') continue;
    const refs = refIds(e.raw);
    if (refs.length < 2) continue;
    const solidId = refs[0];
    const ctxId = refs[refs.length - 1];
    const partName = ctxNames.get(ctxId) || strs(e.raw)[0] || '';
    if (!partName) continue;
    const sm = meshSolid(entities, solidId, colours.get(solidId));
    if (!sm) continue;
    if (!partMeshes.has(partName)) partMeshes.set(partName, []);
    partMeshes.get(partName).push(sm);
  }
  return partMeshes;
}

function mergeSubMeshes(subMeshes) {
  let posLen = 0;
  let idxLen = 0;
  for (const m of subMeshes) {
    posLen += m.position.length;
    idxLen += m.index.length;
  }
  const position = new Float32Array(posLen);
  const index = new Uint32Array(idxLen);
  let vOff = 0;
  let iOff = 0;
  let vBase = 0;
  for (const m of subMeshes) {
    position.set(m.position, vOff);
    for (let i = 0; i < m.index.length; i++) index[iOff + i] = m.index[i] + vBase;
    vOff += m.position.length;
    iOff += m.index.length;
    vBase += m.position.length / 3;
  }
  return { position, index };
}

export function tessellateStpAssemblyToMeshes(stpText, occurrences) {
  const partMeshes = buildPartMeshMap(stpText);
  const raw = [];
  for (const o of occurrences) {
    const keys = [o.partName, o.occurrenceName, o.matchName].filter(Boolean);
    const key = keys.find((k) => partMeshes.has(k));
    if (!key || !o.matrix) continue;
    for (const sub of partMeshes.get(key)) {
      const position = applyMat4ToPosition(sub.position, o.matrix);
      raw.push({
        name: o.occurrenceName || key,
        position,
        index: sub.index,
        color: sub.color,
      });
    }
  }
  return mergeMeshesByColor(raw);
}

function mergeMeshesByColor(meshes) {
  const groups = new Map();
  for (const m of meshes) {
    const ck = (m.color || DEFAULT_COLOR).map((c) => c.toFixed(4)).join(',');
    if (!groups.has(ck)) groups.set(ck, []);
    groups.get(ck).push(m);
  }
  const out = [];
  let i = 0;
  for (const [ck, list] of groups) {
    let posLen = 0;
    let idxLen = 0;
    for (const m of list) {
      posLen += m.position.length;
      idxLen += m.index.length;
    }
    const position = new Float32Array(posLen);
    const index = new Uint32Array(idxLen);
    let vOff = 0;
    let iOff = 0;
    let vBase = 0;
    for (const m of list) {
      position.set(m.position, vOff);
      for (let j = 0; j < m.index.length; j++) index[iOff + j] = m.index[j] + vBase;
      vOff += m.position.length;
      iOff += m.index.length;
      vBase += m.position.length / 3;
    }
    const color = ck.split(',').map(Number);
    out.push({ name: `body_${i++}`, position, index, color });
  }
  return out;
}

export { meshSolid };
