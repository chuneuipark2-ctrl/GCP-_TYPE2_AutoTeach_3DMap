/**
 * 단순 mesh 배열 → GLB 2.0 (이름·OCCT 색상 보존)
 */
import fs from 'fs';

function pad4(n) {
  return (n + 3) & ~3;
}

function normalizeColor(color) {
  if (!color || color.length < 3) return null;
  const scale = color.some((c) => c > 1.001) ? 1 / 255 : 1;
  return [
    Math.min(1, Math.max(0, color[0] * scale)),
    Math.min(1, Math.max(0, color[1] * scale)),
    Math.min(1, Math.max(0, color[2] * scale)),
  ];
}

function materialIndexForColor(color, materials, colorIndex) {
  const rgb = normalizeColor(color);
  if (!rgb) return 0;
  const key = rgb.map((c) => c.toFixed(4)).join(',');
  if (colorIndex.has(key)) return colorIndex.get(key);
  const idx = materials.length;
  materials.push({
    name: `mat_${idx}`,
    pbrMetallicRoughness: {
      baseColorFactor: [rgb[0], rgb[1], rgb[2], 1],
      metallicFactor: 0.12,
      roughnessFactor: 0.72,
    },
    doubleSided: true,
  });
  colorIndex.set(key, idx);
  return idx;
}

function writeGlbJson(meshes) {
  const bufferViews = [];
  const accessors = [];
  const materials = [
    {
      name: 'default',
      pbrMetallicRoughness: {
        baseColorFactor: [0.69, 0.69, 0.72, 1],
        metallicFactor: 0.1,
        roughnessFactor: 0.75,
      },
      doubleSided: true,
    },
  ];
  const colorIndex = new Map();
  const meshDefs = [];
  const nodes = [];
  const binChunks = [];
  let byteOffset = 0;

  for (const m of meshes) {
    const pos = m.position instanceof Float32Array ? m.position : new Float32Array(m.position);
    const idx = m.index instanceof Uint32Array ? m.index : new Uint32Array(m.index);
    if (!m.normal?.length && pos.length >= 9 && idx.length >= 3) {
      m.normal = computeNormals(pos, idx);
    }
    const posBytes = new Uint8Array(pos.buffer, pos.byteOffset, pos.byteLength);
    const idxBytes = new Uint8Array(idx.buffer, idx.byteOffset, idx.byteLength);

    const posView = bufferViews.length;
    bufferViews.push({ buffer: 0, byteOffset, byteLength: posBytes.byteLength, target: 34962 });
    binChunks.push(posBytes);
    byteOffset += pad4(posBytes.byteLength);

    const posAcc = accessors.length;
    accessors.push({
      bufferView: posView,
      componentType: 5126,
      count: pos.length / 3,
      type: 'VEC3',
      min: m.boundsMin || [0, 0, 0],
      max: m.boundsMax || [0, 0, 0],
    });

    let normalAcc = null;
    if (m.normal?.length === pos.length) {
      const norm = m.normal instanceof Float32Array ? m.normal : new Float32Array(m.normal);
      const normBytes = new Uint8Array(norm.buffer, norm.byteOffset, norm.byteLength);
      const normView = bufferViews.length;
      bufferViews.push({ buffer: 0, byteOffset, byteLength: normBytes.byteLength, target: 34962 });
      binChunks.push(normBytes);
      byteOffset += pad4(normBytes.byteLength);
      normalAcc = accessors.length;
      accessors.push({ bufferView: normView, componentType: 5126, count: norm.length / 3, type: 'VEC3' });
    }

    const idxView = bufferViews.length;
    bufferViews.push({ buffer: 0, byteOffset, byteLength: idxBytes.byteLength, target: 34963 });
    binChunks.push(idxBytes);
    byteOffset += pad4(idxBytes.byteLength);

    const idxAcc = accessors.length;
    accessors.push({ bufferView: idxView, componentType: 5125, count: idx.length, type: 'SCALAR' });

    const matIdx = materialIndexForColor(m.color, materials, colorIndex);
    const prim = { attributes: { POSITION: posAcc }, indices: idxAcc, material: matIdx };
    if (normalAcc !== null) prim.attributes.NORMAL = normalAcc;

    const meshIdx = meshDefs.length;
    meshDefs.push({ name: m.name, primitives: [prim] });
    nodes.push({ name: m.name, mesh: meshIdx });
  }

  const gltf = {
    asset: { version: '2.0', generator: 'gcp-stp-to-glb' },
    scene: 0,
    scenes: [{ name: 'Scene', nodes: nodes.map((_, i) => i) }],
    nodes,
    meshes: meshDefs,
    accessors,
    bufferViews,
    buffers: [{ byteLength: byteOffset }],
    materials,
  };

  const json = Buffer.from(JSON.stringify(gltf));
  const jsonPad = pad4(json.length);
  const jsonChunk = Buffer.alloc(jsonPad, 0x20);
  json.copy(jsonChunk);

  const binLen = byteOffset;
  const binBuf = Buffer.alloc(binLen);
  let off = 0;
  for (const chunk of binChunks) {
    binBuf.set(chunk, off);
    off += pad4(chunk.byteLength);
  }

  const total = 12 + 8 + jsonPad + 8 + binLen;
  const out = Buffer.alloc(total);
  let o = 0;
  out.writeUInt32LE(0x46546c67, o);
  o += 4;
  out.writeUInt32LE(2, o);
  o += 4;
  out.writeUInt32LE(total, o);
  o += 4;
  out.writeUInt32LE(jsonPad, o);
  o += 4;
  out.writeUInt32LE(0x4e4f534a, o);
  o += 4;
  jsonChunk.copy(out, o);
  o += jsonPad;
  out.writeUInt32LE(binLen, o);
  o += 4;
  out.writeUInt32LE(0x004e4942, o);
  o += 4;
  binBuf.copy(out, o);

  return out;
}

