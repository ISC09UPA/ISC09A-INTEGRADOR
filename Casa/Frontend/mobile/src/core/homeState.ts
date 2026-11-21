import type { CapabilityResource, HomeStatus, Light } from './models';
export function isResourceFresh(resource: CapabilityResource, now = Date.now()) {
  if (!resource.deviceReportedAtUtc) return false;
  const reported = Date.parse(resource.deviceReportedAtUtc);
  return Number.isFinite(reported) && reported <= now + 30000 && now - reported <= 60000;
}
export function uniqueResource(resources: CapabilityResource[] | null, kind: CapabilityResource['kind']) {
  const matching = resources?.filter(resource => resource.kind === kind) ?? [];
  return matching.length === 1 ? matching[0] : null;
}
/** A recent unrelated device event does not confirm this light's applied state. */
export function isLightFresh(light: Light, now = Date.now()) {
  if (!light.stateReportedAtUtc) return false;
  const reported = Date.parse(light.stateReportedAtUtc);
  return Number.isFinite(reported) && reported <= now + 30000 && now - reported <= 60000;
}
export function validHome(value: HomeStatus, houseId: string) {
  return value && value.houseId === houseId && Number.isFinite(Date.parse(value.updatedAtUtc)) && Array.isArray(value.lights) && value.lights.every(light => light && typeof light.lightId === 'string' && typeof light.lightOn === 'boolean' && ['auto', 'manual'].includes(light.lightingMode) && Number.isInteger(light.stateRevision) && light.stateRevision >= 0 && (light.stateReportedAtUtc == null || Number.isFinite(Date.parse(light.stateReportedAtUtc))));
}
/** Concurrent polls and delayed events must never roll the applied state backward. */
export function mergeHome(previous: HomeStatus | null, incoming: HomeStatus): HomeStatus {
  if (!previous || previous.houseId !== incoming.houseId) return incoming;
  if (Date.parse(previous.updatedAtUtc) > Date.parse(incoming.updatedAtUtc)) return previous;
  return { ...incoming, lights: incoming.lights.map(light => {
    const old = previous.lights.find(item => item.lightId === light.lightId);
    return old && old.stateRevision > light.stateRevision ? old : light;
  }) };
}
