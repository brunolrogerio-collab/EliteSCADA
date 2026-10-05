import type { VisualEditorPoint } from './visualEditorContracts';

export type BezierPathModel = Readonly<{
  tokens: readonly string[];
  points: readonly VisualEditorPoint[];
  anchorIndexes: readonly number[];
}>;

export type ClosedBezierGeometry = Readonly<{
  x: number;
  y: number;
  width: number;
  height: number;
  path: string;
}>;

const TOKEN = /[A-Za-z]|[-+]?(?:\d*\.\d+|\d+\.?\d*)(?:[eE][-+]?\d+)?/g;

/** Reads the absolute SVG path subset used by editable bezier objects. */
export function readEditableBezierPath(path: string): BezierPathModel | null {
  const matches = [...path.matchAll(TOKEN)];
  const residue = path.replace(TOKEN, '').replace(/[\s,]/g, '');
  if (residue || matches.length === 0) return null;
  const tokens = matches.map(match => match[0]);
  const points: VisualEditorPoint[] = [];
  const anchorIndexes: number[] = [];
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
    anchorIndexes.push(points.length - 1);
    cursor += pairCount * 2;
    if (command === 'M') command = 'L';
  }
  if (points.length > 2 && samePoint(points[0], points.at(-1)!)) anchorIndexes.pop();
  return points.length ? Object.freeze({
    tokens: Object.freeze(tokens),
    points: Object.freeze(points),
    anchorIndexes: Object.freeze(anchorIndexes)
  }) : null;
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

/**
 * Inserts an anchor by splitting the final drawable segment at t=0.5. De Casteljau
 * subdivision preserves the original curve exactly (up to path serialization),
 * so adding an editable point does not visibly reshape the object.
 */
export function insertBezierAnchor(path: string, anchorOrdinal?: number): string | null {
  const commands = parsePath(path);
  if (!commands) return null;
  const model = readEditableBezierPath(path);
  if (!model) return null;
  const drawableIndexes = commands.map((command, index) => isDrawable(command) ? index : -1).filter(index => index >= 0);
  if (drawableIndexes.length === 0) return null;
  const requested = anchorOrdinal ?? model.anchorIndexes.length - 1;
  if (requested < 0 || requested >= model.anchorIndexes.length) return null;
  const anchorPointIndex = model.anchorIndexes[requested];
  const lastAnchorOrdinal = model.anchorIndexes.length - 1;
  const drawableOrdinal = requested === lastAnchorOrdinal
    ? drawableIndexes.length - 1
    : Math.min(requested, drawableIndexes.length - 1);
  const targetIndex = drawableIndexes[drawableOrdinal];
  let anchor: VisualEditorPoint | null = requested === 0 ? commands[0].points[0] ?? null : null;
  if (requested > 0) {
    let pointIndex = 0;
    for (const command of commands) {
      if (command.kind === 'M') { pointIndex += 1; continue; }
      if (!isDrawable(command)) continue;
      pointIndex += command.points.length;
      if (pointIndex - 1 === anchorPointIndex) { anchor = command.points.at(-1) ?? null; break; }
    }
  }
  if (!anchor) return null;

  const segment = commands[targetIndex];
  const points = segment.points;
  if (segment.kind === 'L') {
    const end = points[0];
    const midpoint = lerp(anchor, end, 0.5);
    commands.splice(targetIndex, 1, { kind: 'L', points: [midpoint] }, { kind: 'L', points: [end] });
  } else if (segment.kind === 'Q') {
    const [control, end] = points;
    const firstControl = lerp(anchor, control, 0.5);
    const secondControl = lerp(control, end, 0.5);
    const midpoint = lerp(firstControl, secondControl, 0.5);
    commands.splice(targetIndex, 1,
      { kind: 'Q', points: [firstControl, midpoint] },
      { kind: 'Q', points: [secondControl, end] });
  } else if (segment.kind === 'C') {
    const [control1, control2, end] = points;
    const first = lerp(anchor, control1, 0.5);
    const middle = lerp(control1, control2, 0.5);
    const last = lerp(control2, end, 0.5);
    const leftControl2 = lerp(first, middle, 0.5);
    const rightControl1 = lerp(middle, last, 0.5);
    const midpoint = lerp(leftControl2, rightControl1, 0.5);
    commands.splice(targetIndex, 1,
      { kind: 'C', points: [first, leftControl2, midpoint] },
      { kind: 'C', points: [rightControl1, last, end] });
  } else {
    return null;
  }
  return serializePath(commands);
}

