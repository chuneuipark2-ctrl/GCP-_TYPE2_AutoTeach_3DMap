import * as THREE from './lib/three.module.min.js';
import { OrbitControls } from './lib/OrbitControls.js';
import { GLTFLoader } from './lib/GLTFLoader.js';
import { resolveModelPath, formatModelLabel, buildResolvedFromPath, modelKind, resolveAssyDisplayFromStp, deriveGlbPathFromStp } from './modelResolver.js';
import {
  assertGlbSizeAllowed,
  fetchModelByteLength,
  formatSizeMb,
  glbLoadTimeoutMs,
  MAX_GLB_BYTES,
  MAX_OVERVIEW_GLB_BYTES,
} from './modelSizeLimits.js';
import {
  formatGlbHeavyReason,
  isGlbTooHeavyForHmi,
  probeGlbFromUrl,
} from './glbProbe.js';
import {
  mergeAssyIoProfile,
  shouldSkipIoSurfaceSnap,
  getDeoverlapGapMm,
  isReliableManifestPoint,
  filterReliableManifestPoints,
  isExcludedStpFileName,
} from './assy-io-profile.js';
const host = document.getElementById('canvasHost');
if (!host) {
  throw new Error('canvasHost 요소 없음');
}
const hud = document.getElementById('hud');
const hint = document.getElementById('hint');
const perfEl = document.getElementById('perf');
const loadOverlay = document.getElementById('loadOverlay');
const loadText = document.getElementById('loadText');
const loadPct = document.getElementById('loadPct');
const loadBar = document.getElementById('loadBar');
const diagEl = document.getElementById('diag');

const VIEWER_BUILD = '20250709touch-hold-rot';
/** STP manifest 좌표 → 형체 표면 스냅 최대 거리 (mm). 0=비활성(기본) — 원점/표면 자동 끌어당김 금지 */
const IO_STP_SURFACE_SNAP_MAX_MM_DEFAULT = 0;
/** snap/겹침 분리 후 인접 I/O 구체 최소 간격 (mm) — ASSY 프로필로 덮어씀 */
let ioMarkerDeoverlapGapMm = 6;
/** ★ false — PLC 신호(updateIo) 기준 ON/OFF. true 는 좌표 확인용 테스트 점멸만 */
const IO_TEST_BLINK_ENABLED = false;
/** 테스트 점멸 대상 ASSY (PLC 없이 마커·좌표 확인) */
const IO_TEST_BLINK_ASSY_IDS = new Set(['Lower_Frame_assy', 'SCP', 'Carriage_Assy']);
const IO_TEST_BLINK_MS = 400;
/** STP manifest 없을 때만 io_layout 수동 좌표 허용 */
const IO_LAYOUT_FALLBACK = false;
const DEFAULT_DETAIL_VIEW = 'fit';
/** 모델 최대변을 scene에서 이 길이로 맞춤 */
const MODEL_SCENE_SIZE = 60;
/** 화면에 모델이 차지하는 비율 목표 (1.0=가장자리, 0.85=여유 있게 전체) */
const FIT_SCREEN_TARGET = 0.85;
/** 이 값 넘으면 "잘림" — 카메라를 더 뒤로 */
const FIT_SCREEN_MAX = 0.94;

/**
 * ★ 맞춤(F) 직후 UI 여백 pan — fitCameraSimple에서 refRadius 비율로 스케일 적용
 * X+ → 모델이 화면에서 왼쪽으로 (오른쪽 잘림 줄일 때 X 키움)
 */
const VIEW_FOCUS_OFFSET = { x: 40, y: 0, z: 0 };
/** I/O 램프 색 — 어두운 녹과 연두 중간 */
const IO_LAMP_COLOR_ON = 0x44d86a;
const IO_LAMP_COLOR_OFF = 0x2a7a42;
const IO_LAMP_COLOR_TEST_BRIGHT = 0x52e878;
const IO_LAMP_COLOR_TEST_DIM = 0x1e6b38;
/** 타워램프 — signalKey 색상명 기준 점멸/ON 색 (그 외 센서는 녹색 유지) */
const TOWER_LAMP_PALETTE = {
  red: { bright: 0xff4040, dim: 0x701818, on: 0xff5252, off: 0x4a1515 },
  yellow: { bright: 0xffe040, dim: 0x806000, on: 0xffeb3b, off: 0x5c4a00 },
  green: { bright: 0x40ff60, dim: 0x186028, on: 0x44d86a, off: 0x2a7a42 },
  white: { bright: 0xffffff, dim: 0x909090, on: 0xf5f5f5, off: 0x606060 },
  blue: { bright: 0x4090ff, dim: 0x183060, on: 0x5599ff, off: 0x1a3050 },
  buzzer: { bright: 0xd8d8d8, dim: 0x505050, on: 0xe8e8e8, off: 0x404040 },
};
/** 개별 I/O — 타워램프 팔레트 재사용 */
const IO_SIGNAL_COLOR_PALETTE = {
  Panel_EMO_SW: 'red',
  Panel_Reset_SW: 'blue',
};
/** io_layout / STP 오버레이 마커 — manifest·mesh 없을 때 bbox 비율 fallback */
const IO_LAMP_MARKER_RADIUS_RATIO = 0.065;
/** 줌 기준 거리 — 커스텀 회전 민감도 */
const ROTATE_REF_DIST = 50;
const ROTATE_PIXEL_BASE = 0.014;
const ROTATE_PIXEL_MIN = 0.006;
const ROTATE_PIXEL_MAX = 0.055;
const drawingBaseUrl = 'https://drawing.local/';
const scene = new THREE.Scene();
scene.background = new THREE.Color(0x1a1a1a);

const camera = new THREE.PerspectiveCamera(50, 1, 0.01, 100000);
camera.position.set(4, 3, 6);

const renderer = new THREE.WebGLRenderer({ antialias: true });
renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
host.appendChild(renderer.domElement);

const controls = new OrbitControls(camera, renderer.domElement);
setupCadNavigation(controls, renderer.domElement);

scene.add(new THREE.AmbientLight(0xffffff, 0.65));
const dir = new THREE.DirectionalLight(0xffffff, 0.9);
dir.position.set(5, 8, 4);
scene.add(dir);
// 격자(모눈) 비표시 — CAD 뷰 가독성
const sceneGrid = new THREE.GridHelper(20, 20, 0x444444, 0x222222);
sceneGrid.visible = false;
scene.add(sceneGrid);

const loader = new GLTFLoader();

function createTextureStrippedGltfLoader() {
  const stripped = new GLTFLoader();
  stripped.register((parser) => ({
    loadTexture: () => {
      const tex = new THREE.Texture();
      tex.name = 'hmi_skipped';
      return Promise.resolve(tex);
    },
  }));
  return stripped;
}
const raycaster = new THREE.Raycaster();
const pointer = new THREE.Vector2();
const _orbitDelta = new THREE.Vector3();
const _ndcPt = new THREE.Vector3();
const _camRight = new THREE.Vector3();
const _camUp = new THREE.Vector3();
const _camFwd = new THREE.Vector3();
const _screenCenterNdc = new THREE.Vector2(0, 0);
const _pivot = new THREE.Vector3();

let catalog = null;
/** WPF에서 디스크 기준으로 확인한 모델 경로 (fetch HEAD/Range 실패 대비) */
const resolvedModels = { assemblies: {}, overview: null };
let mode = 'overview';
let overviewRoot = null;
let detailRoot = null;
/** Detail — 모델 루트 */
let modelPivot = null;
/** GLB/Solid Edge에 박아 둔 I/O 램프 mesh — signalKey → Mesh */
const ioLampMeshes = new Map();

/** I/O당 램프 1개 — STP manifest 오버레이만 */
const IO_LAMP_PRIO = { stpOverlay: 3, layout: 2 };

function ioLampMeshPriority(mesh) {
  if (!mesh) return 0;
  if (mesh.userData?.isStpOverlay) return IO_LAMP_PRIO.stpOverlay;
  if (mesh.userData?.ioLampFromLayout) return IO_LAMP_PRIO.layout;
  return mesh.userData?.isIoLamp ? 1 : 0;
}

/** signalKey 당 단일 점멸 대상 등록 — 낮은 우선순위는 무시 */
function registerIoLamp(key, mesh) {
  if (!key || !mesh) return false;
  const existing = ioLampMeshes.get(key);
  if (existing === mesh) return true;
  if (existing && ioLampMeshPriority(existing) > ioLampMeshPriority(mesh)) return false;
  mesh.userData.isIoLamp = true;
  mesh.userData.signalKey = key;
  ioLampMeshes.set(key, mesh);
  return true;
}

let assyMeshes = [];
let layoutPoints = {};
/** STP에서 추출한 센서 좌표 (io_manifest.json) — 형체 GLB와 분리 */
let ioManifestPoints = {};
/** STP io_manifest 메타 (cadReference 등) */
let ioManifestMeta = null;
/** Solid Edge CAD 기준점 — STP manifest 우선 */
let ioCadReference = null;
/** plc signalKey → 화면 표시명 (IO_SIGNAL_LABELS.txt) */
let ioSignalLabels = {};
let hoveredIoKey = null;
let hoverPickRaf = 0;
let hoverPickX = 0;
let hoverPickY = 0;
const _hoverWorld = new THREE.Vector3();
/** 형체=GLB, 좌표=STP manifest만 */
const IO_COORDS_STP_ONLY = true;
let currentAssyId3d = null;
/** ASSY_IO_PROFILE.json — SCP / Lower_Frame / Carriage 각각 전용 */
let currentAssyIoProfile = null;
const ioStates = {};
let highlightedMarker = null;
/** 중클릭 커스텀 드래그 — rotate | pan */
let customDragMode = null;
let lastPointerX = 0;
let lastPointerY = 0;
/** 터치 PC — 한 손가락 2초 홀드 후 드래그 회전 */
const TOUCH_HOLD_ROTATE_MS = 2000;
const TOUCH_HOLD_PREARM_MOVE_PX = 14;
const NAV_HINT_DEFAULT =
  '중클릭:회전 · 우클릭:이동 · 휠:줌 · 터치 2초길게누른뒤 드래그:회전 · 2손가락:이동';
let touchHoldRotate = null;
let activeTouchPointerIds = new Set();
let suppressPointerClick = false;
let frameCount = 0;
let lastFpsTime = performance.now();
let animStarted = false;

const ASSY_COLORS = {
  SCP: 0x9b59b6,
  LOWER_FRAME_ASSY: 0x5cb85c,
  Carriage_Assy: 0xd9944a,
  CARRIAGE_ASSY: 0xd9944a,
};

const PROC_LAYOUT = {
  SCP: { size: [0.5, 1.2, 0.3], pos: [-1.8, 1, 0.5] },
  Lower_Frame_assy: { size: [3.5, 0.35, 0.5], pos: [0, 0.2, 0] },
  Carriage_Assy: { size: [1.6, 0.4, 0.6], pos: [0, 2.5, 0.2] },
};

function postToHost(payload) {
  if (window.chrome?.webview?.postMessage) {
    window.chrome.webview.postMessage(payload);
  }
}

function formatVec3(v) {
  return `${v.x.toFixed(1)},${v.y.toFixed(1)},${v.z.toFixed(1)}`;
}

/** 디버그용 — postToHost만 (화면 diag는 센서 호버 설명 전용) */
function reportViewDiag(stage, object, extra = {}) {
  if (!object) return;

  let meshCount = 0;
  let visibleCount = 0;
  object.traverse((n) => {
    if (!n.isMesh || !n.geometry?.attributes?.position) return;
    meshCount++;
    if (n.visible && !n.userData.outlierSuppressed) visibleCount++;
  });

  const box = getMeshesBox(object);
  const lines = [`[${stage}] ${VIEWER_BUILD}`];

  lines.push(`mesh ${meshCount}개 | 표시 ${visibleCount}`);
  if (extra.modelPath) lines.push(`파일: ${extra.modelPath}`);
  if (extra.modelKind) lines.push(`형식: ${extra.modelKind}`);

  if (box) {
    const c = box.getCenter(new THREE.Vector3());
    const s = box.getSize(new THREE.Vector3());
    lines.push(`bbox min ${formatVec3(box.min)} max ${formatVec3(box.max)}`);
    lines.push(`bbox ctr ${formatVec3(c)} size ${formatVec3(s)}`);
  } else {
    lines.push('bbox: 없음 (fit 불가)');
  }

  if (extra.dropped != null) lines.push(`outlier 숨김: ${extra.dropped}개`);
  if (object.userData.cadHeavyFastPath) {
    lines.push('heavy fast-path (bake 생략)');
  }
  if (extra.display?.meshCount) {
    lines.push(`형체 재질: GLB PBR 유지 (${extra.display.meshCount} mesh)`);
  }
  if (object.userData.cadZUpFix) lines.push('Z-up→Y-up: 적용');
  if (object.userData.stpOriginAligned) {
    const o = getStpAssemblyOriginZUpMm();
    lines.push(`STP 원점 정렬: ${o ? o.map((v) => v.toFixed(1)).join(',') : 'manifest'} mm`);
  }
  if (extra.prepareOk === false) lines.push('prepare: 실패');
  if (extra.prepareOk === true) lines.push('prepare: 성공');

  lines.push(`scale ${object.scale.x.toFixed(4)} pivot ${formatVec3(object.position)}`);
  lines.push(`맞춤 후 pan ${VIEW_FOCUS_OFFSET.x},${VIEW_FOCUS_OFFSET.y},${VIEW_FOCUS_OFFSET.z} × radius/${MODEL_SCENE_SIZE}`);
  lines.push(`cam tgt ${formatVec3(controls.target)} pos ${formatVec3(camera.position)}`);
  _ndcPt.copy(controls.target).project(camera);
  lines.push(`tgt 화면 ndc ${_ndcPt.x.toFixed(2)},${_ndcPt.y.toFixed(2)} (0,0=정중앙)`);
  if (extra.ndcExt != null) lines.push(`ndc ext ${extra.ndcExt.toFixed(3)}${extra.ndcExt > 1 ? ' 잘림' : ''}`);
  if (extra.ndcCx != null) lines.push(`ndc ctr ${extra.ndcCx.toFixed(3)},${extra.ndcCy.toFixed(3)}`);
  if (extra.allBoxSize) lines.push(`전체 mesh size ${extra.allBoxSize}`);

  if (extra.dist != null) lines.push(`cam dist ${extra.dist.toFixed(2)} aspect ${camera.aspect.toFixed(2)}`);

  if (extra.screenMsg) lines.push(extra.screenMsg);
  if (extra.screenExt != null) {
    const pct = Math.round(extra.screenExt * 100);
    lines.push(`화면 채움 ${pct}%${extra.screenExt > FIT_SCREEN_MAX ? ' → 잘림' : ''}`);
  }
  if (extra.screenCx != null) {
    const off = Math.max(Math.abs(extra.screenCx), Math.abs(extra.screenCy));
    if (off > 0.08) lines.push('화면 위치: 한쪽으로 치우침');
    else lines.push('화면 위치: 가운데');
  }

  postToHost({ type: 'viewDiag', stage, lines: lines.join(' | ') });
}

