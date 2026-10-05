import React, { createContext, useContext } from 'react';

export type RuntimeActionFeedback = Readonly<{ state: 'pending' | 'accepted' | 'confirmed' | 'failed'; label: string; requestId: string }>;
export const RuntimeActionFeedbackContext = createContext<ReadonlyMap<string, RuntimeActionFeedback>>(new Map());
export const useRuntimeActionFeedback = () => useContext(RuntimeActionFeedbackContext);

export function actionFeedbackLabel(state: RuntimeActionFeedback['state'], locale: string) {
  const copy = locale === 'pt-BR' ? { pending: 'Comando pendente', accepted: 'Enviado · aguardando retorno', confirmed: 'Retorno confirmado', failed: 'Falha no comando' }
    : locale === 'es' ? { pending: 'Comando pendiente', accepted: 'Enviado · esperando retorno', confirmed: 'Retorno confirmado', failed: 'Comando fallido' }
      : { pending: 'Command pending', accepted: 'Sent · awaiting feedback', confirmed: 'Feedback confirmed', failed: 'Command failed' };
  return copy[state];
}
