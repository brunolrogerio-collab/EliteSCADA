import React, { useEffect, useMemo, useState } from 'react';
import { applyEngineeringPackage, previewEngineeringPackage } from './api';
import type { EngineeringLocale } from './i18n';
import type { EngineeringSnapshot } from './types';
import './mobile-runtime-engineering.css';

export function MobileRuntimeEngineeringWorkspace({ snapshot, onApplied, locale = 'pt-BR' }: {
  snapshot: EngineeringSnapshot;
  onApplied: () => Promise<void>;
  locale?: EngineeringLocale;
}) {
  const [orientation, setOrientation] = useState<'landscape' | 'portrait'>(snapshot.package.runtimePresentation?.mobileOrientation ?? 'landscape');
  const [preview, setPreview] = useState<{ canApply: boolean; errorCount: number; items: Array<{ issues: Array<{ code: string; message: string; isError: boolean }> }> } | null>(null);
  const [previewSignature, setPreviewSignature] = useState('');
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const text = copy(locale);
  const signature = useMemo(() => JSON.stringify({ orientation, changeVersion: snapshot.workspace.changeVersion }), [orientation, snapshot.workspace.changeVersion]);

  useEffect(() => {
    setOrientation(snapshot.package.runtimePresentation?.mobileOrientation ?? 'landscape');
    setPreview(null);
    setPreviewSignature('');
    setMessage(null);
  }, [snapshot.workspace.changeVersion, snapshot.package.runtimePresentation]);

  const candidate = () => ({
    ...snapshot.package,
    runtimePresentation: {
      ...(snapshot.package.runtimePresentation ?? { historicalPlaybackEnabled: false, version: 1 }),
      mobileOrientation: orientation,
      version: 1
    }
  });

  const validate = async () => {
    setBusy(true); setMessage(null);
    try {
      const result = await previewEngineeringPackage(candidate());
      setPreview(result);
      setPreviewSignature(signature);
      setMessage(result.canApply ? text.valid : text.invalid);
    } catch (error) { setMessage(error instanceof Error ? error.message : String(error)); }
    finally { setBusy(false); }
  };

  const apply = async () => {
    if (!preview?.canApply || previewSignature !== signature) return;
    setBusy(true); setMessage(null);
    try {
      const result = await applyEngineeringPackage(candidate(), snapshot.workspace.changeVersion);
      const issue = result.issues.find(item => item.isError);
      if (issue) throw new Error(`${issue.code}: ${issue.message}`);
      setMessage(text.applied);
      await onApplied();
    } catch (error) { setMessage(error instanceof Error ? error.message : String(error)); }
    finally { setBusy(false); }
  };

  return <section className="eng-section mobile-runtime-editor" data-testid="mobile-runtime-editor">
    <header className="eng-section-header"><div><span className="eng-eyebrow">{text.eyebrow}</span><h1>{text.title}</h1><p>{text.description}</p></div></header>
    <div className="eng-panel mobile-runtime-editor__panel">
      <label className="eng-editor-field"><span>{text.orientation}</span>
        <select data-testid="mobile-runtime-orientation" value={orientation} disabled={busy} onChange={event => {
          setOrientation(event.currentTarget.value as 'landscape' | 'portrait');
          setPreview(null); setPreviewSignature(''); setMessage(null);
        }}>
          <option value="landscape">{text.landscape}</option><option value="portrait">{text.portrait}</option>
        </select>
      </label>
      <p className="mobile-runtime-editor__note">{text.note}</p>
      <div className="branding-editor__actions">
        <button type="button" onClick={() => void validate()} disabled={busy}>{text.validate}</button>
        <button type="button" onClick={() => void apply()} disabled={busy || !preview?.canApply || previewSignature !== signature}>{text.apply}</button>
      </div>
      {message && <p role="status" aria-live="polite">{message}</p>}
      {preview?.errorCount ? <ul className="branding-editor__issues">{preview.items.flatMap(item => item.issues).filter(issue => issue.isError).map(issue => <li key={issue.code}><strong>{issue.code}</strong> {issue.message}</li>)}</ul> : null}
    </div>
  </section>;
}

function copy(locale: EngineeringLocale) {
  if (locale === 'en') return {
    eyebrow: 'Runtime presentation', title: 'Mobile', description: 'Set the default orientation used by the Runtime on coarse-pointer devices. Desktop keeps its normal responsive layout.',
    orientation: 'Mobile orientation', landscape: 'Landscape (rotate the canvas when needed)', portrait: 'Portrait',
    note: 'The Runtime keeps the logical screen proportions in both modes. Device orientation locking depends on browser and operating-system support. Per-screen mobile variants and a custom mobile header are not configured here yet.',
    validate: 'Validate preview', apply: 'Apply to Working', valid: 'Preview is valid. Apply updates only the Working project.', invalid: 'The preview contains validation errors.', applied: 'Mobile Runtime setting applied to Working. Save, Publish and Activate are still required.'
  };
  if (locale === 'es') return {
    eyebrow: 'Presentación de Runtime', title: 'Móvil', description: 'Defina la orientación predeterminada de Runtime en dispositivos táctiles. El escritorio conserva su diseño adaptable habitual.',
    orientation: 'Orientación móvil', landscape: 'Horizontal (girar el canvas si es necesario)', portrait: 'Vertical',
    note: 'Runtime conserva las proporciones de la pantalla lógica en ambos modos. El bloqueo de orientación depende del navegador y del sistema operativo. Las variantes móviles por pantalla y el encabezado móvil personalizado aún no se configuran aquí.',
    validate: 'Validar preview', apply: 'Aplicar a Working', valid: 'El preview es válido. Aplicar solo actualiza el proyecto Working.', invalid: 'El preview contiene errores de validación.', applied: 'Configuración móvil aplicada a Working. Aún debe guardar, publicar y activar.'
  };
  return {
    eyebrow: 'Apresentação do Runtime', title: 'Mobile', description: 'Defina a orientação padrão do Runtime em dispositivos com toque. O desktop mantém seu layout responsivo normal.',
    orientation: 'Orientação mobile', landscape: 'Paisagem (girar o canvas quando necessário)', portrait: 'Retrato',
    note: 'O Runtime preserva as proporções da tela lógica nos dois modos. O bloqueio de orientação depende do suporte do navegador e do sistema operacional. Versões mobile por tela e cabeçalho mobile personalizado ainda não são configurados aqui.',
    validate: 'Validar preview', apply: 'Aplicar ao Working', valid: 'Preview válido. Aplicar atualiza somente o projeto Working.', invalid: 'O preview contém erros de validação.', applied: 'Configuração mobile aplicada ao Working. Ainda é necessário salvar, publicar e ativar.'
  };
}
