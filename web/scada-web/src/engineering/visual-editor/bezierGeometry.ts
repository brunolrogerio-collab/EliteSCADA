import type { VisualEditorPoint } from './visualEditorContracts';

export type BezierPathModel = Readonly<{
  tokens: readonly string[];
  points: readonly VisualEditorPoint[];
}>;

const TOKEN = /[A-Za-z]|[-+]?(?:\d*\.\d+|\d+\.?\d*)(?:[eE][-+]?\d+)?/g;

/** Reads the absolute SVG path subset used by editable bezier objects. */
export function readEditableBezierPath(path: string): BezierPathModel | null {
  const matches = [...path.matchAll(TOKEN)];
  const residue = path.replace(TOKEN, '').replace(/[\s,]/g, '');
  if (residue || matches.length === 0) return null;
  const tokens = matches.map(match => match[0]);
  const points: VisualEditorPoint[] = [];
  let command = '';
  let cursor = 0;
  while (cursor < tokens.length) {
    if (/^[A-Za-z]$/.test(tokens[cursor])) command = tokens[cursor++];
    if (!['M', 'L', 'C', 'Q', 'Z'].includes(command)) return null;
    if (command === 'Z') continue;
    const pairCount = command === 'C' ? 3 : command === 'Q' ? 2 : 1;
    if (cursor + pairCount * 2 > tokens.length) return null;
    for (let pair = 0; pair < pairCount; pair++) {
      const x = Number(tokens[cursor + pair * 2]);
      const y = Number(tokens[cursor + pair * 2 + 1]);
      if (!Number.isFinite(x) || !Number.isFinite(y)) return null;
      points.push(Object.freeze({ x, y }));
    }
    cursor += pairCount * 2;
    if (command === 'M') command = 'L';
  }
  return points.length ? Object.freeze({ tokens: Object.freeze(tokens), points: Object.freeze(points) }) : null;
}

export function writeBezierPathPoint(model: BezierPathModel, pointIndex: number, point: VisualEditorPoint): string | null {
  if (pointIndex < 0 || pointIndex >= model.points.length) return null;
  const tokens = [...model.tokens];
  let cursor = 0;
  let command = '';
  let activePoint = 0;
  while (cursor < tokens.length) {
    if (/^[A-Za-z]$/.test(tokens[cursor])) command = tokens[cursor++];
    if (command === 'Z') continue;
    const pairCount = command === 'C' ? 3 : command === 'Q' ? 2 : 1;
    for (let pair = 0; pair < pairCount; pair++, activePoint++) {
      if (activePoint === pointIndex) {
        tokens[cursor + pair * 2] = compact(point.x);
        tokens[cursor + pair * 2 + 1] = compact(point.y);
        return tokens.join(' ');
      }
    }
    cursor += pairCount * 2;
    if (command === 'M') command = 'L';
  }
  return null;
}

function compact(value: number): string {
  return Number(value.toFixed(3)).toString();
}
