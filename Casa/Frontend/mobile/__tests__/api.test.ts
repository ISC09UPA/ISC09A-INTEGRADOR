jest.mock('../src/core/secureSession', () => ({ saveRefreshToken: jest.fn(), readRefreshToken: jest.fn(), deleteRefreshToken: jest.fn().mockResolvedValue(undefined) }));
process.env.EXPO_PUBLIC_IDENTITY_URL = 'https://identity.example';
process.env.EXPO_PUBLIC_HOME_URL = 'https://home.example';
process.env.EXPO_PUBLIC_SECURITY_URL = 'https://security.example';
// Controlled responses are confined to tests, never shipped as app data.
const { api, clearSession, hasSession, setExpiryHandler } = require('../src/core/api') as typeof import('../src/core/api');
const fetchMock = jest.fn();
global.fetch = fetchMock;
const response = (body: unknown, status = 200) => ({ ok: status < 400, status, json: async () => body });
beforeEach(() => { clearSession(); fetchMock.mockReset(); });
async function login() {
  fetchMock.mockResolvedValueOnce(response({ accessToken: 'test-access', refreshToken: 'test-refresh', expiresIn: 300, sessionId: 'test-session', userId: 'test-user' })).mockResolvedValueOnce(response({ userId: 'test-user', email: 'test@example.com', houses: [{ houseId: 'house', role: 'resident' }] }));
  await api.login('test@example.com', 'test-password');
}
it('rejects unauthenticated house requests without contacting the backend', async () => {
  await expect(api.status('house')).rejects.toMatchObject({ status: 401 });
  expect(fetchMock).not.toHaveBeenCalled();
});
it('preserves a 202 order as pending and sends the same idempotency key', async () => {
  await login(); fetchMock.mockResolvedValueOnce(response({ commandId: 'order', status: 'pending' }, 202));
  const result = await api.light('house', 'light', 'SetLight', { on: true }, 'same-key');
  expect(result.status).toBe('pending');
  expect(fetchMock.mock.calls[2][0]).toBe('https://home.example/api/v1/houses/house/lights/light/commands');
  expect(JSON.parse(fetchMock.mock.calls[2][1].body).idempotencyKey).toBe('same-key');
  expect(fetchMock.mock.calls[2][1].headers.Authorization).toBe('Bearer test-access');
});
it('does not automatically retry an ambiguous physical action', async () => {
  await login(); fetchMock.mockRejectedValueOnce(new TypeError('network'));
  await expect(api.light('house', 'light', 'SetLight', { on: true }, 'key')).rejects.toMatchObject({ definitelyNotApplied: false });
  expect(fetchMock).toHaveBeenCalledTimes(3);
});
it('recovers an ambiguous order by key without submitting another physical action', async () => {
  await login(); fetchMock.mockRejectedValueOnce(new TypeError('network'));
  await expect(api.light('house', 'light', 'SetLight', { on: true }, 'original-key')).rejects.toMatchObject({ definitelyNotApplied: false });
  fetchMock.mockResolvedValueOnce(response({ commandId: 'persisted-order', status: 'pending' }));
  await expect(api.commandByKey('house', 'original-key')).resolves.toMatchObject({ commandId: 'persisted-order', status: 'pending' });
  expect(fetchMock.mock.calls[3][0]).toBe('https://home.example/api/v1/houses/house/commands/by-key/original-key');
  expect(fetchMock.mock.calls[3][1].method).toBe('GET');
  expect(fetchMock.mock.calls.filter(call => call[1].method === 'POST')).toHaveLength(2);
});
it('reports unimplemented endpoints without pretending success', async () => {
  fetchMock.mockResolvedValueOnce(response({}, 501));
  await expect(api.login('test@example.com', 'test-password')).rejects.toMatchObject({ status: 501, definitelyNotApplied: true });
});
it('does not display an invalid security mode as disarmed', async () => {
  await login(); fetchMock.mockResolvedValueOnce(response({ mode: 'unexpected', updatedAtUtc: '2026-11-13T00:00:00Z' }));
  await expect(api.security('house')).rejects.toThrow('El estado de seguridad recibido no es compatible.');
});
it('leaves security unknown when no report exists', async () => {
  await login(); fetchMock.mockResolvedValueOnce(response({}, 404));
  await expect(api.security('house')).resolves.toBeNull();
});
it('loads access history from Security with the house scope and no invented entries', async () => {
  await login(); fetchMock.mockResolvedValueOnce(response({ items: [], nextCursor: null }));
  await expect(api.securityList('house', 'access')).resolves.toEqual({ items: [], nextCursor: undefined });
  expect(fetchMock.mock.calls[2][0]).toBe('https://security.example/api/v1/security/houses/house/activity?type=access');
  expect(fetchMock.mock.calls[2][1].headers.Authorization).toBe('Bearer test-access');
});
it('rejects malformed security history', async () => {
  await login(); fetchMock.mockResolvedValueOnce(response({ items: [{ title: 'Missing id' }], nextCursor: null }));
  await expect(api.securityList('house', 'activity')).rejects.toThrow('La actividad recibida no es compatible.');
});
it('authorizes private image access without putting the session token in its URL', async () => {
  await login();
  const source = await api.captureImageSource('house', 'capture');
  expect(source.uri).toBe('https://security.example/api/v1/security/houses/house/captures/capture/content');
  expect(source.uri).not.toContain('test-access');
  expect(source.headers.Authorization).toBe('Bearer test-access');
});
it('sends capture deletion as an explicit authenticated request', async () => {
  await login(); fetchMock.mockResolvedValueOnce(response({ id: 'capture', status: 'pending' }, 202));
  await expect(api.deleteCapture('house', 'capture')).resolves.toMatchObject({ status: 'pending' });
  expect(fetchMock.mock.calls[2][1].method).toBe('DELETE');
});
it('does not erase a new login when an older request returns unauthorized late', async () => {
  await login();
  let resolveOld!: (value: ReturnType<typeof response>) => void;
  fetchMock.mockImplementationOnce(() => new Promise(resolve => { resolveOld = resolve; }));
  const expired = jest.fn(); setExpiryHandler(expired);
  const oldRequest = api.status('house').catch(error => error);
  clearSession();
  await login();
  resolveOld(response({}, 401));
  await oldRequest;
  expect(hasSession()).toBe(true);
  expect(expired).not.toHaveBeenCalled();
  setExpiryHandler(() => {});
});
it('clears the current session and reports expiration after a protected request returns 401', async () => {
  await login();
  const expired = jest.fn(); setExpiryHandler(expired);
  fetchMock.mockResolvedValueOnce(response({}, 401));
  await expect(api.status('house')).rejects.toMatchObject({ status: 401 });
  expect(hasSession()).toBe(false);
  expect(expired).toHaveBeenCalledTimes(1);
  setExpiryHandler(() => {});
});
it('sends a short-lived door request without choosing a device or pulse', async () => {
  await login(); fetchMock.mockResolvedValueOnce(response({ commandId: 'order', status: 'pending' }, 202));
  const before = Date.now();
  await api.capability('house', 'door', 'OpenDoor', {}, 'key');
  const call = fetchMock.mock.calls[2];
  const body = JSON.parse(call[1].body);
  expect(call[0]).toBe('https://home.example/api/v1/houses/house/resources/door/commands');
  expect(body.payload).toEqual({});
  expect(body.idempotencyKey).toBe('key');
  expect(Date.parse(body.expiresAtUtc)).toBeGreaterThanOrEqual(before + 10000);
  expect(Date.parse(body.expiresAtUtc)).toBeLessThanOrEqual(Date.now() + 10000);
  expect(fetchMock).toHaveBeenCalledTimes(3);
});
it('rejects incompatible resource data instead of enabling a control', async () => {
  await login(); fetchMock.mockResolvedValueOnce(response([{ resourceId: 'door', kind: 'unexpected', deviceReportedAtUtc: null, pendingCommand: null }]));
  await expect(api.resources('house')).rejects.toThrow('Los recursos recibidos no son compatibles.');
});
it('starts a private video session as connecting without inventing a stream URL', async () => {
  await login(); fetchMock.mockResolvedValueOnce(response({ id: 'video', status: 'connecting', expiresAtUtc: '2026-11-13T00:03:00Z', commandId: 'cmd-video', streamUrl: null, streamType: null }, 202));
  await expect(api.startVideo('house', 'camera', 'video-key')).resolves.toEqual({ id: 'video', status: 'connecting', expiresAtUtc: '2026-11-13T00:03:00Z', commandId: 'cmd-video', streamUrl: undefined, streamType: undefined });
  const call = fetchMock.mock.calls[2];
  expect(call[0]).toBe('https://home.example/api/v1/houses/house/video/sessions');
  expect(JSON.parse(call[1].body)).toEqual({ resourceId: 'camera', idempotencyKey: 'video-key' });
  expect(call[1].headers.Authorization).toBe('Bearer test-access');
});
it('rejects a live video session without a protected HLS URL', async () => {
  await login(); fetchMock.mockResolvedValueOnce(response({ id: 'video', status: 'live', expiresAtUtc: '2026-11-13T00:03:00Z', commandId: 'cmd-video', streamUrl: null, streamType: null }));
  await expect(api.videoSession('house', 'video')).rejects.toThrow('La sesión de video recibida no es compatible.');
});
it('builds an authenticated private HLS source without putting the token in the URL', async () => {
  await login();
  const source = await api.videoSource('house', 'video');
  expect(source.uri).toBe('https://home.example/api/v1/houses/house/video/sessions/video/playlist.m3u8');
  expect(source.uri).not.toContain('test-access');
  expect(source.headers.Authorization).toBe('Bearer test-access');
});
it('closes a private video session with an explicit authenticated DELETE', async () => {
  await login(); fetchMock.mockResolvedValueOnce(response({ id: 'video', status: 'ended', expiresAtUtc: '2026-11-13T00:03:00Z', commandId: 'cmd-video', streamUrl: null, streamType: null }));
  await expect(api.stopVideo('house', 'video')).resolves.toMatchObject({ status: 'ended', streamUrl: undefined });
  expect(fetchMock.mock.calls[2][0]).toBe('https://home.example/api/v1/houses/house/video/sessions/video');
  expect(fetchMock.mock.calls[2][1].method).toBe('DELETE');
});
it('recovers a private video session by idempotency key without starting another session', async () => {
  await login(); fetchMock.mockResolvedValueOnce(response({ id: 'video', status: 'connecting', expiresAtUtc: '2026-11-13T00:03:00Z', commandId: 'cmd-video', streamUrl: null, streamType: null }));
  await expect(api.videoByKey('house', 'video-key')).resolves.toMatchObject({ id: 'video', status: 'connecting', streamUrl: undefined });
  expect(fetchMock.mock.calls[2][0]).toBe('https://home.example/api/v1/houses/house/video/sessions/by-key/video-key');
  expect(fetchMock.mock.calls[2][1].method).toBe('GET');
});
it.each([
  ['manualModeRequired', 'Cambia la luz a modo manual antes de encenderla o apagarla.'],
  ['pendingCommand', 'Ya hay una orden en curso para este recurso. Consulta el estado antes de continuar.'],
  ['deviceOffline', 'El dispositivo no tiene un reporte reciente. Actualiza el estado de la casa.'],
  ['duplicateChanged', 'La clave de operación ya se utilizó con otra solicitud.'],
])('treats 409 pre-admission rejection %s as definitelyNotApplied with a specific message', async (code, message) => {
  await login();
  fetchMock.mockResolvedValueOnce(response({ status: 409, code, correlationId: 'corr-1' }, 409));
  await expect(api.light('house', 'light', 'SetLight', { on: true }, 'key')).rejects.toMatchObject({
    status: 409,
    code,
    correlationId: 'corr-1',
    definitelyNotApplied: true,
    message,
  });
});
it('keeps generic 409 conflict and 503 serviceUnavailable as ambiguous (definitelyNotApplied: false)', async () => {
  await login();
  fetchMock.mockResolvedValueOnce(response({ status: 409, code: 'conflict', correlationId: 'corr-conflict' }, 409));
  await expect(api.capability('house', 'door', 'OpenDoor', {}, 'key-1')).rejects.toMatchObject({
    status: 409,
    code: 'conflict',
    definitelyNotApplied: false,
  });
  fetchMock.mockResolvedValueOnce(response({ status: 503, code: 'serviceUnavailable', correlationId: 'corr-503' }, 503));
  await expect(api.capability('house', 'door', 'OpenDoor', {}, 'key-2')).rejects.toMatchObject({
    status: 503,
    code: 'serviceUnavailable',
    definitelyNotApplied: false,
  });
});

