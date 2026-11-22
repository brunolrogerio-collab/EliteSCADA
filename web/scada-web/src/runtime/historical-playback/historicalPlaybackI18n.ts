import type { EngineeringLocale } from '../../engineering/i18n';

const COPY = {
  'pt-BR': {
    title:'Playback histórico', playback:'Playback', readOnly:'somente leitura', backLive:'Voltar ao Live',
    enter:'Entrar no Playback', close:'Fechar', custom:'Personalizado', instant:'Instante', from:'De', to:'Até',
    tags:'TAGs', gaps:'gaps', loading:'Carregando histórico…', liveHint:'Escolha o período e o instante. O Runtime continua Live até você entrar no Playback.'
  },
  en: {
    title:'Historical Playback', playback:'Playback', readOnly:'read-only', backLive:'Return to Live',
    enter:'Enter Playback', close:'Close', custom:'Custom', instant:'Instant', from:'From', to:'To',
    tags:'TAGs', gaps:'gaps', loading:'Loading history…', liveHint:'Choose the range and instant. Runtime stays Live until you enter Playback.'
  },
  es: {
    title:'Playback histórico', playback:'Playback', readOnly:'solo lectura', backLive:'Volver a Live',
    enter:'Entrar en Playback', close:'Cerrar', custom:'Personalizado', instant:'Instante', from:'Desde', to:'Hasta',
    tags:'TAGs', gaps:'gaps', loading:'Cargando histórico…', liveHint:'Elige el período y el instante. Runtime sigue Live hasta entrar en Playback.'
  }
} as const;
export function historicalPlaybackCopy(locale: EngineeringLocale){ return COPY[locale] ?? COPY['pt-BR']; }