/** 왼쪽 하단 — 센서 호버 설명 (디버그 diag 대체) */
function setSensorInfoText(text) {
  if (!diagEl) return;
  diagEl.textContent = text || '';
}

function formatSignalKeyLabel(key) {
  return String(key || '')
    .replace(/_/g, ' ')
    .replace(/\bSW\b/g, 'S/W')
    .replace(/\s+/g, ' ')
    .trim();
}

function getIoSensorDescription(key) {
  if (!key) return '';
  const label = ioSignalLabels[key] || formatSignalKeyLabel(key);
  let stateLine = '';
  if (isIoTestBlinkAssy(currentAssyId3d)) {
    stateLine = '테스트 점멸 중';
  } else if (ioStates[key]) {
    stateLine = '신호 ON';
  } else {
    stateLine = '신호 OFF';
  }
  return `${label}\n${key}  ·  ${stateLine}`;
}

function getIoLabelsRelPath(assy) {
  if (assy?.ioLabels) return assy.ioLabels;
  if (assy?.detailModel) return assy.detailModel.replace(/[^/\\]+$/, 'IO_SIGNAL_LABELS.txt');
  return null;
}

async function loadAssyIoLabels(assy) {
  ioSignalLabels = {};
  const rel = getIoLabelsRelPath(assy);
  if (!rel) return ioSignalLabels;
  try {
    const res = await fetch(drawingBaseUrl + rel, { cache: 'no-store' });
    if (!res.ok) return ioSignalLabels;
    const text = await res.text();
    for (const line of text.split(/\r?\n/)) {
      const t = line.trim();
      if (!t || t.startsWith('#')) continue;
      const parts = t.split('\t');
      const key = parts[0]?.trim();
      const label = (parts[1] ?? '').trim().replace(/\s+/g, ' ');
      if (key && label) ioSignalLabels[key] = label;
    }
    console.info('[io_labels]', rel, Object.keys(ioSignalLabels).length, '개');
  } catch (e) {
    console.warn('IO_SIGNAL_LABELS load skip', rel, e?.message || e);
  }
  return ioSignalLabels;
}

function pickIoLampSignalKey(clientX, clientY) {
  if (mode !== 'detail' || ioLampMeshes.size === 0) return null;
  const rect = renderer.domElement.getBoundingClientRect();
  pointer.x = ((clientX - rect.left) / rect.width) * 2 - 1;
  pointer.y = -((clientY - rect.top) / rect.height) * 2 + 1;
  raycaster.setFromCamera(pointer, camera);
  const hits = raycaster.intersectObjects([...ioLampMeshes.values()], false);
  if (hits.length && hits[0].object?.userData?.signalKey) {
    return hits[0].object.userData.signalKey;
  }
  const maxPx = 28;
  let bestKey = null;
  let bestD = maxPx;
  for (const [key, mesh] of ioLampMeshes) {
    mesh.getWorldPosition(_hoverWorld);
    _hoverWorld.project(camera);
    const sx = (_hoverWorld.x * 0.5 + 0.5) * rect.width + rect.left;
    const sy = (-_hoverWorld.y * 0.5 + 0.5) * rect.height + rect.top;
    const d = Math.hypot(clientX - sx, clientY - sy);
    if (d < bestD) {
      bestD = d;
      bestKey = key;
    }
  }
  return bestKey;
}

function updateIoSensorHover(clientX, clientY) {
  if (mode !== 'detail' || ioLampMeshes.size === 0) {
    if (hoveredIoKey) {
      hoveredIoKey = null;
      setSensorInfoText('');
    }
    return;
  }
  const key = pickIoLampSignalKey(clientX, clientY);
  if (key === hoveredIoKey) return;
  hoveredIoKey = key;
  setSensorInfoText(key ? getIoSensorDescription(key) : '');
}

function scheduleIoSensorHoverPick(clientX, clientY) {
  hoverPickX = clientX;
  hoverPickY = clientY;
  if (hoverPickRaf) return;
  hoverPickRaf = requestAnimationFrame(() => {
    hoverPickRaf = 0;
    updateIoSensorHover(hoverPickX, hoverPickY);
  });
}

function setLoadStatus(message, percent) {
  loadText.textContent = message;
  const p = Math.max(0, Math.min(100, percent ?? 0));
  loadPct.textContent = Math.round(p) + '%';
  loadBar.style.width = p + '%';
  postToHost({ type: 'loadStatus', message, percent: p });
}

function resetLoadOverlayStyle() {
  loadText.style.color = '#B8D8FC';
  loadBar.style.background = '#B8D8FC';
  const banner = document.getElementById('errorBanner');
  if (banner) banner.style.display = 'none';
}

/** STP→GLB/로드 실패 — 회색 박스 대신 뷰어에 명확히 표시 */
function showAssyLoadError(assy, message) {
  const text = message || 'STP→GLB 변환 또는 3D 로드 실패';
  const banner = document.getElementById('errorBanner');
  if (banner) {
    banner.textContent = `⚠ ${text}`;
    banner.style.display = 'block';
  }
  loadText.textContent = text;
  loadText.style.color = '#ff8888';
  loadPct.textContent = '실패';
  loadBar.style.width = '100%';
  loadBar.style.background = '#ff5555';
  loadOverlay.style.display = 'flex';
  hud.textContent = `${assy.label || assy.id} — 실패`;
  if (diag) diag.textContent = text;
  postToHost({ type: 'loadStatus', message: text, error: true, assyId: assy.id });
}

function hideLoadOverlay() {
  loadOverlay.style.display = 'none';
  postToHost({ type: 'loadStatus', message: '표시 완료', percent: 100, done: true });
}

function resize() {
  const w = host.clientWidth || 1;
  const h = host.clientHeight || 1;
  camera.aspect = w / h;
  camera.updateProjectionMatrix();
  renderer.setSize(w, h, false);
}

window.addEventListener('resize', resize);
resize();
if (typeof ResizeObserver !== 'undefined') {
  new ResizeObserver(() => resize()).observe(host);
}

/** 카메라·target 같이 이동 (화면 안 튐 방지) */
function panCameraAndTarget(delta) {
  if (delta.lengthSq() < 1e-12) return;
  camera.position.add(delta);
  controls.target.add(delta);
  camera.lookAt(controls.target);
  controls.update();
}

function hasViewFocusOffset() {
  return VIEW_FOCUS_OFFSET.x !== 0 || VIEW_FOCUS_OFFSET.y !== 0 || VIEW_FOCUS_OFFSET.z !== 0;
}

/** 맞춤(F) 직후 — VIEW_FOCUS_OFFSET × (모델반경/MODEL_SCENE_SIZE). 좌표계·센서는 건드리지 않음 */
function applyViewFocusOffsetPan(refRadius = MODEL_SCENE_SIZE * 0.5) {
  if (!hasViewFocusOffset()) return;
  const k = Math.max(refRadius, 0.5) / MODEL_SCENE_SIZE;
  camera.updateMatrixWorld(true);
  _camRight.setFromMatrixColumn(camera.matrix, 0);
  _camUp.setFromMatrixColumn(camera.matrix, 1);
  camera.getWorldDirection(_camFwd);
  _orbitDelta.set(0, 0, 0);
  _orbitDelta.addScaledVector(_camRight, VIEW_FOCUS_OFFSET.x * k);
  _orbitDelta.addScaledVector(_camUp, VIEW_FOCUS_OFFSET.y * k);
  _orbitDelta.addScaledVector(_camFwd, VIEW_FOCUS_OFFSET.z * k);
  panCameraAndTarget(_orbitDelta);
}

/** 화면 정중앙 레이 → 모델 hit (없으면 null) */
function raycastModelAtScreenCenter(object) {
  if (!object) return null;
  raycaster.setFromCamera(_screenCenterNdc, camera);
  const hits = raycaster.intersectObject(object, true);
  for (const h of hits) {
    if (!h.object.isMesh || !h.object.visible || h.object.userData.outlierSuppressed) continue;
    return h.point.clone();
  }
  return null;
}

/** 화면 정중앙 시선이 모델에 닿는 지점 = 회전 중심 (회전 조작용 fallback) */
function pickOrbitTargetAtScreenCenter(object, fallbackDist) {
  const hit = raycastModelAtScreenCenter(object);
  if (hit) return hit;
  raycaster.setFromCamera(_screenCenterNdc, camera);
  const dist = fallbackDist ?? Math.max(camera.position.distanceTo(controls.target), 0.1);
  return camera.position.clone().add(raycaster.ray.direction.clone().multiplyScalar(dist));
}

/** fit 후 — 레이가 모델에 맞을 때만 target 이동 (miss 시 fallback pan 금지 → 화면 밖 방지) */
function syncOrbitTargetToScreenCenter(object, fallbackDist) {
  const hit = raycastModelAtScreenCenter(object);
  if (!hit) return false;
  _orbitDelta.subVectors(hit, controls.target);
  panCameraAndTarget(_orbitDelta);
  return true;
}

/** 주황 회전 아이콘 위치 = 화면 정중앙 깊이의 3D 점 */
function getScreenCenterPivot(out = _pivot) {
  const dist = Math.max(camera.position.distanceTo(controls.target), 0.1);
  out.copy(pickOrbitTargetAtScreenCenter(getActiveModelRoot(), dist));
  return out;
}

/** 가까울수록·멀수록 모두 쓸 만한 민감도 */
function getRotatePixelSpeed() {
  const d = Math.max(camera.position.distanceTo(controls.target), 0.05);
  const ratio = ROTATE_REF_DIST / d;
  return THREE.MathUtils.clamp(ROTATE_PIXEL_BASE * ratio, ROTATE_PIXEL_MIN, ROTATE_PIXEL_MAX);
}

/**
 * 화면 정중앙(주황 아이콘) 축 기준 무한 회전
 */
function orbitRotateByPixels(dx, dy) {
  getScreenCenterPivot(_pivot);

  _orbitDelta.subVectors(camera.position, _pivot);
  const len = _orbitDelta.length();
  if (len < 1e-6) return;

  const speed = getRotatePixelSpeed();
  camera.updateMatrixWorld(true);
  _camRight.setFromMatrixColumn(camera.matrix, 0);
  _camUp.setFromMatrixColumn(camera.matrix, 1);

  _orbitDelta.applyAxisAngle(_camUp, -dx * speed);
  _orbitDelta.applyAxisAngle(_camRight, -dy * speed);
  camera.position.copy(_pivot).add(_orbitDelta.normalize().multiplyScalar(len));

  camera.up.applyAxisAngle(_camUp, -dx * speed);
  camera.up.applyAxisAngle(_camRight, -dy * speed);
  controls.target.copy(_pivot);
  camera.lookAt(_pivot);
  controls.update();
}

function orbitPanByPixels(dx, dy) {
  const dist = Math.max(camera.position.distanceTo(controls.target), 0.05);
  const vFov = (camera.fov * Math.PI) / 180;
  const worldPerPx = (2 * dist * Math.tan(vFov / 2)) / Math.max(host.clientHeight, 1);
  camera.updateMatrixWorld(true);
  _camRight.setFromMatrixColumn(camera.matrix, 0);
  _camUp.setFromMatrixColumn(camera.matrix, 1);
  _orbitDelta.set(0, 0, 0);
  _orbitDelta.addScaledVector(_camRight, -dx * worldPerPx);
  _orbitDelta.addScaledVector(_camUp, dy * worldPerPx);
  panCameraAndTarget(_orbitDelta);
}

/** 화면 안에서 90° 회전 — 시선축(target→카메라) 기준 */
function rotateViewInPlane(angleRad) {
  const target = controls.target;
  _camFwd.subVectors(camera.position, target);
  if (_camFwd.lengthSq() < 1e-12) return;
  _camFwd.normalize();

  _orbitDelta.subVectors(camera.position, target).applyAxisAngle(_camFwd, angleRad);
  camera.position.copy(target).add(_orbitDelta);
  camera.up.applyAxisAngle(_camFwd, angleRad);
  camera.lookAt(target);
  controls.update();
}

/** 상하 반전 — 카메라 오른쪽 축 기준 180° */
function flipViewVertical() {
  camera.updateMatrixWorld(true);
  _camRight.setFromMatrixColumn(camera.matrix, 0).normalize();
  const target = controls.target;
  _orbitDelta.subVectors(camera.position, target).applyAxisAngle(_camRight, Math.PI);
  camera.position.copy(target).add(_orbitDelta);
  camera.up.applyAxisAngle(_camRight, Math.PI);
  camera.lookAt(target);
  controls.update();
}

function applyViewRotateAction(mode) {
  if (mode === 'cw90') rotateViewInPlane(-Math.PI / 2);
  else if (mode === 'ccw90') rotateViewInPlane(Math.PI / 2);
  else if (mode === 'flip') flipViewVertical();
}

function clearTouchHoldRotate() {
  if (touchHoldRotate?.timer) clearTimeout(touchHoldRotate.timer);
  touchHoldRotate = null;
}

function armTouchHoldRotate(domElement, e) {
  if (!touchHoldRotate || touchHoldRotate.pointerId !== e.pointerId) return;
  touchHoldRotate.armed = true;
  lastPointerX = e.clientX;
  lastPointerY = e.clientY;
  const root = getActiveModelRoot();
  if (root) syncOrbitTargetToScreenCenter(root);
  try {
    domElement.setPointerCapture(e.pointerId);
  } catch (_) {
    /* noop */
  }
  if (hint) hint.textContent = '터치 회전 — 손가락 떼면 종료';
}

function setupTouchHoldRotate(domElement) {
  domElement.addEventListener(
    'pointerdown',
    (e) => {
      if (e.pointerType !== 'touch' || e.button !== 0) return;
      activeTouchPointerIds.add(e.pointerId);
      if (activeTouchPointerIds.size > 1) {
        clearTouchHoldRotate();
        return;
      }
      if (customDragMode) return;

      clearTouchHoldRotate();
      const startX = e.clientX;
      const startY = e.clientY;
      const pointerId = e.pointerId;
      const timer = setTimeout(() => {
        if (!touchHoldRotate || touchHoldRotate.pointerId !== pointerId) return;
        if (!activeTouchPointerIds.has(pointerId)) return;
        armTouchHoldRotate(domElement, { pointerId, clientX: startX, clientY: startY });
      }, TOUCH_HOLD_ROTATE_MS);
      touchHoldRotate = { pointerId, startX, startY, armed: false, timer };
    },
    true
  );

  domElement.addEventListener(
    'pointermove',
    (e) => {
      if (!touchHoldRotate || touchHoldRotate.pointerId !== e.pointerId) return;

      if (!touchHoldRotate.armed) {
        const dx = e.clientX - touchHoldRotate.startX;
        const dy = e.clientY - touchHoldRotate.startY;
        if (dx * dx + dy * dy > TOUCH_HOLD_PREARM_MOVE_PX * TOUCH_HOLD_PREARM_MOVE_PX) {
          clearTouchHoldRotate();
        }
        return;
      }

      const dx = e.clientX - lastPointerX;
      const dy = e.clientY - lastPointerY;
      lastPointerX = e.clientX;
      lastPointerY = e.clientY;
      orbitRotateByPixels(dx, dy);
      e.preventDefault();
      e.stopPropagation();
    },
    true
  );

  const endTouchHoldRotate = (e) => {
    if (e.pointerType === 'touch') {
      activeTouchPointerIds.delete(e.pointerId);
    }
    if (!touchHoldRotate || touchHoldRotate.pointerId !== e.pointerId) return;
    const wasArmed = touchHoldRotate.armed;
    clearTouchHoldRotate();
    if (wasArmed) {
      suppressPointerClick = true;
      pointerDownPos = null;
      if (hint) hint.textContent = NAV_HINT_DEFAULT;
      try {
        domElement.releasePointerCapture(e.pointerId);
      } catch (_) {
        /* noop */
      }
      e.preventDefault();
      e.stopPropagation();
    }
  };

  domElement.addEventListener('pointerup', endTouchHoldRotate, true);
  domElement.addEventListener('pointercancel', endTouchHoldRotate, true);
  domElement.addEventListener('pointerleave', endTouchHoldRotate, true);
}

