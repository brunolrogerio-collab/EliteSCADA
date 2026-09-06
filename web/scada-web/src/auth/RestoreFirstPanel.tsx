import React, { useEffect, useState } from 'react';

const API = (import.meta.env.VITE_SCADA_API ?? '').replace(/\/$/, '');

export type RecoveryLocale = 'pt-BR' | 'en' | 'es';
export type RecoverySelection = {
  applicationFile: File;
  licenseFile?: File;
};

type Props = {
  mode: 'bootstrap' | 'application';
  locale: RecoveryLocale;
  selection?: RecoverySelection | null;
  onAuthorityRestored?: (selection: RecoverySelection) => Promise<void> | void;
  onApplicationRecovered?: () => Promise<void> | void;
  onCancel: () => void;
};

type RecoveryResponse = {
  recovered?: boolean;
  stage?: string;
  blockers?: string[];
  issues?: string[];
  error?: string;
};

const text = {
  'pt-BR': {
    title: 'Restaurar backup',
    bootstrapIntro: 'Valide o backup da Authority e o pacote da aplicação antes de alterar o servidor. A senha abaixo protege somente o backup da Authority.',
    applicationIntro: 'Valide o pacote da aplicação com a Authority restaurada antes de criar qualquer projeto novo.',
    application: 'Pacote da aplicação (.escadapkg)',
    authority: 'Backup da Authority (.json)',
    authorityPassword: 'Senha do backup da Authority',
    license: 'Licença (opcional)',
    licenseHint: 'A licença é opcional e não bloqueia a recuperação da aplicação.',
    validateBackups: 'Validar backups',
    validateApplication: 'Validar aplicação',
    validating: 'Validando…',
    backupsValid: 'Backups válidos. A Authority pode ser restaurada sem criar um usuário provisório.',
    applicationValid: 'Pacote válido e compatível com esta recuperação.',
    restoreAuthority: 'Restaurar Authority',
    restoringAuthority: 'Restaurando Authority…',
    restoreApplication: 'Restaurar aplicação',
    restoringApplication: 'Restaurando aplicação…',
    authorityDone: 'Authority restaurada. Entre com um Administrador restaurado para continuar.',
    applicationDone: 'Aplicação restaurada, publicada e ativada.',
    licenseFailed: 'A aplicação foi recuperada, mas a licença opcional não pôde ser instalada.',
    continueWithoutLicense: 'Continuar sem licença',
    separateData: 'Banco de dados e Historian permanecem uma etapa separada e devem usar o procedimento nativo suportado.',
    cancel: 'Voltar',
    chooseFiles: 'Selecione os arquivos exigidos para continuar.',
    unavailable: 'Não foi possível concluir esta etapa de recuperação.'
  },
  en: {
    title: 'Restore backup',
    bootstrapIntro: 'Validate the Authority backup and application package before changing the server. The password below protects only the Authority backup.',
    applicationIntro: 'Validate the application package with the restored Authority before creating any new project.',
    application: 'Application package (.escadapkg)',
    authority: 'Authority backup (.json)',
    authorityPassword: 'Authority backup password',
    license: 'License (optional)',
    licenseHint: 'The license is optional and does not block application recovery.',
    validateBackups: 'Validate backups',
    validateApplication: 'Validate application',
    validating: 'Validating…',
    backupsValid: 'Backups are valid. Authority can be restored without creating a provisional user.',
    applicationValid: 'Package is valid and compatible with this recovery.',
    restoreAuthority: 'Restore Authority',
    restoringAuthority: 'Restoring Authority…',
    restoreApplication: 'Restore application',
    restoringApplication: 'Restoring application…',
    authorityDone: 'Authority restored. Sign in with a restored Administrator to continue.',
    applicationDone: 'Application restored, published, and activated.',
    licenseFailed: 'The application was recovered, but the optional license could not be installed.',
    continueWithoutLicense: 'Continue without license',
    separateData: 'Database and Historian recovery remain a separate step and must use the supported native procedure.',
    cancel: 'Back',
    chooseFiles: 'Select the required files to continue.',
    unavailable: 'This recovery step could not be completed.'
  },
  es: {
    title: 'Restaurar backup',
    bootstrapIntro: 'Valide el backup de Authority y el paquete de la aplicación antes de modificar el servidor. La contraseña siguiente protege solamente el backup de Authority.',
    applicationIntro: 'Valide el paquete de la aplicación con la Authority restaurada antes de crear un proyecto nuevo.',
    application: 'Paquete de la aplicación (.escadapkg)',
    authority: 'Backup de Authority (.json)',
    authorityPassword: 'Contraseña del backup de Authority',
    license: 'Licencia (opcional)',
    licenseHint: 'La licencia es opcional y no bloquea la recuperación de la aplicación.',
    validateBackups: 'Validar backups',
    validateApplication: 'Validar aplicación',
    validating: 'Validando…',
    backupsValid: 'Los backups son válidos. Authority puede restaurarse sin crear un usuario provisional.',
    applicationValid: 'El paquete es válido y compatible con esta recuperación.',
    restoreAuthority: 'Restaurar Authority',
    restoringAuthority: 'Restaurando Authority…',
    restoreApplication: 'Restaurar aplicación',
    restoringApplication: 'Restaurando aplicación…',
    authorityDone: 'Authority restaurada. Ingrese con un Administrador restaurado para continuar.',
    applicationDone: 'Aplicación restaurada, publicada y activada.',
    licenseFailed: 'La aplicación fue recuperada, pero la licencia opcional no pudo instalarse.',
    continueWithoutLicense: 'Continuar sin licencia',
    separateData: 'La recuperación de la base de datos y del Historian sigue siendo una etapa separada y debe usar el procedimiento nativo soportado.',
    cancel: 'Volver',
    chooseFiles: 'Seleccione los archivos requeridos para continuar.',
    unavailable: 'No fue posible completar esta etapa de recuperación.'
  }
} as const;

