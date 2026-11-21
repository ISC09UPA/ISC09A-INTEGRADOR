import { CameraPlayer } from './CameraPlayer';
import { randomUUID } from 'expo-crypto';
import { useEffect, useRef, useState, useSyncExternalStore } from 'react';
import { Alert, Image, Linking, Switch, View, Text, Pressable } from 'react-native';
import { useRouter } from 'expo-router';
import { beginCommand, type CommandRequestSpec, completeTerminalCommand, createCommandRequest, getTracked, handleCommandPreAdmissionRejection, isUnresolved, markCommandAmbiguous, recoverTrackedCommand, subscribeCommands, updateTracked } from '../../core/commands';
import { isLightFresh, isResourceFresh, uniqueResource } from '../../core/homeState';
import { api, services, missingService, ServiceError } from '../../core/api';
import { useApp } from '../../core/state';
import type { Command, Entry, Filters, Page, Preferences } from '../../core/models';
import { Button, Card, Copy, date, Empty, Field, Note, styles } from './ui';
import { useOperation, useResource } from './useResource';

export const commandText: Record<Command['status'], string> = { pending: 'Esperando confirmación de la casa', executed: 'Ejecución confirmada', rejected: 'Orden rechazada', expired: 'Orden vencida', failed: 'La orden falló', unknown: 'Resultado desconocido. Consulta el estado antes de continuar.' };
// Uniqueness identifier only; never used as a credential or an authorization token.
export const operationKey = () => randomUUID();
function confirm(title: string, action: () => void) { Alert.alert(title, 'La acción se solicitará al servicio y necesitará confirmación.', [{ text: 'Cancelar', style: 'cancel' }, { text: 'Confirmar', onPress: action }]); }
function useHouse() { const app = useApp(); return { ...app, id: app.house?.houseId ?? '', admin: app.house?.role === 'admin' }; }
function requireHouse(id: string) { if (!id) throw new Error('Inicia sesión y selecciona una casa para consultar este servicio.'); return id; }
function ResourceState({ loading, error }: { loading: boolean; error: string }) { return <>{loading && <Copy muted>Cargando…</Copy>}{!!error && <Note error>{error}</Note>}</>; }
function Item({ entry, onPress }: { entry: Entry; onPress?: () => void }) { return <Card title={entry.title}><Copy muted>{entry.description ?? entry.status ?? 'Sin detalle adicional'}</Copy>{!!entry.occurredAtUtc && <Copy muted>Ocurrió: {date(entry.occurredAtUtc)}</Copy>}{!!entry.receivedAtUtc && <Copy muted>Recibido: {date(entry.receivedAtUtc)}</Copy>}{onPress && <Button secondary label="Ver detalle" onPress={onPress} />}</Card>; }
function useCommandTracking(scope: string, fetchCommand: (id: string) => Promise<Command>, reconcile: () => Promise<void>, findByKey?: (key: string, expiresAtUtc?: string) => Promise<Command>) {
  const tracked = useSyncExternalStore(subscribeCommands, () => getTracked(scope), () => getTracked(scope));
  const { command, unresolved } = tracked;
  const [checking, setChecking] = useState(false);
  const operation = useOperation();
  const fetchRef = useRef(fetchCommand); fetchRef.current = fetchCommand;
  const keyRef = useRef(findByKey); keyRef.current = findByKey;
  const checkingRef = useRef(false);
  const reconcileRef = useRef(reconcile); reconcileRef.current = reconcile;
  async function check() {
    if (checkingRef.current) return;
    checkingRef.current = true;
    setChecking(true);
    try {
      const message = await recoverTrackedCommand(scope, {
        fetchCommand: id => fetchRef.current(id),
        findByKey: keyRef.current ? (key, expiresAtUtc) => keyRef.current!(key, expiresAtUtc) : undefined,
        reconcile: () => reconcileRef.current(),
      });
      operation.setMessage(message);
    } catch (e) {
      operation.setMessage((e as Error).message);
    } finally {
      checkingRef.current = false;
      setChecking(false);
    }
  }
  useEffect(() => {
    if (command?.status !== 'pending') return;
    const timer = setInterval(() => void check(), 3000);
    return () => clearInterval(timer);
  }, [command?.commandId, command?.status]);
  function submit(
    requestInput: Omit<CommandRequestSpec, 'expiresAtUtc'> & { expiresAtUtc?: string },
    work: (key: string, request: CommandRequestSpec) => Promise<Command>,
  ) {
    const request = createCommandRequest(requestInput);
    let candidateKey: string;
    try { candidateKey = operationKey(); } catch { operation.setMessage('No se pudo generar un identificador seguro para la operación.'); return; }
    const started = beginCommand(scope, candidateKey, request);
    if (!started.ok) {
      if (started.reason === 'mismatch') {
        operation.setMessage('Hay una solicitud anterior sin confirmar para una acción o parámetro distinto. Consulta el resultado antes de cambiar la operación.');
      } else if (started.reason === 'expired') {
        operation.setMessage('El plazo original de la solicitud pendiente ya venció; no se renovará automáticamente. Consulta el resultado para confirmar si fue registrada.');
      }
      return;
    }
    void operation.run(async () => {
      try { return await work(started.key, started.request); }
      catch (error) {
        if (error instanceof ServiceError && error.definitelyNotApplied) {
          const outcome = handleCommandPreAdmissionRejection(scope, error.code);
          if (error.code && ['pendingCommand', 'manualModeRequired', 'deviceOffline', 'duplicateChanged', 'requestExpired'].includes(error.code)) {
            void reconcileRef.current().catch(() => {});
          }
          if (outcome === 'retained') {
            throw new Error(`${error.message} Se conserva la clave de la solicitud anterior porque aún podría completarse; consulta el resultado antes de intentar otra orden.`);
          }
        } else {
          markCommandAmbiguous(scope);
        }
        throw error;
      } finally { updateTracked(scope, { sending: false }); }
    }, value => {
      if (!value.commandId || !(value.status in commandText)) { markCommandAmbiguous(scope); operation.setMessage('Respuesta incompatible. Es necesario reconciliar el estado.'); return; }
      updateTracked(scope, { command: value });
      if (!isUnresolved(value)) {
        void reconcileRef.current().then(() => completeTerminalCommand(scope, value)).catch(err => operation.setMessage((err as Error).message));
      }
    });
  }
  return { ...operation, busy: operation.busy || tracked.sending, command, unresolved, key: tracked.key, request: tracked.request, checking, submit, check };
}
function CommandNotice({ tracker }: { tracker: ReturnType<typeof useCommandTracking> }) { return <>{tracker.command && <Note>{commandText[tracker.command.status]}{tracker.command.reason ? ` · ${tracker.command.reason}` : ''}</Note>}{tracker.unresolved && !tracker.command && <Note>No se ha confirmado el resultado. Consulta el resultado antes de iniciar una acción distinta.</Note>}{!tracker.unresolved && !tracker.command && !!tracker.key && <Note>Clave de idempotencia conservada para la operación original{tracker.request ? ` (vence: ${date(tracker.request.expiresAtUtc)})` : ''}. Puedes consultar el resultado o repetir la misma acción dentro de su plazo original.</Note>}{!!tracker.message && <Note error>{tracker.message}</Note>}{(tracker.unresolved || !!tracker.key) && <Button secondary disabled={tracker.checking} label="Consultar resultado" onPress={() => void tracker.check()} />}</>; }

