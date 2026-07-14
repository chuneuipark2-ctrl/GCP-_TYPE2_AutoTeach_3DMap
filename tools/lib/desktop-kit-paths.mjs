/**
 * 바탕화면 GCP_3D_Debug 키트 경로
 */
import fs from 'fs';
import path from 'path';

export function getDesktopKitRoot() {
  const candidates = [
    path.join(process.env.USERPROFILE || '', 'OneDrive - 현대그룹', '바탕 화면', 'GCP_3D_Debug'),
    path.join(process.env.USERPROFILE || '', 'Desktop', 'GCP_3D_Debug'),
    path.join(process.env.USERPROFILE || '', 'OneDrive', 'Desktop', 'GCP_3D_Debug'),
  ];
  return candidates.find((p) => p && fs.existsSync(p)) || null;
}

/** STP/GLB 검색 루트 — GCP_3D_Debug 전체 + 바탕 화면(1단) */
export function getDesktopSearchRoots() {
  const roots = [];
  const kit = getDesktopKitRoot();
  if (kit) roots.push(kit);
  const desktop = path.join(process.env.USERPROFILE || '', 'OneDrive - 현대그룹', '바탕 화면');
  if (fs.existsSync(desktop) && !roots.includes(desktop)) roots.push(desktop);
  const desktopPlain = path.join(process.env.USERPROFILE || '', 'Desktop');
  if (fs.existsSync(desktopPlain) && !roots.includes(desktopPlain)) roots.push(desktopPlain);
  return roots;
}

export function getDefaultRuntimeDrawingRoot(projectRoot) {
  return path.join(projectRoot, 'bin/Debug/net6.0-windows/SRM0/3D_Drawing');
}

/** 사용자 안내용 — ASSY 폴더 직하위 STP */
export function formatAssyStpSaveHint(assyId, stpBaseName) {
  const base = stpBaseName || assyId;
  return `SRM0/3D_Drawing/assemblies/${assyId}/${base}.stp`;
}

/** Solid Edge 형체 OBJ 저장 경로 (빌드타임 Assimp 입력) */
export function formatAssyObjSaveHint(assyId, objBaseName) {
  const base = objBaseName || assyId;
  return `SRM0/3D_Drawing/assemblies/${assyId}/${base}.obj`;
}

/** 배포 패키지에 동봉할 prebuilt GLB 경로 */
export function formatAssyGlbDeployHint(assyId, glbBaseName) {
  const base = glbBaseName || assyId;
  return `SRM0/3D_Drawing/assemblies/${assyId}/${base}.glb`;
}
