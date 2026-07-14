/**
 * STP → io_manifest.json (센서 part 이름 + bbox 중심 좌표만, 형체 GLB와 분리)
 *
 * node tools/stp-io-manifest.mjs --assy Carriage_Assy
 * node tools/stp-io-manifest.mjs <input.stp> [output.json]
 * node tools/stp-io-manifest.mjs --all
 *
 * 옵션: --deflection N  (기본 0.2 — 매핑용이라 거칠게, 빠름)
 */
import fs from 'fs';
import path from 'path';
import { createRequire } from 'module';
import { fileURLToPath } from 'url';
import {
  DEFAULT_STEP_PARAMS,
  extractMesh,
  meshBounds,
} from './lib/step-mesh-utils.mjs';
import { parseStpOccurrences, occurrenceToMeshCenters } from './lib/stp-occurrence-parser.mjs';
import { matchIoKeysToMeshes, matchSensorPartsSequential, filterSensorOccurrences, buildStpIndexSensorMeshes } from './lib/io-name-match.mjs';
import { loadAssyIoProfileFromDir, filterReliableManifestPoints } from './lib/assy-io-profile.mjs';
import { readSolidEdgeIoMap, readStpSensorIndexMap } from './lib/read-io-define.mjs';
import { findHighestRevGlb, findHighestRevStp, modelRootFromDetailRel, resolveAssyStpPath } from './lib/rev-model-resolve.mjs';
import {
  buildStpSourceField,
  countReliableManifestPoints,
  diffManifestPoints,
  formatManifestDiffSummary,
  isManifestStale,
} from './lib/manifest-stp-sync.mjs';
import { syncDesktopOcctModels } from './lib/sync-desktop-occt-models.mjs';
import { getDefaultRuntimeDrawingRoot } from './lib/desktop-kit-paths.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '..');
const LIB_DIR = path.join(ROOT, 'Viewer3D/lib');
const RUNTIME_DRAWING = path.join(ROOT, 'bin/Debug/net6.0-windows/SRM0/3D_Drawing');

/** OCCT mesh 없을 때 part명 기준 기본 반경(mm) — Lower_Frame 실측 */
const DEFAULT_SENSOR_RADIUS_MM = {
  GDFL_SENSOR: 20,
  GDFL_SENSOR_mir: 20,
  '8BIT_SENSOR': 40,
  'SENSOR BOX': 30,
};

function computeReferenceFromManifestPoints(points) {
  const vals = Object.values(points || {}).filter(
    (p) => p && Number.isFinite(p.x) && Number.isFinite(p.y) && Number.isFinite(p.z)
  );
  if (!vals.length) return null;
  let sx = 0;
  let sy = 0;
  let sz = 0;
  for (const p of vals) {
    sx += p.x;
    sy += p.y;
    sz += p.z;
  }
  const n = vals.length;
  return { center: [sx / n, sy / n, sz / n], zUp: true, unit: 'mm' };
}

function computeBBoxFromManifestPoints(points) {
  const vals = Object.values(points || {}).filter(
    (p) => p && Number.isFinite(p.x) && Number.isFinite(p.y) && Number.isFinite(p.z)
  );
  if (!vals.length) return null;
  let ax0 = Infinity;
  let ay0 = Infinity;
  let az0 = Infinity;
  let ax1 = -Infinity;
  let ay1 = -Infinity;
  let az1 = -Infinity;
  for (const p of vals) {
    ax0 = Math.min(ax0, p.x);
    ay0 = Math.min(ay0, p.y);
    az0 = Math.min(az0, p.z);
    ax1 = Math.max(ax1, p.x);
    ay1 = Math.max(ay1, p.y);
    az1 = Math.max(az1, p.z);
  }
  return {
    center: [(ax0 + ax1) / 2, (ay0 + ay1) / 2, (az0 + az1) / 2],
    min: [ax0, ay0, az0],
    max: [ax1, ay1, az1],
    zUp: true,
    unit: 'mm',
  };
}

