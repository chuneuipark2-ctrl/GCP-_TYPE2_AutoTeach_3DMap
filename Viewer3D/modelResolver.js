const GLB_EXTS = ['.glb', '.gltf'];
const STEP_EXTS = ['.stp', '.step'];
const MAX_REV = 99;
const FETCH_TIMEOUT_MS = 30000;
const PROBE_TIMEOUT_MS = 12000;

export function getExtension(path) {
  const m = path?.match(/\.([^.\\/]+)$/i);
  return m ? '.' + m[1].toLowerCase() : '';
}

export function isGlbPath(path) {
  return GLB_EXTS.includes(getExtension(path));
}

export function isStepPath(path) {
  return STEP_EXTS.includes(getExtension(path));
}

export function modelKind(path) {
  if (isStepPath(path)) return 'step';
  if (isGlbPath(path)) return 'glb';
  return 'unknown';
}

function basePathWithoutExt(relativePath) {
  return relativePath.replace(/\.[^./\\]+$/i, '');
}

/** Carriage_Assy_REV3 → Carriage_Assy */
export function stripRevSuffix(basePath) {
  return basePath.replace(/_REV\d+$/i, '').replace(/_final$/i, '');
}

function splitDirAndName(basePath) {
  const normalized = basePath.replace(/\\/g, '/');
  const slash = normalized.lastIndexOf('/');
  if (slash < 0) {
    return { dir: '', name: normalized };
  }
  return {
    dir: normalized.slice(0, slash),
    name: normalized.slice(slash + 1),
  };
}

/**
 * {root}_REV{n}.ext 중 n이 큰 순 → 없으면 {root}.ext
 * GLB/GLTF 우선, 이후 STP
 */
export function buildCandidates(relativePath, preferStep = false) {
  if (!relativePath) return [];

  const base = stripRevSuffix(basePathWithoutExt(relativePath));
  const { dir, name } = splitDirAndName(base);
  const prefix = dir ? `${dir}/` : '';
  const exts = preferStep
    ? [...STEP_EXTS, ...GLB_EXTS]
    : [...GLB_EXTS, ...STEP_EXTS];
  const ordered = [];

  for (const ext of exts) {
    ordered.push(`${prefix}${name}_final${ext}`);
    for (let rev = MAX_REV; rev >= 1; rev--) {
      ordered.push(`${prefix}${name}_REV${rev}${ext}`);
    }
    ordered.push(`${prefix}${name}${ext}`);
  }

  return [...new Set(ordered)];
}

async function fetchWithTimeout(url, options = {}, timeoutMs = FETCH_TIMEOUT_MS) {
  const ctrl = new AbortController();
  const timer = setTimeout(() => ctrl.abort(), timeoutMs);
  try {
    return await fetch(url, { ...options, signal: ctrl.signal });
  } finally {
    clearTimeout(timer);
  }
}

async function probeExists(url) {
  try {
    const res = await fetchWithTimeout(
      url,
      {
        method: 'GET',
        headers: { Range: 'bytes=0-0' },
      },
      PROBE_TIMEOUT_MS
    );
    return res.ok || res.status === 206;
  } catch (_) {
    return false;
  }
}

/** 폴더 스캔 불가 시 병렬 probe로 가장 높은 REV 파일 선택 */
async function findHighestRevPath(drawingBaseUrl, rootPath, ext) {
  const finalPath = `${rootPath}_final${ext}`;
  if (await probeExists(drawingBaseUrl + finalPath)) {
    return finalPath;
  }

  const checks = [];
  for (let rev = 1; rev <= MAX_REV; rev++) {
    checks.push({ rev, path: `${rootPath}_REV${rev}${ext}` });
  }
  checks.push({ rev: 0, path: `${rootPath}${ext}` });

  const hits = await Promise.all(
    checks.map(async ({ rev, path }) => {
      const ok = await probeExists(drawingBaseUrl + path);
      return ok ? { rev, path } : null;
    })
  );

  const found = hits.filter(Boolean).sort((a, b) => b.rev - a.rev);
  return found[0]?.path ?? null;
}

/** catalog 경로 그대로 시도 (C#에서 확인된 경로용) */
export function buildResolvedFromPath(drawingBaseUrl, relativePath) {
  if (!relativePath) return null;
  return {
    path: relativePath,
    url: drawingBaseUrl + relativePath,
    kind: modelKind(relativePath),
  };
}

/**
 * STP 원본 → 표시용 GLB 경로 (동일 basename, STP→GLB 변환 결과만)
 */
export function deriveGlbPathFromStp(stpRelativePath) {
  if (!stpRelativePath) return null;
  return stpRelativePath.replace(/\.(stp|step)$/i, '.glb');
}

/**
 * ASSY 형체: STP만 탐색 → 파생 GLB 경로 반환. 폴더 내 독립 GLB 탐색 금지.
 */
export async function resolveAssyDisplayFromStp(drawingBaseUrl, relativePath) {
  const stp = await resolveStpSourcePath(drawingBaseUrl, relativePath);
  if (!stp) return null;
  const glbPath = deriveGlbPathFromStp(stp.path);
  return {
    path: glbPath,
    url: drawingBaseUrl + glbPath,
    kind: 'glb',
    stpPath: stp.path,
    stpUrl: stp.url,
  };
}

/** STP 원본만 (_REV 최대). GLB 직접 탐색 없음 */
export async function resolveStpSourcePath(drawingBaseUrl, relativePath) {
  if (!relativePath) return null;

  const base = stripRevSuffix(basePathWithoutExt(relativePath));
  const { dir, name } = splitDirAndName(base);
  const rootPath = dir ? `${dir}/${name}` : name;

  for (const ext of STEP_EXTS) {
    const path = await findHighestRevPath(drawingBaseUrl, rootPath, ext);
    if (path) {
      return { path, url: drawingBaseUrl + path, kind: modelKind(path) };
    }
  }
  return null;
}

/**
 * overview 등 — GLB/STP 혼합 탐색 (ASSY detail은 resolveAssyDisplayFromStp 사용)
 */
export async function resolveModelPath(drawingBaseUrl, relativePath, preferStep = false) {
  if (!relativePath) return null;

  const base = stripRevSuffix(basePathWithoutExt(relativePath));
  const { dir, name } = splitDirAndName(base);
  const rootPath = dir ? `${dir}/${name}` : name;

  const exts = preferStep
    ? [...STEP_EXTS, ...GLB_EXTS]
    : [...GLB_EXTS, ...STEP_EXTS];

  for (const ext of exts) {
    const path = await findHighestRevPath(drawingBaseUrl, rootPath, ext);
    if (path) {
      return { path, url: drawingBaseUrl + path, kind: modelKind(path) };
    }
  }

  return null;
}

export function formatModelLabel(path) {
  const ext = getExtension(path).toUpperCase().replace('.', '');
  return ext || '3D';
}
