import React, { useEffect, useState } from 'react';
import type { EngineeringLocale } from '../i18n';
import { loadScriptEngineeringContext } from './scriptEngineeringApi';
import type { ScriptVisualEventReference } from './scriptEngineeringTypes';
import { ScriptAssistantPanel } from './ScriptAssistantPanel';
import { PythonScriptReferenceDiagnostics } from './PythonScriptReferenceDiagnostics';

export type PythonScriptAssistantProps = Readonly<{
  locale: EngineeringLocale;
  scriptId: string;
  onInsert(code: string): void;
}>;

/**
 * Adapter between the Monaco editor and the canonical Script Engineering model.
 * The assistant never infers visual ownership from names or project layout; it
 * uses persisted ScriptVisualEventReference records for the selected script.
 */
export function PythonScriptAssistant({
  locale,
  scriptId,
  onInsert
}: PythonScriptAssistantProps) {
  const [visualEventReferences, setVisualEventReferences] = useState<readonly ScriptVisualEventReference[]>([]);
  const [canonicalSource, setCanonicalSource] = useState('');

  useEffect(() => {
    let active = true;

    void loadScriptEngineeringContext()
      .then(context => {
        if (!active) return;
        setCanonicalSource(context.scripts.find(script => script.id === scriptId)?.source ?? '');
        setVisualEventReferences(Object.freeze(
          context.visualEventReferences
            .filter(reference => reference.scriptId === scriptId)
            .map(reference => Object.freeze({
              ...reference,
              tagReference: reference.tagReference ? {
                tagId: reference.tagReference.tagId,
                selector: reference.tagReference.selector ? { ...reference.tagReference.selector } : null
              } : null
            }))
        ));
      })
      .catch(() => {
        if (!active) return;
        setCanonicalSource('');
        setVisualEventReferences(Object.freeze([]));
      });

    return () => {
      active = false;
    };
  }, [scriptId]);

  return (
    <>
      <PythonScriptReferenceDiagnostics locale={locale} source={canonicalSource} />
      <ScriptAssistantPanel
        locale={locale}
        visualEventReferences={visualEventReferences}
        onInsert={onInsert}
      />
    </>
  );
}