function enrichManifestPointRadius(points, meshCenters) {
  for (const pt of Object.values(points)) {
    if (pt.radiusMm > 0) continue;
    let best = null;
    let bestD = 80;
    for (const mc of meshCenters) {
      const d = Math.hypot(mc.cx - pt.x, mc.cy - pt.y, mc.cz - pt.z);
      if (d < bestD) {
        bestD = d;
        best = mc;
      }
    }
    if (best?.radiusMm > 0) {
      pt.radiusMm = best.radiusMm;
      pt.bboxSizeMm = [best.sx, best.sy, best.sz];
      continue;
    }
    const def = DEFAULT_SENSOR_RADIUS_MM[pt.sourceMesh];
    if (def > 0) pt.radiusMm = def;
  }
}

const require = createRequire(import.meta.url);
const occtimportjs = require(path.join(LIB_DIR, 'occt-import-js.js'));

function parseArgs(argv) {
  const opts = { deflection: 0.2, assy: null, all: false, inputs: [], coordsOnly: true, syncStale: true, force: false, drawing: null, noDesktopSync: true };
  for (let i = 2; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--all') opts.all = true;
    else if (a === '--no-desktop-sync') opts.noDesktopSync = true;
    else if (a === '--desktop-sync') opts.noDesktopSync = false;
    else if (a === '--coords-only') opts.coordsOnly = true;
    else if (a === '--with-occt') opts.coordsOnly = false;
    else if (a === '--sync-stale') opts.syncStale = true;
    else if (a === '--no-sync-stale') opts.syncStale = false;
    else if (a === '--force') opts.force = true;
    else if (a === '--drawing') opts.drawing = path.resolve(argv[++i]);
    else if (a === '--assy') opts.assy = argv[++i];
    else if (a === '--deflection') opts.deflection = Number(argv[++i]);
    else if (!a.startsWith('-')) opts.inputs.push(path.resolve(a));
  }
  if (opts.force) opts.syncStale = false;
  return opts;
}

async function loadOcct() {
  return occtimportjs({ locateFile: (p) => path.join(LIB_DIR, p) });
}

function readSignalKeys(drawingRoot, assyId) {
  const catalog = JSON.parse(fs.readFileSync(path.join(drawingRoot, 'assemblies.json'), 'utf8'));
  const assy = catalog.assemblies?.find((a) => a.id === assyId);
  return assy?.signalKeys || [];
}

