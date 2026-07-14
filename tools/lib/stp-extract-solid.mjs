/**
 * STEP entity subgraph 추출 — 단일 MANIFOLD_SOLID_BREP → mini .stp
 */
import fs from 'fs';

export function parseEntities(stpText) {
  const entities = new Map();
  const chunks = stpText.split(/;\r?\n/);
  for (const chunk of chunks) {
    const head = chunk.match(/#(\d+)\s*=\s*([A-Z0-9_]+)?\s*\(/);
    if (!head) continue;
    entities.set(Number(head[1]), { id: Number(head[1]), type: head[2] || 'AGGREGATE', raw: chunk });
  }
  return entities;
}

function refIdsInRaw(raw) {
  const m = raw.match(/\([\s\S]*\)/);
  if (!m) return [];
  return [...m[0].matchAll(/#(\d+)/g)].map((x) => Number(x[1]));
}

export function collectClosure(entities, rootIds) {
  const need = new Set();
  const stack = [...rootIds];
  while (stack.length) {
    const id = stack.pop();
    if (!id || need.has(id)) continue;
    const e = entities.get(id);
    if (!e) continue;
    need.add(id);
    for (const ref of refIdsInRaw(e.raw)) {
      if (!need.has(ref)) stack.push(ref);
    }
  }
  return need;
}

export function writeMiniStep(entities, rootIds, outPath) {
  const ids = collectClosure(entities, rootIds);
  const sorted = [...ids].sort((a, b) => a - b);
  const lines = [
    'ISO-10303-21;',
    'HEADER;',
    "FILE_DESCRIPTION((''),'2;1');",
    "FILE_NAME('extract.stp','',(''),(''),'','','');",
    "FILE_SCHEMA(('AUTOMOTIVE_DESIGN'));",
    'ENDSEC;',
    'DATA;',
  ];
  for (const id of sorted) {
    const e = entities.get(id);
    if (e) lines.push(e.raw.trim() + ';');
  }
  lines.push('ENDSEC;', 'END-ISO-10303-21;', '');
  fs.writeFileSync(outPath, lines.join('\n'), 'utf8');
  return sorted.length;
}

export function listSolidBreps(entities) {
  const out = [];
  for (const e of entities.values()) {
    if (e.type === 'MANIFOLD_SOLID_BREP') out.push(e.id);
  }
  return out;
}
