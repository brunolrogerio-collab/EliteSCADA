import React, { useEffect, useMemo, useRef, useState } from 'react';
import { applyEngineeringPackage, importVisualAsset, previewEngineeringPackage, visualAssetContentUrl } from './api';
import type { ApplicationBrandingEngineering, ApplicationBrandingMode, EngineeringSnapshot, ImportPreviewView, RuntimePresentationEngineering } from './types';
import type { EngineeringLocale } from './i18n';
import { RuntimeHeaderSettings } from './RuntimeHeaderSettings';

const DEFAULT: ApplicationBrandingEngineering = { mode: 'default' };
const DEFAULT_RUNTIME: RuntimePresentationEngineering = { historicalPlaybackEnabled: false, mobileOrientation: 'landscape', version: 1 };

export function BrandingEngineeringWorkspace({ snapshot, onApplied, locale = 'pt-BR' }: { snapshot: EngineeringSnapshot; onApplied: () => Promise<void>; locale?: EngineeringLocale }) {
  const [draft, setDraft] = useState<ApplicationBrandingEngineering>(snapshot.package.branding ?? DEFAULT);
  const [runtimeDraft, setRuntimeDraft] = useState<RuntimePresentationEngineering>(snapshot.package.runtimePresentation ?? DEFAULT_RUNTIME);
  const [preview, setPreview] = useState<ImportPreviewView | null>(null);
  const [previewSignature, setPreviewSignature] = useState('');
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const fileInput = useRef<HTMLInputElement>(null);

  useEffect(() => {
    setDraft(snapshot.package.branding ?? DEFAULT);
    setRuntimeDraft(snapshot.package.runtimePresentation ?? DEFAULT_RUNTIME);
    setPreview(null);
    setPreviewSignature('');
    setMessage(null);
  }, [snapshot.workspace.changeVersion, snapshot.package.branding, snapshot.package.runtimePresentation]);

  const signature = useMemo(() => JSON.stringify({ draft, runtimeDraft }), [draft, runtimeDraft]);
  const assets = snapshot.package.visualAssets ?? [];
  const selectedAsset = draft.visualAssetId ? assets.find(a => a.id?.toLowerCase() === draft.visualAssetId?.toLowerCase()) : undefined;

  const update = (patch: Partial<ApplicationBrandingEngineering>) => {
    setDraft(current => ({ ...current, ...patch }));
    setPreview(null);
    setPreviewSignature('');
    setMessage(null);
  };
  const candidate = () => ({ ...snapshot.package, branding: draft, runtimePresentation: runtimeDraft });

  async function validate() {
    setBusy(true); setMessage(null);
    try {
      const next = await previewEngineeringPackage(candidate());
      setPreview(next); setPreviewSignature(signature);
      setMessage(next.canApply ? 'Preview valid. Apply changes only the Working application.' : 'Preview rejected. Resolve the validation errors.');
    } catch (e) { setMessage(e instanceof Error ? e.message : String(e)); }
    finally { setBusy(false); }
  }

  async function apply() {
    if (!preview?.canApply || previewSignature !== signature) return;
    setBusy(true); setMessage(null);
    try {
      const result = await applyEngineeringPackage(candidate(), snapshot.workspace.changeVersion);
      const issue = result.issues.find(x => x.isError);
      if (issue) throw new Error(`${issue.code}: ${issue.message}`);
      setMessage('Working branding applied. Save, Publish and Activate remain required before the global shell changes.');
      await onApplied();
    } catch (e) { setMessage(e instanceof Error ? e.message : String(e)); }
    finally { setBusy(false); }
  }

  async function uploadBrandImage(file: File) {
    setBusy(true);
    setMessage(null);
    try {
      const result = await importVisualAsset(file, snapshot.workspace.changeVersion, {
        fileName: file.name,
        name: file.name.replace(/\.[^.]+$/, '')
      });
      await onApplied();
      setDraft(current => ({ ...current, mode: 'image', visualAssetId: result.asset.id }));
      setPreview(null);
      setPreviewSignature('');
      setMessage('Image uploaded to project assets. Validate and apply it to Working to use it in branding.');
    } catch (error) {
      setMessage(error instanceof Error ? error.message : String(error));
    } finally {
      setBusy(false);
      if (fileInput.current) fileInput.current.value = '';
    }
  }

  return <div className="eng-section branding-editor" data-testid="branding-editor">
    <header className="eng-section-header"><div><span className="eng-eyebrow">{locale === 'pt-BR' ? 'Aplicativo' : locale === 'es' ? 'Aplicación' : 'Application'}</span><h1>{locale === 'pt-BR' ? 'Identidade do aplicativo e Runtime' : locale === 'es' ? 'Identidad de la aplicación y Runtime' : 'Application identity and Runtime'}</h1>
      <p>{locale === 'pt-BR' ? 'A marca identifica o aplicativo; as opções do Runtime controlam separadamente a barra de operação. As alterações só chegam à interface global depois de Salvar, Publicar e Ativar.' : locale === 'es' ? 'La marca identifica la aplicación; las opciones de Runtime controlan por separado la barra operativa. Los cambios globales requieren Guardar, Publicar y Activar.' : 'Branding identifies the application; Runtime settings separately control the operator bar. Global changes require Save, Publish and Activate.'}</p></div></header>
    <div className="branding-editor__grid">
      <section className="eng-panel branding-editor__form" aria-label={locale === 'pt-BR' ? 'Identidade da marca do aplicativo' : locale === 'es' ? 'Identidad de marca de la aplicación' : 'Application brand identity'}>
        <h2>{locale === 'pt-BR' ? 'Marca do aplicativo' : locale === 'es' ? 'Marca de la aplicación' : 'Application brand'}</h2>
        <p>{locale === 'pt-BR' ? 'Controla o símbolo, nome e subtítulo da plataforma; não configura os controles do Runtime.' : locale === 'es' ? 'Controla el símbolo, nombre y subtítulo de la plataforma; no configura los controles de Runtime.' : 'Controls the platform symbol, name and subtitle; it does not configure Runtime controls.'}</p>
        <label><span>{locale === 'pt-BR' ? 'Apresentação da marca' : locale === 'es' ? 'Presentación de la marca' : 'Brand presentation'}</span><select value={draft.mode} onChange={e => update({ mode: e.target.value as ApplicationBrandingMode, visualAssetId: e.target.value === 'image' ? draft.visualAssetId : null })}>
          <option value="default">{locale === 'pt-BR' ? 'Padrão EliteSCADA' : 'Default — EliteSCADA'}</option><option value="text">{locale === 'pt-BR' ? 'Texto' : locale === 'es' ? 'Texto' : 'Text'}</option><option value="image">{locale === 'pt-BR' ? 'Imagem do projeto' : locale === 'es' ? 'Imagen del proyecto' : 'Project image'}</option><option value="none">{locale === 'pt-BR' ? 'Oculta' : locale === 'es' ? 'Oculta' : 'Hidden'}</option>
        </select></label>
        {(draft.mode === 'text' || draft.mode === 'image') && <><label><span>{draft.mode === 'text' ? 'Application text' : 'Accessible / optional image label'}</span>
          <input value={draft.text ?? ''} maxLength={128} onChange={e => update({ text: e.target.value })}/></label>
          <label><span>{locale === 'pt-BR' ? 'Subtítulo (opcional)' : locale === 'es' ? 'Subtítulo (opcional)' : 'Subtitle (optional)'}</span><input value={draft.subtitle ?? ''} maxLength={256} onChange={e => update({ subtitle: e.target.value })}/></label></>}
        {draft.mode === 'image' && <div className="branding-editor__asset-picker">
          <label><span>{locale === 'pt-BR' ? 'Imagem do projeto' : locale === 'es' ? 'Imagen del proyecto' : 'Project image'}</span><select value={draft.visualAssetId ?? ''} onChange={e => update({ visualAssetId: e.target.value || null })}>
            <option value="">{locale === 'pt-BR' ? 'Selecionar imagem…' : locale === 'es' ? 'Seleccionar imagen…' : 'Select image…'}</option>{assets.map(a => a.id ? <option key={a.id} value={a.id}>{a.name} · {a.mediaType}</option> : null)}
          </select></label>
          <input ref={fileInput} type="file" accept="image/png,image/jpeg,image/bmp,image/svg+xml,.png,.jpg,.jpeg,.bmp,.svg" hidden onChange={event => {
            const file = event.currentTarget.files?.[0];
            if (file) void uploadBrandImage(file);
          }}/>
          <button type="button" className="branding-editor__browse" onClick={() => fileInput.current?.click()} disabled={busy}>{locale === 'pt-BR' ? 'Procurar e enviar imagem…' : locale === 'es' ? 'Buscar y cargar imagen…' : 'Browse and upload image…'}</button>
          <small>{locale === 'pt-BR' ? 'PNG, JPG, BMP ou SVG. A imagem é enviada para os assets do projeto.' : 'PNG, JPG, BMP or SVG. The selected file is uploaded to project assets.'}</small>
        </div>}
      </section>
      <section className="eng-panel branding-preview" aria-label={locale === 'pt-BR' ? 'Prévia da marca em edição' : locale === 'es' ? 'Vista previa de marca en edición' : 'Working brand preview'} data-testid="branding-working-preview">
        <h2>{locale === 'pt-BR' ? 'Prévia da marca' : locale === 'es' ? 'Vista previa de marca' : 'Brand preview'}</h2>
        {draft.mode === 'none' && <p data-testid="branding-preview-none">No brand element or reserved brand space will be mounted after this revision becomes Active.</p>}
        {draft.mode === 'default' && <div className="branding-preview__brand"><b aria-hidden="true">E</b><span><strong>EliteSCADA</strong><small>Industrial SCADA Platform</small></span></div>}
        {draft.mode === 'text' && <div className="branding-preview__brand branding-preview__brand--text"><span><strong>{draft.text || 'Text required'}</strong>{draft.subtitle && <small>{draft.subtitle}</small>}</span></div>}
        {draft.mode === 'image' && (selectedAsset?.id ? <div className="branding-preview__brand"><img src={visualAssetContentUrl(selectedAsset.id)} alt={draft.text?.trim() || selectedAsset.name}/><span>{draft.text && <strong>{draft.text}</strong>}{draft.subtitle && <small>{draft.subtitle}</small>}</span></div> : <p role="status">Select a canonical VisualAsset. Local paths and browser-local blobs are not branding authority.</p>)}
        <p className="branding-preview__lifecycle">Working → Preview/Apply → Save/Reopen → Publish → Activate → Active shell</p>
      </section>
      <section className="runtime-header-settings__section">
        <RuntimeHeaderSettings locale={locale} snapshot={snapshot} value={runtimeDraft.header ?? {}} onChange={header => {
          setRuntimeDraft(current => ({ ...current, header })); setPreview(null); setPreviewSignature(''); setMessage(null);
        }}/>
        <RuntimePlaybackProjectSetting locale={locale} value={runtimeDraft.historicalPlaybackEnabled}
          onChange={value => { setRuntimeDraft(current => ({ ...current, historicalPlaybackEnabled: value, version: 1 })); setPreview(null); setPreviewSignature(''); setMessage(null); }} />
      </section>
      <div className="branding-editor__actions"><button type="button" onClick={() => void validate()} disabled={busy}>{locale === 'pt-BR' ? 'Validar prévia' : locale === 'es' ? 'Validar vista previa' : 'Validate preview'}</button>
        <button type="button" onClick={() => void apply()} disabled={busy || !preview?.canApply || previewSignature !== signature}>{locale === 'pt-BR' ? 'Aplicar ao Working' : locale === 'es' ? 'Aplicar a Working' : 'Apply to Working'}</button></div>
      {message && <p role="status" className="branding-editor__message">{message}</p>}
      {preview && preview.errorCount > 0 && <ul className="branding-editor__issues">{preview.items.flatMap(i => i.issues).filter(i => i.isError).map(i => <li key={i.code + i.entityKey}><strong>{i.code}</strong> {i.message}</li>)}</ul>}
    </div>
  </div>;
}
function RuntimePlaybackProjectSetting({ locale, value, onChange }: { locale: EngineeringLocale; value: boolean; onChange: (value:boolean)=>void }) {
  const copy = locale === 'en'
    ? { title:'Runtime', label:'Make Historical Playback available in Runtime', help:'Hidden by default. When enabled, authorized operators get a compact Playback tool in Runtime.' }
    : locale === 'es'
      ? { title:'Runtime', label:'Habilitar Playback histórico en Runtime', help:'Oculto por defecto. Al habilitarlo, operadores autorizados reciben una herramienta compacta de Playback.' }
      : { title:'Runtime', label:'Disponibilizar Playback histórico no Runtime', help:'Oculto por padrão. Ao habilitar, operadores autorizados recebem uma ferramenta compacta de Playback no Runtime.' };
  return <fieldset className="eng-panel" data-testid="runtime-playback-project-setting">
    <legend>{copy.title}</legend>
    <label><input type="checkbox" checked={value} onChange={e=>onChange(e.target.checked)} /> <span>{copy.label}</span></label>
    <small>{copy.help}</small>
  </fieldset>;
}