async function buildManifest(stpPath, outPath, signalKeys, opts, assyId) {
  if (!fs.existsSync(stpPath)) throw new Error('STP 없음: ' + stpPath);
  if (!signalKeys.length) throw new Error('signalKeys 없음');

  const stpBuf = fs.readFileSync(stpPath);
  const mb = (stpBuf.length / (1024 * 1024)).toFixed(1);
  console.log(`\n[STP→manifest] ${path.basename(stpPath)} (${mb} MB), I/O ${signalKeys.length}개`);

  const assyDir = path.dirname(stpPath);
  const profile = loadAssyIoProfileFromDir(assyDir, assyId);
  const stpText = fs.readFileSync(stpPath, 'latin1');
  const allOccurrences = parseStpOccurrences(stpText);
  const seMap = readSolidEdgeIoMap(assyDir);
  const occCenters = occurrenceToMeshCenters(allOccurrences);
  const plcHits = allOccurrences.filter((o) =>
    signalKeys.some(
      (k) =>
        String(o.occurrenceName || '').toLowerCase() === k.toLowerCase() ||
        String(o.partName || '').toLowerCase() === k.toLowerCase()
    )
  );
  console.log(
    `  STP 배치: ${allOccurrences.length}개 · PLC이름 일치 ${plcHits.length}/${signalKeys.length} [${profile.assyId}]`
  );

  let meshCenters = [];
  if (!opts.coordsOnly) {
    const occt = await loadOcct();
    const params = {
      ...DEFAULT_STEP_PARAMS,
      linearDeflection: opts.deflection,
    };

    const t0 = Date.now();
    process.stdout.write('  occt 파싱...');
    const result = occt.ReadStepFile(stpBuf, params);
    console.log(` ${((Date.now() - t0) / 1000).toFixed(1)}s`);

    if (!result?.meshes?.length) throw new Error('STEP mesh 없음');

    for (const m of result.meshes) {
      const mesh = extractMesh(m);
      if (!mesh) continue;
      const b = meshBounds(mesh.position);
      meshCenters.push({
        name: mesh.name,
        cx: b.cx,
        cy: b.cy,
        cz: b.cz,
        sx: b.sx,
        sy: b.sy,
        sz: b.sz,
        vol: b.vol,
        radiusMm: b.radiusMm,
      });
    }
    if (!meshCenters.length) {
      console.warn(
        '  ⚠ 좌표 추출 0 — occt가 이 STP에서 메쉬 정점을 생성하지 못함 (이름만 있고 position 없음). export 설정 변경 또는 배치행렬 파서 필요'
      );
    }
  } else {
    const t0 = Date.now();
    console.log(`  좌표 전용 — OCCT 생략 (STP occurrence 텍스트만, ${((Date.now() - t0) / 1000).toFixed(2)}s)`);
  }

  let { points, matches } = matchIoKeysToMeshes(signalKeys, occCenters, seMap);
  if (matches.length) {
    console.log(`  배치이름 매칭: ${matches.length}/${signalKeys.length}`);
  }

  /** 배치 원점(occurrence) ≠ 형상 중심 — OCCT mesh bbox 중심으로 보정 (선택, --with-occt) */
  if (meshCenters.length) {
    const meshByName = new Map(meshCenters.map((m) => [m.name, m]));
    let meshCenterFix = 0;
    for (const [key, pt] of Object.entries(points)) {
      const occName = pt.occurrenceName || matches.find((m) => m.key === key)?.mesh || key;
      const mc =
        meshByName.get(occName) ||
        meshByName.get(key) ||
        (pt.sourceMesh ? meshByName.get(pt.sourceMesh) : null);
      if (!mc) continue;
      const dx = mc.cx - pt.x;
      const dy = mc.cy - pt.y;
      const dz = mc.cz - pt.z;
      const dist = Math.hypot(dx, dy, dz);
      if (dist < 0.05) continue;
      pt.occurrenceX = pt.x;
      pt.occurrenceY = pt.y;
      pt.occurrenceZ = pt.z;
      pt.x = mc.cx;
      pt.y = mc.cy;
      pt.z = mc.cz;
      pt.matchMode = 'stp_occurrence_mesh_center';
      meshCenterFix++;
    }
    if (meshCenterFix) {
      console.log(`  mesh 중심 보정: ${meshCenterFix}개 (배치원점↔형상)`);
    }
  }

  const stpIndexMap = profile.manifest?.useIoStpSensorMap ? readStpSensorIndexMap(assyDir) : new Map();
  if (stpIndexMap.size) {
    const sensorMeshes = buildStpIndexSensorMeshes(profile, occCenters, meshCenters, seMap, signalKeys);
    let manual = 0;
    for (const key of signalKeys) {
      const n = stpIndexMap.get(key);
      if (!n) continue;
      if (points[key]?.matchMode === 'stp_occurrence') continue;
      const m = sensorMeshes[n - 1];
      if (!m) {
        console.warn(`  ⚠ ${key}: STP번호 ${n} 없음 (센서 ${sensorMeshes.length}개)`);
        continue;
      }
      points[key] = {
        x: m.cx,
        y: m.cy,
        z: m.cz,
        sourceMesh: m.name,
        stpListNo: n,
        matchMode: 'io_stp_sensor_map',
        radiusMm: m.radiusMm,
        bboxSizeMm: m.sx != null ? [m.sx, m.sy, m.sz] : undefined,
      };
      if (!matches.some((x) => x.key === key)) {
        matches.push({ key, mesh: m.name, meshIndex: m.idx, mode: 'io_stp_sensor_map', stpListNo: n });
      }
      manual++;
    }
    if (manual) console.log(`  IO_STP_SENSOR_MAP 수동: ${manual}개`);
  }

  if (matches.length < signalKeys.length && profile.manifest?.allowSensorPartSeq) {
    const coordMeshes = meshCenters.length ? meshCenters : occCenters;
    const seq = matchSensorPartsSequential(signalKeys, coordMeshes, points, profile);
    points = seq.points;
    matches = [
      ...matches,
      ...seq.matches.filter((m) => !matches.some((x) => x.key === m.key)),
    ];
    if (seq.matches.length) {
      console.log(`  센서 part 순서 매핑: +${seq.matches.length} (총 ${matches.length}/${signalKeys.length})`);
    }
  }

  const seqKeys = Object.entries(points)
    .filter(([, pt]) => pt.matchMode === 'sensor_part_seq')
    .map(([k]) => k);
  if (seqKeys.length) {
    for (const k of seqKeys) delete points[k];
    matches = matches.filter((m) => m.mode !== 'sensor_part_seq');
    console.warn(
      `  sensor_part_seq ${seqKeys.length}개 manifest 제외 (STP occurrence 없음 — CAD 배치명=PLC키 필요)`,
      seqKeys.slice(0, 6).join(', ')
    );
  }

  /** 전 ASSY 동일 — fallback(io_stp_sensor_map·GDFL/8BIT) manifest 기록 금지 */
  const beforeKeys = Object.keys(points).length;
  const { points: reliablePoints, dropped: fallbackDropped } = filterReliableManifestPoints(points);
  points = reliablePoints;
  matches = matches.filter((m) => points[m.key]);
  if (fallbackDropped) {
    console.warn(
      `  ⚠ fallback 좌표 ${fallbackDropped}개 제외 (stp_occurrence만 허용, 어거지 매칭 없음)`
    );
  }

  const reliableN = Object.keys(points).length;
  if (reliableN < signalKeys.length) {
    const missing = signalKeys.length - reliableN;
    console.warn(
      `  ⚠ STP 배치명 매칭 부족: ${reliableN}/${signalKeys.length} (미매칭 ${missing}개 — Solid Edge PathFinder 배치명=PLC signalKey)`
    );
  }
  if (!reliableN && beforeKeys > 0) {
    console.warn('  ⚠ manifest에 좌표 0개 — 이전 fallback 좌표는 폐기됨');
  }

  enrichManifestPointRadius(points, meshCenters);
  for (const pt of Object.values(points)) {
    if (pt.radiusMm > 0) continue;
    if (pt.matchMode === 'stp_occurrence' || pt.occurrenceName) {
      pt.radiusMm = profile.manifest?.defaultRadiusMm || 20;
    }
  }
  const radiusN = Object.values(points).filter((p) => p.radiusMm > 0).length;
  if (radiusN) console.log(`  센서 반경(radiusMm): ${radiusN}/${Object.keys(points).length}`);

  console.log(`  I/O 매칭: ${matches.length}/${signalKeys.length}`);

  const meshNameSample = [...new Set(meshCenters.map((m) => m.name))].slice(0, 30);

  let wSum = 0;
  let wx = 0;
  let wy = 0;
  let wz = 0;
  let ax0 = Infinity;
  let ay0 = Infinity;
  let az0 = Infinity;
  let ax1 = -Infinity;
  let ay1 = -Infinity;
  let az1 = -Infinity;
  for (const m of meshCenters) {
    const w = m.vol > 0 ? m.vol : 1;
    wSum += w;
    wx += m.cx * w;
    wy += m.cy * w;
    wz += m.cz * w;
    ax0 = Math.min(ax0, m.cx);
    ay0 = Math.min(ay0, m.cy);
    az0 = Math.min(az0, m.cz);
    ax1 = Math.max(ax1, m.cx);
    ay1 = Math.max(ay1, m.cy);
    az1 = Math.max(az1, m.cz);
  }
  const cadReference =
    meshCenters.length > 0
      ? { center: [wx / wSum, wy / wSum, wz / wSum], zUp: true, unit: 'mm' }
      : computeReferenceFromManifestPoints(points);
  const assemblyBBox =
    meshCenters.length > 0
      ? {
          center: [(ax0 + ax1) / 2, (ay0 + ay1) / 2, (az0 + az1) / 2],
          min: [ax0, ay0, az0],
          max: [ax1, ay1, az1],
          zUp: true,
          unit: 'mm',
        }
      : computeBBoxFromManifestPoints(points);

  const stpRoot = modelRootFromDetailRel(path.basename(stpPath));
  const glbRoot = profile.stpCanonicalBase || stpRoot;
  const glbPath = findHighestRevGlb(assyDir, glbRoot);
  const sensorOccurrences = plcHits;

  const manifest = {
    assyId: assyId || null,
    assyIoProfile: profile.assyId,
    stpFile: path.basename(stpPath),
    glbFile: glbPath ? path.basename(glbPath) : null,
    stpSource: buildStpSourceField(stpPath),
    generatedAt: new Date().toISOString(),
    linearDeflection: opts.coordsOnly ? null : opts.deflection,
    coordsOnly: !!opts.coordsOnly,
    source: 'stp',
    coordinateSystem: 'solid_edge_z_up_mm',
    _note:
      '형체=GLB. 센서=STP 배치이름(occurrence)+배치좌표 우선, part파일명은 보조.',
    occurrenceCount: allOccurrences.length,
    sensorOccurrenceCount: sensorOccurrences.length,
    cadReference,
    assemblyBBox,
    assemblyOrigin: [0, 0, 0],
    points,
    matchCount: matches.length,
    signalCount: signalKeys.length,
    meshCount: meshCenters.length,
    _meshNameSample: meshNameSample,
  };

  fs.writeFileSync(outPath, JSON.stringify(manifest, null, 2) + '\n', 'utf8');
  console.log(`  저장: ${path.basename(outPath)} (STP ${manifest.stpFile}, GLB ${manifest.glbFile || '없음'})`);

  if (glbPath && fs.existsSync(glbPath)) {
    manifest.glbFile = path.basename(glbPath);
    console.log(`  GLB(형체표시용): ${manifest.glbFile} — 좌표는 STP만 사용`);
  }

  if (matches.length) {
    console.log('  샘플:', matches.slice(0, 5).map((m) => `${m.key}←${m.mesh}`).join(', '));
  }
  return { matched: Object.keys(points).length, total: signalKeys.length, outPath, reliableOnly: true };
}

