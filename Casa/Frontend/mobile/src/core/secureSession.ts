import { Platform } from 'react-native';
import * as SecureStore from 'expo-secure-store';
const key = 'casa.refresh-token.v1';
let queue: Promise<unknown> = Promise.resolve();
function serialize<T>(operation: () => Promise<T>): Promise<T> {
  const next = queue.then(operation, operation); queue = next.catch(() => {}); return next;
}
export async function saveRefreshToken(value: string) { if (Platform.OS !== 'web') await serialize(() => SecureStore.setItemAsync(key, value, { keychainAccessible: SecureStore.WHEN_UNLOCKED_THIS_DEVICE_ONLY })); }
export async function readRefreshToken() { return Platform.OS === 'web' ? null : serialize(() => SecureStore.getItemAsync(key)); }
export async function deleteRefreshToken() { if (Platform.OS !== 'web') await serialize(() => SecureStore.deleteItemAsync(key)); }
