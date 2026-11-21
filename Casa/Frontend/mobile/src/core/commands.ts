import type { Command } from './models';

export type CommandTargetKind = 'light' | 'capability';
export type CommandType = 'SetLight' | 'SetLightingMode' | 'SetSecurityMode' | 'OpenDoor' | 'CaptureImage';
export type CommandPayload =
  | { on: boolean }
  | { mode: 'manual' | 'auto' }
  | { mode: 'armed' | 'disarmed' }
  | Record<string, never>;

export type CommandRequestSpec = {
  houseId: string;
  targetKind: CommandTargetKind;
  targetId: string;
  type: CommandType;
  payload: CommandPayload;
  expiresAtUtc: string;
};

export type TrackedCommand = {
  command: Command | null;
  unresolved: boolean;
  key: string;
  request: CommandRequestSpec | null;
  ambiguous: boolean;
  sending: boolean;
};

export type BeginCommandResult =
  | { ok: true; key: string; request: CommandRequestSpec; reused: boolean }
  | { ok: false; reason: 'unresolved' | 'mismatch' | 'expired' };

const empty: TrackedCommand = {
  command: null,
  unresolved: false,
  key: '',
  request: null,
  ambiguous: false,
  sending: false,
};

const records = new Map<string, TrackedCommand>();
const listeners = new Set<() => void>();

function notify() {
  listeners.forEach(listener => listener());
}

function normalizePayload(payload: CommandPayload): CommandPayload {
  const entries = Object.entries(payload as Record<string, unknown>)
    .filter(([, value]) => value !== undefined)
    .sort(([a], [b]) => a.localeCompare(b));
  return Object.fromEntries(entries) as CommandPayload;
}

function normalizeRequest(request: CommandRequestSpec): CommandRequestSpec {
  return {
    houseId: request.houseId,
    targetKind: request.targetKind,
    targetId: request.targetId,
    type: request.type,
    payload: normalizePayload(request.payload),
    expiresAtUtc: request.expiresAtUtc,
  };
}

export function commandTtlMs(type: CommandType): number {
  return type === 'OpenDoor' ? 10000 : 30000;
}

export function createCommandRequest(
  input: Omit<CommandRequestSpec, 'expiresAtUtc'> & { expiresAtUtc?: string },
  nowMs = Date.now(),
): CommandRequestSpec {
  return {
    houseId: input.houseId,
    targetKind: input.targetKind,
    targetId: input.targetId,
    type: input.type,
    payload: normalizePayload(input.payload),
    expiresAtUtc: input.expiresAtUtc ?? new Date(nowMs + commandTtlMs(input.type)).toISOString(),
  };
}

export function requestSignature(request: Omit<CommandRequestSpec, 'expiresAtUtc'>): string {
  return JSON.stringify({
    houseId: request.houseId,
    targetKind: request.targetKind,
    targetId: request.targetId,
    type: request.type,
    payload: normalizePayload(request.payload),
  });
}

export function getTracked(scope: string): TrackedCommand {
  return records.get(scope) ?? empty;
}