function collectJobs(opts) {
  const drawingRoot = opts.drawing
    ? opts.drawing
    : fs.existsSync(RUNTIME_DRAWING)
      ? RUNTIME_DRAWING
      : path.join(ROOT, 'Viewer3D/3D_Drawing_Template');
  const catalog = JSON.parse(fs.readFileSync(path.join(drawingRoot, 'assemblies.json'), 'utf8'));
  const jobs = [];

  if (opts.inputs.length >= 1) {
    const stp = opts.inputs[0];
    const out =
      opts.inputs[1] ||
      path.join(path.dirname(stp), 'io_manifest.json');
    const assyId = path.basename(path.dirname(stp));
    jobs.push({ assyId, stp, out, keys: readSignalKeys(drawingRoot, assyId) });
    return jobs;
  }

  for (const assy of catalog.assemblies || []) {
    if (opts.assy && assy.id !== opts.assy) continue;
    const rel = assy.detailModel?.replace(/\\/g, '/');
    if (!rel) continue;
    const dir = path.join(drawingRoot, path.dirname(rel));
    const stp = resolveAssyStpPath(dir, assy.id, rel);
    if (!stp) {
      console.warn('STP 없음:', assy.id);
      continue;
    }
    jobs.push({
      assyId: assy.id,
      stp,
      out: path.join(dir, 'io_manifest.json'),
      keys: assy.signalKeys || [],
    });
  }
  return jobs;
}