/** Removes the final anchor/segment while retaining the path's initial point. */
export function removeBezierAnchor(path: string, anchorOrdinal?: number): string | null {
  const commands = parsePath(path);
  if (!commands) return null;
  const model = readEditableBezierPath(path);
  if (!model || model.anchorIndexes.length <= (isClosedPath(commands) ? 3 : 2)) return null;
  const ordinal = anchorOrdinal ?? model.anchorIndexes.length - 1;
  if (ordinal < 0 || ordinal >= model.anchorIndexes.length) return null;
  const drawableIndices = commands.map((command, index) => isDrawable(command) ? index : -1).filter(index => index >= 0);
  const targetIndex = ordinal === 0 ? drawableIndices[0] : drawableIndices[Math.min(ordinal - 1, drawableIndices.length - 1)];
  if (targetIndex === undefined) return null;
  if (ordinal === 0) {
    const nextStart = commands[targetIndex].points.at(-1);
    if (!nextStart || commands[0].kind !== 'M') return null;
    commands[0] = { kind: 'M', points: [nextStart] };
    commands.splice(targetIndex, 1);
    const finalSegmentIndex = commands.map((command, index) => isDrawable(command) ? index : -1).filter(index => index >= 0).at(-1);
    if (finalSegmentIndex !== undefined) {
      const finalSegment = commands[finalSegmentIndex];
      if (isDrawable(finalSegment)) {
        const points = [...finalSegment.points];
        points[points.length - 1] = nextStart;
        commands[finalSegmentIndex] = { kind: finalSegment.kind, points };
      }
    }
  } else commands.splice(targetIndex, 1);
  return serializePath(commands);
}

/** Builds a smooth, closed SVG cubic path from clicked canvas anchors. */
export function createClosedBezierGeometry(points: readonly VisualEditorPoint[]): ClosedBezierGeometry | null {
  if (points.length < 3 || new Set(points.map(point => `${point.x}\u0000${point.y}`)).size < 3) return null;
  const anchors = points.map(point => ({ x: point.x, y: point.y }));
  const segments = anchors.map((start, index) => {
    const end = anchors[(index + 1) % anchors.length];
    const previous = anchors[(index + anchors.length - 1) % anchors.length];
    const after = anchors[(index + 2) % anchors.length];
    return {
      start,
      first: { x: start.x + (end.x - previous.x) / 6, y: start.y + (end.y - previous.y) / 6 },
      second: { x: end.x - (after.x - start.x) / 6, y: end.y - (after.y - start.y) / 6 },
      end
    };
  });
  const all = segments.flatMap(segment => [segment.start, segment.first, segment.second, segment.end]);
  const minX = Math.min(...all.map(point => point.x));
  const minY = Math.min(...all.map(point => point.y));
  const maxX = Math.max(...all.map(point => point.x));
  const maxY = Math.max(...all.map(point => point.y));
  const width = Math.max(maxX - minX, 1);
  const height = Math.max(maxY - minY, 1);
  const normalize = (point: VisualEditorPoint) => ({ x: (point.x - minX) * 100 / width, y: (point.y - minY) * 100 / height });
  const first = normalize(anchors[0]);
  const path = [`M ${compact(first.x)} ${compact(first.y)}`];
  for (const segment of segments) {
    const firstControl = normalize(segment.first);
    const secondControl = normalize(segment.second);
    const end = normalize(segment.end);
    path.push(`C ${compact(firstControl.x)} ${compact(firstControl.y)} ${compact(secondControl.x)} ${compact(secondControl.y)} ${compact(end.x)} ${compact(end.y)}`);
  }
  path.push('Z');
  return Object.freeze({ x: minX, y: minY, width, height, path: path.join(' ') });
}

