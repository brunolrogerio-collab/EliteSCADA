import type { ReportExecutionRequest, ReportExecutionResult } from './reportContracts';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');

export class ReportPreviewApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly code?: string
  ) {
    super(message);
    this.name = 'ReportPreviewApiError';
  }
}

export async function previewReportExecution(
  request: ReportExecutionRequest,
  signal?: AbortSignal
): Promise<ReportExecutionResult> {
  const response = await fetch(`${API}/api/reports/preview`, {
    method: 'POST',
    headers: {
      accept: 'application/json',
      'content-type': 'application/json; charset=utf-8'
    },
    body: JSON.stringify(request),
    signal
  });

  if (!response.ok) {
    const body = await response.text();
    let message = body || `${response.status} ${response.statusText}`;
    let code: string | undefined;
    try {
      const parsed = JSON.parse(body) as { error?: string; code?: string };
      message = parsed.error ?? message;
      code = parsed.code;
    } catch {
      // Plain-text platform errors remain useful without inventing another envelope.
    }
    throw new ReportPreviewApiError(message, response.status, code);
  }

  return await response.json() as ReportExecutionResult;
}
