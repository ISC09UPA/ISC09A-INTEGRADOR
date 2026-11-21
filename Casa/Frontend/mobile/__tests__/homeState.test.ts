import { isLightFresh, isResourceFresh, mergeHome, uniqueResource, validHome } from '../src/core/homeState';
import type { HomeStatus } from '../src/core/models';
const current: HomeStatus = { houseId: 'house', updatedAtUtc: '2026-11-12T12:00:00Z', lights: [{ lightId: 'light', lightOn: true, lightingMode: 'manual', stateRevision: 3, lastCommand: null }] };
it('does not roll back an applied light when a delayed response arrives', () => {
  const delayed = { ...current, updatedAtUtc: '2026-11-12T11:59:00Z', lights: [{ ...current.lights[0], stateRevision: 2, lightOn: false }] };
  expect(mergeHome(current, delayed).lights[0].lightOn).toBe(true);
});
it('preserves monotonic revisions even if the projection timestamp is newer', () => {
  const response = { ...current, updatedAtUtc: '2026-11-12T12:00:01Z', lights: [{ ...current.lights[0], stateRevision: 1, lightOn: false }] };
  expect(mergeHome(current, response).lights[0].stateRevision).toBe(3);
});
it('rejects a response for another house', () => { expect(validHome(current, 'other')).toBe(false); });
it('requires a recent report for each light even when the house has newer activity', () => {
  const now = Date.parse('2026-11-12T12:02:00Z');
  expect(isLightFresh({ ...current.lights[0], stateReportedAtUtc: '2026-11-12T12:00:00Z' }, now)).toBe(false);
  expect(isLightFresh({ ...current.lights[0], stateReportedAtUtc: '2026-11-12T12:01:45Z' }, now)).toBe(true);
  expect(isLightFresh(current.lights[0], now)).toBe(false);
});
it('never selects an ambiguous or stale door resource', () => {
  const now = Date.parse('2026-11-12T12:02:00Z');
  const door = { resourceId: 'door', kind: 'door' as const, deviceReportedAtUtc: '2026-11-12T12:00:00Z', pendingCommand: null };
  expect(isResourceFresh(door, now)).toBe(false);
  expect(isResourceFresh({ ...door, deviceReportedAtUtc: '2026-11-12T12:01:45Z' }, now)).toBe(true);
  expect(uniqueResource([door, { ...door, resourceId: 'second' }], 'door')).toBeNull();
  expect(uniqueResource([door], 'security')).toBeNull();
});
