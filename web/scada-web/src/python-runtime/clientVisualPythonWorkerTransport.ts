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

type PythonBridgeRequestEnvelope = PythonWorkerEnvelope<PythonWorkerRequest> & { kind?: never };
type PythonBridgeResponseEnvelope = PythonWorkerEnvelope<PythonWorkerResponse> & { kind?: never };

export type ClientVisualPythonPrivateWorkerRequest =
  | PythonEngineBootstrapRequest
  | PythonBridgeRequestEnvelope;

export type ClientVisualPythonPrivateWorkerResponse =
  | PythonEngineBootstrapResponse
  | PythonEngineBootstrapFailure
  | PythonBridgeResponseEnvelope;
