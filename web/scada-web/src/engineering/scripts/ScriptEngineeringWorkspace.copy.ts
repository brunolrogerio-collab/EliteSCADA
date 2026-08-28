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
      subtitle: 'Scripts canônicos versionados. Esta etapa edita definição e fonte em texto simples; execução Python pertence à Wave 06.',
      refresh: 'Atualizar',
      newScript: 'Novo Script',
      search: 'Buscar por nome, path ou descrição',
      empty: 'Nenhum Script canônico configurado.',
      selectHint: 'Selecione um Script ou crie um novo.',
      working: 'Working',
      dirty: 'Alterações não salvas',
      clean: 'Sem alterações pendentes',
      name: 'Nome',
      path: 'Path',
      scope: 'Escopo',
      enabled: 'Habilitado',
      description: 'Descrição',
      language: 'Linguagem',
      source: 'Fonte Python',
      sourceHint: 'Editor simples desta wave. Sem execução, autocomplete ou sandbox ainda.',
      entryPoints: 'Entry points',
      entryPointsHint: 'Declara eventos e handlers do Script. Associações visuais existentes são preservadas separadamente.',
      addEntryPoint: 'Adicionar entry point',
      event: 'Evento',
      handler: 'Handler',
      target: 'Referência alvo opcional',
      dependencies: 'Dependências',
      dependenciesHint: 'Use IDs/referências estáveis do modelo canônico. O backend valida existência, escopo e ciclos.',
      addDependency: 'Adicionar dependência',
      kind: 'Tipo',
      stableReference: 'Referência estável',
      remove: 'Remover',
      visualReferences: 'Associações visuais preservadas',
      visualReferencesHint: 'Estas referências são reenviadas no Apply para evitar perda silenciosa. A edição visual pertence às waves posteriores.',
      preview: 'Validar / Preview',
      apply: 'Aplicar Preview',
      delete: 'Excluir Script',
      cancel: 'Cancelar',
      confirmDelete: 'Confirmar exclusão',
      deleteWarning: 'A exclusão usa CAS e será recusada se outro Script ainda depender deste.',
      previewReady: 'Preview válido. O Apply usará exatamente o pacote e a versão do Working validados.',
      previewInvalid: 'O Preview contém erros e não pode ser aplicado.',
      previewExpired: 'O formulário mudou depois do Preview. Valide novamente antes de aplicar.',
      created: 'Script criado no Working.',
      updated: 'Script atualizado no Working.',
      deleted: 'Script removido do Working.',
      createMode: 'Criação',
      updateMode: 'Atualização',
      noExecution: 'Sem execução Python nesta wave',
      validationTitle: 'Corrija o formulário antes do Preview',
      validation: {
        id: 'ID estável ausente.',
        name: 'Nome é obrigatório.',
        path: "Path é obrigatório, deve estar sem espaços nas extremidades e usar '/' em vez de '\\'.",
        source: 'Fonte Python não pode ficar vazia.',
        languageVersion: 'Versão da linguagem é obrigatória.',
        entryPoint: 'Handler de entry point deve ser um identificador Python válido.',
        entryPointDuplicate: 'Há entry points duplicados.',
        dependency: 'Dependência exige uma referência estável.',
        dependencyDuplicate: 'Há dependências duplicadas.'
      },
      errors: {
        unauthorized: 'Sessão ausente ou expirada. Entre novamente para continuar.',
        forbidden: 'Seu usuário não possui permissão de Engenharia para esta operação.',
        conflict: 'O Working mudou desde o Preview. Atualize, valide novamente e só então aplique.',
        deleteConflict: 'Este Script ainda possui dependências e não pode ser excluído.',
        badRequest: 'O backend rejeitou a definição do Script. Consulte os erros do Preview.',
        unavailable: 'A API de Engenharia está indisponível.',
        generic: 'Não foi possível concluir a operação de Script.'
      }
    },
    en: {
      title: 'Engineering Scripts', subtitle: 'Versioned canonical Scripts. This wave edits definitions and plain-text source; Python execution belongs to Wave 06.', refresh: 'Refresh', newScript: 'New Script', search: 'Search by name, path, or description', empty: 'No canonical Scripts configured.', selectHint: 'Select a Script or create a new one.', working: 'Working', dirty: 'Unsaved changes', clean: 'No pending changes', name: 'Name', path: 'Path', scope: 'Scope', enabled: 'Enabled', description: 'Description', language: 'Language', source: 'Python source', sourceHint: 'Plain editor for this wave. No execution, autocomplete, or sandbox yet.', entryPoints: 'Entry points', entryPointsHint: 'Declares Script events and handlers. Existing visual associations are preserved separately.', addEntryPoint: 'Add entry point', event: 'Event', handler: 'Handler', target: 'Optional target reference', dependencies: 'Dependencies', dependenciesHint: 'Use stable canonical IDs/references. The backend validates existence, scope, and cycles.', addDependency: 'Add dependency', kind: 'Kind', stableReference: 'Stable reference', remove: 'Remove', visualReferences: 'Preserved visual associations', visualReferencesHint: 'These references are sent back on Apply to prevent silent loss. Visual association editing belongs to later waves.', preview: 'Validate / Preview', apply: 'Apply Preview', delete: 'Delete Script', cancel: 'Cancel', confirmDelete: 'Confirm delete', deleteWarning: 'Delete uses CAS and is rejected while another Script still depends on this one.', previewReady: 'Preview is valid. Apply will use the exact package and Working version that were validated.', previewInvalid: 'Preview contains errors and cannot be applied.', previewExpired: 'The form changed after Preview. Validate again before applying.', created: 'Script created in Working.', updated: 'Script updated in Working.', deleted: 'Script removed from Working.', createMode: 'Create', updateMode: 'Update', noExecution: 'No Python execution in this wave', validationTitle: 'Fix the form before Preview', validation: { id: 'Stable ID is missing.', name: 'Name is required.', path: "Path is required, must be trimmed, and must use '/' instead of '\\'.", source: 'Python source cannot be empty.', languageVersion: 'Language version is required.', entryPoint: 'Entry-point handler must be a valid Python identifier.', entryPointDuplicate: 'Duplicate entry points are present.', dependency: 'A dependency requires a stable reference.', dependencyDuplicate: 'Duplicate dependencies are present.' }, errors: { unauthorized: 'Your session is missing or expired. Sign in again to continue.', forbidden: 'Your user does not have Engineering permission for this operation.', conflict: 'Working changed after Preview. Refresh and validate again before applying.', deleteConflict: 'This Script still has dependencies and cannot be deleted.', badRequest: 'The backend rejected the Script definition. Review the Preview errors.', unavailable: 'The Engineering API is unavailable.', generic: 'The Script operation could not be completed.' }
    },
    es: {
      title: 'Scripts de Ingeniería', subtitle: 'Scripts canónicos versionados. Esta etapa edita definición y fuente en texto simple; la ejecución Python pertenece a Wave 06.', refresh: 'Actualizar', newScript: 'Nuevo Script', search: 'Buscar por nombre, path o descripción', empty: 'No hay Scripts canónicos configurados.', selectHint: 'Seleccione un Script o cree uno nuevo.', working: 'Working', dirty: 'Cambios sin guardar', clean: 'Sin cambios pendientes', name: 'Nombre', path: 'Path', scope: 'Ámbito', enabled: 'Habilitado', description: 'Descripción', language: 'Lenguaje', source: 'Fuente Python', sourceHint: 'Editor simple de esta wave. Aún sin ejecución, autocompletado ni sandbox.', entryPoints: 'Entry points', entryPointsHint: 'Declara eventos y handlers del Script. Las asociaciones visuales existentes se conservan por separado.', addEntryPoint: 'Agregar entry point', event: 'Evento', handler: 'Handler', target: 'Referencia de destino opcional', dependencies: 'Dependencias', dependenciesHint: 'Use IDs/referencias estables del modelo canónico. El backend valida existencia, ámbito y ciclos.', addDependency: 'Agregar dependencia', kind: 'Tipo', stableReference: 'Referencia estable', remove: 'Eliminar', visualReferences: 'Asociaciones visuales preservadas', visualReferencesHint: 'Estas referencias se reenvían en Apply para evitar pérdidas silenciosas. Su edición visual pertenece a waves posteriores.', preview: 'Validar / Preview', apply: 'Aplicar Preview', delete: 'Eliminar Script', cancel: 'Cancelar', confirmDelete: 'Confirmar eliminación', deleteWarning: 'La eliminación usa CAS y será rechazada si otro Script todavía depende de este.', previewReady: 'Preview válido. Apply usará exactamente el paquete y la versión de Working validados.', previewInvalid: 'El Preview contiene errores y no puede aplicarse.', previewExpired: 'El formulario cambió después del Preview. Valide nuevamente antes de aplicar.', created: 'Script creado en Working.', updated: 'Script actualizado en Working.', deleted: 'Script eliminado de Working.', createMode: 'Creación', updateMode: 'Actualización', noExecution: 'Sin ejecución Python en esta wave', validationTitle: 'Corrija el formulario antes del Preview', validation: { id: 'Falta el ID estable.', name: 'El nombre es obligatorio.', path: "El path es obligatorio, debe estar recortado y usar '/' en lugar de '\\'.", source: 'La fuente Python no puede estar vacía.', languageVersion: 'La versión del lenguaje es obligatoria.', entryPoint: 'El handler debe ser un identificador Python válido.', entryPointDuplicate: 'Hay entry points duplicados.', dependency: 'La dependencia requiere una referencia estable.', dependencyDuplicate: 'Hay dependencias duplicadas.' }, errors: { unauthorized: 'La sesión falta o expiró. Inicie sesión nuevamente.', forbidden: 'Su usuario no tiene permiso de Ingeniería para esta operación.', conflict: 'Working cambió después del Preview. Actualice y valide de nuevo antes de aplicar.', deleteConflict: 'Este Script aún tiene dependencias y no puede eliminarse.', badRequest: 'El backend rechazó la definición del Script. Revise los errores del Preview.', unavailable: 'La API de Ingeniería no está disponible.', generic: 'No se pudo completar la operación de Script.' }
    }
  } as const;
  return copies[locale];
}

