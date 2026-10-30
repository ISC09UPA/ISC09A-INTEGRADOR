import * as LocalAuthentication from 'expo-local-authentication';

export interface BiometricCheckResult {
  hasHardware: boolean;
  isEnrolled: boolean;
  supportedTypes: LocalAuthentication.AuthenticationType[];
  biometryName: 'Face ID' | 'Touch ID' | 'Biometrics' | 'None';
}

export const checkBiometricSupport = async (): Promise<BiometricCheckResult> => {
  try {
    const hasHardware = await LocalAuthentication.hasHardwareAsync();
    const isEnrolled = await LocalAuthentication.isEnrolledAsync();
    const supportedTypes = await LocalAuthentication.supportedAuthenticationTypesAsync();

    let biometryName: 'Face ID' | 'Touch ID' | 'Biometrics' | 'None' = 'None';
    if (supportedTypes.includes(LocalAuthentication.AuthenticationType.FACIAL_RECOGNITION)) {
      biometryName = 'Face ID';
    } else if (supportedTypes.includes(LocalAuthentication.AuthenticationType.FINGERPRINT)) {
      biometryName = 'Touch ID';
    } else if (hasHardware && isEnrolled) {
      biometryName = 'Biometrics';
    }

    return {
      hasHardware,
      isEnrolled,
      supportedTypes,
      biometryName,
    };
  } catch (error) {
    console.warn('Biometrics check error:', error);
    return {
      hasHardware: false,
      isEnrolled: false,
      supportedTypes: [],
      biometryName: 'None',
    };
  }
};

export const authenticateWithBiometrics = async (
  promptMessage: string = 'Autenticar para acceder a AeroPark'
): Promise<boolean> => {
  try {
    const { hasHardware, isEnrolled } = await checkBiometricSupport();
    if (!hasHardware || !isEnrolled) {
      // In development / simulator or devices without biometric, allow simulated approval
      return true;
    }

    const result = await LocalAuthentication.authenticateAsync({
      promptMessage,
      fallbackLabel: 'Ingresar con contraseña',
      disableDeviceFallback: false,
      cancelLabel: 'Cancelar',
    });

    return result.success;
  } catch (error) {
    console.warn('Biometric auth error:', error);
    return false;
  }
};
