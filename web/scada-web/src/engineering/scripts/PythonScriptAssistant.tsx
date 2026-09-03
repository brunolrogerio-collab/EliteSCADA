import React, { useEffect, useState } from 'react';
import type { EngineeringLocale } from '../i18n';
import { loadScriptEngineeringContext } from './scriptEngineeringApi';
import type { ScriptVisualEventReference } from './scriptEngineeringTypes';
import { ScriptAssistantPanel } from './ScriptAssistantPanel';

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

  useEffect(() => {
    const cancellation = new AbortController();
    let active = true;

    void loadScriptEngineeringContext(cancellation.signal)
      .then(context => {
        if (!active) return;
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
        if (active) setVisualEventReferences(Object.freeze([]));
      });

    return () => {
      active = false;
      cancellation.abort();
    };
  }, [scriptId]);

  return (
    <ScriptAssistantPanel
      locale={locale}
      visualEventReferences={visualEventReferences}
      onInsert={onInsert}
    />
  );
}
