import type { EngineeringLocale } from '../i18n';

export function pythonEditorCopy(locale: EngineeringLocale) {
  const copies = {
    'pt-BR': {
      editorLabel: 'Editor de código Python',
      diagnosticsReady: 'Diagnósticos conectados',
      diagnosticsChecking: 'CHECKING · verificando sintaxe…',
      diagnosticsValid: 'VALID · sintaxe válida',
      diagnosticsError: 'ERROR · erro de sintaxe',
      diagnosticsList: 'Diagnósticos de sintaxe com navegação por linha e coluna',
      diagnosticsUnavailable: 'VALIDATOR_UNAVAILABLE · o validador Python não está disponível. A pré-visualização não deve tratar este estado como válido.',
      diagnosticsStale: 'STALE · a fonte mudou depois da última validação. Os marcadores anteriores foram descartados.',
      diagnosticsRejected: 'diagnóstico(s) inválido(s) ignorado(s)',
      errors: 'erros',
      warnings: 'avisos',
      entryPointContext: 'Handlers disponíveis',
      noEntryPoints: 'Nenhum entry point declarado.',
      apiHelp: 'Client Visual API v1',
      apiHelpHint: 'Capacidades públicas estáveis do bridge. O editor não inventa nomes privados de API.',
      serverScopeHint: 'Scripts Server usam o parser CPython isolado para validação de sintaxe e mantêm a autoridade do Runtime Server.',
      sourceAuthority: 'A fonte editada permanece no rascunho e só entra na Área de trabalho após pré-visualização e aplicação validadas.',
      editorUnavailable: 'O editor de código não pôde ser iniciado.'
    },
    en: {
      editorLabel: 'Python code editor',
      diagnosticsReady: 'Diagnostics connected',
      diagnosticsChecking: 'CHECKING · validating syntax…',
      diagnosticsValid: 'VALID · syntax is valid',
      diagnosticsError: 'ERROR · syntax error',
      diagnosticsList: 'Syntax diagnostics with line and column navigation',
      diagnosticsUnavailable: 'VALIDATOR_UNAVAILABLE · the Python validator is unavailable. Preview must not treat this state as valid.',
      diagnosticsStale: 'STALE · source changed after the last validation. Previous markers were discarded.',
      diagnosticsRejected: 'invalid diagnostic(s) ignored',
      errors: 'errors',
      warnings: 'warnings',
      entryPointContext: 'Available handlers',
      noEntryPoints: 'No entry points declared.',
      apiHelp: 'Client Visual API v1',
      apiHelpHint: 'Stable public bridge capabilities. The editor does not invent private API names.',
      serverScopeHint: 'Server Scripts use the isolated CPython parser for syntax validation while Server Runtime authority remains unchanged.',
      sourceAuthority: 'Edited source remains in the draft and reaches the Workspace only after validated Preview and Apply.',
      editorUnavailable: 'The code editor could not be initialized.'
    },
    es: {
      editorLabel: 'Editor de código Python',
      diagnosticsReady: 'Diagnósticos conectados',
      diagnosticsChecking: 'CHECKING · validando sintaxis…',
      diagnosticsValid: 'VALID · sintaxis válida',
      diagnosticsError: 'ERROR · error de sintaxis',
      diagnosticsList: 'Diagnósticos de sintaxis con navegación por línea y columna',
      diagnosticsUnavailable: 'VALIDATOR_UNAVAILABLE · el validador Python no está disponible. La vista previa no debe tratar este estado como válido.',
      diagnosticsStale: 'STALE · la fuente cambió después de la última validación. Los marcadores anteriores fueron descartados.',
      diagnosticsRejected: 'diagnóstico(s) inválido(s) ignorado(s)',
      errors: 'errores',
      warnings: 'avisos',
      entryPointContext: 'Handlers canónicos',
      noEntryPoints: 'No hay entry points declarados.',
      apiHelp: 'Client Visual API v1',
      apiHelpHint: 'Capacidades públicas estables del bridge. El editor no inventa nombres privados de API.',
      serverScopeHint: 'Los Scripts Server usan el parser CPython aislado para validar sintaxis y conservan la autoridad del Runtime Server.',
      sourceAuthority: 'La fuente editada permanece en el borrador y solo llega al Área de trabajo después de una vista previa y aplicación validadas.',
      editorUnavailable: 'El editor de código no pudo iniciarse.'
    }
  } as const;

  return copies[locale];
}
