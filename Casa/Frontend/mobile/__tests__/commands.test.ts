jest.mock('../src/core/secureSession', () => ({ saveRefreshToken: jest.fn(), readRefreshToken: jest.fn(), deleteRefreshToken: jest.fn().mockResolvedValue(undefined) }));
process.env.EXPO_PUBLIC_IDENTITY_URL = 'https://identity.example';
process.env.EXPO_PUBLIC_HOME_URL = 'https://home.example';

import {
  beginCommand,
  clearCommands,
  completeTerminalCommand,
  createCommandRequest,
  getTracked,
  handleCommandPreAdmissionRejection,
  isUnresolved,
  markCommandAmbiguous,
  reconcileMissingCommand,
  recoverTrackedCommand,
  releaseRejectedCommand,
  updateTracked,
} from '../src/core/commands';

const { api, clearSession, ServiceError } = require('../src/core/api') as typeof import('../src/core/api');
const fetchMock = jest.fn();
global.fetch = fetchMock;
const response = (body: unknown, status = 200) => ({ ok: status < 400, status, json: async () => body });

beforeEach(() => {
  clearCommands();
  clearSession();
  fetchMock.mockReset();
});

async function login() {
  fetchMock
    .mockResolvedValueOnce(response({ accessToken: 'test-access', refreshToken: 'test-refresh', expiresIn: 300, sessionId: 'test-session', userId: 'test-user' }))
    .mockResolvedValueOnce(response({ userId: 'test-user', email: 'test@example.com', houses: [{ houseId: 'house', role: 'admin' }] }));
  await api.login('test@example.com', 'test-password');
}

it('retains ambiguous orders across navigation and blocks duplicate submissions while unresolved', () => {
  const now = 1_700_000_000_000;
  const doorReq = createCommandRequest({ houseId: 'house', targetKind: 'capability', targetId: 'door', type: 'OpenDoor', payload: {} }, now);
  expect(beginCommand('house:door', 'key', doorReq, now)).toEqual({ ok: true, key: 'key', request: doorReq, reused: false });
  updateTracked('house:door', { sending: false });
  expect(beginCommand('house:door', 'second-key', doorReq, now + 1000)).toEqual({ ok: false, reason: 'unresolved' });
  expect(getTracked('house:door').key).toBe('key');
  updateTracked('house:door', { command: { commandId: 'order', status: 'unknown' } });
  expect(isUnresolved(getTracked('house:door').command!)).toBe(true);
});

it('isolates scopes by house and device and clears key on terminal status', () => {
  const now = 1_700_000_000_000;
  const r1 = createCommandRequest({ houseId: 'house', targetKind: 'light', targetId: '1', type: 'SetLight', payload: { on: true } }, now);
  const r2 = createCommandRequest({ houseId: 'house', targetKind: 'light', targetId: '2', type: 'SetLight', payload: { on: true } }, now);
  expect(beginCommand('house:light:1', 'key', r1, now)).toMatchObject({ ok: true, key: 'key', reused: false });
  expect(beginCommand('house:light:2', 'other', r2, now)).toMatchObject({ ok: true, key: 'other', reused: false });
  completeTerminalCommand('house:light:1', { commandId: 'order', status: 'executed' });
  expect(beginCommand('house:light:1', 'new', r1, now + 500)).toMatchObject({ ok: true, key: 'new', reused: false });
});

it('releases first-attempt pre-admission rejections immediately when there was no prior ambiguity', () => {
  const now = 1_700_000_000_000;
  const req = createCommandRequest({ houseId: 'house', targetKind: 'light', targetId: '1', type: 'SetLight', payload: { on: true } }, now);
  expect(beginCommand('house:light:1', 'key-pre', req, now)).toMatchObject({ ok: true, key: 'key-pre', reused: false });
  expect(handleCommandPreAdmissionRejection('house:light:1', 'deviceOffline')).toBe('released');
  expect(getTracked('house:light:1')).toMatchObject({ unresolved: false, ambiguous: false, key: '', request: null, command: null });
  expect(beginCommand('house:light:1', 'key-next', req, now + 100)).toMatchObject({ ok: true, key: 'key-next', reused: false });

  // duplicateChanged always retains identity to enforce recovery.
  expect(handleCommandPreAdmissionRejection('house:light:1', 'duplicateChanged')).toBe('retained');
  expect(getTracked('house:light:1')).toMatchObject({ unresolved: true, ambiguous: true, key: 'key-next' });
  releaseRejectedCommand('house:light:1');
  expect(getTracked('house:light:1').key).toBe('');
});