export function LoginScreen() {
  const app = useApp(); const op = useOperation(); const router = useRouter();
  const [email, setEmail] = useState(''); const [password, setPassword] = useState('');
  return <><Copy muted>CASA DOMÓTICA</Copy><Card title="Bienvenido a tu hogar"><Copy muted>Consulta y controla tu casa desde un solo lugar.</Copy><Field label="Correo electrónico" value={email} onChangeText={setEmail} autoCapitalize="none" keyboardType="email-address" autoComplete="email" /><Field label="Contraseña" value={password} onChangeText={setPassword} secureTextEntry autoComplete="current-password" /><Button disabled={app.restoring || op.busy || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim()) || !password} label={op.busy ? 'Iniciando sesión…' : 'Iniciar sesión'} onPress={() => void op.run(() => app.login(email.trim(), password), () => { setPassword(''); router.replace('/'); })} />{!!op.message && <Note error>{op.message}</Note>}</Card><Note>Usa una cuenta habilitada por el administrador y su contraseña de Casa Domótica.</Note></>;
}
export function HomeScreen() {
  const app = useHouse(); const router = useRouter();
  const recent = useResource(() => api.securityList(requireHouse(app.id), 'activity'), [app.id, app.sessionKey]);
  return <><Copy muted>TU HOGAR, A LA MANO</Copy><Copy>{app.house?.name ?? 'Tu casa'}</Copy><Note>{app.connected ? 'Servicio disponible' : 'Esperando conexión con tu casa'}</Note><Copy muted>Último reporte · {date(app.home?.updatedAtUtc)}</Copy><View style={{ backgroundColor: '#142E40', borderRadius: 22, padding: 24, gap: 16 }}><Text style={{ color: '#8DD4C1', fontSize: 24 }}>◇</Text><Text style={{ color: '#FFFDF8', fontSize: 23, fontWeight: '600' }}>La seguridad de tu hogar</Text><Text style={{ color: '#D5E1E3', fontSize: 15 }}>Consulta el modo efectivo y los incidentes reportados por tu casa.</Text><Button secondary label="Ver seguridad →" onPress={() => router.push('/security' as never)} /></View><Copy>Tu casa</Copy><View style={{ flexDirection: 'row', gap: 12 }}>{[['lights', 'Luces', '☼'], ['camera', 'Cámara', '▣']].map(([route, title, icon]) => <Pressable key={route} accessibilityRole="button" accessibilityLabel={title} onPress={() => router.push(`/${route}` as never)} style={[styles.card, { flex: 1, minHeight: 140 }]}><Text style={{ color: '#087F78', fontSize: 25 }}>{icon}</Text><Copy>{title}</Copy><Copy muted>{route === 'lights' ? app.home ? `${app.home.lights.length} luces reportadas` : 'Sin estado recibido' : 'Consultar cámara'}</Copy></Pressable>)}</View><View style={[styles.row, { justifyContent: 'space-between' }]}><Copy>Actividad reciente</Copy><Button secondary label="Ver todo" onPress={() => router.push('/activity' as never)} /></View><ResourceState {...recent} />{recent.data?.items.slice(0, 3).map(entry => <Item key={entry.id} entry={entry} onPress={() => router.push({ pathname: '/[screen]', params: { screen: 'incident', id: entry.id, kind: 'activity' } })} />)}{!recent.loading && !recent.data?.items.length && <Empty text={recent.error ? 'La actividad aparecerá cuando el servicio esté disponible.' : 'No hay actividad reportada.'} />}<TelemetryScreen /></>;
}
export function TelemetryScreen() {
  const app = useHouse(); const resource = useResource(() => api.telemetry(requireHouse(app.id)), [app.id]);
  return <Card title="Sensores"><ResourceState {...resource} />{resource.data?.map(reading => <View key={reading.sensorId} style={{ gap: 5 }}><Copy>{reading.name}: {String(reading.value)} {reading.unit ?? ''}</Copy><Copy muted>Medido: {date(reading.occurredAtUtc)}</Copy><Copy muted>Recibido: {date(reading.receivedAtUtc)}</Copy></View>)}{!resource.data?.length && <Copy muted>Sin lecturas disponibles. Los sensores enviarán sus reportes a través de Raspberry, IoT Hub y el backend.</Copy>}<Button secondary label="Consultar sensores" onPress={() => void resource.reload().catch(() => {})} /></Card>;
}
export function LightsScreen() {
  const app = useHouse();
  return <><Copy muted>Controla el estado aplicado de cada luz y su modo de funcionamiento.</Copy>{!app.home?.lights.length && <Empty text="Aún no se han recibido luces de esta casa." />}{app.home?.lights.map(light => <LightCard key={light.lightId} light={light} />)}</>;
}
function LightCard({ light }: { light: NonNullable<ReturnType<typeof useHouse>['home']>['lights'][number] }) {
  const app = useHouse(); const track = useCommandTracking(`${app.sessionKey}:${app.id}:light:${light.lightId}`, api.command, app.reload, (key, expiresAtUtc) => api.commandByKey(app.id, key, expiresAtUtc));
  const blocked = !app.connected || !app.stateAvailable || app.loading || app.stale || !isLightFresh(light) || !app.id || track.busy || track.unresolved || !!light.pendingCommand && ['pending', 'unknown'].includes(light.pendingCommand.status);
  return <Card title={light.lightId}><Copy>{light.lightOn ? 'Encendida' : 'Apagada'}</Copy><Copy muted>Modo {light.lightingMode === 'auto' ? 'automático' : 'manual'} · Última confirmación: {date(light.stateReportedAtUtc ?? undefined)}</Copy>{!isLightFresh(light) && <Note>El estado de esta luz no tiene una confirmación reciente.</Note>}{light.pendingCommand && <Note>{commandText[light.pendingCommand.status]}</Note>}<Button disabled={blocked || light.lightingMode !== 'manual'} label={light.lightOn ? 'Apagar luz' : 'Encender luz'} onPress={() => track.submit({ houseId: app.id, targetKind: 'light', targetId: light.lightId, type: 'SetLight', payload: { on: !light.lightOn } }, (key, request) => api.light(request.houseId, request.targetId, 'SetLight', request.payload as { on: boolean }, key, request.expiresAtUtc))} /><Button secondary disabled={blocked} label={light.lightingMode === 'auto' ? 'Cambiar a manual' : 'Cambiar a automático'} onPress={() => track.submit({ houseId: app.id, targetKind: 'light', targetId: light.lightId, type: 'SetLightingMode', payload: { mode: light.lightingMode === 'auto' ? 'manual' : 'auto' } }, (key, request) => api.light(request.houseId, request.targetId, 'SetLightingMode', request.payload as { mode: 'manual' | 'auto' }, key, request.expiresAtUtc))} /><CommandNotice tracker={track} /></Card>;
}
export function SecurityScreen() {
  const app = useHouse(); const router = useRouter(); const resource = useResource(() => api.security(requireHouse(app.id)), [app.id, app.sessionKey]);
  const devices = useResource(() => api.resources(requireHouse(app.id)), [app.id, app.sessionKey]);
  const incidents = useResource(() => api.securityList(requireHouse(app.id), 'incidents'), [app.id, app.sessionKey]);
  const incidentScope = `${app.sessionKey}:${app.id}`; const currentScope = useRef(incidentScope); currentScope.current = incidentScope;
  const [incidentPage, setIncidentPage] = useState<{ scope: string; page: Page } | null>(null); const incidentOp = useOperation();
  useEffect(() => { if (incidents.data) setIncidentPage({ scope: incidentScope, page: incidents.data }); }, [incidents.data]);
  const currentIncidents = incidentPage?.scope === incidentScope ? incidentPage.page : null;
  const target = uniqueResource(devices.data, 'security');
  const track = useCommandTracking(`${app.sessionKey}:${app.id}:security:${target?.resourceId ?? ''}`, api.command, async () => { await Promise.all([resource.reload(), devices.reload(), app.reload()]); }, (key, expiresAtUtc) => api.commandByKey(app.id, key, expiresAtUtc));
  const modeAt = resource.data ? Math.min(Date.parse(resource.data.updatedAtUtc), Date.parse(resource.data.receivedAtUtc)) : NaN;
  const modeFresh = Number.isFinite(modeAt) && modeAt <= Date.now() + 30000 && Date.now() - modeAt <= 60000;
  const blocked = !app.admin || !app.connected || !app.stateAvailable || app.loading || app.stale || !!devices.error || devices.loading || !!resource.error || resource.loading || !target || !isResourceFresh(target) || !resource.data || !modeFresh || track.busy || track.unresolved || !!target.pendingCommand;
  return <><ResourceState {...resource} /><ResourceState {...devices} /><Card title="Protección de tu casa"><Copy>{resource.data ? resource.data.mode === 'armed' ? 'Seguridad armada' : 'Seguridad desarmada' : 'Modo efectivo sin reporte'}</Copy><Copy muted>{date(resource.data?.updatedAtUtc)}</Copy>{!!resource.data && !modeFresh && <Note>El último reporte de seguridad no es reciente.</Note>}{!target && <Note>Esperando el dispositivo de seguridad de la casa.</Note>}{!app.admin && <Note>Armar y desarmar requiere permiso administrativo.</Note>}{target?.pendingCommand && <Note>{commandText[target.pendingCommand.status]}</Note>}<Button disabled={blocked} label={resource.data?.mode === 'armed' ? 'Desarmar seguridad' : 'Armar seguridad'} onPress={() => { if (target && resource.data) confirm('¿Cambiar el modo de seguridad?', () => track.submit({ houseId: app.id, targetKind: 'capability', targetId: target.resourceId, type: 'SetSecurityMode', payload: { mode: resource.data!.mode === 'armed' ? 'disarmed' : 'armed' } }, (key, request) => api.capability(request.houseId, request.targetId, 'SetSecurityMode', request.payload as { mode: 'armed' | 'disarmed' }, key, request.expiresAtUtc))); }} /><CommandNotice tracker={track} /></Card><ResourceState {...incidents} />{currentIncidents?.items.length ? currentIncidents.items.map(incident => <Item key={incident.id} entry={incident} onPress={() => router.push({ pathname: '/[screen]', params: { screen: 'incident', id: incident.id, kind: 'incident' } })} />) : !incidents.loading && <Empty text={incidents.error ? 'Los incidentes aparecerán cuando el servicio esté disponible.' : 'No hay incidentes reportados.'} />}{!!currentIncidents?.nextCursor && <Button secondary disabled={incidentOp.busy} label="Cargar más incidentes" onPress={() => { const requestScope = incidentScope; const cursor = currentIncidents.nextCursor; void incidentOp.run(() => api.securityList(requireHouse(app.id), 'incidents', { cursor }), page => { if (currentScope.current === requestScope) setIncidentPage(previous => previous?.scope === requestScope ? { scope: requestScope, page: { items: [...previous.page.items, ...page.items.filter(item => !previous.page.items.some(existing => existing.id === item.id))], nextCursor: page.nextCursor } } : previous); }); }} />}{!!incidentOp.message && <Note error>{incidentOp.message}</Note>}<Button secondary label="Actualizar seguridad" onPress={() => void Promise.all([resource.reload(), devices.reload(), incidents.reload()]).catch(() => {})} /></>;
}
export function ListScreen({ kind }: { kind: 'activity' | 'persons' | 'access' | 'captures' | 'members' }) {
  const app = useHouse(); const router = useRouter(); const [filters, setFilters] = useState<Filters>({}); const [items, setItems] = useState<Entry[]>([]); const [cursor, setCursor] = useState<string>(); const op = useOperation();
  const listScope = `${app.sessionKey}:${app.id}:${kind}:${filters.type ?? ''}:${filters.result ?? ''}:${filters.date ?? ''}`; const currentListScope = useRef(listScope); currentListScope.current = listScope;
  useEffect(() => { setItems([]); setCursor(undefined); }, [listScope]);
  const loadPage = (query: Filters) => kind === 'activity' || kind === 'access' || kind === 'captures' ? api.securityList(requireHouse(app.id), kind, query) : services().list(kind, requireHouse(app.id), query);
  const resource = useResource<Page>(() => loadPage(filters), [kind, app.id, app.sessionKey, filters.type, filters.result, filters.date]);
  useEffect(() => { setItems(resource.data?.items ?? []); setCursor(resource.data?.nextCursor); }, [resource.data]);
  const [dateInput, setDateInput] = useState('');
  const [inviteEmail, setInviteEmail] = useState('');
  const retention = useResource(() => kind === 'captures' && app.admin ? api.captureRetention(requireHouse(app.id)) : Promise.resolve(null), [kind, app.id, app.sessionKey, app.admin]);
  const [retentionDays, setRetentionDays] = useState('');
  useEffect(() => { setRetentionDays(retention.data?.retentionDays?.toString() ?? ''); }, [retention.data]);
  return <><ResourceState {...resource} />{kind === 'activity' && <Card title="Filtrar actividad"><Field label="Tipo de evento" value={filters.type ?? ''} onChangeText={type => setFilters(value => ({ ...value, type }))} /><Field label="Resultado" value={filters.result ?? ''} onChangeText={result => setFilters(value => ({ ...value, result }))} /><Field label="Fecha (AAAA-MM-DD)" value={dateInput} onChangeText={setDateInput} /><Button secondary disabled={!!dateInput && !/^\d{4}-\d{2}-\d{2}$/.test(dateInput)} label="Aplicar fecha" onPress={() => setFilters(old => ({ ...old, date: dateInput }))} /><Button secondary label="Limpiar filtros" onPress={() => { setFilters({}); setDateInput(''); }} /></Card>}{kind === 'captures' && app.admin && <Card title="Conservación de capturas"><ResourceState {...retention} /><Copy muted>Sin plazo configurado no se eliminan capturas automáticamente. Al configurar un plazo se eliminarán las capturas que ya lo superen.</Copy><Field label="Conservar durante días" value={retentionDays} onChangeText={setRetentionDays} keyboardType="number-pad" /><Button secondary disabled={op.busy || retention.loading || !!retention.error || !/^\d+$/.test(retentionDays) || Number(retentionDays) < 1 || Number(retentionDays) > 3650} label="Guardar plazo" onPress={() => confirm('¿Activar el borrado automático de capturas antiguas?', () => void op.run(() => api.setCaptureRetention(requireHouse(app.id), Number(retentionDays)), () => void retention.reload().catch(() => {})))} /><Button secondary disabled={op.busy || retention.loading || !!retention.error || retention.data?.retentionDays == null} label="Desactivar borrado automático" onPress={() => void op.run(() => api.setCaptureRetention(requireHouse(app.id), null), () => void retention.reload().catch(() => {}))} /></Card>}{kind === 'persons' && <Button disabled={!app.admin} label="Registrar persona" onPress={() => router.push('/enrollment' as never)} />}{kind === 'access' && <DoorScreen />}{kind === 'members' && app.admin && <Card title="Invitar residente"><Field label="Correo de la persona" value={inviteEmail} onChangeText={setInviteEmail} keyboardType="email-address" autoCapitalize="none" /><Button disabled={app.restoring || op.busy || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(inviteEmail)} label="Enviar invitación" onPress={() => void op.run(() => services().invite(app.id, inviteEmail.trim(), 'resident'), () => { setInviteEmail(''); void resource.reload().catch(() => {}); })} /></Card>}{items.map(entry => <Item key={entry.id} entry={entry} onPress={() => router.push({ pathname: '/[screen]', params: { screen: kind === 'persons' ? 'person' : kind === 'captures' ? 'capture' : kind === 'activity' ? 'incident' : 'detail', id: entry.id, kind } })} />)}{!resource.loading && !items.length && <Empty text={resource.error ? 'La lista se mostrará cuando el servicio esté disponible.' : 'No hay registros para mostrar.'} />}{!!cursor && <Button secondary disabled={op.busy} label="Cargar más" onPress={() => { const requestScope = listScope; void op.run(() => loadPage({ ...filters, cursor }), page => { if (currentListScope.current !== requestScope) return; setItems(old => [...old, ...page.items.filter(item => !old.some(existing => existing.id === item.id))]); setCursor(page.nextCursor); }); }} />}{!!op.message && <Note error>{op.message}</Note>}<Button secondary label="Actualizar lista" onPress={() => void resource.reload().catch(() => {})} /></>;
}
export function DoorScreen() {
  const app = useHouse(); const devices = useResource(() => api.resources(requireHouse(app.id)), [app.id]); const target = uniqueResource(devices.data, 'door');
  const track = useCommandTracking(`${app.sessionKey}:${app.id}:door:${target?.resourceId ?? ''}`, api.command, async () => { await Promise.all([devices.reload(), app.reload()]); }, (key, expiresAtUtc) => api.commandByKey(app.id, key, expiresAtUtc));
  return <Card title="Acceso principal"><ResourceState {...devices} /><Copy muted>La confirmación indica que se activó el mecanismo. La posición física de la puerta necesita un sensor.</Copy>{!target && <Note>Esperando el dispositivo de la puerta.</Note>}{target?.pendingCommand && <Note>{commandText[target.pendingCommand.status]}</Note>}<Button label="Solicitar apertura" disabled={!app.admin || !app.connected || !app.stateAvailable || app.loading || app.stale || !!devices.error || devices.loading || !target || !isResourceFresh(target) || !!target.pendingCommand || track.busy || track.unresolved} onPress={() => { if (target) confirm('¿Activar el mecanismo de apertura?', () => track.submit({ houseId: app.id, targetKind: 'capability', targetId: target.resourceId, type: 'OpenDoor', payload: {} }, (key, request) => api.capability(requireHouse(request.houseId), request.targetId, 'OpenDoor', {}, key, request.expiresAtUtc))); }} /><CommandNotice tracker={track} /><Button secondary disabled={devices.loading} label="Actualizar estado de la puerta" onPress={() => void devices.reload().catch(() => {})} /></Card>;
}
export function EnrollmentScreen() {
  const app = useHouse(); const op = useOperation(); const [name, setName] = useState(''); const [until, setUntil] = useState(''); const [method, setMethod] = useState<'face' | 'fingerprint'>('face'); const [entry, setEntry] = useState<Awaited<ReturnType<ReturnType<typeof services>['enroll']>> | null>(null); const [review, setReview] = useState(false);
  const validDate = Number.isFinite(Date.parse(until)) && Date.parse(until) > Date.now();
  return <><Card title={review ? 'Revisar registro' : 'Datos de acceso'}><Field label="Nombre" value={name} onChangeText={setName} editable={!review} /><Field label="Vigencia hasta (AAAA-MM-DD)" value={until} onChangeText={setUntil} editable={!review} /><View style={styles.row}><Button secondary disabled={review} label={method === 'face' ? '✓ Rostro' : 'Rostro'} onPress={() => setMethod('face')} /><Button secondary disabled={review} label={method === 'fingerprint' ? '✓ Huella' : 'Huella'} onPress={() => setMethod('fingerprint')} /></View><Copy muted>{method === 'fingerprint' ? 'La huella se registra en el lector físico de la casa.' : 'El registro facial requiere conectar el servicio de imágenes autorizado de la casa.'}</Copy>{review ? <><Note>No se concederá acceso hasta que el backend confirme el registro completo.</Note><Button disabled={!app.admin || op.busy || !!entry && !['failed', 'cancelled'].includes(entry.status)} label="Solicitar registro" onPress={() => void op.run(() => services().enroll(requireHouse(app.id), { name: name.trim(), method, validUntilUtc: new Date(until).toISOString() }), setEntry)} /><Button secondary disabled={!!entry && !['failed', 'cancelled'].includes(entry.status)} label="Editar datos" onPress={() => { setReview(false); setEntry(null); }} /></> : <Button disabled={!name.trim() || !validDate || !app.admin} label="Revisar" onPress={() => setReview(true)} />}{entry && <Card title="Registro biométrico"><Copy>Estado: {entry.status}</Copy>{entry.instruction && <Copy>{entry.instruction}</Copy>}{entry.error && <Note error>{entry.error}</Note>}{entry.status === 'completed' ? <Note>Registro completado por el servicio.</Note> : entry.status !== 'cancelled' && entry.status !== 'failed' && <><Button secondary disabled={op.busy} label="Consultar avance" onPress={() => void op.run(() => services().enrollment(entry.id), setEntry)} />{method === 'face' && <Button disabled={op.busy} label="Solicitar captura del rostro en casa" onPress={() => void op.run(() => services().captureEnrollment(entry.id), setEntry)} />}<Button secondary disabled={op.busy} label="Cancelar registro" onPress={() => void op.run(() => services().cancelEnrollment(entry.id), setEntry)} /></>}</Card>}{!!op.message && <Note error>{op.message}</Note>}</Card></>;
}
export function DetailScreen({ id, kind = 'activity' }: { id: string; kind?: string }) {
  const app = useHouse(); const router = useRouter(); const op = useOperation();
  const listKind = ['persons', 'captures', 'access', 'members'].includes(kind) ? kind as 'persons' | 'captures' | 'access' | 'members' : 'activity';
  const resource = useResource(async () => kind === 'incident' ? api.securityDetail(requireHouse(app.id), 'incidents', id) : listKind === 'activity' || listKind === 'access' ? api.securityDetail(requireHouse(app.id), 'activity', id) : listKind === 'captures' ? api.securityDetail(requireHouse(app.id), 'captures', id) : services().detail(listKind, requireHouse(app.id), id), [id, kind, listKind, app.id, app.sessionKey]);
  const captureImage = useResource(async () => listKind === 'captures' && resource.data ? api.captureImageSource(requireHouse(app.id), id) : null, [id, listKind, app.id, app.sessionKey, resource.data?.id]);
  const [imageError, setImageError] = useState(false);
  useEffect(() => { setImageError(false); }, [captureImage.data]);
  const deletion = useResource(async () => listKind === 'captures' && app.admin ? api.captureDeletion(requireHouse(app.id), id) : null, [id, listKind, app.id, app.sessionKey, app.admin]);
  const deletionActive = !!deletion.data && deletion.data.status !== 'none';
  return <><ResourceState {...resource} />{listKind === 'captures' && app.admin && <ResourceState {...deletion} />}{deletionActive && <Card title="Eliminación de captura"><Copy>{deletion.data?.status === 'deleted' ? 'Eliminación confirmada.' : deletion.data?.status === 'blocked' ? 'La eliminación está bloqueada porque el archivo cambió; no se ha borrado.' : 'Eliminación solicitada; esperando confirmación.'}</Copy><Button secondary label="Actualizar eliminación" onPress={() => void deletion.reload().catch(() => {})} /><Button secondary label="Volver a capturas" onPress={() => router.replace('/captures' as never)} /></Card>}{resource.data && !deletionActive ? <><Item entry={resource.data} />{listKind === 'captures' && <ResourceState {...captureImage} />}{listKind === 'captures' && captureImage.data && <Image accessibilityLabel="Captura privada de la casa" source={captureImage.data} onError={() => setImageError(true)} style={{ width: '100%', height: 230, borderRadius: 20 }} resizeMode="contain" />}{listKind === 'captures' && imageError && <Note error>No se pudo cargar la imagen privada; actualiza la captura para volver a intentarlo.</Note>}{!!resource.data.analysis && <Card title="Análisis de la imagen (inglés)"><Copy>{resource.data.analysis}</Copy></Card>}{listKind === 'persons' && <Button disabled={!app.admin || op.busy || resource.data.status === 'revoked'} label="Revocar acceso" onPress={() => confirm('¿Revocar el permiso de esta persona?', () => void op.run(() => services().revoke(id), () => void resource.reload().catch(() => {})))} />}{listKind === 'captures' && <><Copy muted>Estado del análisis: {resource.data.status ?? 'sin solicitar'}</Copy><Button disabled={!app.admin || op.busy || ['pending', 'analyzing', 'completed'].includes(resource.data.status ?? '')} label="Solicitar análisis de imagen" onPress={() => void op.run(() => api.analyzeCapture(requireHouse(app.id), id), () => void resource.reload().catch(() => {}))} /><Button secondary disabled={!app.admin || op.busy || deletion.loading || !!deletion.error || !deletion.data} label="Eliminar captura" onPress={() => confirm('¿Solicitar la eliminación de esta captura?', () => void op.run(() => api.deleteCapture(requireHouse(app.id), id), () => void deletion.reload().catch(() => {})))} /><Button secondary label="Actualizar captura" onPress={() => void Promise.all([resource.reload(), captureImage.reload()]).catch(() => {})} /></>}{listKind === 'activity' && <><Button secondary label="Consultar capturas" onPress={() => router.push('/captures' as never)} /><Button secondary label="Consultar avisos" onPress={() => router.push('/preferences' as never)} /></>}</> : !deletionActive && <Empty text="Sin detalle disponible." />}{!!op.message && <Note error>{op.message}</Note>}</>;
}
export function CameraScreen() {
  const app = useHouse(); const op = useOperation(); const camera = app.cameraSession; const setCamera = app.setCameraSession;
  const devices = useResource(() => api.resources(requireHouse(app.id)), [app.id, app.sessionKey]); const target = uniqueResource(devices.data, 'camera');
  const capture = useCommandTracking(`${app.sessionKey}:${app.id}:camera:${target?.resourceId ?? ''}`, api.command, async () => { await Promise.all([devices.reload(), app.reload()]); }, (key, expiresAtUtc) => api.commandByKey(app.id, key, expiresAtUtc));
  const [source, setSource] = useState<Awaited<ReturnType<typeof api.videoSource>> | null>(null);
  const [sourceError, setSourceError] = useState('');
  const [pollError, setPollError] = useState('');
  const [sourceRetry, setSourceRetry] = useState(0);
  const [pendingKey, setPendingKey] = useState('');
  const [videoUnresolved, setVideoUnresolved] = useState(false);
  useEffect(() => { setPendingKey(''); setVideoUnresolved(false); }, [app.id, app.sessionKey]);
  useEffect(() => {
    if (!camera || camera.status === 'ended' || camera.status === 'failed' || !app.id) return;
    let active = true; setPollError('');
    const timer = setInterval(() => {
      void api.videoSession(app.id, camera.id).then(value => { if (active) setCamera(value); }).catch(error => {
        if (!active) return;
        setPollError((error as Error).message);
        if (error instanceof ServiceError && [401, 403, 404].includes(error.status)) setCamera(null);
      });
    }, camera.status === 'connecting' ? 3000 : 10000);
    return () => { active = false; clearInterval(timer); };
  }, [app.id, app.sessionKey, camera?.id, camera?.status, setCamera]);
  useEffect(() => {
    let active = true; setSource(null); setSourceError('');
    if (!app.id || !camera || camera.status !== 'live') return;
    void api.videoSource(app.id, camera.id).then(value => { if (active) setSource(value); }).catch(error => { if (active) setSourceError((error as Error).message); });
    return () => { active = false; };
  }, [app.id, app.sessionKey, camera?.id, camera?.status, sourceRetry]);
  const startable = !!app.admin && !!app.id && app.connected && app.stateAvailable && !app.loading && !app.stale && !devices.loading && !devices.error && !!target && isResourceFresh(target) && !target.pendingCommand && !op.busy && !videoUnresolved && (!camera || camera.status === 'ended' || camera.status === 'failed');
  const stopable = !!camera && camera.status !== 'ended' && !op.busy;
  const start = () => {
    if (!target || videoUnresolved) return;
    let key: string;
    try { key = pendingKey || operationKey(); } catch { op.setMessage('No se pudo generar un identificador seguro para la operación.'); return; }
    setPendingKey(key);
    setVideoUnresolved(true);
    void op.run(async () => {
      try { return await api.startVideo(requireHouse(app.id), target.resourceId, key); }
      catch (error) {
        if (error instanceof ServiceError && error.definitelyNotApplied) {
          if (error.code === 'duplicateChanged') {
            setVideoUnresolved(true);
          } else {
            setPendingKey('');
            setVideoUnresolved(false);
          }
          if (error.code && ['pendingCommand', 'deviceOffline', 'duplicateChanged'].includes(error.code)) {
            void devices.reload().catch(() => {});
          }
        }
        throw error;
      }
    }, value => { setPendingKey(''); setVideoUnresolved(false); setCamera(value); setPollError(''); });
  };
  const recover = () => {
    if (!pendingKey) return;
    void op.run(async () => {
      try { return await api.videoByKey(requireHouse(app.id), pendingKey); }
      catch (error) {
        if (error instanceof ServiceError && error.status === 404) {
          await Promise.all([devices.reload(), app.reload()]);
          setVideoUnresolved(false);
          throw new Error('La sesión de video aún no aparece registrada en el servicio y se actualizó el estado de la cámara. Si vuelves a iniciarla, se reutilizará la misma clave de idempotencia.');
        }
        throw error;
      }
    }, value => { setPendingKey(''); setVideoUnresolved(false); setCamera(value); setPollError(''); });
  };
  const stop = () => { if (camera) void op.run(() => api.stopVideo(requireHouse(app.id), camera.id), setCamera); };
  return <><Card title="Cámara de la casa"><Copy muted>{camera ? `Sesión: ${camera.status}` : 'Sin sesión de video'}</Copy>{camera?.status === 'connecting' && <Note>Conectando con la cámara. La transmisión solo aparecerá cuando el dispositivo entregue segmentos reales.</Note>}{camera?.status === 'live' && source && <CameraPlayer source={source} />}{camera?.status === 'live' && !source && !sourceError && <Note>Cargando la playlist privada…</Note>}{!!sourceError && <><Note error>{sourceError}</Note><Button secondary disabled={op.busy} label="Reintentar video" onPress={() => setSourceRetry(value => value + 1)} /></>}{camera?.status === 'failed' && <Note error>La sesión de video falló o la orden fue rechazada.</Note>}{camera?.status === 'ended' && <Note>La sesión de video terminó.</Note>}{(videoUnresolved || !!pendingKey) && (!camera || camera.status === 'ended' || camera.status === 'failed') && <><Note>{videoUnresolved ? 'No se confirmó si la sesión de video quedó registrada. Recupera el estado antes de solicitar otra.' : 'Clave de idempotencia conservada para evitar duplicar la sesión si la solicitud anterior aún está en curso.'}</Note><Button secondary disabled={op.busy} label="Recuperar sesión de cámara" onPress={recover} /></>}<View style={styles.row}><Button disabled={!startable} label="Iniciar sesión de cámara" onPress={start} /><Button secondary disabled={!stopable} label="Terminar sesión" onPress={stop} /></View><Button secondary disabled={op.busy || !camera} label="Actualizar sesión de cámara" onPress={() => { if (camera) void op.run(() => api.videoSession(requireHouse(app.id), camera.id), setCamera); }} /><ResourceState {...devices} />{!target && <Note>Esperando una cámara asociada a la casa.</Note>}{!app.admin && <Note>Iniciar o cerrar la cámara requiere permiso administrativo.</Note>}{target?.pendingCommand && <Note>Hay una operación de cámara pendiente de confirmación.</Note>}<Button secondary disabled={!app.admin || !app.id || !app.connected || !app.stateAvailable || app.loading || app.stale || devices.loading || !!devices.error || !target || !isResourceFresh(target) || !!target.pendingCommand || capture.busy || capture.unresolved} label="Tomar y guardar captura" onPress={() => { if (target) confirm('¿Solicitar una captura de la cámara?', () => capture.submit({ houseId: app.id, targetKind: 'capability', targetId: target.resourceId, type: 'CaptureImage', payload: {} }, (key, request) => api.capability(requireHouse(request.houseId), request.targetId, 'CaptureImage', {}, key, request.expiresAtUtc))); }} /><CommandNotice tracker={capture} />{!!pollError && <Note error>{pollError}</Note>}{!!op.message && <Note error>{op.message}</Note>}</Card><Note>Las capturas guardadas tienen un flujo independiente del video. Sin cámara física no se muestra una transmisión inventada.</Note></>;
}

