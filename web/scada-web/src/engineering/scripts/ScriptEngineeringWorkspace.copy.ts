import type { EngineeringLocale } from '../i18n';
import type {
  ScriptEngineeringDependencyKind,
  ScriptEngineeringEventKind,
  ScriptEngineeringScope
} from './scriptEngineeringTypes';

export type ScriptWorkspaceCopy = ReturnType<typeof scriptWorkspaceCopy>;

export function scriptWorkspaceCopy(locale: EngineeringLocale) {
  const copies = {
    'pt-BR': {
      title: 'Scripts de Engenharia',
      subtitle: 'Scripts versionados com editor de código Python guiado. A execução pertence ao ambiente isolado do produto, não ao editor.',
      refresh: 'Atualizar',
      newScript: 'Novo Script',
      search: 'Buscar por nome, caminho ou descrição',
      empty: 'Nenhum Script configurado.',
      selectHint: 'Selecione um Script ou crie um novo.',
      working: 'Em edição',
      dirty: 'Alterações não salvas',
      clean: 'Sem alterações pendentes',
      name: 'Nome',
      path: 'Caminho',
      scope: 'Escopo',
      enabled: 'Habilitado',
      disabled: 'Desabilitado',
      description: 'Descrição',
      language: 'Linguagem',
      source: 'Fonte Python',
      sourceHint: 'O editor de código altera somente a fonte do rascunho. A Área de trabalho só muda depois de uma pré-visualização validada e da aplicação.',
      entryPoints: 'Entry points',
      entryPointsHint: 'Declara eventos e handlers do Script. Associações visuais existentes são preservadas separadamente.',
      addEntryPoint: 'Adicionar entry point',
      event: 'Evento',
      handler: 'Handler',
      target: 'Referência alvo opcional',
      dependencies: 'Dependências',
      dependenciesHint: 'Use IDs/referências estáveis do projeto. O sistema valida existência, escopo e ciclos.',
      addDependency: 'Adicionar dependência',
      kind: 'Tipo',
      stableReference: 'Referência estável',
      remove: 'Remover',
      visualReferences: 'Associações visuais preservadas',
      visualReferencesHint: 'Estas referências são reenviadas no Apply para evitar perda silenciosa. A edição visual pertence às waves posteriores.',
      preview: 'Validar / Pré-visualizar',
      apply: 'Aplicar pré-visualização',
      delete: 'Excluir Script',
      cancel: 'Cancelar',
      confirmDelete: 'Confirmar exclusão',
      deleteWarning: 'A exclusão usa CAS e será recusada se outro Script ainda depender deste.',
      previewReady: 'Pré-visualização válida. A aplicação usará exatamente o pacote e a versão da Área de trabalho validados.',
      previewInvalid: 'A pré-visualização contém erros e não pode ser aplicada.',
      previewExpired: 'O formulário mudou depois da pré-visualização. Valide novamente antes de aplicar.',
      previewCreate: 'Criar',
      previewUpdate: 'Atualizar',
      previewSkip: 'Ignorar',
      previewErrors: 'Erros',
      created: 'Script criado na Área de trabalho.',
      updated: 'Script atualizado na Área de trabalho.',
      deleted: 'Script removido da Área de trabalho.',
      createMode: 'Criação',
      updateMode: 'Atualização',
      noExecution: 'Editor sem autoridade de execução',
      validationTitle: 'Corrija o formulário antes da pré-visualização',
      validation: {
        id: 'ID estável ausente.',
        name: 'Nome é obrigatório.',
        path: "Caminho é obrigatório, deve estar sem espaços nas extremidades e usar '/' em vez de '\\'.",
        source: 'Fonte Python não pode ficar vazia.',
        languageVersion: 'Versão da linguagem é obrigatória.',
        entryPoint: 'Handler de entry point deve ser um identificador Python válido.',
        entryPointDuplicate: 'Há entry points duplicados.',
        entryPointScope: 'O evento selecionado não é suportado pelo escopo deste Script.',
        timerIntervalMs: 'Timer exige intervalo inteiro de pelo menos 50 ms.',
        timerIntervalUnexpected: 'Intervalo de Timer só pode ser usado no evento Timer.',
        tagReference: 'TAG alterada exige uma referência estável por TagId.',
        tagReferenceUnexpected: 'Referência de TAG só pode ser usada no evento TAG alterada.',
        tagSelector: 'O seletor de TAG deve ser um bit com índice inteiro não negativo.',
        targetReference: 'Client Memory alterada exige um ID estável de definição.',
        targetReferenceUnexpected: 'Este evento não aceita referência alvo neste campo.',
        dependency: 'Dependência exige uma referência estável.',
        dependencyDuplicate: 'Há dependências duplicadas.'
      },
      errors: {
        unauthorized: 'Sessão ausente ou expirada. Entre novamente para continuar.',
        forbidden: 'Seu usuário não possui permissão de Engenharia para esta operação.',
        conflict: 'A Área de trabalho mudou desde a pré-visualização. Atualize, valide novamente e só então aplique.',
        deleteConflict: 'Este Script ainda possui dependências e não pode ser excluído.',
        badRequest: 'O sistema rejeitou a definição do Script. Consulte os erros da pré-visualização.',
        unavailable: 'A API de Engenharia está indisponível.',
        generic: 'Não foi possível concluir a operação de Script.'
      }
    },
    en: {
      title: 'Engineering Scripts', subtitle: 'Versioned Scripts with guided Python code editing. Execution runs in the product isolated environment, not in the editor.', refresh: 'Refresh', newScript: 'New Script', search: 'Search by name, path, or description', empty: 'No Scripts configured.', selectHint: 'Select a Script or create a new one.', working: 'Workspace', dirty: 'Unsaved changes', clean: 'No pending changes', name: 'Name', path: 'Path', scope: 'Scope', enabled: 'Enabled', disabled: 'Disabled', description: 'Description', language: 'Language', source: 'Python source', sourceHint: 'The code editor changes only the draft source. The Workspace changes only after validated Preview and Apply.', entryPoints: 'Entry points', entryPointsHint: 'Declares Script events and handlers. Existing visual associations are preserved separately.', addEntryPoint: 'Add entry point', event: 'Event', handler: 'Handler', target: 'Optional target reference', dependencies: 'Dependencies', dependenciesHint: 'Use stable project IDs/references. The system validates existence, scope, and cycles.', addDependency: 'Add dependency', kind: 'Kind', stableReference: 'Stable reference', remove: 'Remove', visualReferences: 'Preserved visual associations', visualReferencesHint: 'These references are sent back on Apply to prevent silent loss. Visual associations are edited from their dedicated authoring surface.', preview: 'Validate / Preview', apply: 'Apply Preview', delete: 'Delete Script', cancel: 'Cancel', confirmDelete: 'Confirm delete', deleteWarning: 'Delete uses CAS and is rejected while another Script still depends on this one.', previewReady: 'Preview is valid. Apply will use the exact package and Working version that were validated.', previewInvalid: 'Preview contains errors and cannot be applied.', previewExpired: 'The form changed after Preview. Validate again before applying.', previewCreate: 'Create', previewUpdate: 'Update', previewSkip: 'Skip', previewErrors: 'Errors', created: 'Script created in Working.', updated: 'Script updated in Working.', deleted: 'Script removed from Working.', createMode: 'Create', updateMode: 'Update', noExecution: 'Editor has no execution authority', validationTitle: 'Fix the form before Preview', validation: { id: 'Stable ID is missing.', name: 'Name is required.', path: "Path is required, must be trimmed, and must use '/' instead of '\\'.", source: 'Python source cannot be empty.', languageVersion: 'Language version is required.', entryPoint: 'Entry-point handler must be a valid Python identifier.', entryPointDuplicate: 'Duplicate entry points are present.', entryPointScope: 'The selected event is not supported by this Script scope.', timerIntervalMs: 'Timer requires an integer interval of at least 50 ms.', timerIntervalUnexpected: 'Timer interval is only valid for the Timer event.', tagReference: 'TAG Changed requires a stable TagId reference.', tagReferenceUnexpected: 'A TAG reference is only valid for TAG Changed.', tagSelector: 'The TAG selector must be a bit with a non-negative integer index.', targetReference: 'Client Memory Changed requires a stable definition ID.', targetReferenceUnexpected: 'This event does not accept a target reference in this field.', dependency: 'A dependency requires a stable reference.', dependencyDuplicate: 'Duplicate dependencies are present.' }, errors: { unauthorized: 'Your session is missing or expired. Sign in again to continue.', forbidden: 'Your user does not have Engineering permission for this operation.', conflict: 'The Workspace changed after Preview. Refresh and validate again before applying.', deleteConflict: 'This Script still has dependencies and cannot be deleted.', badRequest: 'The Script definition was rejected. Review the Preview errors.', unavailable: 'Engineering services are unavailable.', generic: 'The Script operation could not be completed.' }
    },
    es: {
      title: 'Scripts de Ingeniería', subtitle: 'Scripts versionados con editor de código Python guiado. La ejecución se realiza en el entorno aislado del producto, no en el editor.', refresh: 'Actualizar', newScript: 'Nuevo Script', search: 'Buscar por nombre, ruta o descripción', empty: 'No hay Scripts configurados.', selectHint: 'Seleccione un Script o cree uno nuevo.', working: 'En edición', dirty: 'Cambios sin guardar', clean: 'Sin cambios pendientes', name: 'Nombre', path: 'Ruta', scope: 'Ámbito', enabled: 'Habilitado', disabled: 'Deshabilitado', description: 'Descripción', language: 'Lenguaje', source: 'Fuente Python', sourceHint: 'El editor de código modifica solamente la fuente del borrador. El Área de trabajo cambia solo después de una vista previa validada y de aplicar.', entryPoints: 'Entry points', entryPointsHint: 'Declara eventos y handlers del Script. Las asociaciones visuales existentes se conservan por separado.', addEntryPoint: 'Agregar entry point', event: 'Evento', handler: 'Handler', target: 'Referencia de destino opcional', dependencies: 'Dependencias', dependenciesHint: 'Use IDs/referencias estables del proyecto. El sistema valida existencia, ámbito y ciclos.', addDependency: 'Agregar dependencia', kind: 'Tipo', stableReference: 'Referencia estable', remove: 'Eliminar', visualReferences: 'Asociaciones visuales preservadas', visualReferencesHint: 'Estas referencias se reenvían al aplicar para evitar pérdidas silenciosas. Las asociaciones visuales se editan desde su superficie de autoría dedicada.', preview: 'Validar / Vista previa', apply: 'Aplicar vista previa', delete: 'Eliminar Script', cancel: 'Cancelar', confirmDelete: 'Confirmar eliminación', deleteWarning: 'La eliminación usa CAS y será rechazada si otro Script todavía depende de este.', previewReady: 'Vista previa válida. La aplicación usará exactamente el paquete y la versión del Área de trabajo validados.', previewInvalid: 'La vista previa contiene errores y no puede aplicarse.', previewExpired: 'El formulario cambió después de la vista previa. Valide nuevamente antes de aplicar.', previewCreate: 'Crear', previewUpdate: 'Actualizar', previewSkip: 'Omitir', previewErrors: 'Errores', created: 'Script creado en el Área de trabajo.', updated: 'Script actualizado en el Área de trabajo.', deleted: 'Script eliminado del Área de trabajo.', createMode: 'Creación', updateMode: 'Actualización', noExecution: 'El editor no tiene autoridad de ejecución', validationTitle: 'Corrija el formulario antes de la vista previa', validation: { id: 'Falta el ID estable.', name: 'El nombre es obligatorio.', path: "La ruta es obligatoria, debe estar recortada y usar '/' en lugar de '\\'.", source: 'La fuente Python no puede estar vacía.', languageVersion: 'La versión del lenguaje es obligatoria.', entryPoint: 'El handler debe ser un identificador Python válido.', entryPointDuplicate: 'Hay entry points duplicados.', entryPointScope: 'El evento seleccionado no es compatible con el ámbito de este Script.', timerIntervalMs: 'Timer requiere un intervalo entero de al menos 50 ms.', timerIntervalUnexpected: 'El intervalo de Timer solo es válido para el evento Timer.', tagReference: 'TAG modificada requiere una referencia estable por TagId.', tagReferenceUnexpected: 'La referencia TAG solo es válida para TAG modificada.', tagSelector: 'El selector TAG debe ser un bit con índice entero no negativo.', targetReference: 'Client Memory modificada requiere un ID estable de definición.', targetReferenceUnexpected: 'Este evento no acepta una referencia de destino en este campo.', dependency: 'La dependencia requiere una referencia estable.', dependencyDuplicate: 'Hay dependencias duplicadas.' }, errors: { unauthorized: 'La sesión falta o expiró. Inicie sesión nuevamente.', forbidden: 'Su usuario no tiene permiso de Ingeniería para esta operación.', conflict: 'El Área de trabajo cambió después de la vista previa. Actualice y valide de nuevo antes de aplicar.', deleteConflict: 'Este Script aún tiene dependencias y no puede eliminarse.', badRequest: 'El sistema rechazó la definición del Script. Revise los errores de la vista previa.', unavailable: 'La API de Ingeniería no está disponible.', generic: 'No se pudo completar la operación de Script.' }
    }
  } as const;
  return copies[locale];
}