it('forwards an explicit expiresAtUtc verbatim on api.light and api.capability without renewing it', async () => {
  await login();
  const lightDeadline = '2026-11-18T12:00:30.000Z';
  const doorDeadline = '2026-11-18T12:00:10.000Z';
  fetchMock.mockResolvedValueOnce(response({ commandId: 'light-1', status: 'pending' }, 202));
  await api.light('house', 'light', 'SetLight', { on: true }, 'key-light', lightDeadline);
  fetchMock.mockResolvedValueOnce(response({ commandId: 'door-1', status: 'pending' }, 202));
  await api.capability('house', 'door', 'OpenDoor', {}, 'key-door', doorDeadline);

  expect(JSON.parse(fetchMock.mock.calls[2][1].body)).toMatchObject({
    idempotencyKey: 'key-light',
    expiresAtUtc: lightDeadline,
  });
  expect(JSON.parse(fetchMock.mock.calls[3][1].body)).toMatchObject({
    idempotencyKey: 'key-door',
    expiresAtUtc: doorDeadline,
  });

  fetchMock.mockResolvedValueOnce(response({ commandId: 'door-1', status: 'pending' }, 200));
  await api.commandByKey('house', 'key-door', doorDeadline);
  expect(fetchMock.mock.calls[4][0]).toBe('https://home.example/api/v1/houses/house/commands/by-key/key-door?expiresAtUtc=2026-11-18T12%3A00%3A10.000Z');
});
