import type { EngineeringLocale } from '../i18n';

export function scriptPythonPreviewCopy(locale: EngineeringLocale) {
  const copies = {
    'pt-BR': {
      title: 'Preview Python controlado',
      hint: 'Executa somente o draft Client Visual no sandbox desta sessão. Não salva, publica, ativa nem altera a revisão de Engineering. TAGs continuam somente leitura e Client Memory permanece local a este cliente.',
      handler: 'Handler para teste',
      run: 'Testar handler',
      running: 'Executando no sandbox…',
      completed: 'Handler concluído no sandbox.',
      failed: 'O handler terminou com falha controlada',
      compileFailed: 'A compilação Python encontrou erros. Corrija os marcadores antes do Preview canônico.',
      compileUnavailable: 'Não foi possível executar a compilação Python no sandbox.',
      unavailable: 'Não foi possível executar o preview Python.',
      noHandler: 'Adicione um entry point válido para testar um handler.'
    },
    en: {
      title: 'Controlled Python preview',
      hint: 'Runs only the Client Visual draft in this session sandbox. It does not save, publish, activate, or change the Engineering revision. TAGs remain read-only and Client Memory remains local to this client.',
      handler: 'Handler to test',
      run: 'Test handler',
      running: 'Running in sandbox…',
      completed: 'Handler completed in the sandbox.',
      failed: 'The handler ended with a controlled failure',
      compileFailed: 'Python compilation found errors. Fix the markers before canonical Preview.',
      compileUnavailable: 'Python compilation could not run in the sandbox.',
      unavailable: 'Python preview could not run.',
      noHandler: 'Add a valid entry point to test a handler.'
    },
    es: {
      title: 'Preview Python controlado',
      hint: 'Ejecuta solo el draft Client Visual en el sandbox de esta sesión. No guarda, publica, activa ni cambia la revisión de Engineering. Las TAGs siguen siendo de solo lectura y Client Memory permanece local a este cliente.',
      handler: 'Handler para probar',
      run: 'Probar handler',
      running: 'Ejecutando en el sandbox…',
      completed: 'Handler finalizado en el sandbox.',
      failed: 'El handler terminó con una falla controlada',
      compileFailed: 'La compilación Python encontró errores. Corrija los marcadores antes del Preview canónico.',
      compileUnavailable: 'No fue posible ejecutar la compilación Python en el sandbox.',
      unavailable: 'No fue posible ejecutar el preview Python.',
      noHandler: 'Agregue un entry point válido para probar un handler.'
    }
  } as const;

  return copies[locale];
}