export function LocationScreen() {
  const app = useHouse(); const op = useOperation(); const [location, setLocation] = useState<Awaited<ReturnType<ReturnType<typeof services>['location']>> | null>(null);
  return <Card title="Ubicación de tu casa"><Copy muted>Se consulta la ubicación configurada de la casa. No se necesita la ubicación de este teléfono.</Copy><Button disabled={op.busy || !app.id} label="Ver ubicación" onPress={() => void op.run(() => services().location(requireHouse(app.id)), setLocation)} />{location && <><Copy>{location.label}</Copy><Copy muted>{location.latitude}, {location.longitude}</Copy><Button secondary label="Abrir mapa" onPress={() => void op.run(async () => { if (!Number.isFinite(location.latitude) || !Number.isFinite(location.longitude) || Math.abs(location.latitude) > 90 || Math.abs(location.longitude) > 180) throw new Error('Coordenadas inválidas.'); await Linking.openURL(`https://www.google.com/maps/search/?api=1&query=${location.latitude},${location.longitude}`); })} /></>}{!!op.message && <Note error>{op.message}</Note>}</Card>;
}
export function PreferencesScreen() {
  const app = useHouse(); const resource = useResource(() => services().preferences(requireHouse(app.id)), [app.id]); const op = useOperation(); const [draft, setDraft] = useState<Preferences | null>(null); const [delivery, setDelivery] = useState('');
  useEffect(() => setDraft(resource.data), [resource.data]);
  return <><ResourceState {...resource} /><Card title="Evidencias y avisos">{(['captures', 'analysis', 'email'] as const).map((key, index) => <View key={key} style={styles.row}><View style={{ flex: 1 }}><Copy>{['Capturas automáticas', 'Análisis automático', 'Avisos por correo'][index]}</Copy></View><Switch accessibilityLabel={['Capturas automáticas', 'Análisis automático', 'Avisos por correo'][index]} value={draft?.[key] ?? false} disabled={!draft || op.busy || !app.admin} onValueChange={value => setDraft(old => old ? { ...old, [key]: value } : null)} trackColor={{ true: '#087F78' }} /></View>)}<Copy muted>Destino verificado: {draft?.verifiedEmail ?? 'Sin destino recibido'}</Copy><Button disabled={!draft || !app.admin || op.busy} label="Guardar preferencias" onPress={() => { if (draft) void op.run(() => services().savePreferences(app.id, draft), value => { setDraft(value); op.setMessage('Preferencias guardadas por el servicio.'); }); }} /><Button secondary disabled={!draft?.verifiedEmail || op.busy || !app.admin} label="Enviar correo de prueba" onPress={() => void op.run(() => services().testEmail(app.id), value => setDelivery(`Solicitud ${value.id}: ${value.status}. El resultado del proveedor no confirma recepción en la bandeja.`))} />{!!delivery && <Note>{delivery}</Note>}{!!op.message && <Note>{op.message}</Note>}</Card></>;
}
export function AssistantScreen() {
  const app = useHouse(); const op = useOperation(); const [message, setMessage] = useState(''); const [reply, setReply] = useState<{ text: string; updatedAtUtc?: string } | null>(null);
  return <><Card title="Pregunta por tu casa"><Copy muted>Consulta el último estado reportado. El asistente es de solo lectura.</Copy><Field label="Tu consulta" multiline value={message} onChangeText={setMessage} maxLength={2000} /><Button disabled={!message.trim() || !app.id || op.busy} label={op.busy ? 'Consultando…' : 'Consultar'} onPress={() => void op.run(() => api.ask(app.id, message.trim()), setReply)} />{reply && <><Copy>{reply.text}</Copy><Copy muted>Datos consultados: {date(reply.updatedAtUtc)}</Copy></>}{!!op.message && <Note error>{op.message}</Note>}</Card><Note>No puede abrir la puerta, desarmar ni ejecutar órdenes.</Note></>;
}
export function SettingsScreen() {
  const app = useHouse(); const router = useRouter(); const op = useOperation();
  return <><Card title="Tu cuenta"><Copy>{app.profile?.email ?? 'Sin sesión iniciada'}</Copy><Copy muted>{app.house?.role ?? 'Sin rol'} · {app.house?.name ?? 'Sin casa asignada'}</Copy><Copy muted>En Android, la sesión se conserva en almacenamiento protegido. En el navegador dura hasta cerrar o recargar la página.</Copy></Card><Button secondary label="Avisos y preferencias" onPress={() => router.push('/preferences' as never)} /><Button secondary disabled={!app.admin} label="Miembros de la casa" onPress={() => router.push('/members' as never)} /><Button label={app.profile ? 'Cerrar sesión' : 'Iniciar sesión'} disabled={op.busy} onPress={() => { if (!app.profile) router.push('/login' as never); else confirm('¿Cerrar sesión?', () => void op.run(async () => { try { await app.logout(); } finally { router.replace('/login' as never); } })); }} />{!!op.message && <Note error>{op.message}</Note>}</>;
}
export function MoreScreen() {
  const router = useRouter();
  return <>{[['persons', 'Personas autorizadas'], ['access', 'Accesos y puerta'], ['captures', 'Capturas'], ['location', 'Ubicación de la casa'], ['preferences', 'Avisos y preferencias'], ['assistant', 'Asistente'], ['settings', 'Configuración']].map(([screen, label]) => <Button key={screen} secondary label={label} onPress={() => router.push(`/${screen}` as never)} />)}</>;
}
export function UnavailableScreen() { return <Note>{missingService().message}</Note>; }
