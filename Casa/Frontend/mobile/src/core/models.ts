export type Role = 'admin' | 'resident';
export type Profile = { userId: string; email: string; houses: { houseId: string; name?: string; role: Role }[] };
export type Session = { accessToken: string; refreshToken: string; sessionId: string; userId: string; expiresIn: number; refreshExpiresIn: number };
export type CommandState = 'pending' | 'executed' | 'rejected' | 'expired' | 'failed' | 'unknown';
export type Command = { commandId: string; status: CommandState; reason?: string; stateRevision?: number | null; snapshot?: { lightOn: boolean; lightingMode: 'auto' | 'manual'; source: string; appliedAtUtc: string } | null };
export type Light = { lightId: string; lightOn: boolean; lightingMode: 'auto' | 'manual'; stateRevision: number; stateReportedAtUtc?: string | null; lastCommand: Command | null; pendingCommand?: Command | null };
export type HomeStatus = { houseId: string; updatedAtUtc: string; lights: Light[] };
export type Entry = { id: string; title: string; description?: string; occurredAtUtc?: string; receivedAtUtc?: string; result?: string; type?: string; status?: string; imageUrl?: string; analysis?: string; method?: string; validUntilUtc?: string };
export type Page = { items: Entry[]; nextCursor?: string };
export type Filters = { type?: string; result?: string; date?: string; cursor?: string };
export type Security = { mode: 'armed' | 'disarmed'; updatedAtUtc: string; receivedAtUtc: string; incident?: Entry };
export type Preferences = { captures: boolean; analysis: boolean; email: boolean; verifiedEmail?: string };
export type CameraSession = { id: string; status: 'connecting' | 'live' | 'ended' | 'failed'; expiresAtUtc: string; commandId: string; streamUrl?: string; streamType?: 'hls' };
export type Enrollment = { id: string; status: 'pending' | 'acquiring' | 'completed' | 'failed' | 'cancelled'; instruction?: string; error?: string };
export type Location = { latitude: number; longitude: number; label: string };
export type SensorReading = { sensorId: string; name: string; value: number | boolean | string; unit?: string; occurredAtUtc: string; receivedAtUtc: string };
export type CapabilityResource = { resourceId: string; kind: 'security' | 'door' | 'camera'; deviceReportedAtUtc: string | null; pendingCommand: Command | null };

/** UI ports for future backend services (Stages 6-10). */
export interface ServiceExtensions {
  subscribe?(houseId: string, invalidate: () => void): () => void;
  list(kind: 'persons' | 'members', houseId: string, filters: Filters): Promise<Page>;
  detail(kind: 'persons' | 'members', houseId: string, id: string): Promise<Entry>;
  enroll(houseId: string, input: { name: string; method: 'face' | 'fingerprint'; validUntilUtc: string }): Promise<Enrollment>;
  enrollment(id: string): Promise<Enrollment>;
  captureEnrollment(id: string): Promise<Enrollment>;
  cancelEnrollment(id: string): Promise<Enrollment>;
  revoke(id: string): Promise<Entry>;
  location(houseId: string): Promise<Location>;
  preferences(houseId: string): Promise<Preferences>;
  savePreferences(houseId: string, value: Preferences): Promise<Preferences>;
  testEmail(houseId: string): Promise<{ id: string; status: string }>;
  invite(houseId: string, email: string, role: Role): Promise<Entry>;
}