/** Solid Edge / CAD — 중클릭 회전(무한), 우클릭·Shift+중클릭 이동, 휠 줌 */
function setupCadNavigation(ctrl, domElement) {
  ctrl.enableDamping = true;
  ctrl.dampingFactor = 0.1;
  ctrl.enableRotate = false;
  ctrl.panSpeed = 1.0;
  ctrl.zoomSpeed = 0.55;
  ctrl.zoomToCursor = false;
  ctrl.screenSpacePanning = true;
  ctrl.mouseButtons = {
    LEFT: -1,
    MIDDLE: -1,
    RIGHT: THREE.MOUSE.PAN,
  };

  domElement.addEventListener('contextmenu', (e) => e.preventDefault());

  domElement.addEventListener(
    'pointerdown',
    (e) => {
      if (e.button !== 1) return;
      customDragMode = e.shiftKey ? 'pan' : 'rotate';
      lastPointerX = e.clientX;
      lastPointerY = e.clientY;
      if (customDragMode === 'rotate') {
        const root = getActiveModelRoot();
        if (root) syncOrbitTargetToScreenCenter(root);
      }
      domElement.setPointerCapture(e.pointerId);
      e.preventDefault();
      e.stopPropagation();
    },
    true
  );

  domElement.addEventListener(
    'pointermove',
    (e) => {
      if (!customDragMode) return;
      const dx = e.clientX - lastPointerX;
      const dy = e.clientY - lastPointerY;
      lastPointerX = e.clientX;
      lastPointerY = e.clientY;
      if (customDragMode === 'rotate') orbitRotateByPixels(dx, dy);
      else orbitPanByPixels(dx, dy);
      e.preventDefault();
    },
    true
  );

  const endCustomDrag = (e) => {
    if (e.button !== 1) return;
    customDragMode = null;
    try {
      domElement.releasePointerCapture(e.pointerId);
    } catch (_) { /* noop */ }
  };
  domElement.addEventListener('pointerup', endCustomDrag, true);
  domElement.addEventListener('pointercancel', endCustomDrag, true);

  setupTouchHoldRotate(domElement);

  let zoomPivotTimer = null;
  domElement.addEventListener(
    'wheel',
    () => {
      clearTimeout(zoomPivotTimer);
      zoomPivotTimer = setTimeout(() => {
        const root = getActiveModelRoot();
        if (root) syncOrbitTargetToScreenCenter(root);
      }, 100);
    },
    { passive: true }
  );
}

function fitActiveView() {
  const modelRoot = getActiveModelRoot();
  if (!modelRoot) return;
  fitCameraToModel(modelRoot, { prepare: true, viewId: 'fit' });
  setViewToolbarActive(null);
}

function setupViewToolbar() {
  const bar = document.getElementById('viewToolbar');
  if (!bar) return;
  bar.addEventListener('click', (e) => {
    const rotBtn = e.target.closest('button[data-rotate]');
    if (rotBtn) {
      applyViewRotateAction(rotBtn.dataset.rotate);
      return;
    }
    const btn = e.target.closest('button[data-view]');
    if (!btn) return;
    setStandardView(btn.dataset.view);
  });
}

async function fetchJson(url) {
  const res = await fetch(url);
  if (!res.ok) throw new Error('fetch failed: ' + url);
  return res.json();
}

function makeBox(name, size, pos, color, clickable) {
  const geo = new THREE.BoxGeometry(size[0], size[1], size[2]);
  const mat = new THREE.MeshStandardMaterial({ color, transparent: true, opacity: clickable ? 0.75 : 0.9 });
  const mesh = new THREE.Mesh(geo, mat);
  mesh.name = name;
  mesh.position.set(pos[0], pos[1], pos[2]);
  if (clickable) {
    mesh.userData.clickable = true;
    assyMeshes.push(mesh);
  }
  return mesh;
}

function loadGltfWithProgress(url, onProgress, timeoutMs = 120000, { stripTextures = false } = {}) {
  const activeLoader = stripTextures ? createTextureStrippedGltfLoader() : loader;
  return new Promise((resolve, reject) => {
    const timer = setTimeout(
      () => reject(new Error(`GLB 로드 시간 초과 (${Math.round(timeoutMs / 1000)}초)`)),
      timeoutMs
    );
    let indeterminate = null;
    if (onProgress) {
      indeterminate = setInterval(() => {
        onProgress(-1);
      }, 400);
    }
    activeLoader.load(
      url,
      (gltf) => {
        clearTimeout(timer);
        if (indeterminate) clearInterval(indeterminate);
        if (stripTextures) {
          gltf.scene.traverse((node) => {
            if (!node.isMesh || !node.material) return;
            const mats = Array.isArray(node.material) ? node.material : [node.material];
            for (const mat of mats) {
              if (!mat) continue;
              mat.map = null;
              mat.normalMap = null;
              mat.roughnessMap = null;
              mat.metalnessMap = null;
              mat.emissiveMap = null;
              mat.aoMap = null;
            }
          });
        }
        resolve(gltf);
      },
      (xhr) => {
        if (!onProgress) return;
        if (xhr.total > 0) {
          onProgress((xhr.loaded / xhr.total) * 100);
        }
      },
      (err) => {
        clearTimeout(timer);
        if (indeterminate) clearInterval(indeterminate);
        reject(err);
      }
    );
  });
}

function clearGroup(g) {
  if (!g) return;
  while (g.children.length) {
    const c = g.children[0];
    g.remove(c);
    if (c.geometry) c.geometry.dispose();
    if (c.material) {
      if (Array.isArray(c.material)) c.material.forEach((m) => m.dispose());
      else c.material.dispose();
    }
  }
}

function buildProceduralOverview() {
  const root = new THREE.Group();
  assyMeshes = [];
  if (!catalog?.assemblies) return root;

  for (const assy of catalog.assemblies) {
    const layout = PROC_LAYOUT[assy.id] || { size: [0.5, 0.5, 0.5], pos: [0, 1, 0] };
    const meshName = assy.meshName || (assy.id.toUpperCase() + '_ASSY');
    const color = ASSY_COLORS[meshName] || 0x888888;
    const mesh = makeBox(meshName, layout.size, layout.pos, color, true);
    mesh.userData.assyId = assy.id;
    root.add(mesh);
  }
  return root;
}

/** GLB/STP — 노드 transform을 geometry에 bake (bbox·렌더 좌표 일치) */
const _bakeMatrix = new THREE.Matrix4();

function bakeMeshWorldTransforms(root) {
  const usedGeometry = new Set();
  root.updateMatrixWorld(true);

  root.traverse((node) => {
    if (!node.isMesh || !node.geometry?.attributes?.position) return;
    if (node.userData.isIoLampMarker) return;
    node.updateWorldMatrix(true, false);
    _bakeMatrix.copy(node.matrixWorld);

    let geo = node.geometry;
    if (usedGeometry.has(geo)) {
      geo = geo.clone();
      node.geometry = geo;
    } else {
      usedGeometry.add(geo);
    }

    geo.applyMatrix4(_bakeMatrix);
    geo.computeBoundingBox();
    geo.computeBoundingSphere();
    node.position.set(0, 0, 0);
    node.rotation.set(0, 0, 0);
    node.scale.set(1, 1, 1);
  });

  root.traverse((node) => {
    if (node.isMesh) return;
    node.position.set(0, 0, 0);
    node.rotation.set(0, 0, 0);
    node.scale.set(1, 1, 1);
  });

  root.updateMatrixWorld(true);
}

/** bake/GLB 생성 후 법선 없음 → MeshStandardMaterial이 검게 렌더 (Carriage JT 병합 GLB) */
function isCadFormMesh(node) {
  if (!node?.isMesh) return false;
  if (node.userData?.cadFormMerged) return false;
  if (node.userData?.cadCollapsedHidden) return false;
  if (node.userData?.isIoLampMarker || node.userData?.isStpOverlay || node.userData?.isIoLamp) {
    return false;
  }
  return true;
}

/** WebView2 — mesh 300+ bake(88만 tri) 시 RenderProcessUnresponsive */
const HEAVY_GLB_MESH_FAST = 300;
const CAD_SHARED_BASIC_MESH_MIN = 100;
let cadSharedBasicMaterial = null;

function getCadSharedBasicMaterial() {
  if (!cadSharedBasicMaterial) {
    cadSharedBasicMaterial = new THREE.MeshBasicMaterial({
      color: 0xc8ccd6,
      side: THREE.DoubleSide,
    });
  }
  return cadSharedBasicMaterial;
}

function resetCadFormMerge(pivot) {
  if (!pivot) return;
  pivot.traverse((node) => {
    if (node.userData?.cadCollapsedHidden) {
      node.visible = true;
      delete node.userData.cadCollapsedHidden;
    }
  });
  const prev = pivot.getObjectByName('CadFormMerged');
  if (prev) {
    pivot.remove(prev);
    prev.geometry?.dispose();
    if (prev.material) {
      if (Array.isArray(prev.material)) prev.material.forEach((m) => m.dispose());
      else prev.material.dispose();
    }
  }
  const grp = pivot.getObjectByName('CadFormMergedGroup');
  if (grp) {
    pivot.remove(grp);
    grp.traverse((n) => {
      n.geometry?.dispose();
      if (n.material && n.material !== cadSharedBasicMaterial) n.material.dispose();
    });
  }
  delete pivot.userData.cadFormMerged;
  delete pivot.userData.cadFormMeshCount;
  delete pivot.userData.cadSharedBasicCount;
}

function countVisibleFormMeshes(root) {
  let n = 0;
  root?.traverse((node) => {
    if (!isCadFormMesh(node)) return;
    if (!node.visible || node.userData.outlierSuppressed) return;
    if (!node.geometry?.attributes?.position) return;
    n++;
  });
  return n;
}

/** Lower_Frame 패턴 — mesh N개 · 재질 1개(MeshBasicMaterial) 공유 */
function applyCadSharedBasicMaterial(pivot) {
  const mat = getCadSharedBasicMaterial();
  let meshCount = 0;
  pivot.traverse((node) => {
    if (!isCadFormMesh(node)) return;
    if (!node.visible || node.userData.outlierSuppressed) return;
    if (!node.geometry?.attributes?.position) return;
    node.material = mat;
    node.frustumCulled = false;
    meshCount++;
  });
  if (meshCount > 0) {
    pivot.userData.cadSharedBasicCount = meshCount;
  }
  return { shared: meshCount >= CAD_SHARED_BASIC_MESH_MIN, meshCount };
}

/** GLB 형체 — 로더 PBR 색상 유지, 법선·면 방향만 보정 (회색 Basic 덮어쓰기 금지) */
function ensureCadDisplayMeshes(root) {
  if (!root) return;
  root.traverse((node) => {
    if (!isCadFormMesh(node)) return;
    const geo = node.geometry;
    if (!geo?.attributes?.position) return;
    if (!geo.attributes.normal) geo.computeVertexNormals();
    geo.computeBoundingBox();
    geo.computeBoundingSphere();
    node.frustumCulled = false;

    const mats = Array.isArray(node.material) ? node.material : [node.material];
    for (const mat of mats) {
      if (!mat) continue;
      mat.side = geo.attributes.normal ? THREE.FrontSide : THREE.DoubleSide;
      if (mat.isMeshStandardMaterial || mat.isMeshPhysicalMaterial) {
        if (mat.metalness == null) mat.metalness = 0.15;
        if (mat.roughness == null) mat.roughness = 0.75;
        mat.needsUpdate = true;
      }
    }
  });
}

/** @deprecated ensureCadDisplayMeshes / collapseCadDetailMeshes 사용 */
function recomputeMeshNormals(root) {
  ensureCadDisplayMeshes(root);
}

/** 표시 중인 mesh만 bbox */
function getMeshesBox(object) {
  if (!object) return null;
  object.updateMatrixWorld(true);
  const box = new THREE.Box3();
  let ok = false;

  object.traverse((node) => {
    if (!node.isMesh || !node.visible || node.userData.outlierSuppressed) return;
    if (node.userData.isIoLampMarker) return;
    if (!node.geometry?.attributes?.position) return;
    const geo = node.geometry;
    geo.computeBoundingBox();
    if (!geo.boundingBox || geo.boundingBox.isEmpty()) return;
    const tmp = geo.boundingBox.clone().applyMatrix4(node.matrixWorld);
    if (!ok) {
      box.copy(tmp);
      ok = true;
    } else {
      box.union(tmp);
    }
  });

  return ok ? box : null;
}

/** 카메라 fit 전용 — 주 형체 cluster bbox (먼 outlier mesh 제외, geometry/센서 좌표 변경 없음) */
function getFitClusterBox(object) {
  const items = collectMeshItems(object).filter(
    (it) =>
      it.mesh.visible &&
      !it.mesh.userData.outlierSuppressed &&
      !it.mesh.userData.isIoLampMarker
  );
  if (!items.length) return getMeshesBox(object);
  if (items.length === 1) return items[0].box.clone();

  items.sort((a, b) => b.vol - a.vol);
  let wSum = 0;
  const wCenter = new THREE.Vector3();
  for (const it of items) {
    wSum += it.vol;
    wCenter.addScaledVector(it.center, it.vol);
  }
  wCenter.divideScalar(wSum);

  const clusterR = Math.max(items[0].diag * 2.5, 1e-3);
  const box = new THREE.Box3();
  let ok = false;
  for (const it of items) {
    if (it.center.distanceTo(wCenter) <= clusterR) {
      if (!ok) {
        box.copy(it.box);
        ok = true;
      } else {
        box.union(it.box);
      }
    }
  }
  return ok ? box : getMeshesBox(object);
}

