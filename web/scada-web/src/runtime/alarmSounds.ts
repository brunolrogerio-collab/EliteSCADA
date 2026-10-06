import type { RuntimeAlarmCenterItem, RuntimeAlarmCenterLocale, RuntimeAlarmDefinition } from './alarmCenterTypes';
import { normalizeRuntimeAlarmState, runtimeAlarmPriorityRank } from './alarmCenterModel';

export const ALARM_SOUND_PROFILES = ['short', 'double', 'triple', 'rising', 'alternating'] as const;
export type AlarmSoundProfileId = typeof ALARM_SOUND_PROFILES[number];

type ToneStep = { frequency: number; duration: number; gap: number };
type SoundEntry = { definitionId: string; profile: AlarmSoundProfileId; priority: number };

const patterns: Record<AlarmSoundProfileId, readonly ToneStep[]> = {
  short: [{ frequency: 880, duration: 190, gap: 0 }],
  double: [{ frequency: 880, duration: 115, gap: 100 }, { frequency: 880, duration: 115, gap: 0 }],
  triple: [{ frequency: 1040, duration: 90, gap: 80 }, { frequency: 740, duration: 90, gap: 80 }, { frequency: 1040, duration: 90, gap: 0 }],
  rising: [{ frequency: 520, duration: 105, gap: 35 }, { frequency: 640, duration: 105, gap: 35 }, { frequency: 780, duration: 105, gap: 35 }, { frequency: 920, duration: 105, gap: 0 }],
  alternating: [{ frequency: 880, duration: 120, gap: 85 }, { frequency: 620, duration: 120, gap: 85 }, { frequency: 880, duration: 120, gap: 85 }, { frequency: 620, duration: 120, gap: 0 }]
};

let audioContext: AudioContext | null = null;

export function alarmSoundProfileLabel(profile: AlarmSoundProfileId, locale: RuntimeAlarmCenterLocale): string {
  const labels: Record<RuntimeAlarmCenterLocale, Record<AlarmSoundProfileId, string>> = {
    'pt-BR': { short: 'Pulso curto', double: 'Duplo', triple: 'Triplo', rising: 'Ascendente', alternating: 'Alternado' },
    en: { short: 'Short pulse', double: 'Double', triple: 'Triple', rising: 'Rising', alternating: 'Alternating' },
    es: { short: 'Pulso corto', double: 'Doble', triple: 'Triple', rising: 'Ascendente', alternating: 'Alternado' }
  };
  return labels[locale][profile];
}

export function selectUnacknowledgedAlarmSounds(
  alarms: readonly RuntimeAlarmCenterItem[],
  definitions: readonly RuntimeAlarmDefinition[]
): SoundEntry[] {
  const soundById = new Map(definitions.map(definition => [definition.id, definition.soundProfile]));
  return alarms.flatMap(alarm => {
    if (normalizeRuntimeAlarmState(alarm.state) !== 'active') return [];
    const profile = soundById.get(alarm.definitionId);
    if (!isAlarmSoundProfile(profile)) return [];
    return [{ definitionId: alarm.definitionId, profile, priority: runtimeAlarmPriorityRank(alarm) }];
  }).sort((left, right) => right.priority - left.priority);
}

export function isAlarmSoundProfile(value: unknown): value is AlarmSoundProfileId {
  return typeof value === 'string' && (ALARM_SOUND_PROFILES as readonly string[]).includes(value);
}

export function resumeAlarmAudio(): Promise<void> {
  const context = getAudioContext();
  return context.resume().then(() => {
    if (context.state !== 'running') throw new Error('Alarm audio could not be enabled.');
  });
}

export function supportsAlarmAudio(): boolean {
  return typeof window !== 'undefined' && typeof window.AudioContext === 'function';
}

export async function playAlarmSoundProfile(profile: AlarmSoundProfileId): Promise<void> {
  const context = getAudioContext();
  if (context.state !== 'running') await resumeAlarmAudio();
  const start = context.currentTime + 0.02;
  let offset = 0;
  for (const step of patterns[profile]) {
    const toneStart = start + offset / 1000;
    const toneEnd = toneStart + step.duration / 1000;
    const oscillator = context.createOscillator();
    const gain = context.createGain();
    oscillator.type = 'triangle';
    oscillator.frequency.setValueAtTime(step.frequency, toneStart);
    gain.gain.setValueAtTime(0.0001, toneStart);
    gain.gain.exponentialRampToValueAtTime(0.16, toneStart + 0.012);
    gain.gain.exponentialRampToValueAtTime(0.0001, toneEnd);
    oscillator.connect(gain);
    gain.connect(context.destination);
    oscillator.start(toneStart);
    oscillator.stop(toneEnd + 0.015);
    offset += step.duration + step.gap;
  }
}

function getAudioContext(): AudioContext {
  if (audioContext) return audioContext;
  if (typeof window === 'undefined') throw new Error('Alarm audio is unavailable outside the browser.');
  const AudioContextConstructor = window.AudioContext;
  if (!AudioContextConstructor) throw new Error('This browser does not support alarm audio.');
  audioContext = new AudioContextConstructor();
  return audioContext;
}
