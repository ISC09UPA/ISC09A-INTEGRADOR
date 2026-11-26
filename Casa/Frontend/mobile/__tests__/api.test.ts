jest.mock('../src/core/secureSession', () => ({ saveRefreshToken: jest.fn(), readRefreshToken: jest.fn(), deleteRefreshToken: jest.fn().mockResolvedValue(undefined) }));
process.env.EXPO_PUBLIC_IDENTITY_URL = 'https://identity.example';
process.env.EXPO_PUBLIC_HOME_URL = 'https://home.example';
process.env.EXPO_PUBLIC_SECURITY_URL = 'https://security.example';
process.env.EXPO_PUBLIC_NOTIFICATIONS_URL = 'https://notifications.example';
process.env.EXPO_PUBLIC_ASSISTANT_URL = 'https://assistant.example';
// Controlled responses are confined to tests, never shipped as app data.
const { api, clearSession, hasSession, setExpiryHandler, setPermissionRejectedHandler } = require('../src/core/api') as typeof import('../src/core/api');
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

it('loads and saves split security and notification preferences', async () => {
  await login();
  fetchMock
    .mockResolvedValueOnce(response({ captures: true, analysis: false, updatedAtUtc: '2026-11-20T01:00:00Z' }))
    .mockResolvedValueOnce(response({ email: true, verifiedEmail: 'casa@example.com', verifiedAtUtc: '2026-11-20T00:30:00Z', pendingRecipientEmail: null, updatedAtUtc: '2026-11-20T01:05:00Z' }));

  const loaded = await api.preferences('house');
  expect(loaded).toMatchObject({
    captures: true,
    analysis: false,
    email: true,
    verifiedEmail: 'casa@example.com',
    pendingRecipientEmail: null,
  });
  expect(fetchMock.mock.calls[2][0]).toBe('https://security.example/api/v1/security/houses/house/preferences');
  expect(fetchMock.mock.calls[3][0]).toBe('https://notifications.example/api/v1/notifications/houses/house/preferences');

  fetchMock
    .mockResolvedValueOnce(response({ captures: true, analysis: true, updatedAtUtc: '2026-11-20T02:00:00Z' }))
    .mockResolvedValueOnce(response({ email: false, verifiedEmail: 'casa@example.com', verifiedAtUtc: '2026-11-20T00:30:00Z', pendingRecipientEmail: null, updatedAtUtc: '2026-11-20T02:00:00Z' }));

  const saved = await api.savePreferences('house', { captures: true, analysis: true, email: false });
  expect(saved).toMatchObject({ captures: true, analysis: true, email: false });
  expect(fetchMock.mock.calls[4][1].method).toBe('PUT');
  expect(JSON.parse(fetchMock.mock.calls[4][1].body)).toEqual({ captures: true, analysis: true });
  expect(fetchMock.mock.calls[5][1].method).toBe('PUT');
  expect(JSON.parse(fetchMock.mock.calls[5][1].body)).toEqual({ email: false });
});

it('verifies and confirms a notification recipient email', async () => {
  await login();
  fetchMock.mockResolvedValueOnce(response({
    pendingRecipientEmail: 'alertas@example.com',
    status: 'accepted',
    expiresAtUtc: '2026-11-20T03:00:00Z',
    deliveryId: 'delivery-verify-1',
  }, 202));

  const challenge = await api.verifyNotificationRecipient('house', 'alertas@example.com');
  expect(challenge).toMatchObject({ pendingRecipientEmail: 'alertas@example.com', status: 'accepted', deliveryId: 'delivery-verify-1' });
  expect(fetchMock.mock.calls[2][0]).toBe('https://notifications.example/api/v1/notifications/houses/house/recipient/verify');
  expect(JSON.parse(fetchMock.mock.calls[2][1].body)).toEqual({ email: 'alertas@example.com' });

  fetchMock.mockResolvedValueOnce(response({
    email: false,
    verifiedEmail: 'alertas@example.com',
    verifiedAtUtc: '2026-11-20T02:50:00Z',
    pendingRecipientEmail: null,
    updatedAtUtc: '2026-11-20T02:50:00Z',
  }));

  const confirmed = await api.confirmNotificationRecipient('house', '123456');
  expect(confirmed.verifiedEmail).toBe('alertas@example.com');
  expect(fetchMock.mock.calls[3][0]).toBe('https://notifications.example/api/v1/notifications/houses/house/recipient/confirm');
  expect(JSON.parse(fetchMock.mock.calls[3][1].body)).toEqual({ code: '123456' });
});