function panCameraToScreenCenter(object, dist, scr) {
  if (!scr || scr.ext < 1e-6) return;
  if (Math.abs(scr.cx) <= 0.015 && Math.abs(scr.cy) <= 0.015) return;
  camera.updateMatrixWorld(true);
  _camRight.setFromMatrixColumn(camera.matrix, 0);
  _camUp.setFromMatrixColumn(camera.matrix, 1);
  const vFov = (camera.fov * Math.PI) / 180;
  const worldPerNdcY = dist * Math.tan(vFov / 2);
  const worldPerNdcX = worldPerNdcY * Math.max(camera.aspect, 0.01);
  const sx = -scr.cx * worldPerNdcX;
  const sy = -scr.cy * worldPerNdcY;
  controls.target.addScaledVector(_camRight, sx);
  controls.target.addScaledVector(_camUp, sy);
  camera.position.addScaledVector(_camRight, sx);
  camera.position.addScaledVector(_camUp, sy);
  camera.lookAt(controls.target);
  controls.update();
}

/** 숨김 mesh 포함 전체 bbox — fit bbox와 비교용 */
function getAllMeshesBox(object) {
  if (!object) return null;
  object.updateMatrixWorld(true);
  const box = new THREE.Box3();
  let ok = false;
  object.traverse((node) => {
    if (!node.isMesh || !node.geometry?.attributes?.position) return;
    const geo = node.geometry;
    geo.computeBoundingBox();
    if (!geo.boundingBox || geo.boundingBox.isEmpty()) return;
    const tmp = geo.boundingBox.clone().applyMatrix4(node.matrixWorld);
    if (!ok) {
      box.copy(tmp);
      ok = true;
    } else {
      box.union(tmp);
    }
  });
  return ok ? box : null;
}

function collectMeshItems(object) {
  const items = [];
  object.updateMatrixWorld(true);
  object.traverse((node) => {
    if (!node.isMesh || !node.geometry?.attributes?.position) return;
    node.geometry.computeBoundingBox();
    if (!node.geometry.boundingBox || node.geometry.boundingBox.isEmpty()) return;
    const box = node.geometry.boundingBox.clone().applyMatrix4(node.matrixWorld);
    const size = box.getSize(new THREE.Vector3());
    const vol = Math.max(size.x, 1e-3) * Math.max(size.y, 1e-3) * Math.max(size.z, 1e-3);
    items.push({ mesh: node, box, center: box.getCenter(new THREE.Vector3()), vol, diag: size.length() });
  });
  return items;
}

/** GLB/STP — 멀리 떨어진 작은 mesh 숨김 (CAD export 아티팩트) */
function suppressOutlierMeshes(object) {
  const items = collectMeshItems(object);
  if (items.length <= 1) return 0;

  items.sort((a, b) => b.vol - a.vol);
  let wSum = 0;
  const wCenter = new THREE.Vector3();
  for (const it of items) {
    wSum += it.vol;
    wCenter.add(it.center.clone().multiplyScalar(it.vol));
  }
  wCenter.divideScalar(wSum);

  const mainDiag = items[0].diag;
  const maxVol = items[0].vol;
  const maxDist = Math.max(mainDiag * 2.5, 8000);
  let dropped = 0;

  for (const it of items) {
    if (it.mesh.userData.isIoLamp) continue;
    const d = it.center.distanceTo(wCenter);
    const tiny = it.vol < maxVol * 0.01;
    const suppress = (d > maxDist && tiny) || d > maxDist * 5;
    if (suppress) {
      it.mesh.visible = false;
      it.mesh.userData.outlierSuppressed = true;
      dropped++;
    }
  }

  let visibleLeft = 0;
  for (const it of items) {
    if (it.mesh.visible && !it.mesh.userData.outlierSuppressed) visibleLeft++;
  }
  if (visibleLeft === 0) {
    for (const it of items) {
      it.mesh.visible = true;
      delete it.mesh.userData.outlierSuppressed;
    }
    return 0;
  }
  return dropped;
}

/** bake 후 geometry를 지정점으로 이동 — 원점 = center */
function centerGeometryAtPoint(object, center) {
  object.updateMatrixWorld(true);
  const c = center.clone();
  const usedGeometry = new Set();

  object.traverse((node) => {
    if (!node.isMesh || !node.visible || node.userData.outlierSuppressed) return;
    if (node.userData.isIoLampMarker) return;
    if (!node.geometry?.attributes?.position) return;

    let geo = node.geometry;
    if (usedGeometry.has(geo)) {
      geo = geo.clone();
      node.geometry = geo;
    } else {
      usedGeometry.add(geo);
    }

    geo.translate(-c.x, -c.y, -c.z);
    geo.computeBoundingBox();
    geo.computeBoundingSphere();
  });

  object.updateMatrixWorld(true);
  return true;
}

function getGlbOriginOffsetZUpMm() {
  const o = ioManifestMeta?.glbOriginOffsetMm;
  return o?.length === 3 ? o : null;
}

/** manifest glbOriginOffsetMm — ImageToStl 등 외부 GLB 원점 보정 (Z-up mm) */
function applyGlbOriginOffsetToGeometry(object) {
  const o = getGlbOriginOffsetZUpMm();
  if (!o) return;
  const d = cadZUpMmToYUp(o[0], o[1], o[2]);
  if (Math.abs(d.x) + Math.abs(d.y) + Math.abs(d.z) < 1e-3) return;
  object.updateMatrixWorld(true);
  object.traverse((node) => {
    if (!node.isMesh || !node.geometry) return;
    node.geometry = node.geometry.clone();
    node.geometry.translate(d.x, d.y, d.z);
    node.geometry.computeBoundingBox();
    node.geometry.computeBoundingSphere();
  });
  object.updateMatrixWorld(true);
}

/** io_manifest — SE 어셈블리 원점 [0,0,0] 만. bbox/cadReference 중심 이동 금지 */
function getStpAssemblyOriginZUpMm() {
  if (ioManifestMeta?.assemblyOrigin?.length === 3) {
    return ioManifestMeta.assemblyOrigin;
  }
  if (manifestCoordsAreZUpMm()) {
    return [0, 0, 0];
  }
  return null;
}

function isAssemblyOriginZeroZUp() {
  const o = getStpAssemblyOriginZUpMm();
  if (!o) return false;
  return Math.abs(o[0]) + Math.abs(o[1]) + Math.abs(o[2]) < 1e-6;
}

function getSurfaceSnapMaxMm() {
  const v = currentAssyIoProfile?.viewer?.surfaceSnapMaxMm;
  if (v === 0 || v === false) return 0;
  if (typeof v === 'number' && v > 0) return v;
  return IO_STP_SURFACE_SNAP_MAX_MM_DEFAULT;
}

function zUpMmToDisplayAtPrepare(x, y, z, zUpFix) {
  if (zUpFix) return cadZUpMmToYUp(x, y, z);
  return new THREE.Vector3(x, y, z);
}

/** GLB 중심 — STP manifest 원점 우선 (동일 export 좌표계) */
function manifestCoordsAreZUpMm() {
  return (
    ioManifestMeta?.coordinateSystem === 'solid_edge_z_up_mm' ||
    ioManifestMeta?.cadReference?.zUp === true
  );
}

function resolveLayoutCenter(object, zUpFix) {
  if (isAssemblyOriginZeroZUp()) {
    return zUpFix ? cadZUpMmToYUp(0, 0, 0) : new THREE.Vector3(0, 0, 0);
  }
  const stpOrigin = getStpAssemblyOriginZUpMm();
  if (stpOrigin) {
    const useZUp = manifestCoordsAreZUpMm() ? true : zUpFix;
    return zUpMmToDisplayAtPrepare(stpOrigin[0], stpOrigin[1], stpOrigin[2], useZUp);
  }
  return new THREE.Vector3(0, 0, 0);
}

/** Solid Edge STEP/GLB — 자식을 CadOrient 그룹으로 감쌈 */
function getOrientGroup(pivot) {
  let g = pivot.getObjectByName('CadOrient');
  if (!g) {
    g = new THREE.Group();
    g.name = 'CadOrient';
    while (pivot.children.length) g.add(pivot.children[0]);
    pivot.add(g);
  }
  return g;
}

const _snapProbe = new THREE.Vector3();
const _snapBestWorld = new THREE.Vector3();

function objectHasUserDataFlag(object, key) {
  if (!object) return false;
  if (object.userData?.[key]) return true;
  let found = false;
  object.traverse((node) => {
    if (node.userData?.[key]) found = true;
  });
  return found;
}

/** ImageToStl 등 — 파일이 이미 SE Z-up mm, cadZUpMmToYUp 만 맞추면 됨 (회전·bake 금지) */
function isImageToStlZUpGlb(object) {
  if (!object) return false;
  if (objectHasUserDataFlag(object, 'imageToStlGlb')) return true;
  if (ioManifestMeta?.glbExportHint === 'image_to_stl_z_up') return true;
  let cadMesh = 0;
  let total = 0;
  object.traverse((node) => {
    if (!node.isMesh) return;
    total++;
    const n = node.name || '';
    if (/^(Mesh\d+|Node\d+)$/i.test(n)) cadMesh++;
  });
  if (total > 0 && cadMesh === total && total <= 24) {
    const box = getMeshesBox(object);
    if (box) {
      const s = box.getSize(new THREE.Vector3());
      const tall = Math.max(s.x, s.y, s.z);
      const others = [s.x, s.y, s.z].filter((v) => v < tall * 0.99);
      if (others.length && tall > Math.max(...others) * 1.1) return true;
    }
  }
  return false;
}

/** ImageToStl Node* / manifest 힌트 — 이미 Y-up, SE Z-up 회전 금지 */
function isLikelyExternalYUpGlb(object) {
  if (!object) return false;
  if (objectHasUserDataFlag(object, 'externalYUpGlb')) return true;
  if (ioManifestMeta?.glbExportHint === 'external_y_up') return true;
  let nodeMeshes = 0;
  let namedMeshes = 0;
  object.traverse((node) => {
    if (!node.isMesh) return;
    if (/^Node\d+$/i.test(node.name || '')) nodeMeshes++;
    else if (node.name) namedMeshes++;
  });
  return nodeMeshes >= 3 && nodeMeshes > namedMeshes;
}

/** STEP/manifest Z-up → Three.js Y-up. ImageToStl Z-up GLB는 회전 생략 */
function needsSolidEdgeZUpFix(object) {
  if (isImageToStlZUpGlb(object)) return false;
  if (isLikelyExternalYUpGlb(object)) return false;
  if (manifestCoordsAreZUpMm()) return true;
  const box = getMeshesBox(object);
  if (!box) return false;
  const s = box.getSize(new THREE.Vector3());
  return s.z > s.y * 1.1 && s.z > s.x * 0.25;
}

function prepareModelForView(object, { isStep = false, skipOutlierSuppress = false } = {}) {
  resetCadFormMerge(object);
  object.position.set(0, 0, 0);
  object.rotation.set(0, 0, 0);
  object.scale.set(1, 1, 1);
  object.updateMatrixWorld(true);

  let dropped = 0;
  if (isStep) {
    const orient = getOrientGroup(object);
    orient.rotation.set(0, 0, 0);
    if (!skipOutlierSuppress) dropped = suppressOutlierMeshes(object);
    if (needsSolidEdgeZUpFix(object)) {
      orient.rotation.set(-Math.PI / 2, 0, 0);
      object.userData.cadZUpFix = true;
    } else {
      object.userData.cadZUpFix = false;
    }
    object.updateMatrixWorld(true);
      bakeMeshWorldTransforms(object);
      orient.rotation.set(0, 0, 0);
      applyGlbOriginOffsetToGeometry(object);
      const layoutCenter = resolveLayoutCenter(object, object.userData.cadZUpFix);
      if (!isAssemblyOriginZeroZUp()) {
        centerGeometryAtPoint(object, layoutCenter);
      }
    object.userData.cadLayoutCenter = layoutCenter.clone();
    object.userData.stpOriginAligned = !!getStpAssemblyOriginZUpMm();
  } else {
    if (!skipOutlierSuppress) dropped = suppressOutlierMeshes(object);
    object.updateMatrixWorld(true);

    const fastPath = countVisibleFormMeshes(object) >= HEAVY_GLB_MESH_FAST;
    object.userData.cadHeavyFastPath = fastPath;
    const zUpFix = needsSolidEdgeZUpFix(object);
    object.userData.cadZUpFix = zUpFix;

    if (fastPath) {
      // CadOrient 회전만 — bake/center 생략 (WebView 멈춤 방지)
      if (zUpFix) {
        getOrientGroup(object).rotation.set(-Math.PI / 2, 0, 0);
      }
      object.userData.cadOrientOnly = true;
      const layoutCenter = resolveLayoutCenter(object, zUpFix);
      object.userData.cadLayoutCenter = layoutCenter?.clone() || new THREE.Vector3();
      object.userData.stpOriginAligned = !!getStpAssemblyOriginZUpMm();
    } else {
      if (zUpFix) {
        const orient = getOrientGroup(object);
        orient.rotation.set(-Math.PI / 2, 0, 0);
        object.updateMatrixWorld(true);
        bakeMeshWorldTransforms(object);
        orient.rotation.set(0, 0, 0);
      } else {
        bakeMeshWorldTransforms(object);
      }
      applyGlbOriginOffsetToGeometry(object);
      const layoutCenter = resolveLayoutCenter(object, zUpFix);
      if (!isAssemblyOriginZeroZUp()) {
        if (!centerGeometryAtPoint(object, layoutCenter)) return { ok: false, dropped };
      }
      object.userData.cadLayoutCenter = layoutCenter.clone();
      object.userData.stpOriginAligned = !!getStpAssemblyOriginZUpMm();
    }
  }

  const box = getMeshesBox(object);
  if (!box) return { ok: false, dropped };

  const size = box.getSize(new THREE.Vector3());
  const maxDim = Math.max(size.x, size.y, size.z, 0.001);
  const s = MODEL_SCENE_SIZE / maxDim;
  if (s >= 1e-4 && s <= 5000) {
    object.userData.cadLayoutScale = s;
    object.scale.setScalar(s);
    object.updateMatrixWorld(true);
  } else {
    object.userData.cadLayoutScale = 1;
  }

  resetCadFormMerge(object);
  ensureCadDisplayMeshes(object);

  return { ok: true, dropped, fastPath: !!object.userData.cadHeavyFastPath };
}

/** 사용자 제공 스크린샷 기준 (축 해석 아님, 화면 그대로) */
const CAD_VIEWS = {
  front: { dir: [0, 1, 0], up: [0, 0, -1] },
  right: { dir: [1, 0, 0], up: [0, 1, 0] },
  top: { dir: [0, 1, 0], up: [1, 0, 0] },
  fit: { dir: [1, 1, 1], up: [0, 1, 0] },
};

function getActiveModelRoot() {
  if (mode === 'detail' && modelPivot) return modelPivot;
  return overviewRoot || null;
}

/** bbox 중심 + VIEW_FOCUS_OFFSET — 사용 안 함, applyViewFocusOffsetPan 사용 */
function getFitCenter(box) {
  return box.getCenter(new THREE.Vector3());
}

