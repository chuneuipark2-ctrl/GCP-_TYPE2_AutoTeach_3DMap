/**
 * ASSY 폴더별 I/O 정의 파일 생성/갱신
 *   PLC_PART_IO_DEFINE.txt      — 마이컴(PLC) signalKey + ioType
 *   SOLID_EDGE_IO_DEFINE.txt    — plc_signalKey ↔ Solid Edge occurrence 이름
 *
 * node tools/sync-io-define-files.mjs
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const TEMPLATE = path.join(ROOT, 'Viewer3D/3D_Drawing_Template');
const RUNTIME = path.join(ROOT, 'bin/Debug/net6.0-windows/SRM0/3D_Drawing');

function readJson(p) {
  if (!fs.existsSync(p)) return null;
  return JSON.parse(fs.readFileSync(p, 'utf8'));
}

/** io_assy_mapping.json (레거시) 또는 기존 PLC 파일에서 ioType */
function loadIoTypes(baseDir) {
  const types = new Map();
  const legacy = path.join(baseDir, 'io_assy_mapping.json');
  if (fs.existsSync(legacy)) {
    for (const s of readJson(legacy)?.signals || []) {
      if (s.name) types.set(s.name, s.ioType || '');
    }
  }
  return types;
}

/** 기존 SOLID_EDGE 파일 occurrence 매핑 보존 */
function loadSeMap(filePath) {
  const map = new Map();
  if (!fs.existsSync(filePath)) return map;
  for (const line of fs.readFileSync(filePath, 'utf8').split(/\r?\n/)) {
    const t = line.trim();
    if (!t || t.startsWith('#')) continue;
    const [plc, occ] = t.split('\t');
    if (plc) map.set(plc.trim(), (occ || '').trim());
  }
  return map;
}

function writePlcDefine(assyDir, assy, ioTypes) {
  const keys = assy.signalKeys || [];
  const lines = [
    `# PLC_PART_IO_DEFINE — ${assy.id}`,
    '# 마이컴(PLC) signalKey — PageDIO / GCP_PlcRead 와 동일한 이름',
    '# 한 줄: signalKey<TAB>ioType',
    '#',
  ];
  for (const key of keys) {
    lines.push(`${key}\t${ioTypes.get(key) || ''}`);
  }
  lines.push('');
  fs.writeFileSync(path.join(assyDir, 'PLC_PART_IO_DEFINE.txt'), lines.join('\r\n'), 'utf8');
}

function writeSolidEdgeDefine(assyDir, assy, prevMap) {
  const keys = assy.signalKeys || [];
  const lines = [
    `# SOLID_EDGE_IO_DEFINE — ${assy.id}`,
    '# Solid Edge PathFinder occurrence(배치) 이름',
    '# 한 줄: plc_signalKey<TAB>occurrence_name',
    '# occurrence_name 비우면 plc_signalKey 와 동일해야 함 (STP 자동매칭)',
    '#',
  ];
  for (const key of keys) {
    const occ = prevMap.get(key);
    lines.push(`${key}\t${occ ?? ''}`);
  }
  lines.push('');
  fs.writeFileSync(path.join(assyDir, 'SOLID_EDGE_IO_DEFINE.txt'), lines.join('\r\n'), 'utf8');
}

function removeLegacy(baseDir) {
  const toDelete = [
    path.join(baseDir, 'io_assy_mapping.json'),
    path.join(baseDir, 'IO_SENSOR_NAMES.txt'),
    path.join(baseDir, 'IO_SENSOR_NAMES'),
  ];
  for (const p of toDelete) {
    if (fs.existsSync(p) && fs.statSync(p).isFile()) fs.unlinkSync(p);
    else if (fs.existsSync(p)) fs.rmSync(p, { recursive: true, force: true });
  }
  const assyRoot = path.join(baseDir, 'assemblies');
  if (!fs.existsSync(assyRoot)) return;
  for (const folder of fs.readdirSync(assyRoot, { withFileTypes: true })) {
    if (!folder.isDirectory()) continue;
    const d = path.join(assyRoot, folder.name);
    for (const name of fs.readdirSync(d)) {
      if (/^IO_SENSOR_NAMES/i.test(name)) {
        fs.unlinkSync(path.join(d, name));
      }
    }
  }
}

function syncBase(baseDir) {
  const assemblies = readJson(path.join(baseDir, 'assemblies.json'));
  if (!assemblies?.assemblies?.length) {
    console.warn('skip', baseDir);
    return;
  }
  const ioTypes = loadIoTypes(baseDir);
  console.log('\n[' + baseDir + ']');
  for (const assy of assemblies.assemblies) {
    const keys = assy.signalKeys || [];
    if (!keys.length) continue;
    const assyDir = path.join(baseDir, 'assemblies', assy.id);
    fs.mkdirSync(assyDir, { recursive: true });
    const sePath = path.join(assyDir, 'SOLID_EDGE_IO_DEFINE.txt');
    const prevSe = loadSeMap(sePath);
    writePlcDefine(assyDir, assy, ioTypes);
    writeSolidEdgeDefine(assyDir, assy, prevSe);
    console.log(`  ${assy.id}: PLC ${keys.length}건, SOLID_EDGE ${keys.length}건`);
  }
  removeLegacy(baseDir);
  console.log('  legacy IO_SENSOR_NAMES / io_assy_mapping 제거');
}

for (const base of [TEMPLATE, RUNTIME]) {
  if (fs.existsSync(base)) syncBase(base);
}
