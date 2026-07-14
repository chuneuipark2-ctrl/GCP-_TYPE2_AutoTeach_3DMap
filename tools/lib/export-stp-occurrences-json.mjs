/**
 * STP occurrence → JT occurrence placer용 JSON (파트 JT 키 + 배치 이름)
 */
import fs from 'fs';
import path from 'path';
import { parseStpOccurrences } from './stp-occurrence-parser.mjs';

function escapeJson(s) {
  return String(s ?? '')
    .replace(/\\/g, '\\\\')
    .replace(/"/g, '\\"');
}

/**
 * @param {string} stpPath
 * @param {string} partsDir — part JT 폴더 (Carriage_Assy_final/)
 * @returns {{ jsonPath: string, count: number }}
 */
export function writeOccurrencesJson(stpPath, partsDir, jsonPath) {
  if (!fs.existsSync(stpPath)) throw new Error('STP 없음: ' + stpPath);
  if (!fs.existsSync(partsDir)) throw new Error('JT parts 폴더 없음: ' + partsDir);

  const jtMap = new Set(
    fs
      .readdirSync(partsDir)
      .filter((f) => f.toLowerCase().endsWith('.jt'))
      .map((f) => f.slice(0, -3))
  );

  const stpText = fs.readFileSync(stpPath, 'utf8');
  const occurrences = parseStpOccurrences(stpText);
  const items = [];
  for (const o of occurrences) {
    const key = [o.partName, o.occurrenceName, o.matchName].find((k) => k && jtMap.has(k));
    if (!key) continue;
    items.push({
      key,
      occurrenceName: o.occurrenceName || '',
      partName: o.partName || '',
      label: o.occurrenceName || o.partName || key,
      matrix: o.matrix || null,
    });
  }
  if (!items.length) throw new Error('STP occurrence ↔ JT 파트 매칭 0');

  const body = items
    .map((it) => {
      const mat =
        it.matrix && it.matrix.length === 16
          ? `,"matrix":[${it.matrix.map((v) => Number(v).toFixed(6)).join(',')}]`
          : '';
      return `{"key":"${escapeJson(it.key)}","occurrenceName":"${escapeJson(it.occurrenceName)}","partName":"${escapeJson(it.partName)}","label":"${escapeJson(it.label)}"${mat}}`;
    })
    .join(',\n');
  fs.mkdirSync(path.dirname(jsonPath), { recursive: true });
  fs.writeFileSync(jsonPath, '[\n' + body + '\n]\n', 'utf8');
  return { jsonPath, count: items.length };
}
