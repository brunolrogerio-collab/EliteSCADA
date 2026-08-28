import type {
  PythonRuntimeIdentity,
  PythonWorkerEnvelope,
  PythonWorkerRequest,
  PythonWorkerResponse
} from './pythonRuntimeContracts';

export type PythonEngineBootstrapRequest = {
  kind: 'engine-bootstrap';
  generation: number;
  identity: PythonRuntimeIdentity;
  pyodideIndexUrl: string;
  interruptBuffer: SharedArrayBuffer;
};

export type PythonEngineBootstrapResponse = {
  kind: 'engine-ready';
  generation: number;
  identity: PythonRuntimeIdentity;
};

export type PythonEngineBootstrapFailure = {
  kind: 'engine-bootstrap-failed';
  generation: number;
  identity: PythonRuntimeIdentity;
  sanitizedError: string;
};

export type ClientVisualPythonPrivateWorkerRequest =
  | PythonEngineBootstrapRequest
  | PythonWorkerEnvelope<PythonWorkerRequest>;

export type ClientVisualPythonPrivateWorkerResponse =
  | PythonEngineBootstrapResponse
  | PythonEngineBootstrapFailure
  | PythonWorkerEnvelope<PythonWorkerResponse>;

export function isPythonWorkerEnvelope(
  value: ClientVisualPythonPrivateWorkerRequest | ClientVisualPythonPrivateWorkerResponse
): value is PythonWorkerEnvelope<PythonWorkerRequest | PythonWorkerResponse> {
  return typeof value === 'object' && value !== null && 'bridgeVersion' in value && 'message' in value;
}