async function main() {
  const opts = parseArgs(process.argv);

  if (!opts.noDesktopSync && !opts.inputs.length && !opts.drawing) {
    const drawingRoot = fs.existsSync(RUNTIME_DRAWING) ? RUNTIME_DRAWING : getDefaultRuntimeDrawingRoot(ROOT);
    console.log('바탕화면 OCCT STP 동기화...');
    await syncDesktopOcctModels({ drawingRoot, assyFilter: opts.assy || null });
  }

  const jobs = collectJobs(opts);
  if (!jobs.length) {
    console.log(`사용법:
  node tools/stp-io-manifest.mjs --assy Carriage_Assy
  node tools/stp-io-manifest.mjs --all
  node tools/stp-io-manifest.mjs <input.stp> [io_manifest.json]`);
    process.exit(1);
  }

  console.log('엔진: STP occurrence 텍스트 (기본 — OCCT 생략, 센서좌표만)');
  const results = [];
  for (const job of jobs) {
    try {
      if (opts.syncStale && fs.existsSync(job.out) && !isManifestStale(job.out, job.stp)) {
        const stMeta = buildStpSourceField(job.stp);
        console.log(
          `  = ${job.assyId}: STP 변경 없음 (${stMeta?.file} ${new Date(stMeta?.mtimeMs || 0).toISOString()}) — manifest 유지`
        );
        results.push({ ...job, skipped: true, matched: null, total: job.keys.length, outPath: job.out });
        continue;
      }

      let oldManifest = null;
      if (fs.existsSync(job.out)) {
        try {
          oldManifest = JSON.parse(fs.readFileSync(job.out, 'utf8'));
        } catch {
          oldManifest = null;
        }
      }

      const built = await buildManifest(job.stp, job.out, job.keys, opts, job.assyId);

      if (oldManifest?.points) {
        const fresh = JSON.parse(fs.readFileSync(job.out, 'utf8'));
        const oldReliable = countReliableManifestPoints(oldManifest);
        const newReliable = countReliableManifestPoints(fresh);
        if (newReliable === 0 && oldReliable > 0) {
          const blocked = {
            ...oldManifest,
            glbFile: fresh.glbFile ?? oldManifest.glbFile ?? null,
            _regenBlocked: {
              at: new Date().toISOString(),
              reason: 'new_stp_match_zero',
              attemptedStp: buildStpSourceField(job.stp),
              wouldMatchCount: fresh.matchCount ?? 0,
            },
          };
          fs.writeFileSync(job.out, JSON.stringify(blocked, null, 2) + '\n', 'utf8');
          console.warn(
            `  ⚠ ${job.assyId}: 새 STP PLC 매칭 0 — 기존 좌표 ${oldReliable}개 유지 (STP export·배치명 확인)`
          );
          results.push({
            ...job,
            ...built,
            matched: oldReliable,
            blockedWipe: true,
            dataChanged: false,
          });
          continue;
        }

        const diff = diffManifestPoints(oldManifest.points, fresh.points);
        if (diff.changed) {
          console.log(`  Δ ${job.assyId}: ${formatManifestDiffSummary(diff)}`);
          if (diff.moved.length) {
            console.log(
              '    이동 샘플:',
              diff.moved
                .slice(0, 4)
                .map((m) => `${m.key} ${m.distMm}mm`)
                .join(', ')
            );
          }
          if (diff.added.length) console.log('    추가:', diff.added.slice(0, 8).join(', '));
          if (diff.removed.length) console.log('    삭제:', diff.removed.slice(0, 8).join(', '));
        } else {
          console.log(`  = ${job.assyId}: STP 날짜 갱신됐으나 I/O 좌표 동일`);
        }
        built.dataChanged = diff.changed;
      }

      results.push({ ...job, ...built });
    } catch (e) {
      console.error('실패:', job.assyId, e.message);
      results.push({ ...job, error: e.message });
    }
  }

  console.log('\n=== 완료 ===');
  for (const r of results) {
    if (r.error) console.log(`  ✗ ${r.assyId}: ${r.error}`);
    else if (r.skipped) console.log(`  · ${r.assyId}: skip (STP 동일)`);
    else console.log(`  ✓ ${r.assyId}: ${r.matched}/${r.total} → ${path.basename(r.outPath)}${r.dataChanged ? ' [좌표변경]' : ''}`);
  }
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
