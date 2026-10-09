import React, { createContext, useContext } from 'react';

export type RuntimeRichCommandOutcome = 'Rejected' | 'Accepted' | 'Completed' | 'Failed' | 'TimedOut' | 'Unknown';
export type RuntimeActionFeedbackState = 'pending' | 'accepted' | 'confirmed' | 'failed' | RuntimeRichCommandOutcome;
export type RuntimeActionFeedback = Readonly<{ state: RuntimeActionFeedbackState; label: string; requestId: string }>;
export const RuntimeActionFeedbackContext = createContext<ReadonlyMap<string, RuntimeActionFeedback>>(new Map());
export const useRuntimeActionFeedback = () => useContext(RuntimeActionFeedbackContext);

export function actionFeedbackLabel(state: RuntimeActionFeedback['state'], locale: string) {
  if (state === 'Rejected' || state === 'Accepted' || state === 'Completed' ||
      state === 'Failed' || state === 'TimedOut' || state === 'Unknown') return state;
  const copy = locale === 'pt-BR' ? { pending: 'Comando pendente', accepted: 'Enviado · aguardando retorno', confirmed: 'Retorno confirmado', failed: 'Falha no comando' }
    : locale === 'es' ? { pending: 'Comando pendiente', accepted: 'Enviado · esperando retorno', confirmed: 'Retorno confirmado', failed: 'Comando fallido' }
      : { pending: 'Command pending', accepted: 'Sent · awaiting feedback', confirmed: 'Feedback confirmed', failed: 'Command failed' };
  return copy[state];
}