export function scopeLabel(scope: ScriptEngineeringScope, locale: EngineeringLocale): string {
  const labels = {
    'pt-BR': { clientVisual: 'Cliente visual', server: 'Servidor' },
    en: { clientVisual: 'Client Visual', server: 'Server' },
    es: { clientVisual: 'Cliente visual', server: 'Servidor' }
  } as const;
  return labels[locale][scope];
}

export function eventKindLabel(kind: ScriptEngineeringEventKind, locale: EngineeringLocale): string {
  const labels: Record<EngineeringLocale, Record<ScriptEngineeringEventKind, string>> = {
    'pt-BR': {
      initialize: 'Inicializar', dispose: 'Finalizar', objectInteraction: 'Interação com objeto', tagChanged: 'TAG alterada', clientMemoryChanged: 'Client Memory alterada', timer: 'Temporizador', propertyChanged: 'Propriedade alterada', frameTick: 'Tick de frame', serverRuntimeEvent: 'Evento de Server Runtime'
    },
    en: {
      initialize: 'Initialize', dispose: 'Dispose', objectInteraction: 'Object Interaction', tagChanged: 'TAG Changed', clientMemoryChanged: 'Client Memory Changed', timer: 'Timer', propertyChanged: 'Property Changed', frameTick: 'Frame Tick', serverRuntimeEvent: 'Server Runtime Event'
    },
    es: {
      initialize: 'Inicializar', dispose: 'Finalizar', objectInteraction: 'Interacción con objeto', tagChanged: 'TAG modificada', clientMemoryChanged: 'Client Memory modificada', timer: 'Temporizador', propertyChanged: 'Propiedad modificada', frameTick: 'Tick de frame', serverRuntimeEvent: 'Evento de Server Runtime'
    }
  };
  return labels[locale][kind];
}

export function dependencyKindLabel(kind: ScriptEngineeringDependencyKind, locale: EngineeringLocale): string {
  const labels: Record<EngineeringLocale, Record<ScriptEngineeringDependencyKind, string>> = {
    'pt-BR': {
      script: 'Script', visualDefinition: 'Definição visual', visualObject: 'Objeto visual', tag: 'TAG', clientMemoryTag: 'TAG de Client Memory', serverMemoryTag: 'TAG de Server Memory', resource: 'Recurso'
    },
    en: {
      script: 'Script', visualDefinition: 'Visual Definition', visualObject: 'Visual Object', tag: 'TAG', clientMemoryTag: 'Client Memory TAG', serverMemoryTag: 'Server Memory TAG', resource: 'Resource'
    },
    es: {
      script: 'Script', visualDefinition: 'Definición visual', visualObject: 'Objeto visual', tag: 'TAG', clientMemoryTag: 'TAG de Client Memory', serverMemoryTag: 'TAG de Server Memory', resource: 'Recurso'
    }
  };
  return labels[locale][kind];
}