/** 지금 카메라 기준 — 표시 mesh가 화면에서 차지하는 영역 */
function measureOnScreen(object) {
  let minX = Infinity;
  let maxX = -Infinity;
  let minY = Infinity;
  let maxY = -Infinity;
  let meshOnScreen = 0;
  let ok = false;

  object.updateMatrixWorld(true);
  object.traverse((node) => {
    if (!node.isMesh || !node.visible || node.userData.outlierSuppressed) return;
    if (!node.geometry?.attributes?.position) return;
    const geo = node.geometry;
    geo.computeBoundingBox();
    if (!geo.boundingBox || geo.boundingBox.isEmpty()) return;

    let onThis = false;
    const { min, max } = geo.boundingBox;
    for (const x of [min.x, max.x]) {
      for (const y of [min.y, max.y]) {
        for (const z of [min.z, max.z]) {
          _ndcPt.set(x, y, z).applyMatrix4(node.matrixWorld).project(camera);
          if (_ndcPt.x >= -1.05 && _ndcPt.x <= 1.05 && _ndcPt.y >= -1.05 && _ndcPt.y <= 1.05) {
            onThis = true;
          }
          minX = Math.min(minX, _ndcPt.x);
          maxX = Math.max(maxX, _ndcPt.x);
          minY = Math.min(minY, _ndcPt.y);
          maxY = Math.max(maxY, _ndcPt.y);
          ok = true;
        }
      }
    }
    if (onThis) meshOnScreen++;
  });

  if (!ok) return { cx: 0, cy: 0, ext: 0, meshOnScreen: 0 };
  return {
    cx: (minX + maxX) * 0.5,
    cy: (minY + maxY) * 0.5,
    ext: Math.max(maxX - minX, maxY - minY) * 0.5,
    meshOnScreen,
  };
}

function applyCameraPose(center, dist, viewDir, viewUp) {
  camera.up.copy(viewUp);
  camera.position.copy(center).add(viewDir.clone().multiplyScalar(dist));
  camera.lookAt(center);
  controls.target.copy(center);
  controls.minDistance = Math.max(dist * 0.02, 0.01);
  controls.maxDistance = Math.max(dist * 30, 100);
  camera.near = Math.max(dist * 0.002, 0.01);
  camera.far = Math.max(dist * 100, 50000);
  camera.updateProjectionMatrix();
  controls.update();
}

/** 화면에 "전체가 들어오게" — 거리·위치를 눈 기준으로 맞춤 (좌표계/센서 변경 없음) */
function fitCameraSimple(object, viewId = DEFAULT_DETAIL_VIEW) {
  const box = getFitClusterBox(object) || getMeshesBox(object);
  if (!box || box.isEmpty()) return null;

  const cfg = CAD_VIEWS[viewId] || CAD_VIEWS.fit;
  let fitCenter = getFitCenter(box);
  const sphere = box.getBoundingSphere(new THREE.Sphere());
  const vFov = (camera.fov * Math.PI) / 180;
  const hFov = 2 * Math.atan(Math.tan(vFov / 2) * Math.max(camera.aspect, 0.01));
  let dist =
    Math.max(sphere.radius / Math.sin(vFov / 2), sphere.radius / Math.sin(hFov / 2)) * 1.05;

  const viewDir = new THREE.Vector3(cfg.dir[0], cfg.dir[1], cfg.dir[2]).normalize();
  const viewUp = new THREE.Vector3(cfg.up[0], cfg.up[1], cfg.up[2]);
  const minDist = Math.max(sphere.radius * 0.2, 0.05);
  const maxDist = Math.max(sphere.radius * 25, minDist * 4);

  controls.enableDamping = false;
  applyCameraPose(fitCenter, dist, viewDir, viewUp);
  camera.updateMatrixWorld(true);

  for (let pass = 0; pass < 24; pass++) {
    let scr = measureOnScreen(object);

    panCameraToScreenCenter(object, dist, scr);
    scr = measureOnScreen(object);

    if (scr.meshOnScreen === 0) {
      const fb = getFitClusterBox(object) || getMeshesBox(object);
      if (fb && !fb.isEmpty()) {
        fitCenter = getFitCenter(fb);
        applyCameraPose(fitCenter, dist, viewDir, viewUp);
        camera.updateMatrixWorld(true);
        scr = measureOnScreen(object);
      }
      if (scr.meshOnScreen === 0) break;
    }

    if (scr.ext > FIT_SCREEN_MAX) {
      dist *= scr.ext / FIT_SCREEN_TARGET;
    } else if (scr.ext < FIT_SCREEN_TARGET * 0.82) {
      dist *= Math.max(scr.ext / FIT_SCREEN_TARGET, 0.45);
    } else {
      break;
    }

    dist = Math.min(Math.max(dist, minDist), maxDist);
    const fb = getFitClusterBox(object);
    if (fb && !fb.isEmpty()) fitCenter = getFitCenter(fb);
    applyCameraPose(fitCenter, dist, viewDir, viewUp);
    camera.updateMatrixWorld(true);
  }

  controls.enableDamping = true;

  const camSaved = camera.position.clone();
  const tgtSaved = controls.target.clone();
  const beforeUi = measureOnScreen(object);

  syncOrbitTargetToScreenCenter(object, dist);
  if (hasViewFocusOffset() && beforeUi.meshOnScreen > 0 && beforeUi.ext >= FIT_SCREEN_TARGET * 0.35) {
    applyViewFocusOffsetPan(sphere.radius);
  }

  let final = measureOnScreen(object);
  if (final.meshOnScreen === 0 && beforeUi.meshOnScreen > 0) {
    camera.position.copy(camSaved);
    controls.target.copy(tgtSaved);
    camera.lookAt(controls.target);
    controls.update();
    final = beforeUi;
  }
  if (final.ext < FIT_SCREEN_TARGET * 0.55) {
    const fb = getFitClusterBox(object) || getMeshesBox(object);
    if (fb && !fb.isEmpty()) {
      fitCenter = getFitCenter(fb);
      dist = Math.min(Math.max(dist * Math.max(final.ext / FIT_SCREEN_TARGET, 0.4), minDist), maxDist);
      applyCameraPose(fitCenter, dist, viewDir, viewUp);
      panCameraToScreenCenter(object, dist, measureOnScreen(object));
      final = measureOnScreen(object);
    }
  }
  if (final.meshOnScreen === 0) {
    applyCameraPose(fitCenter, dist, viewDir, viewUp);
    final = measureOnScreen(object);
  }

  let screenMsg = '화면: 전체 들어옴';
  if (final.ext > FIT_SCREEN_MAX) screenMsg = '화면: 아직 잘림 (F키 또는 맞춤 다시)';
  else if (final.ext < FIT_SCREEN_TARGET * 0.5) screenMsg = '화면: 너무 작음 (휠로 확대)';
  else if (Math.abs(final.cx) > 0.1 || Math.abs(final.cy) > 0.1) {
    screenMsg = '화면: 한쪽으로 치우침 (우클릭 드래그로 이동)';
  }

  perfEl.textContent = `화면 ${Math.round(final.ext * 100)}% · ${VIEWER_BUILD}`;
  return { ...final, dist, screenMsg };
}

function setViewToolbarActive(viewId) {
  document.querySelectorAll('#viewToolbar button[data-view]').forEach((btn) => {
    btn.classList.toggle('active', !!viewId && btn.dataset.view === viewId);
  });
}

function applyViewToModel(object, viewId, diagExtra = {}) {
  const cfg = CAD_VIEWS[viewId];
  if (!cfg) return false;
  const screen = fitCameraSimple(object, viewId);
  if (!screen) return false;

  setViewToolbarActive(viewId);
  reportViewDiag(viewId, object, {
    ...diagExtra,
    prepareOk: true,
    dist: screen.dist,
    screenMsg: screen.screenMsg,
    screenExt: screen.ext,
    screenCx: screen.cx,
    screenCy: screen.cy,
    meshOnScreen: screen.meshOnScreen,
  });
  return true;
}

function fitCameraToModel(object, { prepare = false, diagExtra = {}, viewId = DEFAULT_DETAIL_VIEW } = {}) {
  if (!object) return false;
  const isStep = diagExtra.modelKind === 'step';

  if (prepare) {
    const r = prepareModelForView(object, { isStep });
    reportViewDiag('prepare', object, { ...diagExtra, dropped: r.dropped, prepareOk: r.ok, display: r.display });
    if (!r.ok) return false;
  }

  if (!applyViewToModel(object, viewId, diagExtra)) {
    reportViewDiag('fit-fail', object, { ...diagExtra, prepareOk: false });
    perfEl.textContent = `fit FAIL · ${VIEWER_BUILD}`;
    return false;
  }
  return true;
}

async function scheduleFitToModel(object, diagExtra = {}) {
  const isStep = diagExtra.modelKind === 'step';

  await new Promise((r) => requestAnimationFrame(r));
  await new Promise((r) => requestAnimationFrame(r));
  resize();

  setLoadStatus('형체 화면 맞춤 중...', 55);
  await new Promise((r) => setTimeout(r, 0));

  let r = prepareModelForView(object, { isStep });
  reportViewDiag('prepare', object, { ...diagExtra, dropped: r.dropped, prepareOk: r.ok, display: r.display });

  if (!r.ok) {
    object.position.set(0, 0, 0);
    object.rotation.set(0, 0, 0);
    object.scale.set(1, 1, 1);
    object.updateMatrixWorld(true);
    r = prepareModelForView(object, { isStep, skipOutlierSuppress: true });
    reportViewDiag('prepare-retry', object, { ...diagExtra, dropped: r.dropped, prepareOk: r.ok, display: r.display });
  }

  await new Promise((r) => requestAnimationFrame(r));
  resize();

  const screen = fitCameraSimple(object, DEFAULT_DETAIL_VIEW);
  if (!screen) {
    reportViewDiag('fit-fail', object, { ...diagExtra, prepareOk: false });
    perfEl.textContent = `fit FAIL · ${VIEWER_BUILD}`;
    setLoadStatus('카메라 맞춤 실패 (bbox 없음)', 100);
    postToHost({ type: 'loadStatus', message: 'fit 실패', error: true });
    return false;
  }

  setViewToolbarActive(DEFAULT_DETAIL_VIEW);
  reportViewDiag(DEFAULT_DETAIL_VIEW, object, {
    ...diagExtra,
    prepareOk: true,
    dist: screen.dist,
    screenMsg: screen.screenMsg,
    screenExt: screen.ext,
    screenCx: screen.cx,
    screenCy: screen.cy,
    meshOnScreen: screen.meshOnScreen,
  });
  return true;
}

function setStandardView(viewId) {
  const modelRoot = getActiveModelRoot();
  if (!modelRoot) return;
  applyViewToModel(modelRoot, viewId);
}

function fitCameraToObject(object) {
  fitCameraToModel(object);
}

function showLoadOverlay() {
  loadOverlay.style.display = 'flex';
}

async function urlExists(url) {
  try {
    const res = await fetch(url, { method: 'GET', headers: { Range: 'bytes=0-0' } });
    return res.ok || res.status === 206;
  } catch (_) {
    return false;
  }
}

/** prebuilt GLB 존재 여부 (C# glbReady 또는 URL 확인) */
async function isAssyGlbDisplayReady(resolved, assy) {
  const meta = resolvedModels.assemblies[assy.id];
  if (meta?.glbReady === true) return true;
  if (!(await urlExists(resolved.url))) return false;
  const bytes = await fetchModelByteLength(resolved.url);
  return !!(bytes && bytes >= 1024);
}

/** prebuilt GLB 없으면 즉시 실패 */
async function ensureAssyGlbReady(resolved, assy) {
  if (await isAssyGlbDisplayReady(resolved, assy)) return resolved;
  const meta = resolvedModels.assemblies[assy.id];
  const hint = meta?.convertError || '배포 패키지에 prebuilt GLB가 없습니다';
  throw new Error(`형체 GLB 없음: ${hint}`);
}

/** Inventor GLB(텍스처 수천) → WebView 멈춤. ASSY는 STP→GLB 변환본만 (다른 GLB 탐색 금지) */
async function pickHmiSafeGlbResolved(primary, assy) {
  if (!primary || primary.kind !== 'glb') return primary;

  const meta = await probeGlbFromUrl(primary.url);
  if (meta && !isGlbTooHeavyForHmi(meta)) {
    return { ...primary, glbMeta: meta };
  }

  const reason = meta ? formatGlbHeavyReason(meta) : 'GLB 분석 실패';
  console.warn('[3D] STP→GLB 변환본 HMI 주의', primary.path, reason);

  return {
    ...primary,
    glbMeta: meta,
    hmiUnsafe: true,
    fallbackReason: reason,
  };
}

async function resolveAssyModel(assy) {
  if (!assy.detailModel) return null;

  let resolved = null;
  const cached = resolvedModels.assemblies[assy.id];
  if (cached?.stpPath) {
    const glbPath = deriveGlbPathFromStp(cached.stpPath) || cached.path;
    resolved = {
      path: glbPath,
      url: drawingBaseUrl + glbPath,
      kind: 'glb',
      stpPath: cached.stpPath,
      stpUrl: drawingBaseUrl + cached.stpPath,
    };
  } else if (cached?.path) {
    const kind =
      cached.kind === 'stp' || cached.kind === 'step'
        ? 'step'
        : cached.kind === 'glb' || cached.kind === 'gltf'
          ? 'glb'
          : modelKind(cached.path);
    if (kind === 'step') {
      const glbPath = deriveGlbPathFromStp(cached.path);
      resolved = {
        path: glbPath,
        url: drawingBaseUrl + glbPath,
        kind: 'glb',
        stpPath: cached.path,
        stpUrl: drawingBaseUrl + cached.path,
      };
    } else {
      resolved = { path: cached.path, url: drawingBaseUrl + cached.path, kind };
    }
  } else {
    resolved = await resolveAssyDisplayFromStp(drawingBaseUrl, assy.detailModel);
  }

  if (!resolved) return null;

  if (resolved.kind === 'glb') {
    const probed = await pickHmiSafeGlbResolved(resolved, assy);
    if (probed.hmiUnsafe) {
      console.warn('[3D] GLB — 텍스처 스킵 로드', probed.path, probed.fallbackReason);
      resolved = {
        ...probed,
        stripHeavyTextures: true,
        stpPath: resolved.stpPath,
        stpUrl: resolved.stpUrl,
      };
    } else {
      resolved = { ...probed, stpPath: resolved.stpPath, stpUrl: resolved.stpUrl };
    }
  }
  return resolved;
}

async function resolveOverviewModel() {
  if (!catalog?.overviewModel) return null;

  if (resolvedModels.overview) {
    return buildResolvedFromPath(drawingBaseUrl, resolvedModels.overview);
  }

  const resolved = await resolveModelPath(drawingBaseUrl, catalog.overviewModel);
  if (resolved) return resolved;

  return buildResolvedFromPath(drawingBaseUrl, catalog.overviewModel);
}