it('Case 1: never reuses a conserved key when the user changes action, payload, or target; keeps recovery bound to the original request', async () => {
  await login();
  const scope = 'sess:house:light:light-1';
  const t0 = 1_700_000_000_000;
  const originalReq = createCommandRequest({
    houseId: 'house',
    targetKind: 'light',
    targetId: 'light-1',
    type: 'SetLight',
    payload: { on: true },
  }, t0);

  // 1. Original request sent and becomes ambiguous (503).
  expect(beginCommand(scope, 'K1', originalReq, t0)).toEqual({ ok: true, key: 'K1', request: originalReq, reused: false });
  fetchMock.mockResolvedValueOnce(response({ status: 503, code: 'serviceUnavailable', correlationId: 'c-503' }, 503));
  await expect(api.light(originalReq.houseId, originalReq.targetId, 'SetLight', { on: true }, 'K1', originalReq.expiresAtUtc)).rejects.toMatchObject({ definitelyNotApplied: false });
  markCommandAmbiguous(scope);

  // 2. Early by-key check returns 404 before deadline -> key and original request remain conserved.
  fetchMock.mockResolvedValueOnce(response({ status: 404, code: 'notFound' }, 404));
  await expect(api.commandByKey('house', getTracked(scope).key)).rejects.toMatchObject({ status: 404 });
  expect(reconcileMissingCommand(scope)).toBe('conserved');
  expect(getTracked(scope)).toMatchObject({ unresolved: false, ambiguous: true, key: 'K1', request: originalReq });

  const callsBeforeMismatch = fetchMock.mock.calls.length;

  // 3a. User changes payload (SetLight { on: false }).
  const flippedPayloadReq = createCommandRequest({ houseId: 'house', targetKind: 'light', targetId: 'light-1', type: 'SetLight', payload: { on: false } }, t0 + 5000);
  expect(beginCommand(scope, 'K2', flippedPayloadReq, t0 + 5000)).toEqual({ ok: false, reason: 'mismatch' });

  // 3b. User changes action (SetLightingMode { mode: 'auto' }).
  const modeReq = createCommandRequest({ houseId: 'house', targetKind: 'light', targetId: 'light-1', type: 'SetLightingMode', payload: { mode: 'auto' } }, t0 + 5000);
  expect(beginCommand(scope, 'K3', modeReq, t0 + 5000)).toEqual({ ok: false, reason: 'mismatch' });

  // 3c. User changes targetId.
  const otherTargetReq = createCommandRequest({ houseId: 'house', targetKind: 'light', targetId: 'light-2', type: 'SetLight', payload: { on: true } }, t0 + 5000);
  expect(beginCommand(scope, 'K4', otherTargetReq, t0 + 5000)).toEqual({ ok: false, reason: 'mismatch' });

  // No HTTP requests were sent and original identity (K1, originalReq) stayed bound.
  expect(fetchMock.mock.calls.length).toBe(callsBeforeMismatch);
  expect(getTracked(scope)).toMatchObject({ key: 'K1', request: originalReq, ambiguous: true, unresolved: false });

  // 4. Recovery query by conserved K1 confirms the delayed SetLight { on: true } was committed.
  fetchMock.mockResolvedValueOnce(response({ commandId: 'light-cmd-1', status: 'executed' }, 200));
  const recovered = await api.commandByKey('house', getTracked(scope).key);
  expect(recovered).toEqual({ commandId: 'light-cmd-1', status: 'executed' });
  completeTerminalCommand(scope, recovered);
  expect(getTracked(scope)).toMatchObject({ key: '', request: null, ambiguous: false, unresolved: false });
});

