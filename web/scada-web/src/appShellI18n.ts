import { resolveInitialLocale, type EngineeringLocale } from './engineering/i18n';

export type AppShellLocale = EngineeringLocale;

const ptBR = {
  subtitle: 'Plataforma industrial',
  currentArea: 'Área atual',
  runtime: 'Runtime',
  runtimeDescription: 'Operação',
  runtimeOverview: 'Visão geral',
  runtimeHistory: 'Histórico',
  engineering: 'Engineering',
  engineeringDescription: 'Área de projeto',
  audit: 'Auditoria',
  auditDescription: 'Rastreabilidade',
  licensing: 'Licenciamento',
  licensingDescription: 'Licença do produto',
  theme: 'Tema',
  themeDark: 'Escuro',
  themeLight: 'Claro',
  fullscreen: 'Tela cheia',
  exitFullscreen: 'Sair da tela cheia',
  accessDenied: 'Você não possui permissão para acessar esta área.',
  capabilitiesUnavailable: 'Não foi possível carregar as permissões efetivas da sessão.',
  runtimeUnavailable: 'Runtime não disponível para esta sessão.',
  emptyVisual: 'Nenhum objeto visual.'
} as const;

export type AppShellTextKey = keyof typeof ptBR;

const en: Record<AppShellTextKey, string> = {
  subtitle: 'Industrial platform',
  currentArea: 'Current area',
  runtime: 'Runtime',
  runtimeDescription: 'Operations',
  runtimeOverview: 'Overview',
  runtimeHistory: 'History',
  engineering: 'Engineering',
  engineeringDescription: 'Project area',
  audit: 'Audit',
  auditDescription: 'Traceability',
  licensing: 'Licensing',
  licensingDescription: 'Product license',
  theme: 'Theme',
  themeDark: 'Dark',
  themeLight: 'Light',
  fullscreen: 'Fullscreen',
  exitFullscreen: 'Exit fullscreen',
  accessDenied: 'You do not have permission to access this area.',
  capabilitiesUnavailable: 'The effective session permissions could not be loaded.',
  runtimeUnavailable: 'Runtime is not available for this session.',
  emptyVisual: 'No visual objects.'
};

const es: Record<AppShellTextKey, string> = {
  subtitle: 'Plataforma industrial',
  currentArea: 'Área actual',
  runtime: 'Runtime',
  runtimeDescription: 'Operación',
  runtimeOverview: 'Vista general',
  runtimeHistory: 'Histórico',
  engineering: 'Engineering',
  engineeringDescription: 'Área de proyecto',
  audit: 'Auditoría',
  auditDescription: 'Trazabilidad',
  licensing: 'Licenciamiento',
  licensingDescription: 'Licencia del producto',
  theme: 'Tema',
  themeDark: 'Oscuro',
  themeLight: 'Claro',
  fullscreen: 'Pantalla completa',
  exitFullscreen: 'Salir de pantalla completa',
  accessDenied: 'No tiene permiso para acceder a esta área.',
  capabilitiesUnavailable: 'No fue posible cargar los permisos efectivos de la sesión.',
  runtimeUnavailable: 'Runtime no está disponible para esta sesión.',
  emptyVisual: 'No hay objetos visuales.'
};

const resources: Record<AppShellLocale, Record<AppShellTextKey, string>> = {
  'pt-BR': ptBR,
  en,
  es
};

export function resolveAppShellLocale(): AppShellLocale {
  return resolveInitialLocale();
}

export function appShellText(locale: AppShellLocale) {
  return resources[locale];
}