function showProceduralOverview() {
  if (overviewRoot) {
    clearGroup(overviewRoot);
    scene.remove(overviewRoot);
  }
  overviewRoot = buildProceduralOverview();
  scene.add(overviewRoot);
  fitCameraSimple(overviewRoot, 'fit');
}

function tagAssyMeshesFromGlb(root) {
  const meshes = [];
  root.traverse((obj) => {
    if (!obj.isMesh) return;
    const assy = catalog.assemblies?.find(
      (a) => a.meshName && (obj.name === a.meshName || obj.name.includes(a.meshName))
    );
    if (assy) {
      obj.userData.assyId = assy.id;
      meshes.push(obj);
    }
  });
  return meshes;
}

async function loadGlbScene(url, onProgress, { maxBytes = MAX_GLB_BYTES, stripTextures = false } = {}) {
  const byteLength = await fetchModelByteLength(url);
  if (byteLength) {
    assertGlbSizeAllowed(byteLength, maxBytes);
    onProgress?.(`GLB ${formatSizeMb(byteLength)}MB 로드 중...`, 5);
  } else {
    onProgress?.(`GLB 로드 중... (한도 ${formatSizeMb(maxBytes)}MB)`, 5);
  }
  const timeoutMs = glbLoadTimeoutMs(byteLength || maxBytes);
  const gltf = await loadGltfWithProgress(url, onProgress, timeoutMs, { stripTextures });
  const root = new THREE.Group();
  root.add(gltf.scene);
  if (byteLength) root.userData.glbFileBytes = byteLength;
  if (stripTextures) root.userData.glbTexturesStripped = true;
  const gen = String(gltf?.asset?.generator || '').toLowerCase();
  if (gen.includes('imagetostl')) {
    root.userData.imageToStlGlb = true;
    console.info('[3D] ImageToStl GLB — SE Z-up 회전 생략 (STP cadZUpMmToYUp 만 적용)');
  }
  return root;
}

async function loadModelScene(resolved, { forOverview = false, onProgress } = {}) {
  if (resolved.kind !== 'glb') {
    throw new Error('런타임 형체는 GLB만 지원 — STP 직접 로드 금지');
  }

  const root = await loadGlbScene(
    resolved.url,
    (msgOrPct, pctHint) => {
      if (typeof msgOrPct === 'string') {
        onProgress?.(msgOrPct, pctHint ?? 40);
        return;
      }
      const pct = msgOrPct;
      if (pct < 0) {
        onProgress?.('GLB 읽는 중... (대용량 파일)', 40);
        return;
      }
      onProgress?.('GLB 다운로드 중...', 15 + pct * 0.75);
    },
    {
      maxBytes: forOverview ? MAX_OVERVIEW_GLB_BYTES : MAX_GLB_BYTES,
      stripTextures: !!resolved.stripHeavyTextures,
    }
  );
  if (isLikelyExternalYUpGlb(root)) {
    root.userData.externalYUpGlb = true;
    console.info('[3D] external Y-up GLB — SE Z-up 회전 생략', resolved.path);
  }
  if (isImageToStlZUpGlb(root)) {
    root.userData.imageToStlGlb = true;
    console.info('[3D] ImageToStl Z-up GLB — 회전 생략, STP cadZUpMmToYUp', resolved.path);
  }
  const meshes = forOverview ? tagAssyMeshesFromGlb(root) : [];
  return { root, assyMeshes: meshes };
}

async function tryLoadOverviewModel() {
  const resolved = await resolveOverviewModel();
  if (!resolved) return false;

  const label = formatModelLabel(resolved.path);
  setLoadStatus(`${label} 로드 중: ` + resolved.path, 15);

  try {
    const { root, assyMeshes: loadedAssyMeshes } = await loadModelScene(resolved, {
      forOverview: true,
      onProgress: (msg, pct) => setLoadStatus(msg, pct),
    });

    assyMeshes = loadedAssyMeshes;

    if (overviewRoot) {
      clearGroup(overviewRoot);
      scene.remove(overviewRoot);
    }

    scene.add(root);
    overviewRoot = root;
    prepareModelForView(overviewRoot, { skipOutlierSuppress: true });
    const fit = fitCameraSimple(overviewRoot, 'fit');
    if (!fit || fit.meshOnScreen === 0) {
      throw new Error('overview 카메라 맞춤 실패 (표시 mesh 없음)');
    }

    setLoadStatus(`${label} 표시 완료`, 100);
    hud.textContent = '3D MAP — ' + resolved.path;
    return true;
  } catch (e) {
    console.warn('overview model failed', e);
    postToHost({ type: 'loadStatus', message: label + ' 실패: ' + e.message, percent: 100, error: true });
    setLoadStatus(`${label} 실패 — ASSY 박스 표시 (` + e.message + ')', 100);
    showProceduralOverview();
    return false;
  }
}

function disposeIoLampMarker(mesh) {
  mesh.parent?.remove(mesh);
  mesh.geometry?.dispose();
  const mats = Array.isArray(mesh.material) ? mesh.material : [mesh.material];
  for (const m of mats) m?.dispose();
}

function clearIoLampBindings(pivot = null) {
  ioLampMeshes.clear();
  const p = pivot || modelPivot;
  if (!p) return;
  const markers = [];
  p.traverse((node) => {
    if (node.isMesh && node.userData.isIoLampMarker) markers.push(node);
    else if (node.isMesh) {
      delete node.userData.isIoLamp;
      delete node.userData.signalKey;
      delete node.userData.ioLampMatCloned;
    }
  });
  for (const m of markers) disposeIoLampMarker(m);
  const lampGroup = p.getObjectByName('IoLampGroup');
  if (lampGroup && lampGroup.children.length === 0) lampGroup.parent?.remove(lampGroup);
}

function getDetailModelRoot() {
  if (!modelPivot) return null;
  for (const ch of modelPivot.children) {
    if (ch.name === 'IoLampGroup') continue;
    if (ch.name === 'CadOrient' && ch.children.length) return ch.children[0];
    return ch;
  }
  return modelPivot.children[0] || null;
}

/** prepareModelForView 가 적용된 pivot (cadLayoutCenter / scale 보관) */
function getModelViewPivot() {
  return modelPivot;
}

/** GLB/STP 화면 정렬용 userData — scheduleFitToModel은 modelPivot에 저장 */
function getCadViewUserData() {
  const pivot = getModelViewPivot();
  if (pivot?.userData?.cadLayoutCenter != null) return pivot.userData;
  return getDetailModelRoot()?.userData || {};
}

function usesCadZUpFix() {
  const ud = getCadViewUserData();
  if (ud.cadZUpFix != null) return !!ud.cadZUpFix;
  return true;
}

/** io_manifest 좌표(mm) → 화면 Y-up — manifest coordinateSystem 우선 (GLB 로드 시 cadZUpFix=false 여도 동일) */
function stpMmToDisplaySpace(x, y, z) {
  if (manifestCoordsAreZUpMm()) return cadZUpMmToYUp(x, y, z);
  if (usesCadZUpFix()) return cadZUpMmToYUp(x, y, z);
  return new THREE.Vector3(x, y, z);
}

/** modelPivot 로컬 좌표 (scale 적용 전 — pivot.scale 이 형체와 마커에 공통 적용) */
function stpCadPointToModelLocal(x, y, z) {
  const p = stpMmToDisplaySpace(x, y, z);
  if (isAssemblyOriginZeroZUp()) {
    return p;
  }
  const gc = getCadViewUserData().cadLayoutCenter;
  if (gc) {
    p.sub(gc.isVector3 ? gc.clone() : new THREE.Vector3(gc.x, gc.y, gc.z));
  } else if (ioManifestMeta?.assemblyOrigin?.length === 3) {
    const o = ioManifestMeta.assemblyOrigin;
    p.sub(stpMmToDisplaySpace(o[0], o[1], o[2]));
  }
  return p;
}

/** 로드된 센서 part mesh bbox → 반경 (prepare 후 model local mm) */
function getSensorMeshRadiusFromScene(key) {
  const modelRoot = getDetailModelRoot();
  if (!modelRoot || !key) return 0;
  const pt = ioManifestPoints[key];
  const srcName = pt?.sourceMesh || pt?.occurrenceName || key;
  const kn = normalizeIoName(srcName);
  let found = null;
  modelRoot.traverse((node) => {
    if (!node.isMesh || node.userData.isIoLampMarker) return;
    if (normalizeIoName(node.name) === kn) found = node;
  });
  if (!found?.geometry) return 0;
  const geo = found.geometry;
  geo.computeBoundingBox();
  if (!geo.boundingBox || geo.boundingBox.isEmpty()) return 0;
  const s = geo.boundingBox.getSize(new THREE.Vector3());
  return Math.max(s.x, s.y, s.z, 0) / 2;
}

/** OCCT mesh 없을 때 manifest sourceMesh 기준 기본 반경(mm) */
const DEFAULT_SENSOR_RADIUS_MM = {
  GDFL_SENSOR: 20,
  GDFL_SENSOR_mir: 20,
  '8BIT_SENSOR': 40,
  'SENSOR BOX': 30,
};

/** 센서 구 반경 — manifest radiusMm 우선, 없으면 화면 mesh bbox */
function getIoLampMarkerRadiusForKey(key) {
  const pt = key ? ioManifestPoints[key] : null;
  if (pt?.radiusMm > 0) return pt.radiusMm;
  if (pt?.sourceMesh && DEFAULT_SENSOR_RADIUS_MM[pt.sourceMesh] > 0) {
    return DEFAULT_SENSOR_RADIUS_MM[pt.sourceMesh];
  }
  const fromMesh = getSensorMeshRadiusFromScene(key);
  if (fromMesh > 0) return fromMesh;
  return getIoLampMarkerRadiusFallback();
}

function getIoLampMarkerRadiusFallback() {
  const modelRoot = getDetailModelRoot();
  const box = modelRoot ? getMeshesBox(modelRoot) : null;
  if (box && !box.isEmpty()) {
    const s = box.getSize(new THREE.Vector3());
    const maxDim = Math.max(s.x, s.y, s.z, 1);
    return maxDim * IO_LAMP_MARKER_RADIUS_RATIO;
  }
  return MODEL_SCENE_SIZE * 0.08;
}

/** @deprecated getIoLampMarkerRadiusForKey(key) 사용 */
function getIoLampMarkerRadiusLocal() {
  return getIoLampMarkerRadiusFallback();
}

function getIoSignalKeys(assy) {
  if (assy?.signalKeys?.length) return assy.signalKeys;
  const fromManifest = Object.keys(ioManifestPoints || {}).filter(
    (k) => ioManifestPoints[k]?.x != null
  );
  if (fromManifest.length) return fromManifest;
  return Object.keys(layoutPoints || {}).filter((k) => layoutPoints[k]?.x != null);
}

/** GLB / 부모 노드 이름 정규화 — I/O 키 비교용 */
function normalizeIoName(name) {
  return String(name || '')
    .trim()
    .toLowerCase()
    .replace(/^io[_-]?/, '')
    .replace(/[\s-]+/g, '_');
}

/** STP manifest 좌표 → 형체 GLB와 동일 로컬 좌표계 (prepare 후 model root 기준 mm) */
function getIoMarkerParent() {
  return getDetailModelRoot() || getModelViewPivot() || modelPivot;
}

/** 센서 구는 형체 root 와 동일 좌표계 — pivot.scale 이 형체·마커에 공통 적용 */
function ensureIoLampGroup(pivot = null) {
  const p = pivot || getIoMarkerParent();
  if (!p) return null;
  let g = p.getObjectByName('IoLampGroup');
  if (!g) {
    g = new THREE.Group();
    g.name = 'IoLampGroup';
    p.add(g);
  }
  return g;
}

/** io_manifest STP 좌표(mm, Z-up) — 뷰어는 이 값만 사용 (glbSnap·GLB mesh 좌표 금지) */
function getManifestSensorCenterZUpMm(pt) {
  if (!pt || pt.x == null || pt.y == null || pt.z == null) return null;
  return { x: pt.x, y: pt.y, z: pt.z };
}

/** manifest STP mm → modelPivot 로컬 (형체 GLB와 동일 좌표계) */
function getIoLampPositionInPivotLocal(key) {
  const pt = getStpSensorPoint(key);
  if (!pt) return null;
  const c = getManifestSensorCenterZUpMm(pt);
  return stpCadPointToModelLocal(c.x, c.y, c.z);
}

function formatIoLampStatusMessage(ioKeys) {
  const total = ioKeys?.length || 0;
  const stpN = [...ioLampMeshes.values()].filter((m) => m.userData?.isStpOverlay).length;
  const unmatched = Math.max(0, total - stpN);
  if (stpN === 0) {
    return `형체 GLB · STP 좌표 매칭 없음 (${total}개 I/O) · ${VIEWER_BUILD}`;
  }
  if (unmatched > 0) {
    return `형체 GLB · STP 좌표 ${stpN}/${total} (매칭 없음 ${unmatched}개) · ${VIEWER_BUILD}`;
  }
  return `형체 GLB · STP 좌표 ${stpN}/${total} · ${VIEWER_BUILD}`;
}

/** Solid Edge Z-up mm → Three.js Y-up */
function cadZUpMmToYUp(x, y, z) {
  return new THREE.Vector3(x, z, -y);
}

function computeCadReferenceFromPoints(points) {
  const vals = Object.values(points || {}).filter((p) => p?.x != null && p?.y != null && p?.z != null);
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
  return { center: [sx / n, sy / n, sz / n], zUp: true };
}

function getCadReferenceCenter() {
  if (ioCadReference?.center) return ioCadReference.center;
  return null;
}

/** @deprecated stpCadPointToModelLocal 사용 */
function cadLayoutPointToPivot(x, y, z, modelRoot) {
  return stpCadPointToModelLocal(x, y, z);
}

function cadLayoutPointToLocal(x, y, z, modelRoot) {
  return stpCadPointToModelLocal(x, y, z);
}

function createIoLampLayoutMarker(key, position) {
  const r = getIoLampMarkerRadiusForKey(key);
  const mesh = new THREE.Mesh(
    new THREE.SphereGeometry(r, 14, 14),
    new THREE.MeshStandardMaterial({
      color: IO_LAMP_COLOR_OFF,
      transparent: true,
      opacity: 0.85,
      emissive: 0x000000,
      emissiveIntensity: 0,
      depthTest: false,
    })
  );
  mesh.renderOrder = 999;
  mesh.name = `IoLamp_${key}`;
  mesh.position.copy(position);
  mesh.userData.isIoLampMarker = true;
  mesh.userData.isIoLamp = true;
  mesh.userData.signalKey = key;
  return mesh;
}

/** STP occurrence 만 표시 — 전 ASSY 동일, fallback 금지 */
function isManifestPointReliable(pt) {
  return isReliableManifestPoint(pt);
}

