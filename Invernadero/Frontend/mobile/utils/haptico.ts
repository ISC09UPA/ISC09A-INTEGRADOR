import * as ExpoHaptics from 'expo-haptics';

export function hapticoToque(): void {
  ExpoHaptics.selectionAsync().catch(() => undefined);
}

export function hapticoExito(): void {
  ExpoHaptics.notificationAsync(ExpoHaptics.NotificationFeedbackType.Success).catch(() => undefined);
}

export function hapticoAviso(): void {
  ExpoHaptics.notificationAsync(ExpoHaptics.NotificationFeedbackType.Warning).catch(() => undefined);
}

export function hapticoError(): void {
  ExpoHaptics.notificationAsync(ExpoHaptics.NotificationFeedbackType.Error).catch(() => undefined);
}