function samePoint(left: VisualEditorPoint, right: VisualEditorPoint): boolean {
  return Math.abs(left.x - right.x) <= 1e-6 && Math.abs(left.y - right.y) <= 1e-6;
}

function isDrawable(command: ParsedCommand): command is Extract<ParsedCommand, { kind: 'L' | 'Q' | 'C' }> {
  return command.kind === 'L' || command.kind === 'Q' || command.kind === 'C';
}

function isClosedPath(commands: readonly ParsedCommand[]): boolean {
  if (commands.some(command => command.kind === 'Z')) return true;
  const first = commands.find(command => command.kind === 'M')?.points[0];
  const last = [...commands].reverse().find(isDrawable)?.points.at(-1);
  return Boolean(first && last && samePoint(first, last));
}

type ParsedCommand = Readonly<{ kind: 'M'; points: readonly VisualEditorPoint[] }>
  | Readonly<{ kind: 'L'; points: readonly VisualEditorPoint[] }>
  | Readonly<{ kind: 'Q'; points: readonly VisualEditorPoint[] }>
  | Readonly<{ kind: 'C'; points: readonly VisualEditorPoint[] }>
  | Readonly<{ kind: 'Z'; points: readonly [] }>;

function parsePath(path: string): ParsedCommand[] | null {
  const matches = [...path.matchAll(TOKEN)];
  const residue = path.replace(TOKEN, '').replace(/[\s,]/g, '');
  if (residue || matches.length === 0) return null;
  const tokens = matches.map(match => match[0]);
  const commands: ParsedCommand[] = [];
  let command: string | null = null;
  let cursor = 0;
  while (cursor < tokens.length) {
    if (/^[A-Za-z]$/.test(tokens[cursor])) {
      const next = tokens[cursor++];
      if (!['M', 'L', 'Q', 'C', 'Z'].includes(next)) return null;
      command = next;
      if (command === 'Z') {
        commands.push({ kind: 'Z', points: [] });
        command = null;
        continue;
      }
    }
    if (!command) return null;
    if (!['M', 'L', 'Q', 'C'].includes(command)) return null;
    const drawingCommand = command as 'M' | 'L' | 'Q' | 'C';
    const count = drawingCommand === 'C' ? 3 : drawingCommand === 'Q' ? 2 : 1;
    if (cursor + count * 2 > tokens.length) return null;
    const points: VisualEditorPoint[] = [];
    for (let index = 0; index < count; index++) {
      const x = Number(tokens[cursor + index * 2]);
      const y = Number(tokens[cursor + index * 2 + 1]);
      if (!Number.isFinite(x) || !Number.isFinite(y)) return null;
      points.push(Object.freeze({ x, y }));
    }
    cursor += count * 2;
    commands.push(Object.freeze({ kind: drawingCommand, points: Object.freeze(points) }) as ParsedCommand);
    if (drawingCommand === 'M') command = 'L';
  }
  return commands.length > 0 && commands[0].kind === 'M' ? commands : null;
}

function serializePath(commands: readonly ParsedCommand[]): string {
  return commands.map(command => command.kind === 'Z'
    ? 'Z'
    : `${command.kind} ${command.points.map(point => `${compact(point.x)} ${compact(point.y)}`).join(' ')}`)
    .join(' ');
}

function lerp(left: VisualEditorPoint, right: VisualEditorPoint, t: number): VisualEditorPoint {
  return Object.freeze({ x: left.x + (right.x - left.x) * t, y: left.y + (right.y - left.y) * t });
}

function compact(value: number): string {
  return Number(value.toFixed(3)).toString();
}
