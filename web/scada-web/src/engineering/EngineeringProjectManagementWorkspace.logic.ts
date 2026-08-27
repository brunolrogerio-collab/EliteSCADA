import { ProjectPortabilityApiError } from './projectPortabilityApi';
import type {
  PortabilityPreviewToken,
  ProjectPortabilityMergeMode,
  ProjectPortabilityPreview
} from './projectPortabilityTypes';
import type { EngineeringLocale } from './i18n';

export const PROJECT_PORTABILITY_MERGE_MODES: ProjectPortabilityMergeMode[] = [
  'CreateOnly',
  'UpdateExisting',
  'CreateAndUpdate'
];

export type CanonicalJsonCandidateIdentity = {
  validJson: boolean;
  schema: string | null;
  schemaVersion: number | null;
};

export function canonicalJsonCandidateIdentity(jsonText: string): CanonicalJsonCandidateIdentity {
  try {
    const value = JSON.parse(jsonText) as unknown;
    if (!value || typeof value !== 'object') return { validJson: true, schema: null, schemaVersion: null };
    const record = value as Record<string, unknown>;
    return {
      validJson: true,
      schema: typeof record.schema === 'string' ? record.schema : null,
      schemaVersion: typeof record.schemaVersion === 'number' ? record.schemaVersion : null
    };
  } catch {
    return { validJson: false, schema: null, schemaVersion: null };
  }
}

export function projectPortabilityFileFingerprint(file: File): string {
  return `${file.name}::${file.size}::${file.lastModified}`;
}

export function canonicalTextFingerprint(file: File, text: string): string {
  return `${projectPortabilityFileFingerprint(file)}::${text.length}`;
}

export function previewTokenMatches(
  token: PortabilityPreviewToken | null,
  sourceFingerprint: string | null,
  mode: ProjectPortabilityMergeMode
): boolean {
  return Boolean(
    token &&
    sourceFingerprint &&
    token.sourceFingerprint === sourceFingerprint &&
    token.mode === mode &&
    token.preview.canApply
  );
}

export function previewHasChanges(preview: ProjectPortabilityPreview | null): boolean {
  return Boolean(preview && (preview.createCount > 0 || preview.updateCount > 0));
}

export function projectPortabilityErrorText(error: unknown, locale: EngineeringLocale): string {
  const copy = errorCopy(locale);
  if (error instanceof ProjectPortabilityApiError) {
    if (error.status === 400) return `${copy.invalid} ${error.message}`.trim();
    if (error.status === 401) return copy.unauthorized;
    if (error.status === 403) return copy.forbidden;
    if (error.status === 409) return `${copy.conflict} ${error.message}`.trim();
    if (error.status === 422) return `${copy.validation} ${error.message}`.trim();
    if (error.status === 503) return copy.unavailable;
    return error.message || copy.generic;
  }
  return error instanceof Error ? error.message : copy.generic;
}

export function mergeModeLabel(mode: ProjectPortabilityMergeMode, locale: EngineeringLocale): string {
  const labels: Record<EngineeringLocale, Record<ProjectPortabilityMergeMode, string>> = {
    'pt-BR': {
      CreateOnly: 'Criar apenas novos',
      UpdateExisting: 'Atualizar existentes',
      CreateAndUpdate: 'Criar e atualizar'
    },
    en: {
      CreateOnly: 'Create new only',
      UpdateExisting: 'Update existing',
      CreateAndUpdate: 'Create and update'
    },
    es: {
      CreateOnly: 'Crear solo nuevos',
      UpdateExisting: 'Actualizar existentes',
      CreateAndUpdate: 'Crear y actualizar'
    }
  };
  return labels[locale][mode];
}

function errorCopy(locale: EngineeringLocale) {
  if (locale === 'en') return {
    invalid: 'The portability request is invalid.',
    unauthorized: 'Authentication is required for this project operation.',
    forbidden: 'Your current role is not authorized for this project operation.',
    conflict: 'Working changed after Preview. Refresh and preview the selected source again.',
    validation: 'The server validated the request but could not apply it.',
    unavailable: 'The Engineering service required for this operation is unavailable.',
    generic: 'The project portability operation failed.'
  };
  if (locale === 'es') return {
    invalid: 'La solicitud de portabilidad no es válida.',
    unauthorized: 'Se requiere autenticación para esta operación del proyecto.',
    forbidden: 'Su rol actual no está autorizado para esta operación del proyecto.',
    conflict: 'Working cambió después del Preview. Actualice y vuelva a previsualizar el origen seleccionado.',
    validation: 'El servidor validó la solicitud pero no pudo aplicarla.',
    unavailable: 'El servicio de Engineering requerido no está disponible.',
    generic: 'La operación de portabilidad del proyecto falló.'
  };
  return {
    invalid: 'A solicitação de portabilidade é inválida.',
    unauthorized: 'É necessário autenticar para esta operação do projeto.',
    forbidden: 'Seu papel atual não está autorizado para esta operação do projeto.',
    conflict: 'O Working mudou após o Preview. Atualize e valide novamente a origem selecionada.',
    validation: 'O servidor validou a solicitação, mas não conseguiu aplicá-la.',
    unavailable: 'O serviço de Engineering necessário para esta operação está indisponível.',
    generic: 'A operação de portabilidade do projeto falhou.'
  };
}