function sanitizeIoManifestPoints(assyId, points, profile = null) {
  if (!points) return {};
  const { points: out, dropped } = filterReliableManifestPoints(points);
  if (dropped) {
    console.warn(
      `[io_manifest] ${assyId || 'ASSY'} fallback 좌표 ${dropped}개 제외 — STP 배치명=PLC signalKey (stp_occurrence) 필요`
    );
  }
  const stpFile = profile?.stpFile || ioManifestMeta?.stpFile || '';
  const canonicalStp =
    currentAssyIoProfile?.stpCanonicalBase ||
    (ioManifestMeta?.stpFile || '').replace(/\.(stp|step)$/i, '') ||
    assyId;
  if (stpFile && isExcludedStpFileName(stpFile, profile || currentAssyIoProfile)) {
    console.warn(`[io_manifest] ${assyId} 제외 STP 사용 중: ${stpFile} — ${canonicalStp}.stp 사용`);
    postToHost({
      type: 'loadStatus',
      message: `제외 STP(${stpFile}) — ${canonicalStp}.stp 로 교체·manifest 재생성`,
      error: true,
    });
  }
  return out;
}

/** STP io_manifest 좌표만 (목표: GLB 형체 + STP 센서 데이터) */
function getStpSensorPoint(key) {
  const m = ioManifestPoints[key];
  if (isManifestPointReliable(m)) return m;
  if (IO_LAYOUT_FALLBACK) {
    const l = layoutPoints[key];
    if (l && l.x != null && l.y != null && l.z != null) return l;
  }
  return null;
}

function countStpSensorPoints() {
  return Object.keys(ioManifestPoints).filter((k) => isManifestPointReliable(ioManifestPoints[k])).length;
}

/** @deprecated stpCadPointToModelLocal */
function stpCadPointToPivot(x, y, z, modelRoot) {
  return stpCadPointToModelLocal(x, y, z);
}

function createIoStpOverlayLamp(key, position) {
  const r = getIoLampMarkerRadiusForKey(key);
  const mesh = new THREE.Mesh(
    new THREE.SphereGeometry(r, 14, 14),
    new THREE.MeshStandardMaterial({
      color: IO_LAMP_COLOR_OFF,
      transparent: true,
      opacity: 0.85,
      emissive: 0x000000,
      emissiveIntensity: 0,
      depthTest: false,
    })
  );
  mesh.renderOrder = 999;
  mesh.name = `StpSensor_${key}`;
  mesh.position.copy(position.isVector3 ? position : new THREE.Vector3(position.x, position.y, position.z));
  mesh.userData.isIoLampMarker = true;
  mesh.userData.isIoLamp = true;
  mesh.userData.isStpOverlay = true;
  mesh.userData.signalKey = key;
  mesh.userData.ioLampRadiusMm = r;
  return mesh;
}

/** STP manifest 좌표 → GLB 위 점멸 오버레이 */
function bindIoStpOverlayLamps(keys) {
  const markerParent = getIoMarkerParent();
  const pivot = getModelViewPivot() || modelPivot;
  if (!markerParent || !pivot || !keys?.length) return 0;

  const stpKeys = keys.filter((k) => getStpSensorPoint(k));
  if (!stpKeys.length) {
    const mc = ioManifestMeta?.meshCount ?? 0;
    if (mc === 0) {
      console.warn('[io_stp] STP geometry 없음 — exact geometry STEP 재export 필요');
    } else {
      console.warn('[io_stp] STP part명≠I/O명 — Solid Edge 센서 part 이름 확인');
    }
    return 0;
  }

  const group = ensureIoLampGroup(markerParent);
  let count = 0;
  for (const key of stpKeys) {
    if (ioLampMeshes.get(key)?.userData?.isStpOverlay) continue;
    const pos = getIoLampPositionInPivotLocal(key);
    if (!pos) continue;
    const mesh = createIoStpOverlayLamp(key, pos);
    group.add(mesh);
    registerIoLamp(key, mesh);
    count++;
  }
  console.info('[io_stp] STP좌표', count, '/', keys.length);
  return count;
}

/** STP manifest 점 → 형체 표면 스냅 (marker parent 로컬 mm) */
function closestPointOnFormInPivotLocal(localPoint, markerParent) {
  const formRoot = getDetailModelRoot();
  const pivot = getModelViewPivot() || modelPivot;
  const spaceRoot = markerParent || formRoot || pivot;
  if (!formRoot || !spaceRoot || !localPoint) return null;
  spaceRoot.updateMatrixWorld(true);
  formRoot.updateMatrixWorld(true);
  const worldTarget = spaceRoot.localToWorld(localPoint.clone());
  let bestDist = getSurfaceSnapMaxMm();
  if (bestDist <= 0) return null;
  let bestLocal = null;
  formRoot.traverse((node) => {
    if (!node.isMesh || node.userData.isIoLampMarker || !node.visible) return;
    const pos = node.geometry?.attributes?.position;
    if (!pos) return;
    const stride = Math.max(1, Math.floor(pos.count / 5000));
    for (let i = 0; i < pos.count; i += stride) {
      _snapProbe.fromBufferAttribute(pos, i);
      node.localToWorld(_snapProbe);
      const d = worldTarget.distanceTo(_snapProbe);
      if (d < bestDist) {
        bestDist = d;
        _snapBestWorld.copy(_snapProbe);
        bestLocal = spaceRoot.worldToLocal(_snapBestWorld.clone());
      }
    }
  });
  return bestLocal ? { point: bestLocal, dist: bestDist } : null;
}

async function loadAssyIoProfile(assy) {
  const assyId = assy?.id || currentAssyId3d;
  if (!assyId) {
    currentAssyIoProfile = null;
    ioMarkerDeoverlapGapMm = 6;
    return null;
  }
  const rel = `assemblies/${assyId}/ASSY_IO_PROFILE.json`;
  try {
    const json = await fetchJson(drawingBaseUrl + rel);
    currentAssyIoProfile = mergeAssyIoProfile(assyId, json);
  } catch {
    currentAssyIoProfile = mergeAssyIoProfile(assyId, null);
  }
  ioMarkerDeoverlapGapMm = getDeoverlapGapMm(currentAssyIoProfile);
  console.info('[assy_io_profile]', assyId, currentAssyIoProfile.viewer);
  return currentAssyIoProfile;
}

/** ASSY 전용 — 표면 snap 생략 패턴 (ASSY_IO_PROFILE.json) */
function skipIoSurfaceSnap(key) {
  return shouldSkipIoSurfaceSnap(key, currentAssyIoProfile);
}

function snapStpOverlayLampsToFormSurface(keys) {
  const snapMax = getSurfaceSnapMaxMm();
  if (snapMax <= 0) return 0;
  const markerParent = getIoMarkerParent();
  const pivot = getModelViewPivot() || modelPivot;
  if (!markerParent || !pivot || !keys?.length) return 0;
  let n = 0;
  for (const key of keys) {
    if (skipIoSurfaceSnap(key)) continue;
    const lamp = ioLampMeshes.get(key);
    if (!lamp?.userData?.isStpOverlay) continue;
    const hit = closestPointOnFormInPivotLocal(lamp.position, markerParent);
    if (hit?.point && hit.dist <= snapMax) {
      lamp.position.copy(hit.point);
      n++;
    }
  }
  if (n) console.info('[io_stp] surface snap', n, '/', keys.length);
  return n;
}

/** snap 후 구체 겹침 분리 — STP 원래 상대 위치 방향으로 밀어냄 (SCP 타워램프·Lower 스테이션 등) */
function separateOverlappingIoMarkers(keys) {
  const items = [];
  for (const key of keys || []) {
    const lamp = ioLampMeshes.get(key);
    if (!lamp?.userData?.isStpOverlay) continue;
    const r = lamp.userData.ioLampRadiusMm || getIoLampMarkerRadiusForKey(key);
    const orig = getIoLampPositionInPivotLocal(key);
    items.push({
      key,
      lamp,
      r,
      orig: orig?.clone() || lamp.position.clone(),
    });
  }
  if (items.length < 2) return 0;

  let moves = 0;
  const _push = new THREE.Vector3();
  for (let iter = 0; iter < 32; iter++) {
    let moved = false;
    for (let i = 0; i < items.length; i++) {
      for (let j = i + 1; j < items.length; j++) {
        const a = items[i];
        const b = items[j];
        const minD = a.r + b.r + ioMarkerDeoverlapGapMm;
        _push.subVectors(b.lamp.position, a.lamp.position);
        let dist = _push.length();
        if (dist >= minD) continue;

        if (dist < 1e-4) {
          _push.subVectors(b.orig, a.orig);
          if (_push.lengthSq() < 1e-4) _push.set(0, minD, 0);
        }
        _push.normalize().multiplyScalar((minD - Math.max(dist, 0)) * 0.5);
        a.lamp.position.sub(_push);
        b.lamp.position.add(_push);
        moved = true;
        moves++;
      }
    }
    if (!moved) break;
  }
  if (moves) console.info('[io_stp] deoverlap', moves, 'adjustments');
  return moves;
}

function getIoManifestRelPath(assy) {
  if (assy?.ioManifest) return assy.ioManifest;
  if (assy?.layout) return assy.layout.replace(/io_layout\.json$/i, 'io_manifest.json');
  return null;
}

function ensureIoLampMaterial(mesh) {
  if (!mesh?.isMesh) return null;
  if (!mesh.userData.ioLampMatCloned) {
    if (Array.isArray(mesh.material)) {
      mesh.material = mesh.material.map((m) => m.clone());
    } else if (mesh.material) {
      mesh.material = mesh.material.clone();
    } else {
      mesh.material = new THREE.MeshStandardMaterial({ color: IO_LAMP_COLOR_OFF });
    }
    mesh.userData.ioLampMatCloned = true;
  }
  const mat = Array.isArray(mesh.material) ? mesh.material[0] : mesh.material;
  return mat;
}

function getTowerLampPaletteKey(signalKey) {
  const m = String(signalKey || '').match(/^Tower_Lamp_(RED|Yellow|Green|White|Blue|Buzzer)$/i);
  if (!m) return null;
  return m[1].toLowerCase();
}

function getIoLampPaletteKey(signalKey) {
  const tl = getTowerLampPaletteKey(signalKey);
  if (tl) return tl;
  return IO_SIGNAL_COLOR_PALETTE[signalKey] || null;
}

function getIoLampColorHex(signalKey, { litTest = false, on = false } = {}) {
  const pk = getIoLampPaletteKey(signalKey);
  if (pk && TOWER_LAMP_PALETTE[pk]) {
    const p = TOWER_LAMP_PALETTE[pk];
    if (litTest === 'bright') return p.bright;
    if (litTest === 'dim') return p.dim;
    return on ? p.on : p.off;
  }
  if (litTest === 'bright') return IO_LAMP_COLOR_TEST_BRIGHT;
  if (litTest === 'dim') return IO_LAMP_COLOR_TEST_DIM;
  return on ? IO_LAMP_COLOR_ON : IO_LAMP_COLOR_OFF;
}

function applyIoMarkerMaterial(mat, on, litTest = false, signalKey = null) {
  const isBasic = !!mat?.isMeshBasicMaterial;
  const colorHex = getIoLampColorHex(signalKey, { litTest: litTest || false, on });
  if (litTest) {
    mat.transparent = true;
    mat.opacity = litTest === 'bright' ? (isBasic ? 1 : 0.92) : isBasic ? 0.35 : 0.28;
    mat.color.setHex(colorHex);
    if (!isBasic && mat.emissive) {
      mat.emissive.setHex(litTest === 'bright' ? colorHex : 0x000000);
      mat.emissiveIntensity = litTest === 'bright' ? 0.85 : 0;
    }
    return;
  }
  mat.transparent = true;
  mat.opacity = on ? (isBasic ? 1 : 0.92) : isBasic ? 0.45 : 0.32;
  mat.color.setHex(colorHex);
  if (!isBasic && mat.emissive) {
    mat.emissive.setHex(on ? colorHex : 0x000000);
    mat.emissiveIntensity = on ? 0.45 : 0;
  }
}

function isIoTestBlinkAssy(assyId) {
  return IO_TEST_BLINK_ENABLED && IO_TEST_BLINK_ASSY_IDS.has(assyId);
}

/** 테스트 — 바인딩된 램프 전체 점멸 */
function updateIoTestBlink(now) {
  if (!isIoTestBlinkAssy(currentAssyId3d)) return;
  const lit = Math.floor(now / IO_TEST_BLINK_MS) % 2 === 0;
  const phase = lit ? 'bright' : 'dim';
  for (const [key, mesh] of ioLampMeshes) {
    const mat = ensureIoLampMaterial(mesh);
    if (mat) applyIoMarkerMaterial(mat, false, phase, key);
  }
}

/** io_layout 좌표 램프 재질 갱신 */
function syncIoLampVisuals() {
  if (isIoTestBlinkAssy(currentAssyId3d)) return;

  for (const [key, mesh] of ioLampMeshes) {
    const mat = ensureIoLampMaterial(mesh);
    if (!mat) continue;
    applyIoMarkerMaterial(mat, !!ioStates[key], false, key);
  }
}

function warnIoLampBinding(keys = null) {
  const allKeys = keys || [];
  const found = allKeys.filter((k) => ioLampMeshes.has(k));
  const missing = allKeys.filter((k) => !ioLampMeshes.has(k));
  const stpBound = found.filter((k) => ioLampMeshes.get(k)?.userData?.isStpOverlay);

  if (missing.length) {
    const msg = `STP 좌표 미매칭 ${missing.length}개 / 연결 ${stpBound.length}개`;
    console.warn('[io_lamp]', msg, { missing: missing.slice(0, 8) });
    postToHost({ type: 'loadStatus', message: msg, error: stpBound.length === 0 });
  } else if (found.length) {
    postToHost({ type: 'loadStatus', message: `STP I/O ${found.length}개 연결`, error: false });
  }

  return { found, missing, allKeys };
}