it('Case 2: second-attempt pre-admission rejection does not erase delayed first-attempt identity', async () => {
  await login();
  const scope = 'sess:house:door:door-1';
  const t0 = 1_700_000_000_000;
  const doorReq = createCommandRequest({ houseId: 'house', targetKind: 'capability', targetId: 'door-1', type: 'OpenDoor', payload: {} }, t0);

  // 1. Original OpenDoor request hangs/aborts on the client while delayed on server.
  const firstStart = beginCommand(scope, 'K1', doorReq, t0);
  expect(firstStart).toEqual({ ok: true, key: 'K1', request: doorReq, reused: false });
  fetchMock.mockRejectedValueOnce(new Error('network timeout'));
  await expect(api.capability('house', 'door-1', 'OpenDoor', {}, firstStart.ok ? firstStart.key : '', doorReq.expiresAtUtc)).rejects.toMatchObject({ definitelyNotApplied: false });
  markCommandAmbiguous(scope);

  // 2. Early by-key query returns 404 before delayed admission commits -> conserve K1 + doorReq.
  fetchMock.mockResolvedValueOnce(response({ status: 404, code: 'notFound' }, 404));
  await expect(api.commandByKey('house', 'K1')).rejects.toMatchObject({ status: 404 });
  expect(reconcileMissingCommand(scope)).toBe('conserved');

  // 3. Within window (t0 + 4000), user repeats OpenDoor -> reuses K1 AND original expiresAtUtc.
  const retryCandidate = createCommandRequest({ houseId: 'house', targetKind: 'capability', targetId: 'door-1', type: 'OpenDoor', payload: {} }, t0 + 4000);
  const secondStart = beginCommand(scope, 'K2', retryCandidate, t0 + 4000);
  expect(secondStart).toEqual({ ok: true, key: 'K1', request: doorReq, reused: true });

  // 4. Second attempt receives pre-admission 409 deviceOffline while attempt 1 is still delayed!
  fetchMock.mockResolvedValueOnce(response({ status: 409, code: 'deviceOffline', correlationId: 'c-offline' }, 409));
  let caught: unknown;
  try {
    await api.capability('house', 'door-1', 'OpenDoor', {}, 'K1', doorReq.expiresAtUtc);
  } catch (err) {
    caught = err;
  }
  expect(caught).toBeInstanceOf(ServiceError);
  expect((caught as InstanceType<typeof ServiceError>).definitelyNotApplied).toBe(true);
  expect(handleCommandPreAdmissionRejection(scope, (caught as InstanceType<typeof ServiceError>).code)).toBe('retained');

  // Original identity (K1 + doorReq) MUST remain retained and unresolved!
  expect(getTracked(scope)).toMatchObject({ key: 'K1', request: doorReq, ambiguous: true, unresolved: true, sending: false, command: null });

  // Same guarantee holds if second attempt receives pre-admission 422 requestExpired while ambiguous.
  expect(handleCommandPreAdmissionRejection(scope, 'requestExpired')).toBe('retained');
  expect(getTracked(scope)).toMatchObject({ key: 'K1', request: doorReq, ambiguous: true, unresolved: true });

  // 5. Delayed attempt 1 finishes committing on the server; by-key lookup recovers door-cmd-1.
  fetchMock.mockResolvedValueOnce(response({ commandId: 'door-cmd-1', status: 'executed' }, 200));
  const recovered = await api.commandByKey('house', getTracked(scope).key);
  expect(recovered).toEqual({ commandId: 'door-cmd-1', status: 'executed' });
  completeTerminalCommand(scope, recovered);
  expect(getTracked(scope)).toMatchObject({ key: '', request: null, ambiguous: false, unresolved: false });

  // 6. Only now can a subsequent request obtain a fresh key K3.
  const freshReq = createCommandRequest({ houseId: 'house', targetKind: 'capability', targetId: 'door-1', type: 'OpenDoor', payload: {} }, t0 + 5000);
  expect(beginCommand(scope, 'K3', freshReq, t0 + 5000)).toEqual({ ok: true, key: 'K3', request: freshReq, reused: false });
});

