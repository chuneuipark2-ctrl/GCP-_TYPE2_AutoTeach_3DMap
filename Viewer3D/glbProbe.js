/** GLB 헤더/JSON만 읽어 HMI(WebView2) 적합 여부 판단 */
import { fetchModelByteLength, formatSizeMb } from './modelSizeLimits.js';

/** Inventor export — texture 수천 개면 WebView OOM/무한로딩 */
export const MAX_GLB_HMI_IMAGES = 50;
/** CAD ASSY — part당 mesh 1개라 400 초과 흔함. 바이트·텍스처 위주 판단 */
export const MAX_GLB_HMI_MESHES = 3000;

export function parseGlbMetaFromBuffer(buf, byteLength = 0) {
  if (!buf || buf.byteLength < 20) return null;
  const view = new DataView(buf);
  if (view.getUint32(0, true) !== 0x46546c67) return null;
  const totalLen = byteLength > 0 ? byteLength : view.getUint32(8, true);
  const jsonLen = view.getUint32(12, true);
  if (view.getUint32(16, true) !== 0x4e4f534a) return null;
  if (20 + jsonLen > buf.byteLength) return null;
  const jsonBytes = new Uint8Array(buf, 20, jsonLen);
  let gltf;
  try {
    gltf = JSON.parse(new TextDecoder().decode(jsonBytes));
  } catch (_) {
    return null;
  }
  return {
    byteLength: totalLen,
    meshCount: gltf.meshes?.length ?? 0,
    imageCount: gltf.images?.length ?? 0,
    materialCount: gltf.materials?.length ?? 0,
    nodeCount: gltf.nodes?.length ?? 0,
    textureCount: gltf.textures?.length ?? 0,
  };
}

export async function probeGlbFromUrl(url, timeoutMs = 30000) {
  const byteLength = await fetchModelByteLength(url, timeoutMs);
  const fetchLen = Math.min(Math.max(byteLength || 0, 65536), 16 * 1024 * 1024);
  const ctrl = new AbortController();
  const timer = setTimeout(() => ctrl.abort(), timeoutMs);
  try {
    const res = await fetch(url, {
      headers: { Range: `bytes=0-${fetchLen - 1}` },
      signal: ctrl.signal,
    });
    if (!res.ok && res.status !== 206) return null;
    const buf = await res.arrayBuffer();
    return parseGlbMetaFromBuffer(buf, byteLength || 0);
  } catch (_) {
    return null;
  } finally {
    clearTimeout(timer);
  }
}

export function isGlbTooHeavyForHmi(meta) {
  if (!meta) return false;
  if (meta.imageCount > MAX_GLB_HMI_IMAGES) return true;
  if (meta.meshCount > MAX_GLB_HMI_MESHES) return true;
  return false;
}

export function formatGlbHeavyReason(meta) {
  if (!meta) return 'GLB 메타 읽기 실패';
  const parts = [];
  if (meta.imageCount > MAX_GLB_HMI_IMAGES) {
    parts.push(`텍스처 ${meta.imageCount}개 (한도 ${MAX_GLB_HMI_IMAGES})`);
  }
  if (meta.meshCount > MAX_GLB_HMI_MESHES) {
    parts.push(`mesh ${meta.meshCount}개 (한도 ${MAX_GLB_HMI_MESHES})`);
  }
  return parts.join(', ') || 'HMI 한도 초과';
}

export function formatGlbHeavyHint() {
  return 'Inventor/ CAD GLB — "형상만" 또는 "텍스처 없음"으로 재export 하세요.';
}
