/** ASSY 폴더 PLC_PART_IO_DEFINE / SOLID_EDGE_IO_DEFINE 읽기 */
import fs from 'fs';
import path from 'path';

export function readPlcPartIoDefine(assyDir) {
  const file = path.join(assyDir, 'PLC_PART_IO_DEFINE.txt');
  const keys = [];
  const ioTypes = new Map();
  if (!fs.existsSync(file)) return { keys, ioTypes };
  for (const line of fs.readFileSync(file, 'utf8').split(/\r?\n/)) {
    const t = line.trim();
    if (!t || t.startsWith('#')) continue;
    const [key, ioType] = t.split('\t');
    if (!key) continue;
    keys.push(key.trim());
    if (ioType?.trim()) ioTypes.set(key.trim(), ioType.trim());
  }
  return { keys, ioTypes };
}

/** plc signalKey → Solid Edge occurrence 이름 (비면 plc 키 그대로) */
export function readSolidEdgeIoMap(assyDir) {
  const file = path.join(assyDir, 'SOLID_EDGE_IO_DEFINE.txt');
  const map = new Map();
  if (!fs.existsSync(file)) return map;
  for (const line of fs.readFileSync(file, 'utf8').split(/\r?\n/)) {
    const t = line.trim();
    if (!t || t.startsWith('#')) continue;
    const [plc, occ] = t.split('\t');
    if (!plc) continue;
    const k = plc.trim();
    const o = (occ || '').trim();
    map.set(k, o || k);
  }
  return map;
}

/**
 * IO_STP_SENSOR_MAP.txt — plc signalKey → STP_센서좌표_목록 번호 (1-based)
 * 형식: signalKey<TAB>번호
 */
export function readStpSensorIndexMap(assyDir) {
  const file = path.join(assyDir, 'IO_STP_SENSOR_MAP.txt');
  const map = new Map();
  if (!fs.existsSync(file)) return map;
  for (const line of fs.readFileSync(file, 'utf8').split(/\r?\n/)) {
    const t = line.trim();
    if (!t || t.startsWith('#')) continue;
    const [key, numStr] = t.split('\t');
    if (!key?.trim()) continue;
    const n = Number(String(numStr || '').trim());
    if (!Number.isFinite(n) || n < 1) continue;
    map.set(key.trim(), Math.floor(n));
  }
  return map;
}