it('Case 3: enforces original deadline on retry without renewing it and never deletes the key using phone clock', async () => {
  await login();
  const scope = 'sess:house:door:door-1';
  const t0 = 1_700_000_000_000;
  const originalReq = createCommandRequest({ houseId: 'house', targetKind: 'capability', targetId: 'door-1', type: 'OpenDoor', payload: {} }, t0);
  const originalDeadline = t0 + 10_000;
  expect(Date.parse(originalReq.expiresAtUtc)).toBe(originalDeadline);

  // 1. Attempt 1 times out at t0.
  expect(beginCommand(scope, 'K1', originalReq, t0)).toMatchObject({ ok: true, key: 'K1', reused: false });
  fetchMock.mockRejectedValueOnce(new Error('timeout'));
  await expect(api.capability('house', 'door-1', 'OpenDoor', {}, 'K1', originalReq.expiresAtUtc)).rejects.toMatchObject({ definitelyNotApplied: false });
  markCommandAmbiguous(scope);

  // 2. 404 before deadline -> conserves K1 + originalReq.
  fetchMock.mockResolvedValueOnce(response({ status: 404, code: 'notFound' }, 404));
  await expect(api.commandByKey('house', 'K1', originalReq.expiresAtUtc)).rejects.toMatchObject({ status: 404 });
  expect(reconcileMissingCommand(scope)).toBe('conserved');

  // 3. Recovery retry within window (t0 + 6000) reuses K1 and MUST send the original expiresAtUtc (not t0 + 16000).
  const candidateInWindow = createCommandRequest({ houseId: 'house', targetKind: 'capability', targetId: 'door-1', type: 'OpenDoor', payload: {} }, t0 + 6000);
  const recovery = beginCommand(scope, 'K2', candidateInWindow, t0 + 6000);
  expect(recovery).toEqual({ ok: true, key: 'K1', request: originalReq, reused: true });
  fetchMock.mockResolvedValueOnce(response({ status: 503, code: 'serviceUnavailable' }, 503));
  await expect(api.capability('house', 'door-1', 'OpenDoor', {}, 'K1', recovery.ok ? recovery.request.expiresAtUtc : '')).rejects.toMatchObject({ status: 503 });
  expect(JSON.parse(fetchMock.mock.calls[fetchMock.mock.calls.length - 1][1].body).expiresAtUtc).toBe(originalReq.expiresAtUtc);
  markCommandAmbiguous(scope);

  fetchMock.mockResolvedValueOnce(response({ status: 404, code: 'notFound' }, 404));
  await expect(api.commandByKey('house', 'K1', originalReq.expiresAtUtc)).rejects.toMatchObject({ status: 404 });
  expect(reconcileMissingCommand(scope)).toBe('conserved');

  // 4. After local deadline, retrying same action MUST return expired without renewing or calling network.
  const callsBeforeExpiredTap = fetchMock.mock.calls.length;
  const expiredCandidate = createCommandRequest({ houseId: 'house', targetKind: 'capability', targetId: 'door-1', type: 'OpenDoor', payload: {} }, originalDeadline);
  expect(beginCommand(scope, 'K3', expiredCandidate, originalDeadline)).toEqual({ ok: false, reason: 'expired' });
  expect(fetchMock.mock.calls.length).toBe(callsBeforeExpiredTap);
  expect(getTracked(scope)).toMatchObject({ key: 'K1', request: originalReq, ambiguous: true });

  // Even far past phone deadline, reconcileMissingCommand never clears K1 on phone clock alone.
  const nowSpy = jest.spyOn(Date, 'now').mockReturnValue(originalDeadline + 60_000);
  try {
    expect(reconcileMissingCommand(scope)).toBe('conserved');
    expect(getTracked(scope)).toMatchObject({ key: 'K1', request: originalReq, unresolved: false });
  } finally {
    nowSpy.mockRestore();
  }
});

