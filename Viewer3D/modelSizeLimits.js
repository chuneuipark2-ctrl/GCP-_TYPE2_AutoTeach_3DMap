/** GLB/STP 용량 한도 — HMI WebView2 기준 */
export const MAX_GLB_BYTES = 70 * 1024 * 1024;
/** overview(srm_overview.glb) — ASSY 합본이라 상한 별도 */
export const MAX_OVERVIEW_GLB_BYTES = 100 * 1024 * 1024;
export const MAX_STEP_BYTES = 150 * 1024 * 1024;

/** 파일 크기(MB) → GLB 로드 타임아웃(ms). 70MB ≈ 8분 */
export function glbLoadTimeoutMs(byteLength) {
  const mb = byteLength > 0 ? byteLength / (1024 * 1024) : 0;
  return Math.min(600000, Math.max(120000, 120000 + mb * 5000));
}

export function formatSizeMb(byteLength) {
  if (!Number.isFinite(byteLength) || byteLength <= 0) return '?';
  return (byteLength / (1024 * 1024)).toFixed(1);
}

/** Range probe → Content-Range / Content-Length */
export async function fetchModelByteLength(url, timeoutMs = 12000) {
  const ctrl = new AbortController();
  const timer = setTimeout(() => ctrl.abort(), timeoutMs);
  try {
    const res = await fetch(url, {
      method: 'GET',
      headers: { Range: 'bytes=0-0' },
      signal: ctrl.signal,
    });
    if (!res.ok && res.status !== 206) return null;
    const cr = res.headers.get('Content-Range');
    if (cr) {
      const m = cr.match(/\/(\d+)\s*$/);
      if (m) return Number(m[1]);
    }
    const cl = res.headers.get('Content-Length');
    if (cl) return Number(cl);
    return null;
  } catch (_) {
    return null;
  } finally {
    clearTimeout(timer);
  }
}

export function assertGlbSizeAllowed(byteLength, maxBytes = MAX_GLB_BYTES) {
  if (!byteLength || byteLength <= 0) return;
  const limit = maxBytes > 0 ? maxBytes : MAX_GLB_BYTES;
  if (byteLength > limit) {
    throw new Error(
      `GLB 용량 초과 (${formatSizeMb(byteLength)}MB / 한도 ${formatSizeMb(limit)}MB)`
    );
  }
}

export function assertStepSizeAllowed(byteLength) {
  if (!byteLength || byteLength <= 0) return;
  if (byteLength > MAX_STEP_BYTES) {
    throw new Error(
      `STEP 용량 초과 (${formatSizeMb(byteLength)}MB / 한도 ${formatSizeMb(MAX_STEP_BYTES)}MB). GLB로 변환하세요.`
    );
  }
}
