import * as ExpoHaptics from 'expo-haptics';

let habilitado = true;

export function fijarHaptico(valor: boolean): void {
  habilitado = valor;
}

function ejecutar(accion: () => Promise<void>): void {
  if (!habilitado) return;
  accion().catch(() => undefined);
}

export function hapticoToque(): void {
  ejecutar(() => ExpoHaptics.selectionAsync());
}

export function hapticoExito(): void {
  ejecutar(() => ExpoHaptics.notificationAsync(ExpoHaptics.NotificationFeedbackType.Success));
}

export function hapticoAviso(): void {
  ejecutar(() => ExpoHaptics.notificationAsync(ExpoHaptics.NotificationFeedbackType.Warning));
}

export function hapticoError(): void {
  ejecutar(() => ExpoHaptics.notificationAsync(ExpoHaptics.NotificationFeedbackType.Error));
}