it('Closure 1: keeps key intact when phone clock is ahead of server clock and releases only after authoritative 422 requestExpired + reconciliation', async () => {
  await login();
  const scope = 'sess:house:door:door-1';
  const t0 = 1_700_000_000_000;
  const originalReq = createCommandRequest({ houseId: 'house', targetKind: 'capability', targetId: 'door-1', type: 'OpenDoor', payload: {} }, t0);
  const originalDeadline = t0 + 10_000;

  expect(beginCommand(scope, 'K1', originalReq, t0)).toMatchObject({ ok: true, key: 'K1' });
  fetchMock.mockRejectedValueOnce(new Error('timeout'));
  await expect(api.capability('house', 'door-1', 'OpenDoor', {}, 'K1', originalReq.expiresAtUtc)).rejects.toMatchObject({ definitelyNotApplied: false });
  markCommandAmbiguous(scope);

  // Phone clock is 60s ahead of server clock, while server has NOT expired and returns 404 (pre- and post-reconcile).
  const nowSpy = jest.spyOn(Date, 'now').mockReturnValue(originalDeadline + 60_000);
  try {
    fetchMock
      .mockResolvedValueOnce(response({ status: 404, code: 'notFound' }, 404))
      .mockResolvedValueOnce(response({ status: 404, code: 'notFound' }, 404));
    const reconcile = jest.fn().mockResolvedValue(undefined);

    const msg = await recoverTrackedCommand(scope, {
      fetchCommand: id => api.command(id),
      findByKey: (key, expiresAtUtc) => api.commandByKey('house', key, expiresAtUtc),
      reconcile,
    });

    expect(reconcile).toHaveBeenCalledTimes(1);
    expect(msg).toContain('reutilizará la misma clave de idempotencia');
    expect(getTracked(scope)).toMatchObject({ key: 'K1', request: originalReq, unresolved: false });

    // Server clock still had not expired, so delayed request commits and recovers by K1!
    fetchMock.mockResolvedValueOnce(response({ commandId: 'door-cmd-late', status: 'executed' }, 200));
    await expect(recoverTrackedCommand(scope, {
      fetchCommand: id => api.command(id),
      findByKey: (key, expiresAtUtc) => api.commandByKey('house', key, expiresAtUtc),
      reconcile,
    })).resolves.toBe('');
    expect(getTracked(scope)).toMatchObject({ key: '', request: null, unresolved: false });
  } finally {
    nowSpy.mockRestore();
  }

  // Second check: server confirms 422 requestExpired, but reconciliation fails -> key MUST NOT be released yet.
  const req2 = createCommandRequest({ houseId: 'house', targetKind: 'capability', targetId: 'door-1', type: 'OpenDoor', payload: {} }, t0 + 70_000);
  expect(beginCommand(scope, 'K2', req2, t0 + 70_000)).toMatchObject({ ok: true, key: 'K2' });
  markCommandAmbiguous(scope);

  fetchMock.mockResolvedValueOnce(response({ status: 422, code: 'requestExpired', correlationId: 'c-exp' }, 422));
  await expect(recoverTrackedCommand(scope, {
    fetchCommand: id => api.command(id),
    findByKey: (key, expiresAtUtc) => api.commandByKey('house', key, expiresAtUtc),
    reconcile: async () => { throw new Error('status refresh failed'); },
  })).rejects.toThrow('status refresh failed');
  expect(getTracked(scope)).toMatchObject({ key: 'K2', request: req2, unresolved: true });

  // When server confirms 422 requestExpired AND reconciliation succeeds -> K2 is released and user can start K3.
  fetchMock.mockResolvedValueOnce(response({ status: 422, code: 'requestExpired', correlationId: 'c-exp' }, 422));
  const releasedMsg = await recoverTrackedCommand(scope, {
    fetchCommand: id => api.command(id),
    findByKey: (key, expiresAtUtc) => api.commandByKey('house', key, expiresAtUtc),
    reconcile: async () => undefined,
  });
  expect(releasedMsg).toContain('plazo de la solicitud venció');
  expect(getTracked(scope)).toMatchObject({ key: '', request: null, unresolved: false, command: null });

  const req3 = createCommandRequest({ houseId: 'house', targetKind: 'capability', targetId: 'door-1', type: 'OpenDoor', payload: {} }, t0 + 90_000);
  expect(beginCommand(scope, 'K3', req3, t0 + 90_000)).toEqual({ ok: true, key: 'K3', request: req3, reused: false });
});

