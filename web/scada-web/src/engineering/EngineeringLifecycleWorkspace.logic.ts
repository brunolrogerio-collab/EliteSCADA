import type {
  EngineeringLifecycleAction,
  EngineeringLifecycleState,
  EngineeringProjectLifecycle,
  EngineeringRevisionMetadata
} from './engineeringLifecycleTypes';
import type { EngineeringLocale } from './i18n';

export type LifecycleStepState = 'complete' | 'current' | 'pending' | 'warning';
export type LifecycleStep = { key: 'working' | 'revision' | 'published' | 'active'; state: LifecycleStepState; revision: number | null };

type ApiErrorLike = Error & { status: number };

export function buildLifecycleSteps(state: EngineeringLifecycleState): LifecycleStep[] {
  const lifecycle = state.lifecycle;
  const baseRevision = state.workspace.baseRevision ?? null;
  const latestRevision = state.revisions[0]?.revision ?? baseRevision;
  const publishedRevision = lifecycle?.publishedRevision ?? null;
  const activeRevision = lifecycle?.activeRevision ?? null;
  return [
    { key: 'working', state: state.workspace.isDirty ? 'warning' : 'current', revision: baseRevision },
    { key: 'revision', state: latestRevision ? (state.workspace.isDirty ? 'complete' : 'current') : 'pending', revision: latestRevision ?? null },
    { key: 'published', state: publishedRevision ? (activeRevision === publishedRevision ? 'complete' : 'current') : 'pending', revision: publishedRevision },
    { key: 'active', state: activeRevision ? (state.runtime?.consistent === false ? 'warning' : 'current') : 'pending', revision: activeRevision }
  ];
}

export function canSaveWorkspace(state: EngineeringLifecycleState): boolean {
  if (!state.persistence.enabled || !state.projectKey || !state.workspace.projectName?.trim()) return false;
  return state.workspace.isDirty || !state.workspace.baseRevision;
}

export function canActivatePublished(state: EngineeringLifecycleState): boolean {
  if (!state.persistence.enabled || !state.projectKey || !state.lifecycle?.publishedRevision) return false;
  const configured = state.persistence.configuredProjectKey?.trim();
  if (!configured) return false;
  return configured.toLocaleLowerCase() === state.projectKey.toLocaleLowerCase();
}

export function isRevisionPublished(revision: EngineeringRevisionMetadata, lifecycle: EngineeringProjectLifecycle | null): boolean { return lifecycle?.publishedRevision === revision.revision; }
export function isRevisionActive(revision: EngineeringRevisionMetadata, lifecycle: EngineeringProjectLifecycle | null): boolean { return lifecycle?.activeRevision === revision.revision; }
export function isWorkspaceBaseRevision(revision: EngineeringRevisionMetadata, state: EngineeringLifecycleState): boolean { return state.workspace.baseRevision === revision.revision; }

export function projectLifecycleName(value: string | number | null | undefined): string {
  if (typeof value === 'number') return ['Empty', 'Draft', 'Published', 'ChangesPending'][value] ?? String(value);
  return value ?? 'Unknown';
}

export function runtimeLifecycleName(value: string | number | null | undefined): string {
  if (typeof value === 'number') return ['Inactive', 'ActivationPending', 'Active'][value] ?? String(value);
  return value ?? 'Unknown';
}

export function lifecycleErrorText(error: unknown, locale: EngineeringLocale): string {
  const messages = errorCopy(locale);
  if (isApiError(error)) {
    if (error.status === 401) return messages.unauthorized;
    if (error.status === 403) return messages.forbidden;
    if (error.status === 409) return `${messages.conflict} ${error.message}`.trim();
    if (error.status === 422) return `${messages.validation} ${error.message}`.trim();
    if (error.status === 503) return messages.unavailable;
    return error.message || messages.generic;
  }
  return error instanceof Error ? error.message : messages.generic;
}

export function confirmationText(action: EngineeringLifecycleAction, locale: EngineeringLocale, revision?: number): { title: string; description: string; confirm: string } {
  const copy = confirmationCopy(locale);
  if (action === 'checkout') return { title: copy.checkoutTitle, description: copy.checkoutDescription.replace('{revision}', String(revision ?? '—')), confirm: copy.checkoutConfirm };
  if (action === 'publish') return { title: copy.publishTitle, description: copy.publishDescription.replace('{revision}', String(revision ?? '—')), confirm: copy.publishConfirm };
  if (action === 'activate') return { title: copy.activateTitle, description: copy.activateDescription, confirm: copy.activateConfirm };
  return { title: copy.saveTitle, description: copy.saveDescription, confirm: copy.saveConfirm };
}