export function subscribeCommands(listener: () => void) {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export function updateTracked(scope: string, patch: Partial<TrackedCommand>) {
  records.set(scope, { ...getTracked(scope), ...patch });
  notify();
}

export function clearCommands() {
  records.clear();
  notify();
}

export function beginCommand(
  scope: string,
  candidateKey: string,
  candidateRequest: CommandRequestSpec,
  nowMs = Date.now(),
): BeginCommandResult {
  const current = getTracked(scope);
  if (current.sending || current.unresolved) {
    return { ok: false, reason: 'unresolved' };
  }

  const normalizedCandidate = normalizeRequest(candidateRequest);

  if (current.key) {
    if (!current.request || requestSignature(current.request) !== requestSignature(normalizedCandidate)) {
      return { ok: false, reason: 'mismatch' };
    }
    const deadlineMs = Date.parse(current.request.expiresAtUtc);
    if (!Number.isFinite(deadlineMs) || nowMs >= deadlineMs) {
      return { ok: false, reason: 'expired' };
    }
    updateTracked(scope, {
      command: null,
      unresolved: true,
      sending: true,
      key: current.key,
      request: current.request,
      ambiguous: true,
    });
    return { ok: true, key: current.key, request: current.request, reused: true };
  }

  updateTracked(scope, {
    command: null,
    unresolved: true,
    sending: true,
    key: candidateKey,
    request: normalizedCandidate,
    ambiguous: false,
  });
  return { ok: true, key: candidateKey, request: normalizedCandidate, reused: false };
}

export function markCommandAmbiguous(scope: string) {
  const current = getTracked(scope);
  if (!current.key) return;
  updateTracked(scope, { unresolved: true, ambiguous: true, sending: false });
}

export function releaseRejectedCommand(scope: string) {
  updateTracked(scope, {
    command: null,
    unresolved: false,
    key: '',
    request: null,
    ambiguous: false,
    sending: false,
  });
}

export function handleCommandPreAdmissionRejection(scope: string, code?: string): 'released' | 'retained' {
  const current = getTracked(scope);
  if (!current.key) {
    return 'released';
  }
  if (current.ambiguous || code === 'duplicateChanged') {
    updateTracked(scope, {
      command: null,
      unresolved: true,
      ambiguous: true,
      sending: false,
    });
    return 'retained';
  }
  releaseRejectedCommand(scope);
  return 'released';
}

export function reconcileMissingCommand(scope: string): 'conserved' {
  updateTracked(scope, { command: null, unresolved: false, sending: false });
  return 'conserved';
}

export type CommandRecoveryHandlers = {
  fetchCommand: (commandId: string) => Promise<Command>;
  findByKey?: (key: string, expiresAtUtc?: string) => Promise<Command>;
  reconcile: () => Promise<void>;
};

const validStatuses = new Set<Command['status']>(['pending', 'executed', 'rejected', 'expired', 'failed', 'unknown']);

export async function recoverTrackedCommand(scope: string, handlers: CommandRecoveryHandlers): Promise<string> {
  const snapshot = getTracked(scope);
  const current = snapshot.command;
  const key = snapshot.key;
  const expiresAtUtc = snapshot.request?.expiresAtUtc;

  if (!current && (!key || !handlers.findByKey)) {
    await handlers.reconcile();
    return '';
  }

  async function adoptCommand(raw: Command): Promise<string> {
    if (!raw.commandId || !validStatuses.has(raw.status)) {
      throw new Error('Resultado de orden incompatible.');
    }
    updateTracked(scope, { command: raw, unresolved: isUnresolved(raw), sending: false });
    if (!isUnresolved(raw)) {
      await handlers.reconcile();
      completeTerminalCommand(scope, raw);
    }
    return '';
  }

  try {
    const next = current
      ? await handlers.fetchCommand(current.commandId)
      : await handlers.findByKey!(key, expiresAtUtc);
    return await adoptCommand(next);
  } catch (error) {
    const status = (error as { status?: number })?.status;
    const code = (error as { code?: string })?.code;
    if (!current && status === 422 && code === 'requestExpired') {
      await handlers.reconcile();
      releaseRejectedCommand(scope);
      return 'El servicio confirmó que el plazo de la solicitud venció sin que la orden fuera admitida. Se liberó el seguimiento; si aún deseas realizar la acción, puedes solicitar una orden nueva.';
    }
    if (!current && status === 404) {
      await handlers.reconcile();
      try {
        const postReconcile = await handlers.findByKey!(key, expiresAtUtc);
        return await adoptCommand(postReconcile);
      } catch (followUp) {
        const followStatus = (followUp as { status?: number })?.status;
        const followCode = (followUp as { code?: string })?.code;
        if (followStatus === 422 && followCode === 'requestExpired') {
          releaseRejectedCommand(scope);
          return 'El servicio confirmó que el plazo de la solicitud venció sin que la orden fuera admitida. Se liberó el seguimiento; si aún deseas realizar la acción, puedes solicitar una orden nueva.';
        }
        if (followStatus === 404) {
          reconcileMissingCommand(scope);
          return 'La orden aún no aparece registrada en el servicio y se actualizó el estado. Si vuelves a solicitar exactamente la misma acción dentro de su plazo original, se reutilizará la misma clave de idempotencia.';
        }
        throw followUp;
      }
    }
    throw error;
  }
}

export function completeTerminalCommand(scope: string, command: Command) {
  updateTracked(scope, {
    command,
    unresolved: false,
    key: '',
    request: null,
    ambiguous: false,
    sending: false,
  });
}

export function isUnresolved(command: Command) {
  return command.status === 'pending' || command.status === 'unknown';
}