it('sends test email with Idempotency-Key and queries delivery status', async () => {
  await login();
  fetchMock.mockResolvedValueOnce(response({
    id: 'delivery-1',
    kind: 'test',
    status: 'accepted',
    providerOperationId: 'op-1',
    errorCode: null,
    createdAtUtc: '2026-11-20T04:00:00Z',
    updatedAtUtc: '2026-11-20T04:00:01Z',
  }, 202));

  const created = await api.testEmail('house', 'idem-test-1');
  expect(created).toMatchObject({ id: 'delivery-1', status: 'accepted', kind: 'test' });
  expect(fetchMock.mock.calls[2][0]).toBe('https://notifications.example/api/v1/notifications/houses/house/test-email');
  expect(fetchMock.mock.calls[2][1].headers['Idempotency-Key']).toBe('idem-test-1');

  fetchMock.mockResolvedValueOnce(response({
    id: 'delivery-1',
    kind: 'test',
    status: 'delivered',
    providerOperationId: 'op-1',
    errorCode: null,
    createdAtUtc: '2026-11-20T04:00:00Z',
    updatedAtUtc: '2026-11-20T04:00:05Z',
  }));

  const polled = await api.notificationDelivery('house', 'delivery-1');
  expect(polled.status).toBe('delivered');
  expect(fetchMock.mock.calls[3][0]).toBe('https://notifications.example/api/v1/notifications/houses/house/deliveries/delivery-1');
});

it('rejects enabling email notifications when recipient is not verified', async () => {
  await login();
  fetchMock.mockResolvedValueOnce(response({ status: 422, code: 'recipientNotVerified', correlationId: 'corr-notif' }, 422));
  await expect(api.testEmail('house', 'idem-test-2')).rejects.toMatchObject({
    status: 422,
    code: 'recipientNotVerified',
    definitelyNotApplied: true,
    message: 'Verifica primero un correo destinatario antes de activar los avisos o enviar pruebas.',
  });
});

it('lists, invites, revokes invitations and removes residents via Identity API', async () => {
  await login();
  fetchMock.mockResolvedValueOnce(response({
    members: [{ userId: 'u-1', email: 'admin@example.com', role: 'admin', enabled: true, emailVerified: true }],
    invitations: [{ id: 'inv-1', houseId: 'house', email: 'res@example.com', role: 'resident', status: 'pending', createdAtUtc: '2026-11-20T00:00:00Z', expiresAtUtc: '2026-11-23T00:00:00Z' }],
  }));

  const listed = await api.members('house');
  expect(listed.members).toHaveLength(1);
  expect(listed.invitations).toHaveLength(1);
  expect(fetchMock.mock.calls[2][0]).toBe('https://identity.example/api/v1/identity/houses/house/members');

  fetchMock.mockResolvedValueOnce(response({
    invitation: { id: 'inv-2', houseId: 'house', email: 'nuevo@example.com', role: 'resident', status: 'pending', createdAtUtc: '2026-11-20T00:00:00Z', expiresAtUtc: '2026-11-23T00:00:00Z' },
    invitationToken: 'opaque-invite-token',
  }, 201));

  const created = await api.inviteResident('house', 'nuevo@example.com');
  expect(created.invitationToken).toBe('opaque-invite-token');
  expect(fetchMock.mock.calls[3][0]).toBe('https://identity.example/api/v1/identity/houses/house/invitations');

  fetchMock.mockResolvedValueOnce(response({}, 204));
  await api.revokeInvitation('house', 'inv-2');
  expect(fetchMock.mock.calls[4][0]).toBe('https://identity.example/api/v1/identity/houses/house/invitations/inv-2');
  expect(fetchMock.mock.calls[4][1].method).toBe('DELETE');

  fetchMock.mockResolvedValueOnce(response({}, 204));
  await api.removeResident('house', 'u-2');
  expect(fetchMock.mock.calls[5][0]).toBe('https://identity.example/api/v1/identity/houses/house/members/u-2');
  expect(fetchMock.mock.calls[5][1].method).toBe('DELETE');
});

it('accepts invitation, changes password and recovers password without requiring session on public routes', async () => {
  fetchMock.mockResolvedValueOnce(response({ houseId: 'house', userId: 'u-new', role: 'resident', createdNewUser: true }));
  const accepted = await api.acceptInvitation('opaque-invite-token', 'nuevo@example.com', 'Resident-Pass-2026!');
  expect(accepted).toMatchObject({ role: 'resident', createdNewUser: true });
  expect(fetchMock.mock.calls[0][0]).toBe('https://identity.example/api/v1/identity/invitations/accept');

  fetchMock.mockResolvedValueOnce(response({ status: 'accepted' }, 202));
  await expect(api.requestPasswordReset('nuevo@example.com')).resolves.toEqual({ status: 'accepted' });
  expect(fetchMock.mock.calls[1][0]).toBe('https://identity.example/api/v1/identity/auth/password-reset/request');

  fetchMock.mockResolvedValueOnce(response({}, 204));
  await api.confirmPasswordReset('opaque-reset-token', 'nuevo@example.com', 'Updated-Pass-2026!');
  expect(fetchMock.mock.calls[2][0]).toBe('https://identity.example/api/v1/identity/auth/password-reset/confirm');

  await login();
  fetchMock.mockResolvedValueOnce(response({}, 204));
  await api.changePassword('test-password', 'Changed-Pass-2026!');
  expect(fetchMock.mock.calls[5][0]).toBe('https://identity.example/api/v1/identity/auth/password/change');
  expect(fetchMock.mock.calls[5][1].headers.Authorization).toBe('Bearer test-access');
});

