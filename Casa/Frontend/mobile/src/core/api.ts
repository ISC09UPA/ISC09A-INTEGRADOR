import { saveRefreshToken, readRefreshToken, deleteRefreshToken } from './secureSession';
import type { AcceptedInvitation, AssistantTurn, AuthorizedPerson, CameraSession, CapabilityResource, Command, CreatedInvitation, Enrollment, Entry, FaceAvailability, Filters, HomeStatus, HouseMembersResponse, Location, NotificationDelivery, Page, PersonEnrollmentCreated, PersonsPage, Preferences, Profile, RecipientVerificationChallenge, Security, SensorReading, Session } from './models';

export class ServiceError extends Error {
  constructor(message: string, public status = 0, public correlationId?: string, public definitelyNotApplied = false, public code?: string) { super(message); }
}
const preAdmissionCodes = new Set(['badRequest', 'invalidCredential', 'sessionRevoked', 'forbidden', 'notFound', 'noReport', 'requestExpired', 'manualModeRequired', 'pendingCommand', 'duplicateChanged', 'deviceOffline', 'recipientNotVerified', 'verificationNotFound', 'verificationExpired', 'invalidVerificationCode']);
const problemMessages: Record<string, string> = {
  manualModeRequired: 'Cambia la luz a modo manual antes de encenderla o apagarla.',
  pendingCommand: 'Ya hay una orden en curso para este recurso. Consulta el estado antes de continuar.',
  deviceOffline: 'El dispositivo no tiene un reporte reciente. Actualiza el estado de la casa.',
  duplicateChanged: 'La clave de operación ya se utilizó con otra solicitud.',
  requestExpired: 'La solicitud venció antes de ser admitida. Intenta de nuevo.',
  recipientNotVerified: 'Verifica primero un correo destinatario antes de activar los avisos o enviar pruebas.',
  verificationNotFound: 'No hay una verificación de correo pendiente.',
  verificationExpired: 'El código de verificación expiró. Solicita uno nuevo.',
  invalidVerificationCode: 'El código de verificación no coincide.',
};
const identityUrl = process.env.EXPO_PUBLIC_IDENTITY_URL ?? '';
const homeUrl = process.env.EXPO_PUBLIC_HOME_URL ?? '';
const securityUrl = process.env.EXPO_PUBLIC_SECURITY_URL ?? '';
const assistantUrl = process.env.EXPO_PUBLIC_ASSISTANT_URL ?? '';
const notificationsUrl = process.env.EXPO_PUBLIC_NOTIFICATIONS_URL ?? '';
let session: Session | null = null;
let sessionVersion = 0;
let expiresAt = 0;
let refresh: Promise<void> | null = null;
let onExpired = () => {};
let onPermissionRejected = () => {};
export function getHomeUrl() { return homeUrl; }
export function getAccessToken() { return authorizedToken(); }
export function requestService<T>(baseUrl: string, path: string, method = 'GET', body?: unknown) { return request<T>(baseUrl, path, method, body); }
export function setExpiryHandler(handler: () => void) { onExpired = handler; }
export function setPermissionRejectedHandler(handler: () => void) { onPermissionRejected = handler; }
export function clearSession() { sessionVersion++; session = null; expiresAt = 0; void deleteRefreshToken().catch(() => {}); }
export function hasSession() { return session !== null; }