async function loadAssyIoManifest(assy) {
  ioManifestPoints = {};
  ioManifestMeta = null;
  const rel = getIoManifestRelPath(assy);
  if (!rel) return ioManifestPoints;
  try {
    const json = await fetchJson(drawingBaseUrl + rel);
    ioManifestMeta = json;
    ioManifestPoints = sanitizeIoManifestPoints(assy?.id, json.points || {}, {
      stpFile: json.stpFile,
      excludeStpNamePatterns: currentAssyIoProfile?.excludeStpNamePatterns,
    });
    if (json.cadReference?.center) ioCadReference = json.cadReference;
    const n = countStpSensorPoints();
    const expected = assy?.signalKeys?.length || json.signalCount || 0;
    console.info('[io_manifest/STP]', rel, '센서좌표', n, 'match', json.matchCount ?? '?', 'mesh', json.meshCount ?? '?');
    if (json.stpSource?.file) {
      console.info(
        '[io_manifest/STP] source',
        json.stpSource.file,
        new Date(json.stpSource.mtimeMs || 0).toISOString()
      );
    }
    if (json._regenBlocked?.reason === 'new_stp_match_zero') {
      const msg = `STP 매칭 0 — 기존 좌표 ${n}개 유지 (완전 export 후 재생성)`;
      console.warn('[io_manifest]', msg, json._regenBlocked);
      postToHost({ type: 'loadStatus', message: msg, error: true });
    } else if (!n) {
      const rawCount = Object.keys(json.points || {}).length;
      const matchCount = json.matchCount ?? 0;
      const sensorOcc = json.sensorOccurrenceCount ?? 0;
      let msg;
      if (rawCount > 0 && matchCount > 0) {
        msg = `manifest ${matchCount}개 있으나 뷰어 신뢰 0 — node tools/stp-io-manifest.mjs --assy ${assy.id} --force`;
      } else if (sensorOcc > 0 && rawCount === 0) {
        msg = `STP 센서 ${sensorOcc}개·manifest 좌표 0 — manifest 재생성: --assy ${assy.id} --force`;
      } else {
        msg = `STP I/O 매칭 0/${expected} — 배치명=PLC signalKey (node tools/check-assy-stp-export.mjs --assy ${assy.id})`;
      }
      console.warn('[io_manifest]', msg);
      postToHost({ type: 'loadStatus', message: msg, error: true });
    } else if (expected > 0 && n < expected) {
      const msg = `STP I/O 매칭 ${n}/${expected} (미매칭 ${expected - n}개)`;
      console.warn('[io_manifest]', msg);
      postToHost({ type: 'loadStatus', message: msg, error: true });
    }
    if (!n) {
      if ((json.meshCount ?? 0) === 0) {
        console.warn('[io_manifest] STP geometry 0 — tessellated STEP 불가, exact geometry로 재export');
      } else {
        console.warn('[io_manifest] STP mesh는 있으나 I/O 이름 매칭 0 — 센서 part 이름=signalKey');
      }
    }
  } catch (e) {
    console.warn('io_manifest 없음 — node tools/stp-io-manifest.mjs --assy', assy?.id, e?.message || e);
  }
  return ioManifestPoints;
}

async function loadAssyLayoutPoints(assy) {
  layoutPoints = {};
  if (!assy?.layout) return layoutPoints;
  try {
    const layoutJson = await fetchJson(drawingBaseUrl + assy.layout);
    layoutPoints = layoutJson.points || {};
    const withXyz = Object.keys(layoutPoints).filter((k) => layoutPoints[k]?.x != null).length;
    console.info('[io_layout]', assy.layout, withXyz, 'pts');
    if (layoutJson.cadReference?.center) {
      ioCadReference = layoutJson.cadReference;
    } else if (!ioCadReference) {
      ioCadReference = computeCadReferenceFromPoints(layoutPoints);
    }
  } catch (e) {
    console.warn('io_layout load failed', e);
  }
  return layoutPoints;
}

function applyLayoutPoints(points) {
  layoutPoints = points || {};
  if (IO_LAYOUT_FALLBACK) {
    bindIoStpOverlayLamps(Object.keys(layoutPoints));
  }
  syncIoLampVisuals();
  return warnIoLampBinding(Object.keys(layoutPoints));
}

function rebuildMarkers() {
  syncIoLampVisuals();
}

async function enterAssyDetail(assyId) {
  const assy = catalog?.assemblies?.find((a) => a.id === assyId);
  if (!assy) return;

  mode = 'detail';
  currentAssyId3d = assyId;
  hud.textContent = assy.label || assyId.toUpperCase();
  setLoadStatus(`${assy.label || assyId} 로드 준비...`, 5);

  if (overviewRoot) overviewRoot.visible = false;
  clearGroup(detailRoot);
  if (detailRoot) scene.remove(detailRoot);
  detailRoot = new THREE.Group();
  modelPivot = null;
  scene.add(detailRoot);
  clearIoLampBindings();
  layoutPoints = {};
  ioManifestPoints = {};
  ioManifestMeta = null;
  ioCadReference = null;
  ioSignalLabels = {};
  hoveredIoKey = null;
  setSensorInfoText('');

  if (assy.detailModel) {
    showLoadOverlay();
    resetLoadOverlayStyle();
    try {
      let resolved = await resolveAssyModel(assy);
      if (!resolved) {
        throw new Error('STP 파일 없음: ' + assy.detailModel);
      }

      resolved = await ensureAssyGlbReady(resolved, assy);

      const label = formatModelLabel(resolved.path);
      let loadMsg = `${label} Detail 로드: ${resolved.path}`;
      if (resolved.fallbackFrom) {
        loadMsg += ` (대체 — ${resolved.fallbackReason || 'HMI fallback'})`;
      }
      if (resolved.stripHeavyTextures) {
        loadMsg += ' · 텍스처 스킵';
      }
      setLoadStatus(loadMsg, 10);
      console.info(
        '[3D] 형체',
        assyId,
        'STP',
        resolved.stpPath || '(없음)',
        '→ GLB',
        resolved.path,
        resolved.glbMeta || ''
      );
      if (resolved.fallbackFrom) {
        console.warn('[3D] GLB fallback', resolved.fallbackFrom, '→', resolved.path);
      }

      await loadAssyIoProfile(assy);
      await loadAssyIoManifest(assy);
      if (ioManifestMeta?.stpFile) {
        const rev = (f) => f?.match(/_REV(\d+)/i)?.[1] || '0';
        console.info(
          '[3D] manifest',
          'STP',
          ioManifestMeta.stpFile,
          'GLB',
          ioManifestMeta.glbFile || '(없음)'
        );
        if (
          rev(ioManifestMeta.stpFile) !== rev(ioManifestMeta.glbFile) ||
          (resolved.kind === 'glb' && rev(resolved.path) !== rev(ioManifestMeta.glbFile))
        ) {
          console.warn(
            '[3D] REV 불일치 — manifest 재생성:',
            'node tools/stp-io-manifest.mjs --assy',
            assyId
          );
        }
      }
      if (IO_LAYOUT_FALLBACK) await loadAssyLayoutPoints(assy);
      await loadAssyIoLabels(assy);

      setLoadStatus('GLB 파싱 중...', 20);
      await new Promise((r) => setTimeout(r, 0));

      const { root } = await loadModelScene(resolved, {
        forOverview: false,
        onProgress: (msg, pct) => setLoadStatus(msg, pct),
      });

      modelPivot = new THREE.Group();
      modelPivot.name = 'ModelPivot';
      detailRoot.add(modelPivot);
      modelPivot.add(root);
      if (objectHasUserDataFlag(root, 'imageToStlGlb')) {
        modelPivot.userData.imageToStlGlb = true;
      }
      if (objectHasUserDataFlag(root, 'externalYUpGlb')) {
        modelPivot.userData.externalYUpGlb = true;
      }

      setLoadStatus('형체 배치·맞춤...', 50);
      await new Promise((r) => setTimeout(r, 0));

      const fitOk = await scheduleFitToModel(modelPivot, {
        modelPath: resolved.path,
        modelKind: resolved.kind,
      });
      if (!fitOk) {
        fitCameraSimple(modelPivot, 'fit');
        hud.textContent = (assy.label || assyId) + ' — 맞춤 보조 적용';
      }

      clearIoLampBindings(modelPivot);

      const ioKeys = getIoSignalKeys(assy);
      bindIoStpOverlayLamps(ioKeys);
      snapStpOverlayLampsToFormSurface(ioKeys);
      separateOverlappingIoMarkers(ioKeys);
      warnIoLampBinding(ioKeys);
      if (isIoTestBlinkAssy(assyId)) {
        updateIoTestBlink(performance.now());
      } else {
        syncIoLampVisuals();
      }

      await new Promise((r) => requestAnimationFrame(r));
      const viewScr = measureOnScreen(modelPivot);
      if (viewScr.ext < FIT_SCREEN_TARGET * 0.55 || viewScr.meshOnScreen === 0) {
        fitCameraSimple(modelPivot, DEFAULT_DETAIL_VIEW);
      }

      let lampMsg = formatIoLampStatusMessage(ioKeys);
      if (resolved.fallbackFrom) {
        lampMsg += ' · GLB=HMI fallback';
      }
      const stpN = [...ioLampMeshes.values()].filter((m) => m.userData?.isStpOverlay).length;
      hud.textContent = `${assy.label || assyId} — ${lampMsg}`;
      postToHost({
        type: 'loadStatus',
        message: lampMsg,
        error: stpN === 0 && ioKeys.length > 0,
      });

      setLoadStatus(`${label} Detail 표시 완료`, 100);
      resetLoadOverlayStyle();
      loadOverlay.style.display = 'none';
      return;
    } catch (e) {
      console.warn('detail model failed', e);
      showAssyLoadError(assy, e.message || String(e));
      return;
    }
  }

  const layout = PROC_LAYOUT[assyId] || { size: [1, 1, 1], pos: [0, 0.5, 0] };
  const meshName = assy.meshName || assyId;
  const fallbackBox = makeBox(
    meshName,
    layout.size,
    [0, layout.size[1] / 2, 0],
    0x666666,
    false
  );
  modelPivot = new THREE.Group();
  modelPivot.name = 'ModelPivot';
  detailRoot.add(modelPivot);
  modelPivot.add(fallbackBox);
  await scheduleFitToModel(modelPivot);

  if (assy.layout) {
    try {
      const layoutJson = await fetchJson(drawingBaseUrl + assy.layout);
      applyLayoutPoints(layoutJson.points || {});
    } catch (e) { /* optional */ }
  } else {
    rebuildMarkers();
  }

  if (!assy.detailModel) {
    setLoadStatus('Detail 모델 미설정 — I/O 마커만', 100);
  }
}

async function exitAssyDetail() {
  mode = 'overview';
  currentAssyId3d = null;
  layoutPoints = {};
  clearIoLampBindings();

  if (detailRoot) {
    clearGroup(detailRoot);
    scene.remove(detailRoot);
    detailRoot = null;
  }
  modelPivot = null;
  hoveredIoKey = null;
  setSensorInfoText('');
  if (overviewRoot) {
    overviewRoot.visible = true;
    fitCameraToObject(overviewRoot);
  }
  hud.textContent = '3D MAP — Overview';
}

function onPointerClick(ev) {
  if (mode !== 'overview' || assyMeshes.length === 0) return;
  if (ev.button !== 0) return;
  const rect = renderer.domElement.getBoundingClientRect();
  pointer.x = ((ev.clientX - rect.left) / rect.width) * 2 - 1;
  pointer.y = -((ev.clientY - rect.top) / rect.height) * 2 + 1;
  raycaster.setFromCamera(pointer, camera);
  const hits = raycaster.intersectObjects(assyMeshes, true);
  if (!hits.length) return;

  let obj = hits[0].object;
  while (obj && !obj.userData.assyId) obj = obj.parent;
  const assyId = obj?.userData.assyId;
  if (assyId) {
    postToHost({ type: 'assyClick', assyId });
    enterAssyDetail(assyId);
  }
}

let pointerDownPos = null;
renderer.domElement.addEventListener('pointerdown', (ev) => {
  if (ev.button === 0) {
    pointerDownPos = { x: ev.clientX, y: ev.clientY };
  }
});
renderer.domElement.addEventListener('pointerup', (ev) => {
  if (ev.button !== 0 || !pointerDownPos) return;
  if (suppressPointerClick) {
    suppressPointerClick = false;
    pointerDownPos = null;
    return;
  }
  const dx = ev.clientX - pointerDownPos.x;
  const dy = ev.clientY - pointerDownPos.y;
  pointerDownPos = null;
  if (dx * dx + dy * dy > 36) return;
  onPointerClick(ev);
});

renderer.domElement.addEventListener('pointermove', (ev) => {
  scheduleIoSensorHoverPick(ev.clientX, ev.clientY);
});
renderer.domElement.addEventListener('pointerleave', () => {
  hoveredIoKey = null;
  setSensorInfoText('');
});

window.addEventListener('keydown', (ev) => {
  if (ev.key === 'f' || ev.key === 'F') {
    fitActiveView();
  }
});

window.enterAssy = (assyId) => enterAssyDetail(assyId);
window.exitAssy = () => exitAssyDetail();
function applyResolvedModels(data) {
  if (!data) return;
  if (data.assemblies) Object.assign(resolvedModels.assemblies, data.assemblies);
  if (data.overview) resolvedModels.overview = data.overview;
}
window.setResolvedModels = (data) => applyResolvedModels(data);
if (window.__resolvedModelsQueue) {
  applyResolvedModels(window.__resolvedModelsQueue);
  window.__resolvedModelsQueue = null;
}
window.updateIo = (states) => {
  if (!states) return;
  for (const [k, v] of Object.entries(states)) ioStates[k] = !!v;
  rebuildMarkers();
};
window.highlightMarker = (signalKey) => {
  if (highlightedMarker?.material?.emissive) {
    highlightedMarker.material.emissive.setHex(0x000000);
  }
  highlightedMarker = ioLampMeshes.get(signalKey) || null;
  const mat = highlightedMarker ? ensureIoLampMaterial(highlightedMarker) : null;
  if (mat?.emissive) mat.emissive.setHex(0x444400);
};

async function boot() {
  if (hint) hint.textContent = `${NAV_HINT_DEFAULT} · F:맞춤 · ${VIEWER_BUILD}`;
  setupViewToolbar();
  postToHost({ type: 'loadStatus', message: 'Three.js 시작', percent: 2 });
  setLoadStatus('설정 파일 로드 중...', 5);
  startAnimate();

  try {
    catalog = await fetchJson(drawingBaseUrl + 'assemblies.json');
  } catch (e) {
    setLoadStatus('assemblies.json 없음 — 기본 ASSY 박스', 8);
    postToHost({ type: 'loadStatus', message: 'assemblies.json 없음 — 기본 박스', error: false });
    catalog = {
      overviewModel: null,
      assemblies: [
        { id: 'SCP', label: 'Standing_Control_Panel', meshName: 'SCP' },
        { id: 'Lower_Frame_assy', label: 'Lower_Frame_assy', meshName: 'LOWER_FRAME_ASSY' },
        { id: 'Carriage_Assy', label: 'Carriage_Assy', meshName: 'Carriage_Assy' },
      ],
    };
  }

  setLoadStatus('ASSY 박스 표시 중...', 10);
  showProceduralOverview();

  await tryLoadOverviewModel();
  hideLoadOverlay();
}

function startAnimate() {
  if (animStarted) return;
  animStarted = true;
  animate();
}

function animate() {
  requestAnimationFrame(animate);
  controls.update();
  updateIoTestBlink(performance.now());
  renderer.render(scene, camera);
  frameCount++;
  const now = performance.now();
  if (now - lastFpsTime > 1000) {
    perfEl.textContent = frameCount + ' fps · ' + VIEWER_BUILD;
    frameCount = 0;
    lastFpsTime = now;
  }
}

window.addEventListener('error', (e) => {
  setLoadStatus('오류: ' + (e.message || 'unknown'), 0);
  postToHost({ type: 'loadStatus', message: e.message || 'unknown', error: true });
});

boot().catch((e) => {
  setLoadStatus('시작 실패: ' + e.message, 0);
  postToHost({ type: 'loadStatus', message: e.message, error: true });
});