it('manages physical persons and biometric enrollments via Security API', async () => {
  await login();
  fetchMock.mockResolvedValueOnce(response({ available: false, code: 'faceAccessPendingApproval', detail: 'Pending Limited Access' }));
  const face = await api.faceAvailability('house');
  expect(face.available).toBe(false);

  fetchMock.mockResolvedValueOnce(response({
    person: { id: 'p-1', name: 'Ana', method: 'face', status: 'pending', validUntilUtc: '2026-12-31T00:00:00Z', createdAtUtc: '2026-11-20T00:00:00Z', updatedAtUtc: '2026-11-20T00:00:00Z', revokedAtUtc: null },
    enrollment: { id: 'e-1', personId: 'p-1', method: 'face', status: 'pending', instruction: 'Esperando captura', error: null, errorCode: null, expiresAtUtc: '2026-11-20T00:15:00Z', updatedAtUtc: '2026-11-20T00:00:00Z' },
  }, 201));
  const created = await api.createPersonEnrollment('house', { name: 'Ana', method: 'face', validUntilUtc: '2026-12-31T00:00:00Z' });
  expect(created.person.status).toBe('pending');
  expect(created.enrollment.id).toBe('e-1');

  fetchMock.mockResolvedValueOnce(response({ id: 'e-1', personId: 'p-1', method: 'face', status: 'failed', instruction: null, error: 'Pending approval', errorCode: 'faceAccessPendingApproval', expiresAtUtc: '2026-11-20T00:15:00Z', updatedAtUtc: '2026-11-20T00:01:00Z' }));
  const captured = await api.captureEnrollment('house', 'e-1', { captureId: 'cap-1' });
  expect(captured.status).toBe('failed');
  expect(captured.errorCode).toBe('faceAccessPendingApproval');

  fetchMock.mockResolvedValueOnce(response({ id: 'p-1', name: 'Ana', method: 'face', status: 'revoked', validUntilUtc: '2026-12-31T00:00:00Z', createdAtUtc: '2026-11-20T00:00:00Z', updatedAtUtc: '2026-11-20T00:02:00Z', revokedAtUtc: '2026-11-20T00:02:00Z' }));
  const revoked = await api.revokePerson('house', 'p-1');
  expect(revoked.status).toBe('revoked');
  expect(fetchMock.mock.calls[5][1].method).toBe('DELETE');
});

it('queries and saves house location without inventing coordinates and forwards bounded assistant history', async () => {
  await login();
  fetchMock.mockResolvedValueOnce(response({}, 404));
  await expect(api.location('house')).resolves.toBeNull();

  fetchMock.mockResolvedValueOnce(response({ label: 'Casa Centro', latitude: 21.8853, longitude: -102.2916, updatedAtUtc: '2026-11-20T12:00:00Z' }));
  const saved = await api.saveLocation('house', { label: 'Casa Centro', latitude: 21.8853, longitude: -102.2916 });
  expect(saved.label).toBe('Casa Centro');
  expect(fetchMock.mock.calls[3][0]).toBe('https://home.example/api/v1/houses/house/location');
  expect(fetchMock.mock.calls[3][1].method).toBe('PUT');

  fetchMock.mockResolvedValueOnce(response({ text: 'Sin reportes recientes.', updatedAtUtc: null }));
  const answer = await api.ask('house', '¿Cómo está la casa?', [{ role: 'user', content: 'Hola' }]);
  expect(answer.text).toBe('Sin reportes recientes.');
  expect(answer.updatedAtUtc).toBeUndefined();
  expect(JSON.parse(fetchMock.mock.calls[4][1].body)).toEqual({
    message: '¿Cómo está la casa?',
    history: [{ role: 'user', content: 'Hola' }],
  });
});

it('notifies permission handler on 403 outside /identity/me', async () => {
  await login();
  const rejected = jest.fn();
  setPermissionRejectedHandler(rejected);
  fetchMock.mockResolvedValueOnce(response({}, 403));
  await expect(api.status('house')).rejects.toMatchObject({ status: 403 });
  expect(rejected).toHaveBeenCalledTimes(1);
  setPermissionRejectedHandler(() => {});
});
