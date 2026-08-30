import type { EngineeringLocale } from '../i18n';

export function scriptPythonPreviewCopy(locale: EngineeringLocale) {
  const copies = {
    'pt-BR': {
      title: 'Preview/Test Python controlado',
      hint: 'Executa somente o draft Client Visual no sandbox desta sessão. Não salva, publica, ativa nem altera a revisão de Engineering. TAGs continuam somente leitura e Client Memory permanece local a este cliente.',
      handler: 'Handler para teste',
      samplePayload: 'Payload de evento (JSON limitado)',
      run: 'Executar Preview/Test',
      cancel: 'Cancelar execução',
      running: 'Executando no sandbox…',
      completed: 'Handler concluído no sandbox.',
      failed: 'O handler terminou com falha controlada',
      compileFailed: 'A compilação Python encontrou erros. Corrija os marcadores antes do Preview canônico.',
      compileUnavailable: 'Não foi possível executar a compilação Python no sandbox.',
      unavailable: 'Não foi possível executar o Preview/Test Python.',
      noHandler: 'Adicione um entry point válido para testar um handler.',
      duration: 'Duração',
      error: 'Falha segura',
      traceback: 'Traceback seguro',
      line: 'linha',
      blankLine: '<linha em branco>',
      states: {
        idle: 'Pronto para executar.',
        running: 'Execução em andamento.',
        success: 'Execução concluída com sucesso.',
        'validation-error': 'Validação impediu a execução.',
        'runtime-error': 'Execução terminou com erro controlado.',
        'timed-out': 'Execução excedeu o limite de tempo.',
        cancelled: 'Execução cancelada.',
        unavailable: 'Contexto de execução indisponível.'
      },
      sampleErrors: {
        empty: 'Informe um payload JSON para o evento.',
        'too-large': 'O payload excede o limite do Preview/Test.',
        'invalid-json': 'O payload precisa ser JSON válido e finito.',
        'too-deep': 'O payload excede a profundidade permitida.',
        'too-many-values': 'O payload contém valores demais para o Preview/Test.',
        'unsupported-key': 'O payload contém uma chave reservada não permitida.'
      }
    },
    en: {
      title: 'Controlled Python Preview/Test',
      hint: 'Runs only the Client Visual draft in this session sandbox. It does not save, publish, activate, or change the Engineering revision. TAGs remain read-only and Client Memory remains local to this client.',
      handler: 'Handler to test',
      samplePayload: 'Event payload (bounded JSON)',
      run: 'Run Preview/Test',
      cancel: 'Cancel execution',
      running: 'Running in sandbox…',
      completed: 'Handler completed in the sandbox.',
      failed: 'The handler ended with a controlled failure',
      compileFailed: 'Python compilation found errors. Fix the markers before canonical Preview.',
      compileUnavailable: 'Python compilation could not run in the sandbox.',
      unavailable: 'Python Preview/Test could not run.',
      noHandler: 'Add a valid entry point to test a handler.',
      duration: 'Duration',
      error: 'Safe failure',
      traceback: 'Safe traceback',
      line: 'line',
      blankLine: '<blank line>',
      states: {
        idle: 'Ready to run.',
        running: 'Execution in progress.',
        success: 'Execution completed successfully.',
        'validation-error': 'Validation prevented execution.',
        'runtime-error': 'Execution ended with a controlled error.',
        'timed-out': 'Execution exceeded its time limit.',
        cancelled: 'Execution cancelled.',
        unavailable: 'Execution context unavailable.'
      },
      sampleErrors: {
        empty: 'Provide a JSON event payload.',
        'too-large': 'The payload exceeds the Preview/Test limit.',
        'invalid-json': 'The payload must be valid finite JSON.',
        'too-deep': 'The payload exceeds the allowed depth.',
        'too-many-values': 'The payload contains too many values for Preview/Test.',
        'unsupported-key': 'The payload contains a reserved key that is not allowed.'
      }
    },
    es: {
      title: 'Preview/Test Python controlado',
      hint: 'Ejecuta solo el draft Client Visual en el sandbox de esta sesión. No guarda, publica, activa ni cambia la revisión de Engineering. Las TAGs siguen siendo de solo lectura y Client Memory permanece local a este cliente.',
      handler: 'Handler para probar',
      samplePayload: 'Payload del evento (JSON limitado)',
      run: 'Ejecutar Preview/Test',
      cancel: 'Cancelar ejecución',
      running: 'Ejecutando en el sandbox…',
      completed: 'Handler finalizado en el sandbox.',
      failed: 'El handler terminó con una falla controlada',
      compileFailed: 'La compilación Python encontró errores. Corrija los marcadores antes del Preview canónico.',
      compileUnavailable: 'No fue posible ejecutar la compilación Python en el sandbox.',
      unavailable: 'No fue posible ejecutar el Preview/Test Python.',
      noHandler: 'Agregue un entry point válido para probar un handler.',
      duration: 'Duración',
      error: 'Falla segura',
      traceback: 'Traceback seguro',
      line: 'línea',
      blankLine: '<línea en blanco>',
      states: {
        idle: 'Listo para ejecutar.',
        running: 'Ejecución en curso.',
        success: 'Ejecución finalizada correctamente.',
        'validation-error': 'La validación impidió la ejecución.',
        'runtime-error': 'La ejecución terminó con un error controlado.',
        'timed-out': 'La ejecución excedió el límite de tiempo.',
        cancelled: 'Ejecución cancelada.',
        unavailable: 'Contexto de ejecución no disponible.'
      },
      sampleErrors: {
        empty: 'Informe un payload JSON para el evento.',
        'too-large': 'El payload excede el límite del Preview/Test.',
        'invalid-json': 'El payload debe ser JSON válido y finito.',
        'too-deep': 'El payload excede la profundidad permitida.',
        'too-many-values': 'El payload contiene demasiados valores para el Preview/Test.',
        'unsupported-key': 'El payload contiene una clave reservada no permitida.'
      }
    }
  } as const;

  return copies[locale];
}