export function scopeLabel(scope: ScriptEngineeringScope, locale: EngineeringLocale): string {
  const labels = {
    'pt-BR': { clientVisual: 'Client Visual', server: 'Server' },
    en: { clientVisual: 'Client Visual', server: 'Server' },
    es: { clientVisual: 'Client Visual', server: 'Server' }
  } as const;
  return labels[locale][scope];
}

export function eventKindLabel(kind: ScriptEngineeringEventKind): string {
  const labels: Record<ScriptEngineeringEventKind, string> = {
    initialize: 'Initialize', dispose: 'Dispose', objectInteraction: 'Object Interaction', tagChanged: 'TAG Changed', clientMemoryChanged: 'Client Memory Changed', timer: 'Timer', propertyChanged: 'Property Changed', frameTick: 'Frame Tick', serverRuntimeEvent: 'Server Runtime Event'
  };
  return labels[kind];
}

export function dependencyKindLabel(kind: ScriptEngineeringDependencyKind): string {
  const labels: Record<ScriptEngineeringDependencyKind, string> = {
    script: 'Script', visualDefinition: 'Visual Definition', visualObject: 'Visual Object', tag: 'TAG', clientMemoryTag: 'Client Memory TAG', serverMemoryTag: 'Server Memory TAG', resource: 'Resource'
  };
  return labels[kind];
}