it('Closure 2: initial 404 followed by delayed admission during state reconciliation adopts pending or executed order without losing key', async () => {
  await login();
  const scope = 'sess:house:door:door-1';
  const t0 = 1_700_000_000_000;
  const reqPending = createCommandRequest({ houseId: 'house', targetKind: 'capability', targetId: 'door-1', type: 'OpenDoor', payload: {} }, t0);

  expect(beginCommand(scope, 'K1', reqPending, t0)).toMatchObject({ ok: true, key: 'K1' });
  markCommandAmbiguous(scope);

  // Initial by-key returns 404; during reconcile(), delayed admission finishes with pending; post-reconcile lookup finds it!
  fetchMock
    .mockResolvedValueOnce(response({ status: 404, code: 'notFound' }, 404))
    .mockResolvedValueOnce(response({ commandId: 'door-cmd-pending', status: 'pending' }, 200));

  const msgPending = await recoverTrackedCommand(scope, {
    fetchCommand: id => api.command(id),
    findByKey: (key, expiresAtUtc) => api.commandByKey('house', key, expiresAtUtc),
    reconcile: async () => undefined,
  });

  expect(msgPending).toBe('');
  expect(getTracked(scope)).toMatchObject({
    key: 'K1',
    request: reqPending,
    unresolved: true,
    command: { commandId: 'door-cmd-pending', status: 'pending' },
  });

  // Reset to ambiguous before checking delayed admission finishing directly as executed during reconcile().
  clearCommands();
  const reqExec = createCommandRequest({ houseId: 'house', targetKind: 'capability', targetId: 'door-1', type: 'OpenDoor', payload: {} }, t0 + 1000);
  expect(beginCommand(scope, 'K2', reqExec, t0 + 1000)).toMatchObject({ ok: true, key: 'K2' });
  markCommandAmbiguous(scope);

  fetchMock
    .mockResolvedValueOnce(response({ status: 404, code: 'notFound' }, 404))
    .mockResolvedValueOnce(response({ commandId: 'door-cmd-exec', status: 'executed' }, 200));
  const reconcileExec = jest.fn().mockResolvedValue(undefined);

  await expect(recoverTrackedCommand(scope, {
    fetchCommand: id => api.command(id),
    findByKey: (key, expiresAtUtc) => api.commandByKey('house', key, expiresAtUtc),
    reconcile: reconcileExec,
  })).resolves.toBe('');

  expect(reconcileExec).toHaveBeenCalledTimes(2);
  expect(getTracked(scope)).toMatchObject({
    key: '',
    request: null,
    unresolved: false,
    command: { commandId: 'door-cmd-exec', status: 'executed' },
  });
});

it('Closure 3: preserves claimed overdue order whose server outcome is unknown and blocks new orders until terminal', async () => {
  await login();
  const scope = 'sess:house:door:door-1';
  const t0 = 1_700_000_000_000;
  const req = createCommandRequest({ houseId: 'house', targetKind: 'capability', targetId: 'door-1', type: 'OpenDoor', payload: {} }, t0);
  const originalDeadline = t0 + 10_000;

  expect(beginCommand(scope, 'K1', req, t0)).toMatchObject({ ok: true, key: 'K1' });
  markCommandAmbiguous(scope);

  // Even past deadline on phone clock, server returns 200 with status 'unknown' (claimed before expiry).
  const nowSpy = jest.spyOn(Date, 'now').mockReturnValue(originalDeadline + 20_000);
  try {
    fetchMock.mockResolvedValueOnce(response({ commandId: 'door-cmd-unknown', status: 'unknown' }, 200));
    await expect(recoverTrackedCommand(scope, {
      fetchCommand: id => api.command(id),
      findByKey: (key, expiresAtUtc) => api.commandByKey('house', key, expiresAtUtc),
      reconcile: async () => undefined,
    })).resolves.toBe('');

    expect(getTracked(scope)).toMatchObject({
      key: 'K1',
      request: req,
      unresolved: true,
      command: { commandId: 'door-cmd-unknown', status: 'unknown' },
    });

    // Must remain blocked while status is unknown.
    const nextReq = createCommandRequest({ houseId: 'house', targetKind: 'capability', targetId: 'door-1', type: 'OpenDoor', payload: {} }, originalDeadline + 20_000);
    expect(beginCommand(scope, 'K2', nextReq, originalDeadline + 20_000)).toEqual({ ok: false, reason: 'unresolved' });

    // Only when server later reports a terminal status (executed) can K1 clear cleanly.
    fetchMock.mockResolvedValueOnce(response({ commandId: 'door-cmd-unknown', status: 'executed' }, 200));
    await expect(recoverTrackedCommand(scope, {
      fetchCommand: id => api.command(id),
      findByKey: (key, expiresAtUtc) => api.commandByKey('house', key, expiresAtUtc),
      reconcile: async () => undefined,
    })).resolves.toBe('');

    expect(getTracked(scope)).toMatchObject({
      key: '',
      request: null,
      unresolved: false,
      command: { commandId: 'door-cmd-unknown', status: 'executed' },
    });
  } finally {
    nowSpy.mockRestore();
  }
});
