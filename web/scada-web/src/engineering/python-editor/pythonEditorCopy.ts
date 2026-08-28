import type { EngineeringLocale } from '../i18n';

export function pythonEditorCopy(locale: EngineeringLocale) {
  const copies = {
    'pt-BR': {
      editorLabel: 'Editor Python',
      diagnosticsReady: 'Diagnósticos conectados',
      diagnosticsUnavailable: 'Diagnósticos de compilação ainda não conectados nesta composição. O Preview canônico continua obrigatório antes do Apply.',
      diagnosticsStale: 'A fonte mudou depois da última compilação. Os marcadores anteriores foram descartados até chegar um diagnóstico para este texto.',
      diagnosticsRejected: 'diagnóstico(s) inválido(s) ignorado(s)',
      errors: 'erros',
      warnings: 'avisos',
      entryPointContext: 'Handlers canônicos',
      noEntryPoints: 'Nenhum entry point declarado.',
      apiHelp: 'Client Visual API v1',
      apiHelpHint: 'Capacidades públicas estáveis do bridge. O nome final do módulo Python não é inventado pelo editor.',
      serverScopeHint: 'Scripts Server não usam a API Client Visual. Server Python permanece fora da Wave 06.',
      sourceAuthority: 'A fonte editada permanece no draft canônico e só entra no Working por Preview / Apply / CAS.',
      editorUnavailable: 'O Monaco não pôde ser iniciado.'
    },
    en: {
      editorLabel: 'Python Editor',
      diagnosticsReady: 'Diagnostics connected',
      diagnosticsUnavailable: 'Compile diagnostics are not connected in this composition yet. Canonical Preview remains mandatory before Apply.',
      diagnosticsStale: 'Source changed after the last compile. Previous markers were discarded until diagnostics for this exact text arrive.',
      diagnosticsRejected: 'invalid diagnostic(s) ignored',
      errors: 'errors',
      warnings: 'warnings',
      entryPointContext: 'Canonical handlers',
      noEntryPoints: 'No entry points declared.',
      apiHelp: 'Client Visual API v1',
      apiHelpHint: 'Stable public bridge capabilities. The editor does not invent the final Python module name.',
      serverScopeHint: 'Server Scripts do not use the Client Visual API. Server Python remains outside Wave 06.',
      sourceAuthority: 'Edited source remains in the canonical draft and reaches Working only through Preview / Apply / CAS.',
      editorUnavailable: 'Monaco could not be initialized.'
    },
    es: {
      editorLabel: 'Editor Python',
      diagnosticsReady: 'Diagnósticos conectados',
      diagnosticsUnavailable: 'Los diagnósticos de compilación aún no están conectados en esta composición. El Preview canónico sigue siendo obligatorio antes de Apply.',
      diagnosticsStale: 'La fuente cambió después de la última compilación. Los marcadores anteriores se descartaron hasta recibir diagnósticos para este texto exacto.',
      diagnosticsRejected: 'diagnóstico(s) inválido(s) ignorado(s)',
      errors: 'errores',
      warnings: 'avisos',
      entryPointContext: 'Handlers canónicos',
      noEntryPoints: 'No hay entry points declarados.',
      apiHelp: 'Client Visual API v1',
      apiHelpHint: 'Capacidades públicas estables del bridge. El editor no inventa el nombre final del módulo Python.',
      serverScopeHint: 'Los Scripts Server no usan la API Client Visual. Server Python queda fuera de Wave 06.',
      sourceAuthority: 'La fuente editada permanece en el draft canónico y solo llega a Working mediante Preview / Apply / CAS.',
      editorUnavailable: 'Monaco no pudo iniciarse.'
    }
  } as const;

  return copies[locale];
}