function computeBounds(position) {
  let min = [Infinity, Infinity, Infinity];
  let max = [-Infinity, -Infinity, -Infinity];
  for (let i = 0; i < position.length; i += 3) {
    for (let j = 0; j < 3; j++) {
      const v = position[i + j];
      if (v < min[j]) min[j] = v;
      if (v > max[j]) max[j] = v;
    }
  }
  return { min, max };
}

/** STP+JT 병합 GLB — 법선 없으면 WebView에서 형체가 안 보임 */
function computeNormals(position, index) {
  const vertCount = position.length / 3;
  const accum = new Float32Array(position.length);
  const triCount = index ? index.length / 3 : vertCount / 3;

  for (let t = 0; t < triCount; t++) {
    const i0 = index ? index[t * 3] : t * 3;
    const i1 = index ? index[t * 3 + 1] : t * 3 + 1;
    const i2 = index ? index[t * 3 + 2] : t * 3 + 2;
    const ax = position[i0 * 3];
    const ay = position[i0 * 3 + 1];
    const az = position[i0 * 3 + 2];
    const bx = position[i1 * 3];
    const by = position[i1 * 3 + 1];
    const bz = position[i1 * 3 + 2];
    const cx = position[i2 * 3];
    const cy = position[i2 * 3 + 1];
    const cz = position[i2 * 3 + 2];
    const abx = bx - ax;
    const aby = by - ay;
    const abz = bz - az;
    const acx = cx - ax;
    const acy = cy - ay;
    const acz = cz - az;
    const nx = aby * acz - abz * acy;
    const ny = abz * acx - abx * acz;
    const nz = abx * acy - aby * acx;
    for (const idx of [i0, i1, i2]) {
      accum[idx * 3] += nx;
      accum[idx * 3 + 1] += ny;
      accum[idx * 3 + 2] += nz;
    }
  }

  const normals = new Float32Array(position.length);
  for (let i = 0; i < vertCount; i++) {
    const nx = accum[i * 3];
    const ny = accum[i * 3 + 1];
    const nz = accum[i * 3 + 2];
    const len = Math.hypot(nx, ny, nz) || 1;
    normals[i * 3] = nx / len;
    normals[i * 3 + 1] = ny / len;
    normals[i * 3 + 2] = nz / len;
  }
  return normals;
}

export function writeMeshesToGlb(meshes, outPath) {
  const prepared = meshes.map((m) => {
    const bounds = computeBounds(m.position);
    return { ...m, boundsMin: bounds.min, boundsMax: bounds.max };
  });
  const buf = writeGlbJson(prepared);
  fs.writeFileSync(outPath, buf);
  return buf.length;
}
