/**
 * STP STYLED_ITEM → item(FACE/SOLID) 색상
 */
import { parseEntities } from './stp-extract-solid.mjs';

function refIds(raw) {
  const m = raw.match(/\([\s\S]*\)/);
  if (!m) return [];
  return [...m[0].matchAll(/#(\d+)/g)].map((x) => Number(x[1]));
}

function parseRgbEntity(raw) {
  const m = raw.match(/COLOUR_RGB\s*\([^,]*,\s*([^,]+),([^,]+),([^)]+)\)/);
  if (!m) return null;
  return [Number(m[1]), Number(m[2]), Number(m[3])];
}

function resolveColour(entities, rgbById, id, depth = 0) {
  if (!id || depth > 14) return null;
  if (rgbById.has(id)) return rgbById.get(id);
  const e = entities.get(id);
  if (!e) return null;
  if (e.type === 'COLOUR_RGB') return parseRgbEntity(e.raw);
  for (const ref of refIds(e.raw)) {
    const c = resolveColour(entities, rgbById, ref, depth + 1);
    if (c) return c;
  }
  return null;
}

/** @returns {Map<number, [number,number,number]>} face/solid id → rgb */
export function buildItemColourMap(stpText) {
  const entities = parseEntities(stpText);
  const rgbById = new Map();
  for (const e of entities.values()) {
    if (e.type === 'COLOUR_RGB') {
      const c = parseRgbEntity(e.raw);
      if (c) rgbById.set(e.id, c);
    }
  }
  const out = new Map();
  for (const e of entities.values()) {
    if (e.type !== 'STYLED_ITEM') continue;
    const refs = refIds(e.raw);
    if (refs.length < 2) continue;
    const itemId = refs[refs.length - 1];
    const rgb = resolveColour(entities, rgbById, refs[0]);
    if (rgb) out.set(itemId, rgb);
  }
  return out;
}

export const buildSolidColourMap = buildItemColourMap;