function isApiError(error: unknown): error is ApiErrorLike {
  return error instanceof Error && 'status' in error && typeof (error as { status?: unknown }).status === 'number';
}

function errorCopy(locale: EngineeringLocale) {
  if (locale === 'en') return { unauthorized: 'Authentication is required to use Engineering lifecycle operations.', forbidden: 'Your current role is not authorized to modify Engineering lifecycle state.', conflict: 'The lifecycle operation conflicts with the current project/runtime state.', validation: 'The lifecycle operation was validated but could not be completed.', unavailable: 'Engineering persistence is unavailable. Check PostgreSQL/application configuration.', generic: 'The Engineering lifecycle operation failed.' };
  if (locale === 'es') return { unauthorized: 'Se requiere autenticación para usar las operaciones del ciclo de Engineering.', forbidden: 'Su rol actual no está autorizado para modificar el ciclo de Engineering.', conflict: 'La operación entra en conflicto con el estado actual del proyecto/runtime.', validation: 'La operación fue validada pero no pudo completarse.', unavailable: 'La persistencia de Engineering no está disponible. Revise PostgreSQL/configuración.', generic: 'La operación del ciclo de Engineering falló.' };
  return { unauthorized: 'É necessário autenticar para usar as operações do ciclo de Engineering.', forbidden: 'Seu papel atual não está autorizado a modificar o ciclo de Engineering.', conflict: 'A operação entra em conflito com o estado atual do projeto/runtime.', validation: 'A operação foi validada, mas não pôde ser concluída.', unavailable: 'A persistência de Engineering está indisponível. Verifique PostgreSQL/configuração.', generic: 'A operação do ciclo de Engineering falhou.' };
}

function confirmationCopy(locale: EngineeringLocale) {
  if (locale === 'en') return { checkoutTitle: 'Checkout revision?', checkoutDescription: 'Revision {revision} will replace the current Working workspace. Unsaved changes in Working can be lost.', checkoutConfirm: 'Checkout revision', publishTitle: 'Publish revision?', publishDescription: 'Revision {revision} will become the durable Published revision. This does not activate Runtime by itself.', publishConfirm: 'Publish revision', activateTitle: 'Activate Published revision?', activateDescription: 'The Published revision will be staged and validated before replacing the Active Runtime. Activation changes the running application.', activateConfirm: 'Activate Published', saveTitle: 'Save revision?', saveDescription: 'The current Working state will be persisted as a new immutable revision.', saveConfirm: 'Save revision' };
  if (locale === 'es') return { checkoutTitle: '¿Hacer checkout de la revisión?', checkoutDescription: 'La revisión {revision} reemplazará el Working actual. Los cambios no guardados pueden perderse.', checkoutConfirm: 'Hacer checkout', publishTitle: '¿Publicar la revisión?', publishDescription: 'La revisión {revision} pasará a ser la revisión Published durable. Esto no activa Runtime por sí solo.', publishConfirm: 'Publicar revisión', activateTitle: '¿Activar la revisión Published?', activateDescription: 'La revisión Published será preparada y validada antes de reemplazar el Runtime Active. La activación cambia la aplicación en ejecución.', activateConfirm: 'Activar Published', saveTitle: '¿Guardar revisión?', saveDescription: 'El estado Working actual se persistirá como una nueva revisión inmutable.', saveConfirm: 'Guardar revisión' };
  return { checkoutTitle: 'Fazer checkout da revisão?', checkoutDescription: 'A revisão {revision} substituirá o Working atual. Alterações não salvas no Working podem ser perdidas.', checkoutConfirm: 'Fazer checkout', publishTitle: 'Publicar a revisão?', publishDescription: 'A revisão {revision} passará a ser a revisão Published durável. Isso não ativa o Runtime por si só.', publishConfirm: 'Publicar revisão', activateTitle: 'Ativar a revisão Published?', activateDescription: 'A revisão Published será preparada e validada antes de substituir o Runtime Active. A ativação altera a aplicação em execução.', activateConfirm: 'Ativar Published', saveTitle: 'Salvar revisão?', saveDescription: 'O estado Working atual será persistido como uma nova revisão imutável.', saveConfirm: 'Salvar revisão' };
}
