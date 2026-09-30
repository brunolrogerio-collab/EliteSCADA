import React, { useEffect, useState } from 'react';
import type { ApplicationBrandingEngineering, VisualAssetEngineering } from '../engineering/types';
import { loadRuntimeApplicationProjection, runtimeVisualAssetContentUrl } from '../runtime/application/runtimeApplicationApi';

export type ResolvedApplicationBranding =
  | { mode: 'default'; diagnostic?: string }
  | { mode: 'text'; text: string; subtitle?: string }
  | { mode: 'image'; imageUrl: string; alt: string; text?: string; subtitle?: string }
  | { mode: 'none' };

export function resolveApplicationBranding(
  branding: ApplicationBrandingEngineering | null | undefined,
  assets: readonly VisualAssetEngineering[],
  contentUrl: (id: string) => string
): ResolvedApplicationBranding {
  if (!branding || branding.mode === 'default') return { mode: 'default' };
  if (branding.mode === 'none') return { mode: 'none' };
  if (branding.mode === 'text') {
    const text = branding.text?.trim();
    return text
      ? { mode: 'text', text, subtitle: branding.subtitle?.trim() || undefined }
      : { mode: 'default', diagnostic: 'Configured TEXT branding is empty; showing EliteSCADA.' };
  }
  if (branding.mode === 'image') {
    const id = branding.visualAssetId?.trim();
    const asset = id ? assets.find(x => x.id?.toLowerCase() === id.toLowerCase()) : undefined;
    return asset?.id
      ? { mode: 'image', imageUrl: contentUrl(asset.id), alt: branding.text?.trim() || asset.name || 'Application brand', text: branding.text?.trim() || undefined, subtitle: branding.subtitle?.trim() || undefined }
      : { mode: 'default', diagnostic: 'Configured brand image is missing from the Active application; showing EliteSCADA.' };
  }
  return { mode: 'default', diagnostic: 'Branding mode is invalid; showing EliteSCADA.' };
}

export function useActiveApplicationBranding(): ResolvedApplicationBranding {
  const [value, setValue] = useState<ResolvedApplicationBranding>({ mode: 'default' });
  useEffect(() => {
    const controller = new AbortController();
    void loadRuntimeApplicationProjection(controller.signal).then(projection => {
      if (controller.signal.aborted) return;
      const pkg = projection.mode === 'engineering' ? projection.package : null;
      setValue(resolveApplicationBranding(pkg?.branding, pkg?.visualAssets ?? [], runtimeVisualAssetContentUrl));
    }).catch(reason => {
      if (!controller.signal.aborted) setValue({ mode: 'default', diagnostic: `Active branding is unavailable (${reason instanceof Error ? reason.message : String(reason)}); showing EliteSCADA.` });
    });
    return () => controller.abort();
  }, []);
  return value;
}

export function ApplicationBrand({ branding, defaultSubtitle, href }: { branding: ResolvedApplicationBranding; defaultSubtitle: string; href: string }) {
  const [failed, setFailed] = useState(false);
  useEffect(() => setFailed(false), [branding]);
  const effective: ResolvedApplicationBranding = failed && branding.mode === 'image'
    ? { mode: 'default', diagnostic: 'Configured brand image could not be loaded; showing EliteSCADA.' }
    : branding;
  if (effective.mode === 'none') return null;
  const label = effective.mode === 'text' ? effective.text : effective.mode === 'image' ? effective.alt : 'EliteSCADA';
  const title = effective.mode === 'text' ? effective.text : effective.mode === 'image' ? effective.text : 'EliteSCADA';
  const subtitle = effective.mode === 'text' || effective.mode === 'image' ? effective.subtitle : defaultSubtitle;
  return <a className="app-brand" href={href} aria-label={label} data-branding-mode={effective.mode}>
    {effective.mode === 'default' ? <span className="app-brand-mark" aria-hidden="true">E</span> : null}
    {effective.mode === 'image' ? <img className="app-brand-image" src={effective.imageUrl} alt={effective.alt} onError={() => setFailed(true)} /> : null}
    {(title || subtitle) ? <span className="app-brand-copy">{title ? <strong>{title}</strong> : null}{subtitle ? <small>{subtitle}</small> : null}</span> : null}
    {'diagnostic' in effective && effective.diagnostic ? <span className="app-brand-diagnostic" role="status">{effective.diagnostic}</span> : null}
  </a>;
}
