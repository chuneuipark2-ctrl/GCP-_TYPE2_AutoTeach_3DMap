/**
 * 전 ASSY I/O ↔ GLB 정합 디버그 (viewer.js prepare + snap 파이프라인)
 * node tools/debug-assy-io-align.mjs
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import * as THREE from '../Viewer3D/lib/three.module.min.js';
import { GLTFLoader } from '../Viewer3D/lib/GLTFLoader.js';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const DRAWING = path.join(__dirname, '../bin/Debug/net6.0-windows/SRM0/3D_Drawing');
const IO_STP_SURFACE_SNAP_MAX_MM = 900;
const HEAVY_GLB_MESH_FAST = 300;

function cadZUpMmToYUp(x, y, z) {
  return new THREE.Vector3(x, z, -y);
}

function getMeshesBox(object) {
  const box = new THREE.Box3();
  let has = false;
  object.traverse((node) => {
    if (!node.isMesh || !node.geometry) return;
    node.geometry.computeBoundingBox();
    if (node.geometry.boundingBox) {
      box.union(node.geometry.boundingBox);
      has = true;
    }
  });
  return has ? box : null;
}

function countMeshes(object) {
  let n = 0;
  object.traverse((node) => {
    if (node.isMesh) n++;
  });
  return n;
}

function isImageToStlZUpGlb(object, manifest, gltfAsset) {
  const gen = String(gltfAsset?.generator || '').toLowerCase();
  if (gen.includes('imagetostl')) return true;
  if (manifest?.glbExportHint === 'image_to_stl_z_up') return true;
  let mesh0 = 0;
  let total = 0;
  object.traverse((node) => {
    if (!node.isMesh) return;
    total++;
    if (/^Mesh\d+$/i.test(node.name || '')) mesh0++;
  });
  if (total > 0 && mesh0 === total && total <= 24) {
    const box = getMeshesBox(object);
    if (box) {
      const s = box.getSize(new THREE.Vector3());
      if (s.z > s.y * 1.1 && s.z > s.x * 0.25) return true;
    }
  }
  return false;
}

function isLikelyExternalYUpGlb(object, manifest) {
  if (manifest?.glbExportHint === 'external_y_up') return true;
  let nodeMeshes = 0;
  let namedMeshes = 0;
  object.traverse((node) => {
    if (!node.isMesh) return;
    if (/^Node\d+$/i.test(node.name || '')) nodeMeshes++;
    else if (node.name) namedMeshes++;
  });
  return nodeMeshes >= 3 && nodeMeshes > namedMeshes;
}

function manifestCoordsAreZUpMm(manifest) {
  return manifest.coordinateSystem === 'solid_edge_z_up_mm' || manifest.cadReference?.zUp === true;
}

function needsSolidEdgeZUpFix(object, manifest, gltfAsset) {
  if (isImageToStlZUpGlb(object, manifest, gltfAsset)) return false;
  if (isLikelyExternalYUpGlb(object, manifest)) return false;
  if (manifestCoordsAreZUpMm(manifest)) return true;
  const box = getMeshesBox(object);
  if (!box) return false;
  const s = box.getSize(new THREE.Vector3());
  return s.z > s.y * 1.1 && s.z > s.x * 0.25;
}

function bakeMeshWorldTransforms(object) {
  object.updateMatrixWorld(true);
  object.traverse((node) => {
    if (!node.isMesh || !node.geometry) return;
    node.geometry = node.geometry.clone();
    node.geometry.applyMatrix4(node.matrixWorld);
    node.position.set(0, 0, 0);
    node.rotation.set(0, 0, 0);
    node.scale.set(1, 1, 1);
  });
}

function applyGlbOriginOffset(object, manifest, zUpFix) {
  const o = manifest.glbOriginOffsetMm;
  if (!o || o.length !== 3) return;
  const d = manifestCoordsAreZUpMm(manifest) || zUpFix ? cadZUpMmToYUp(o[0], o[1], o[2]) : new THREE.Vector3(o[0], o[1], o[2]);
  if (Math.abs(d.x) + Math.abs(d.y) + Math.abs(d.z) < 1e-3) return;
  object.traverse((node) => {
    if (!node.isMesh || !node.geometry) return;
    node.geometry = node.geometry.clone();
    node.geometry.translate(d.x, d.y, d.z);
  });
}

function getStpAssemblyOriginZUpMm(manifest) {
  if (manifest.assemblyOrigin?.length === 3) return manifest.assemblyOrigin;
  if (manifest.coordinateSystem === 'solid_edge_z_up_mm') return [0, 0, 0];
  if (manifest.assemblyBBox?.center?.length === 3) return manifest.assemblyBBox.center;
  if (manifest.cadReference?.center?.length === 3) return manifest.cadReference.center;
  return null;
}

function zUpMmToDisplay(x, y, z, zUpFix, manifest) {
  if (manifestCoordsAreZUpMm(manifest) || zUpFix) return cadZUpMmToYUp(x, y, z);
  return new THREE.Vector3(x, y, z);
}

function resolveLayoutCenter(object, manifest, zUpFix) {
  const stpOrigin = getStpAssemblyOriginZUpMm(manifest);
  if (stpOrigin) {
    const useZUp = manifestCoordsAreZUpMm(manifest) ? true : zUpFix;
    return zUpMmToDisplay(stpOrigin[0], stpOrigin[1], stpOrigin[2], useZUp, manifest);
  }
  const box = getMeshesBox(object);
  return box ? box.getCenter(new THREE.Vector3()) : new THREE.Vector3();
}

function centerGeometryAtPoint(object, center) {
  const c = center.clone();
  const used = new Set();
  object.traverse((node) => {
    if (!node.isMesh || !node.geometry?.attributes?.position) return;
    let geo = node.geometry;
    if (used.has(geo)) {
      geo = geo.clone();
      node.geometry = geo;
    } else {
      used.add(geo);
    }
    geo.translate(-c.x, -c.y, -c.z);
  });
}

function stpCadPointToModelLocal(x, y, z, manifest, zUpFix, layoutCenter) {
  const p = zUpMmToDisplay(x, y, z, zUpFix, manifest);
  p.sub(layoutCenter);
  return p;
}

function closestPointOnForm(localPoint, formRoot, pivot) {
  pivot.updateMatrixWorld(true);
  formRoot.updateMatrixWorld(true);
  const worldTarget = pivot.localToWorld(localPoint.clone());
  let bestDist = IO_STP_SURFACE_SNAP_MAX_MM;
  let bestLocal = null;
  const probe = new THREE.Vector3();
  const bestWorld = new THREE.Vector3();
  formRoot.traverse((node) => {
    if (!node.isMesh) return;
    const pos = node.geometry?.attributes?.position;
    if (!pos) return;
    const stride = Math.max(1, Math.floor(pos.count / 5000));
    for (let i = 0; i < pos.count; i += stride) {
      probe.fromBufferAttribute(pos, i);
      node.localToWorld(probe);
      const d = worldTarget.distanceTo(probe);
      if (d < bestDist) {
        bestDist = d;
        bestWorld.copy(probe);
        bestLocal = pivot.worldToLocal(bestWorld.clone());
      }
    }
  });
  return bestLocal ? { point: bestLocal, dist: bestDist } : null;
}

function skipIoSurfaceSnap(key) {
  const k = key || '';
  if (/^LiDAR[12]_/i.test(k)) return true;
  if (/^Tower_Lamp_/i.test(k)) return true;
  if (/^Station_(Ready|Stop)_\d+$/i.test(k)) return true;
  return false;
}

function surfaceDistAfterSnap(localPoint, formRoot, pivot, key) {
  if (/^LiDAR[12]_/i.test(key || '')) {
    return closestPointOnForm(localPoint, formRoot, pivot)?.dist ?? 0;
  }
  if (/^Tower_Lamp_/i.test(key || '')) {
    return closestPointOnForm(localPoint, formRoot, pivot)?.dist ?? 0;
  }
  if (/^Station_(Ready|Stop)_\d+$/i.test(key || '') || /^Travel_/i.test(key || '') || /^Modem_Fault$/i.test(key || '')) {
    return closestPointOnForm(localPoint, formRoot, pivot)?.dist ?? 0;
  }
  const hit = closestPointOnForm(localPoint, formRoot, pivot);
  if (hit?.point && hit.dist <= IO_STP_SURFACE_SNAP_MAX_MM) {
    return closestPointOnForm(hit.point, formRoot, pivot)?.dist ?? hit.dist;
  }
  return hit?.dist ?? IO_STP_SURFACE_SNAP_MAX_MM;
}

function median(arr) {
  const s = [...arr].sort((a, b) => a - b);
  const m = Math.floor(s.length / 2);
  return s.length % 2 ? s[m] : (s[m - 1] + s[m]) / 2;
}

async function loadGlb(file) {
  const buf = fs.readFileSync(file);
  const gltf = await new Promise((res, rej) =>
    GLTFLoader.prototype.parse.call(
      new GLTFLoader(),
      buf.buffer.slice(buf.byteOffset, buf.byteOffset + buf.byteLength),
      '',
      res,
      rej
    )
  );
  return { root: gltf.scene.clone(true), asset: gltf.asset };
}

function findGlb(assyDir, standardBase) {
  const canonical = path.join(assyDir, `${standardBase}.glb`);
  if (fs.existsSync(canonical)) return canonical;
  const glbs = fs.readdirSync(assyDir).filter((n) => n.toLowerCase().endsWith('.glb'));
  return glbs.length ? path.join(assyDir, glbs[0]) : null;
}

function isManifestPointReliable(pt) {
  if (!pt || pt.x == null || pt.y == null || pt.z == null) return false;
  const mode = String(pt.matchMode || '');
  if (mode !== 'stp_occurrence' && mode !== 'stp_occurrence_mesh_center') return false;
  if (/GDFL|8BIT/i.test(String(pt.sourceMesh || ''))) return false;
  return true;
}

async function testAssy({ id, folder, standardBase }) {
  const assyDir = path.join(DRAWING, 'assemblies', folder);
  const manifestPath = path.join(assyDir, 'io_manifest.json');
  const glbPath = findGlb(assyDir, standardBase);
  if (!fs.existsSync(manifestPath) || !glbPath) {
    console.log(`[${id}] SKIP — manifest 또는 GLB 없음`);
    return null;
  }

  const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
  let root;
  let gltfAsset;
  try {
    const loaded = await loadGlb(glbPath);
    root = loaded.root;
    gltfAsset = loaded.asset;
  } catch (e) {
    console.log(`[${id}] FAIL GLB parse — ${e.message}`);
    return null;
  }

  const external = isLikelyExternalYUpGlb(root, manifest);
  const imageToStl = isImageToStlZUpGlb(root, manifest, gltfAsset);
  const zUpFix = needsSolidEdgeZUpFix(root, manifest, gltfAsset);
  const heavy = countMeshes(root) >= HEAVY_GLB_MESH_FAST;

  if (zUpFix && !heavy) {
    root.rotation.set(-Math.PI / 2, 0, 0);
    root.updateMatrixWorld(true);
    bakeMeshWorldTransforms(root);
    root.rotation.set(0, 0, 0);
  } else if (zUpFix && heavy) {
    root.rotation.set(-Math.PI / 2, 0, 0);
    root.updateMatrixWorld(true);
  } else {
    bakeMeshWorldTransforms(root);
  }

  applyGlbOriginOffset(root, manifest, zUpFix);
  const layoutCenter = resolveLayoutCenter(root, manifest, zUpFix);
  if (!heavy) centerGeometryAtPoint(root, layoutCenter);

  const pivot = new THREE.Group();
  pivot.add(root);

  const pts = Object.entries(manifest.points || {}).filter(([, p]) => isManifestPointReliable(p));
  const dists = pts.map(([key, p]) => {
    const local = stpCadPointToModelLocal(p.x, p.y, p.z, manifest, zUpFix, layoutCenter);
    return surfaceDistAfterSnap(local, root, pivot, key);
  });

  return {
    id,
    glb: path.basename(glbPath),
    ioCount: pts.length,
    external,
    imageToStl,
    zUpFix,
    heavy,
    median: median(dists),
    p90: [...dists].sort((a, b) => a - b)[Math.floor(dists.length * 0.9)] ?? 0,
    ok: median(dists) < 150,
  };
}

function standardBases(catalog) {
  return (catalog.assemblies || []).map((a) => {
    const rel = a.detailModel || '';
    const standardBase = path.basename(rel, path.extname(rel)).replace(/_REV\d+$/i, '').replace(/_final$/i, '');
    const folder = path.basename(path.dirname(rel));
    return { id: a.id, folder, standardBase };
  });
}

console.log('=== 전 ASSY I/O 정합 디버그 ===\n');
const catalog = JSON.parse(fs.readFileSync(path.join(DRAWING, 'assemblies.json'), 'utf8'));
const results = [];
for (const assy of standardBases(catalog)) {
  results.push(await testAssy(assy));
}

for (const r of results.filter(Boolean)) {
  console.log(
    `[${r.id}] ${r.ok ? 'PASS' : 'WARN'}  median=${r.median.toFixed(0)}mm p90=${r.p90.toFixed(0)}mm  IO=${r.ioCount}  GLB=${r.glb}  imageToStl=${r.imageToStl} external=${r.external} zUpFix=${r.zUpFix} heavy=${r.heavy}`
  );
}

const fail = results.filter((r) => r && !r.ok);
console.log(fail.length ? `\n${fail.length} ASSY 주의 (>150mm)` : '\n전 ASSY PASS');
process.exit(fail.length ? 1 : 0);