async function readFailure(response: Response, fallback: string): Promise<string> {
  try {
    const payload = await response.json() as RecoveryResponse;
    if (payload.error) return payload.error;
    if (payload.blockers?.length) return payload.blockers.join(' ');
    if (payload.issues?.length) return payload.issues.join(' ');
  } catch {
    // The response may intentionally have no JSON body.
  }
  return fallback;
}

async function postPackage(path: string, applicationFile: File): Promise<Response> {
  return fetch(`${API}${path}`, {
    method: 'POST',
    headers: { accept: 'application/json' },
    body: applicationFile
  });
}

export function RestoreFirstPanel({
  mode,
  locale,
  selection,
  onAuthorityRestored,
  onApplicationRecovered,
  onCancel
}: Props) {
  const t = text[locale];
  const [applicationFile, setApplicationFile] = useState<File | null>(selection?.applicationFile ?? null);
  const [authorityFile, setAuthorityFile] = useState<File | null>(null);
  const [licenseFile, setLicenseFile] = useState<File | null>(selection?.licenseFile ?? null);
  const [authorityPassword, setAuthorityPassword] = useState('');
  const [authorityValid, setAuthorityValid] = useState(false);
  const [applicationValid, setApplicationValid] = useState(false);
  const [validating, setValidating] = useState(false);
  const [applying, setApplying] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [status, setStatus] = useState<string | null>(null);
  const [licenseWarning, setLicenseWarning] = useState(false);

  useEffect(() => {
    setApplicationFile(selection?.applicationFile ?? null);
    setLicenseFile(selection?.licenseFile ?? null);
    setApplicationValid(false);
    setError(null);
    setStatus(null);
  }, [selection]);

  const chooseApplication = (file: File | null) => {
    setApplicationFile(file);
    setApplicationValid(false);
    setError(null);
    setStatus(null);
  };

  const chooseAuthority = (file: File | null) => {
    setAuthorityFile(file);
    setAuthorityValid(false);
    setError(null);
    setStatus(null);
  };

  const validateBootstrap = async () => {
    if (!applicationFile || !authorityFile || !authorityPassword) {
      setError(t.chooseFiles);
      return;
    }
    setValidating(true);
    setError(null);
    setStatus(null);
    setAuthorityValid(false);
    setApplicationValid(false);
    try {
      const authorityBackup = await authorityFile.text();
      const authorityResponse = await fetch(`${API}/api/auth/bootstrap/authority-backup/preview`, {
        method: 'POST',
        headers: {
          accept: 'application/json',
          'content-type': 'application/json'
        },
        body: JSON.stringify({ backup: authorityBackup, password: authorityPassword })
      });
      if (!authorityResponse.ok)
        throw new Error(await readFailure(authorityResponse, t.unavailable));

      const applicationResponse = await postPackage(
        '/api/system-recovery/bootstrap/application/preview',
        applicationFile);
      if (!applicationResponse.ok)
        throw new Error(await readFailure(applicationResponse, t.unavailable));

      setAuthorityValid(true);
      setApplicationValid(true);
      setStatus(t.backupsValid);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : t.unavailable);
    } finally {
      setValidating(false);
    }
  };

  const restoreAuthority = async () => {
    if (!applicationFile || !authorityFile || !authorityPassword || !authorityValid || !applicationValid)
      return;
    setApplying(true);
    setError(null);
    try {
      const authorityBackup = await authorityFile.text();
      const response = await fetch(`${API}/api/auth/bootstrap/authority-backup/apply`, {
        method: 'POST',
        headers: {
          accept: 'application/json',
          'content-type': 'application/json'
        },
        body: JSON.stringify({ backup: authorityBackup, password: authorityPassword })
      });
      if (!response.ok)
        throw new Error(await readFailure(response, t.unavailable));

      const nextSelection: RecoverySelection = {
        applicationFile,
        ...(licenseFile ? { licenseFile } : {})
      };
      setAuthorityPassword('');
      setAuthorityFile(null);
      setStatus(t.authorityDone);
      await onAuthorityRestored?.(nextSelection);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : t.unavailable);
    } finally {
      setApplying(false);
    }
  };

  const validateApplication = async () => {
    if (!applicationFile) {
      setError(t.chooseFiles);
      return;
    }
    setValidating(true);
    setError(null);
    setStatus(null);
    setApplicationValid(false);
    try {
      const response = await postPackage('/api/system-recovery/application/preview', applicationFile);
      if (!response.ok)
        throw new Error(await readFailure(response, t.unavailable));
      setApplicationValid(true);
      setStatus(t.applicationValid);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : t.unavailable);
    } finally {
      setValidating(false);
    }
  };

  const finishRecovery = async () => {
    setLicenseWarning(false);
    await onApplicationRecovered?.();
  };

  const restoreApplication = async () => {
    if (!applicationFile || !applicationValid) return;
    setApplying(true);
    setError(null);
    setStatus(null);
    setLicenseWarning(false);
    try {
      const response = await postPackage('/api/system-recovery/application/apply', applicationFile);
      if (!response.ok)
        throw new Error(await readFailure(response, t.unavailable));
      const result = await response.json() as RecoveryResponse;
      if (result.recovered !== true)
        throw new Error(result.issues?.join(' ') || t.unavailable);

      setStatus(t.applicationDone);
      if (!licenseFile) {
        await finishRecovery();
        return;
      }

      const licenseCode = (await licenseFile.text()).trim();
      const licenseResponse = await fetch(`${API}/api/licensing/install`, {
        method: 'POST',
        headers: {
          accept: 'application/json',
          'content-type': 'application/json'
        },
        body: JSON.stringify({ licenseCode })
      });
      if (!licenseResponse.ok) {
        setLicenseWarning(true);
        return;
      }
      await finishRecovery();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : t.unavailable);
    } finally {
      setApplying(false);
    }
  };

  return (
    <div className="auth-page">
      <div className="auth-card auth-card--recovery" data-testid={`restore-first-${mode}`}>
        <div className="auth-mark">E</div>
        <h1>{t.title}</h1>
        <p>{mode === 'bootstrap' ? t.bootstrapIntro : t.applicationIntro}</p>

        <label>
          <span>{t.application}</span>
          <input
            data-testid="recovery-application-file"
            type="file"
            accept=".escadapkg,application/vnd.elitescada.project-package"
            onChange={event => chooseApplication(event.target.files?.[0] ?? null)}
          />
          {applicationFile && <small className="auth-hint">{applicationFile.name}</small>}
        </label>

        {mode === 'bootstrap' && (
          <>
            <label>
              <span>{t.authority}</span>
              <input
                data-testid="recovery-authority-file"
                type="file"
                accept=".json,application/json"
                onChange={event => chooseAuthority(event.target.files?.[0] ?? null)}
              />
              {authorityFile && <small className="auth-hint">{authorityFile.name}</small>}
            </label>
            <label>
              <span>{t.authorityPassword}</span>
              <input
                data-testid="recovery-authority-password"
                type="password"
                autoComplete="off"
                value={authorityPassword}
                onChange={event => {
                  setAuthorityPassword(event.target.value);
                  setAuthorityValid(false);
                  setError(null);
                }}
              />
            </label>
          </>
        )}

        <label>
          <span>{t.license}</span>
          <input
            data-testid="recovery-license-file"
            type="file"
            onChange={event => setLicenseFile(event.target.files?.[0] ?? null)}
          />
          <small className="auth-hint">{licenseFile ? licenseFile.name : t.licenseHint}</small>
        </label>

        {status && <div className="auth-status" role="status">{status}</div>}
        {error && <div className="auth-error" role="alert">{error}</div>}
        {licenseWarning && (
          <div className="auth-warning" role="alert">
            {t.licenseFailed}
            <button type="button" className="auth-secondary" onClick={() => void finishRecovery()}>
              {t.continueWithoutLicense}
            </button>
          </div>
        )}

        <div className="auth-actions">
          <button type="button" className="auth-secondary" onClick={onCancel} disabled={validating || applying}>
            {t.cancel}
          </button>
          {mode === 'bootstrap' ? (
            <>
              <button
                type="button"
                className="auth-secondary"
                onClick={() => void validateBootstrap()}
                disabled={validating || applying || !applicationFile || !authorityFile || !authorityPassword}
              >
                {validating ? t.validating : t.validateBackups}
              </button>
              <button
                type="button"
                className="auth-primary"
                onClick={() => void restoreAuthority()}
                disabled={applying || validating || !authorityValid || !applicationValid}
              >
                {applying ? t.restoringAuthority : t.restoreAuthority}
              </button>
            </>
          ) : (
            <>
              <button
                type="button"
                className="auth-secondary"
                onClick={() => void validateApplication()}
                disabled={validating || applying || !applicationFile}
              >
                {validating ? t.validating : t.validateApplication}
              </button>
              <button
                type="button"
                className="auth-primary"
                onClick={() => void restoreApplication()}
                disabled={applying || validating || !applicationValid || licenseWarning}
              >
                {applying ? t.restoringApplication : t.restoreApplication}
              </button>
            </>
          )}
        </div>

        <div className="auth-divider" />
        <small className="auth-hint">{t.separateData}</small>
      </div>
    </div>
  );
}