async function request<T>(base: string, path: string, method = 'GET', body?: unknown, authenticated = true, extraHeaders?: Record<string, string>): Promise<T> {
  if (!base) throw new ServiceError('Falta configurar la dirección del servicio.', 0, undefined, true);
  if (authenticated && !session) throw new ServiceError('Inicia sesión para consultar tu casa.', 401, undefined, true);
  if (authenticated && Date.now() >= expiresAt) {
    if (!refresh) refresh = rotate().finally(() => { refresh = null; });
    await refresh;
  }
  const requestVersion = sessionVersion;
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 60000);
  try {
    const response = await fetch(`${base.replace(/\/$/, '')}/api/v1${path}`, {
      method, signal: controller.signal,
      headers: { Accept: 'application/json', ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}), ...(authenticated ? { Authorization: `Bearer ${session?.accessToken}` } : {}), ...extraHeaders },
      ...(body === undefined ? {} : { body: JSON.stringify(body) }),
    });
    if (!response.ok) {
      const problem = await response.json().catch(() => ({}));
      if (response.status === 401 && authenticated && requestVersion === sessionVersion) { clearSession(); onExpired(); }
      if (response.status === 403 && authenticated && requestVersion === sessionVersion && !path.startsWith('/identity/me')) { onPermissionRejected(); }
      const code = typeof problem.code === 'string' ? problem.code : undefined;
      const messages: Record<number, string> = { 401: 'La sesión expiró o las credenciales no son válidas.', 403: 'No tienes permiso para esta acción.', 409: 'La operación está en conflicto. Actualiza el estado antes de continuar.', 501: 'El backend aún no implementa esta función.' };
      const message = (code && problemMessages[code]) ?? messages[response.status] ?? 'El servicio no pudo completar la solicitud.';
      const definitelyNotApplied = [400, 401, 403, 404, 422, 501].includes(response.status) || (!!code && preAdmissionCodes.has(code));
      throw new ServiceError(message, response.status, problem.correlationId, definitelyNotApplied, code);
    }
    if (response.status === 204) return undefined as T;
    return await response.json() as T;
  } catch (error) {
    if (error instanceof ServiceError) throw error;
    throw new ServiceError(method === 'GET' ? 'No se pudo consultar el servicio. Revisa tu conexión.' : 'No se pudo confirmar el resultado. Consulta el estado antes de volver a solicitar la acción.');
  } finally { clearTimeout(timeout); }
}
async function acceptSession(value: Session, expectedVersion = sessionVersion) {
  if (expectedVersion !== sessionVersion) throw new ServiceError('La sesión cambió durante la solicitud.', 401);
  if (!value.accessToken || !value.refreshToken || !Number.isFinite(value.expiresIn)) throw new ServiceError('Respuesta de sesión incompatible.');
  await saveRefreshToken(value.refreshToken);
  if (expectedVersion !== sessionVersion) throw new ServiceError('La sesión cambió durante la solicitud.', 401);
  session = value; expiresAt = Date.now() + value.expiresIn * 1000 - 10000;
}
async function rotate() {
  const previous = session;
  const expectedVersion = sessionVersion;
  if (!previous) throw new ServiceError('Inicia sesión.', 401);
  try { await acceptSession(await request<Session>(identityUrl, '/identity/auth/refresh', 'POST', { refreshToken: previous.refreshToken }, false), expectedVersion); }
  catch (error) { if (sessionVersion === expectedVersion) { clearSession(); onExpired(); } throw error; }
}
async function authorizedToken() {
  if (!session) throw new ServiceError('Inicia sesión para consultar tu casa.', 401, undefined, true);
  if (Date.now() >= expiresAt) {
    if (!refresh) refresh = rotate().finally(() => { refresh = null; });
    await refresh;
  }
  if (!session) throw new ServiceError('Inicia sesión para consultar tu casa.', 401, undefined, true);
  return session.accessToken;
}
function videoSessionValue(value: unknown): CameraSession {
  if (!value || typeof value !== 'object') throw new ServiceError('La sesión de video recibida no es compatible.');
  const item = value as Record<string, unknown>;
  const status = String(item.status);
  if (typeof item.id !== 'string' || !item.id || !['connecting', 'live', 'ended', 'failed'].includes(status) || typeof item.expiresAtUtc !== 'string' || !Number.isFinite(Date.parse(item.expiresAtUtc)) || typeof item.commandId !== 'string' || !item.commandId) throw new ServiceError('La sesión de video recibida no es compatible.');
  const streamUrl = item.streamUrl ?? undefined;
  const streamType = item.streamType ?? undefined;
  if (status === 'live') {
    if (typeof streamUrl !== 'string' || !streamUrl.startsWith('https://') || streamType !== 'hls') throw new ServiceError('La sesión de video recibida no es compatible.');
  } else if (streamUrl !== undefined || streamType !== undefined) {
    throw new ServiceError('La sesión de video recibida no es compatible.');
  }
  return { id: item.id, status: status as CameraSession['status'], expiresAtUtc: item.expiresAtUtc, commandId: item.commandId, streamUrl: streamUrl as string | undefined, streamType: streamType as 'hls' | undefined };
}
type SecurityPrefsPayload = { captures: boolean; analysis: boolean; updatedAtUtc?: string | null };
type NotificationPrefsPayload = { email: boolean; verifiedEmail?: string | null; verifiedAtUtc?: string | null; pendingRecipientEmail?: string | null; updatedAtUtc?: string | null };
function securityPrefsValue(value: unknown): SecurityPrefsPayload {
  if (!value || typeof value !== 'object') throw new ServiceError('Las preferencias de seguridad recibidas no son compatibles.');
  const item = value as Record<string, unknown>;
  if (typeof item.captures !== 'boolean' || typeof item.analysis !== 'boolean') throw new ServiceError('Las preferencias de seguridad recibidas no son compatibles.');
  return { captures: item.captures, analysis: item.analysis, updatedAtUtc: typeof item.updatedAtUtc === 'string' ? item.updatedAtUtc : null };
}
function notificationPrefsValue(value: unknown): NotificationPrefsPayload {
  if (!value || typeof value !== 'object') throw new ServiceError('Las preferencias de avisos recibidas no son compatibles.');
  const item = value as Record<string, unknown>;
  if (typeof item.email !== 'boolean') throw new ServiceError('Las preferencias de avisos recibidas no son compatibles.');
  return {
    email: item.email,
    verifiedEmail: typeof item.verifiedEmail === 'string' ? item.verifiedEmail : null,
    verifiedAtUtc: typeof item.verifiedAtUtc === 'string' ? item.verifiedAtUtc : null,
    pendingRecipientEmail: typeof item.pendingRecipientEmail === 'string' ? item.pendingRecipientEmail : null,
    updatedAtUtc: typeof item.updatedAtUtc === 'string' ? item.updatedAtUtc : null,
  };
}
function mergePreferences(security: SecurityPrefsPayload, notifications: NotificationPrefsPayload): Preferences {
  return {
    captures: security.captures,
    analysis: security.analysis,
    email: notifications.email,
    verifiedEmail: notifications.verifiedEmail ?? null,
    verifiedAtUtc: notifications.verifiedAtUtc ?? null,
    pendingRecipientEmail: notifications.pendingRecipientEmail ?? null,
    securityUpdatedAtUtc: security.updatedAtUtc ?? null,
    notificationsUpdatedAtUtc: notifications.updatedAtUtc ?? null,
  };
}
function deliveryValue(value: unknown): NotificationDelivery {
  if (!value || typeof value !== 'object') throw new ServiceError('El estado de envío recibido no es compatible.');
  const item = value as Record<string, unknown>;
  if (typeof item.id !== 'string' || !item.id || !['test', 'event', 'verification'].includes(String(item.kind)) || !['requested', 'accepted', 'failed', 'unknown', 'delivered'].includes(String(item.status)) || typeof item.createdAtUtc !== 'string' || typeof item.updatedAtUtc !== 'string') {
    throw new ServiceError('El estado de envío recibido no es compatible.');
  }
  return {
    id: item.id,
    kind: item.kind as NotificationDelivery['kind'],
    status: item.status as NotificationDelivery['status'],
    providerOperationId: typeof item.providerOperationId === 'string' ? item.providerOperationId : null,
    errorCode: typeof item.errorCode === 'string' ? item.errorCode : null,
    createdAtUtc: item.createdAtUtc,
    updatedAtUtc: item.updatedAtUtc,
  };
}
export const api = {
  async restore() {
    const expectedVersion = sessionVersion;
    const token = await readRefreshToken();
    if (!token || expectedVersion !== sessionVersion) return null;
    try { await acceptSession(await request<Session>(identityUrl, '/identity/auth/refresh', 'POST', { refreshToken: token }, false), expectedVersion); return await api.me(); }
    catch (error) { clearSession(); throw error; }
  },
  async login(email: string, password: string) {
    const expectedVersion = sessionVersion;
    await acceptSession(await request<Session>(identityUrl, '/identity/auth/login', 'POST', { email, password }, false), expectedVersion);
    try { return await api.me(); } catch (error) { clearSession(); throw error; }
  },
  async me() {
    const value = await request<Profile>(identityUrl, '/identity/me');
    if (!value.userId || !value.email || !Array.isArray(value.houses) || value.houses.some(house => !house.houseId || !['admin', 'resident'].includes(house.role))) throw new ServiceError('Perfil recibido incompatible.');
    return value;
  },
  async logout() {
    const previous = session;
    try { if (previous) await request(identityUrl, '/identity/auth/logout', 'POST', { refreshToken: previous.refreshToken }); }
    finally { sessionVersion++; session = null; expiresAt = 0; await deleteRefreshToken(); }
  },
  async members(houseId: string): Promise<HouseMembersResponse> {
    const value = await request<HouseMembersResponse>(identityUrl, `/identity/houses/${encodeURIComponent(houseId)}/members`);
    if (!value || !Array.isArray(value.members) || !Array.isArray(value.invitations)) {
      throw new ServiceError('La lista de miembros recibida no es compatible.');
    }
    return value;
  },
  async inviteResident(houseId: string, email: string): Promise<CreatedInvitation> {
    const value = await request<CreatedInvitation>(identityUrl, `/identity/houses/${encodeURIComponent(houseId)}/invitations`, 'POST', { email });
    if (!value?.invitation?.id || typeof value.invitationToken !== 'string' || !value.invitationToken) {
      throw new ServiceError('La invitación recibida no es compatible.');
    }
    return value;
  },
  revokeInvitation: (houseId: string, invitationId: string) =>
    request<void>(identityUrl, `/identity/houses/${encodeURIComponent(houseId)}/invitations/${encodeURIComponent(invitationId)}`, 'DELETE'),
  removeResident: (houseId: string, memberUserId: string) =>
    request<void>(identityUrl, `/identity/houses/${encodeURIComponent(houseId)}/members/${encodeURIComponent(memberUserId)}`, 'DELETE'),
  acceptInvitation: (token: string, email: string, password?: string) =>
    request<AcceptedInvitation>(identityUrl, '/identity/invitations/accept', 'POST', { token, email, ...(password ? { password } : {}) }, false),
  changePassword: (currentPassword: string, newPassword: string) =>
    request<void>(identityUrl, '/identity/auth/password/change', 'POST', { currentPassword, newPassword }),
  requestPasswordReset: (email: string) =>
    request<{ status: string }>(identityUrl, '/identity/auth/password-reset/request', 'POST', { email }, false),
  confirmPasswordReset: (token: string, email: string, newPassword: string) =>
    request<void>(identityUrl, '/identity/auth/password-reset/confirm', 'POST', { token, email, newPassword }, false),
  status: (houseId: string) => request<HomeStatus>(homeUrl, `/houses/${encodeURIComponent(houseId)}/status`),
  telemetry: (houseId: string) => request<SensorReading[]>(homeUrl, `/houses/${encodeURIComponent(houseId)}/telemetry`),
  async resources(houseId: string) {
    const values = await request<CapabilityResource[]>(homeUrl, `/houses/${encodeURIComponent(houseId)}/resources`);
    if (!Array.isArray(values) || values.some(value => !value || typeof value.resourceId !== 'string' || !value.resourceId || !['security', 'door', 'camera'].includes(value.kind) || !Object.prototype.hasOwnProperty.call(value, 'deviceReportedAtUtc') || !Object.prototype.hasOwnProperty.call(value, 'pendingCommand') || value.deviceReportedAtUtc !== null && !Number.isFinite(Date.parse(value.deviceReportedAtUtc)) || value.pendingCommand !== null && (!value.pendingCommand?.commandId || !['pending', 'unknown'].includes(value.pendingCommand.status)))) throw new ServiceError('Los recursos recibidos no son compatibles.');
    return values;
  },
  capability: (houseId: string, resourceId: string, type: 'SetSecurityMode' | 'OpenDoor' | 'CaptureImage', payload: { mode: 'armed' | 'disarmed' } | Record<string, never>, key: string, expiresAtUtc = new Date(Date.now() + (type === 'OpenDoor' ? 10000 : 30000)).toISOString()) => {
    return request<Command>(homeUrl, `/houses/${encodeURIComponent(houseId)}/resources/${encodeURIComponent(resourceId)}/commands`, 'POST', { type, payload, idempotencyKey: key, expiresAtUtc });
  },
  async security(houseId: string) {
    let value: Security;
    try { value = await request<Security>(securityUrl, `/security/houses/${encodeURIComponent(houseId)}/mode`); }
    catch (error) { if (error instanceof ServiceError && error.status === 404) return null; throw error; }
    if (!value || !['armed', 'disarmed'].includes(value.mode) || !Number.isFinite(Date.parse(value.updatedAtUtc)) || !Number.isFinite(Date.parse(value.receivedAtUtc))) throw new ServiceError('El estado de seguridad recibido no es compatible.');
    return value;
  },
  async securityList(houseId: string, kind: 'activity' | 'access' | 'incidents' | 'captures', filters: Filters = {}): Promise<Page> {
    const params = { ...filters, ...(kind === 'access' ? { type: 'access' } : {}) };
    const query = Object.entries(params).filter((entry): entry is [string, string] => typeof entry[1] === 'string' && !!entry[1]).map(([key, value]) => `${encodeURIComponent(key)}=${encodeURIComponent(value)}`).join('&');
    const path = kind === 'incidents' || kind === 'captures' ? kind : 'activity';
    const value = await request<{ items: Entry[]; nextCursor: string | null }>(securityUrl, `/security/houses/${encodeURIComponent(houseId)}/${path}${query ? `?${query}` : ''}`);
    if (!value || !Array.isArray(value.items) || value.items.some(item => !item || typeof item.id !== 'string' || typeof item.title !== 'string') || value.nextCursor !== null && typeof value.nextCursor !== 'string') throw new ServiceError('La actividad recibida no es compatible.');
    return { items: value.items, nextCursor: value.nextCursor ?? undefined };
  },
  async securityDetail(houseId: string, kind: 'activity' | 'incidents' | 'captures', id: string): Promise<Entry> {
    const value = await request<Entry>(securityUrl, `/security/houses/${encodeURIComponent(houseId)}/${kind}/${encodeURIComponent(id)}`);
    if (!value || typeof value.id !== 'string' || typeof value.title !== 'string') throw new ServiceError('El detalle recibido no es compatible.');
    return value;
  },
  async captureImageSource(houseId: string, id: string) {
    if (!securityUrl) throw new ServiceError('Falta configurar la dirección del servicio.', 0, undefined, true);
    const token = await authorizedToken();
    return { uri: `${securityUrl.replace(/\/$/, '')}/api/v1/security/houses/${encodeURIComponent(houseId)}/captures/${encodeURIComponent(id)}/content`, headers: { Authorization: `Bearer ${token}` } };
  },
  async startVideo(houseId: string, resourceId: string, key: string) {
    return videoSessionValue(await request<unknown>(homeUrl, `/houses/${encodeURIComponent(houseId)}/video/sessions`, 'POST', { resourceId, idempotencyKey: key }));
  },
  async videoSession(houseId: string, sessionId: string) {
    return videoSessionValue(await request<unknown>(homeUrl, `/houses/${encodeURIComponent(houseId)}/video/sessions/${encodeURIComponent(sessionId)}`));
  },
  async videoByKey(houseId: string, key: string) {
    return videoSessionValue(await request<unknown>(homeUrl, `/houses/${encodeURIComponent(houseId)}/video/sessions/by-key/${encodeURIComponent(key)}`));
  },
  async stopVideo(houseId: string, sessionId: string) {
    return videoSessionValue(await request<unknown>(homeUrl, `/houses/${encodeURIComponent(houseId)}/video/sessions/${encodeURIComponent(sessionId)}`, 'DELETE'));
  },
  async videoSource(houseId: string, sessionId: string) {
    if (!homeUrl) throw new ServiceError('Falta configurar la dirección del servicio.', 0, undefined, true);
    const token = await authorizedToken();
    return { uri: `${homeUrl.replace(/\/$/, '')}/api/v1/houses/${encodeURIComponent(houseId)}/video/sessions/${encodeURIComponent(sessionId)}/playlist.m3u8`, headers: { Authorization: `Bearer ${token}` } };
  },
  analyzeCapture: (houseId: string, id: string) => request<{ id: string; status: string }>(securityUrl, `/security/houses/${encodeURIComponent(houseId)}/captures/${encodeURIComponent(id)}/analysis`, 'POST'),
  deleteCapture: (houseId: string, id: string) => request<{ id: string; status: string }>(securityUrl, `/security/houses/${encodeURIComponent(houseId)}/captures/${encodeURIComponent(id)}`, 'DELETE'),
  captureDeletion: (houseId: string, id: string) => request<{ id: string; status: string }>(securityUrl, `/security/houses/${encodeURIComponent(houseId)}/captures/${encodeURIComponent(id)}/deletion`),
  captureRetention: (houseId: string) => request<{ retentionDays: number | null }>(securityUrl, `/security/houses/${encodeURIComponent(houseId)}/capture-retention`),
  setCaptureRetention: (houseId: string, retentionDays: number | null) => request<{ retentionDays: number | null }>(securityUrl, `/security/houses/${encodeURIComponent(houseId)}/capture-retention`, 'PUT', { retentionDays }),
  faceAvailability: (houseId: string) => request<FaceAvailability>(securityUrl, `/security/houses/${encodeURIComponent(houseId)}/diagnostics/face`),
  persons: (houseId: string, cursor?: string) =>
    request<PersonsPage>(securityUrl, `/security/houses/${encodeURIComponent(houseId)}/persons${cursor ? `?cursor=${encodeURIComponent(cursor)}` : ''}`),
  personDetail: (houseId: string, personId: string) =>
    request<AuthorizedPerson>(securityUrl, `/security/houses/${encodeURIComponent(houseId)}/persons/${encodeURIComponent(personId)}`),
  createPersonEnrollment: (houseId: string, input: { name: string; method: 'face' | 'fingerprint'; validUntilUtc: string }) =>
    request<PersonEnrollmentCreated>(securityUrl, `/security/houses/${encodeURIComponent(houseId)}/persons`, 'POST', input),
  enrollment: (houseId: string, enrollmentId: string) =>
    request<Enrollment>(securityUrl, `/security/houses/${encodeURIComponent(houseId)}/enrollments/${encodeURIComponent(enrollmentId)}`),
  captureEnrollment: (houseId: string, enrollmentId: string, body?: { captureId?: string; templateSlotId?: string; confidence?: number }) =>
    request<Enrollment>(securityUrl, `/security/houses/${encodeURIComponent(houseId)}/enrollments/${encodeURIComponent(enrollmentId)}/capture`, 'POST', body ?? {}),
  cancelEnrollment: (houseId: string, enrollmentId: string) =>
    request<Enrollment>(securityUrl, `/security/houses/${encodeURIComponent(houseId)}/enrollments/${encodeURIComponent(enrollmentId)}/cancel`, 'POST'),
  revokePerson: (houseId: string, personId: string) =>
    request<AuthorizedPerson>(securityUrl, `/security/houses/${encodeURIComponent(houseId)}/persons/${encodeURIComponent(personId)}`, 'DELETE'),
  async preferences(houseId: string): Promise<Preferences> {
    const encoded = encodeURIComponent(houseId);
    const [sec, notif] = await Promise.all([
      request<unknown>(securityUrl, `/security/houses/${encoded}/preferences`),
      request<unknown>(notificationsUrl, `/notifications/houses/${encoded}/preferences`),
    ]);
    return mergePreferences(securityPrefsValue(sec), notificationPrefsValue(notif));
  },
  async savePreferences(houseId: string, value: Pick<Preferences, 'captures' | 'analysis' | 'email'>): Promise<Preferences> {
    const encoded = encodeURIComponent(houseId);
    const [sec, notif] = await Promise.all([
      request<unknown>(securityUrl, `/security/houses/${encoded}/preferences`, 'PUT', { captures: value.captures, analysis: value.analysis }),
      request<unknown>(notificationsUrl, `/notifications/houses/${encoded}/preferences`, 'PUT', { email: value.email }),
    ]);
    return mergePreferences(securityPrefsValue(sec), notificationPrefsValue(notif));
  },
  async verifyNotificationRecipient(houseId: string, email: string): Promise<RecipientVerificationChallenge> {
    const raw = await request<Record<string, unknown>>(notificationsUrl, `/notifications/houses/${encodeURIComponent(houseId)}/recipient/verify`, 'POST', { email });
    if (!raw || typeof raw.pendingRecipientEmail !== 'string' || typeof raw.status !== 'string' || typeof raw.expiresAtUtc !== 'string') {
      throw new ServiceError('La solicitud de verificación recibida no es compatible.');
    }
    return { pendingRecipientEmail: raw.pendingRecipientEmail, status: raw.status, expiresAtUtc: raw.expiresAtUtc, deliveryId: typeof raw.deliveryId === 'string' ? raw.deliveryId : null };
  },
  async confirmNotificationRecipient(houseId: string, code: string): Promise<NotificationPrefsPayload> {
    return notificationPrefsValue(await request<unknown>(notificationsUrl, `/notifications/houses/${encodeURIComponent(houseId)}/recipient/confirm`, 'POST', { code }));
  },
  async testEmail(houseId: string, idempotencyKey: string): Promise<NotificationDelivery> {
    return deliveryValue(await request<unknown>(notificationsUrl, `/notifications/houses/${encodeURIComponent(houseId)}/test-email`, 'POST', undefined, true, { 'Idempotency-Key': idempotencyKey }));
  },
  async notificationDelivery(houseId: string, deliveryId: string): Promise<NotificationDelivery> {
    return deliveryValue(await request<unknown>(notificationsUrl, `/notifications/houses/${encodeURIComponent(houseId)}/deliveries/${encodeURIComponent(deliveryId)}`));
  },
  async ask(houseId: string, message: string, history?: AssistantTurn[]) {
    const trimmedHistory = history?.slice(-6).map(item => ({ role: item.role, content: item.content.slice(0, 500) }));
    const value = await request<{ text: string; updatedAtUtc: string | null }>(
      assistantUrl,
      `/assistant/houses/${encodeURIComponent(houseId)}/ask`,
      'POST',
      trimmedHistory?.length ? { message, history: trimmedHistory } : { message },
    );
    if (!value || typeof value.text !== 'string' || !value.text.trim() || (value.updatedAtUtc !== null && !Number.isFinite(Date.parse(value.updatedAtUtc)))) throw new ServiceError('La respuesta del asistente no es compatible.');
    return { text: value.text, updatedAtUtc: value.updatedAtUtc ?? undefined };
  },
  assistantDiagnostics: (houseId: string) =>
    request<{ status: string; deployment?: string }>(assistantUrl, `/assistant/houses/${encodeURIComponent(houseId)}/diagnostics/model`),
  async location(houseId: string): Promise<Location | null> {
    try {
      const value = await request<Location>(homeUrl, `/houses/${encodeURIComponent(houseId)}/location`);
      if (!value || typeof value.label !== 'string' || !Number.isFinite(value.latitude) || !Number.isFinite(value.longitude)) {
        throw new ServiceError('La ubicación recibida no es compatible.');
      }
      return value;
    } catch (error) {
      if (error instanceof ServiceError && error.status === 404) return null;
      throw error;
    }
  },
  async saveLocation(houseId: string, input: { label: string; latitude: number; longitude: number }): Promise<Location> {
    const value = await request<Location>(homeUrl, `/houses/${encodeURIComponent(houseId)}/location`, 'PUT', input);
    if (!value || typeof value.label !== 'string' || !Number.isFinite(value.latitude) || !Number.isFinite(value.longitude)) {
      throw new ServiceError('La ubicación recibida no es compatible.');
    }
    return value;
  },
  light: (houseId: string, lightId: string, type: 'SetLight' | 'SetLightingMode', payload: { on: boolean } | { mode: 'manual' | 'auto' }, key: string, expiresAtUtc = new Date(Date.now() + 30000).toISOString()) => request<Command>(homeUrl, `/houses/${encodeURIComponent(houseId)}/lights/${encodeURIComponent(lightId)}/commands`, 'POST', { type, payload, idempotencyKey: key, expiresAtUtc }),
  command: (id: string) => request<Command>(homeUrl, `/commands/${encodeURIComponent(id)}`),
  commandByKey: (houseId: string, key: string, expiresAtUtc?: string) => request<Command>(homeUrl, `/houses/${encodeURIComponent(houseId)}/commands/by-key/${encodeURIComponent(key)}${expiresAtUtc ? `?expiresAtUtc=${encodeURIComponent(expiresAtUtc)}` : ''}`),
};
