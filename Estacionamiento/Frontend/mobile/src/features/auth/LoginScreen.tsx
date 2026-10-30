import React, { useState } from 'react';
import {
  View,
  Text,
  TextInput,
  StyleSheet,
  TouchableOpacity,
  KeyboardAvoidingView,
  Platform,
  ScrollView,
  Alert,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { AeroLogoStacked } from '@/components/brand/AeroLogo';
import { PillButton } from '@/components/ui/PillButton';
import { useAuthStore } from '@/store/authStore';
import { Radii } from '@/theme/tokens';

export interface LoginScreenProps {
  onNavigateToRegister?: () => void;
}

export const LoginScreen: React.FC<LoginScreenProps> = ({ onNavigateToRegister }) => {
  // Login form state
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');

  const { loginWithPassword, loginWithBiometrics, isLoading } = useAuthStore();
  const insets = useSafeAreaInsets();

  const handleLogin = async () => {
    if (!email.trim() || !password) {
      Alert.alert('Datos requeridos', 'Por favor ingresa tu correo y contraseña.');
      return;
    }

    const res = await loginWithPassword(email.trim(), password);
    if (!res.success) {
      Alert.alert('Error de acceso', res.error || 'Credenciales incorrectas.');
    }
  };

  return (
    <KeyboardAvoidingView
      behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
      style={styles.container}
    >
      <ScrollView
        contentContainerStyle={[
          styles.scrollContent,
          {
            paddingTop: Math.max(insets.top, 24),
            paddingBottom: Math.max(insets.bottom, 24),
          },
        ]}
        keyboardShouldPersistTaps="handled"
      >
        {/* Delta Apex Brand Hero */}
        <View style={styles.brandHero}>
          <AeroLogoStacked size={350} color="#000000" showTagline={true} />
          <Text style={styles.heroSub}>Seamless Airport Mobility · Cashless & IoT</Text>
        </View>

        {/* Form Container Card */}
        <View style={styles.formCard}>
          <Text style={styles.formTitle}>Iniciar Sesión</Text>
          <Text style={styles.formSubtitle}>Ingresa a tu cuenta o consola de control</Text>

          <View style={styles.inputGroup}>
            <Text style={styles.label}>CORREO ELECTRÓNICO</Text>
            <TextInput
              style={styles.input}
              value={email}
              onChangeText={setEmail}
              placeholder="correo@aeropark.mx"
              placeholderTextColor="rgba(0, 0, 0, 0.35)"
              autoCapitalize="none"
              keyboardType="email-address"
            />
          </View>

          <View style={styles.inputGroup}>
            <Text style={styles.label}>CONTRASEÑA</Text>
            <TextInput
              style={styles.input}
              value={password}
              onChangeText={setPassword}
              placeholder="••••••••••••"
              placeholderTextColor="rgba(0, 0, 0, 0.35)"
              secureTextEntry
            />
          </View>

          <PillButton
            label={isLoading ? 'Iniciando Sesión...' : 'Iniciar Sesión'}
            onPress={handleLogin}
            variant="primary"
            size="large"
            loading={isLoading}
            style={styles.submitBtn}
          />

          <TouchableOpacity
            style={styles.biometricBtn}
            onPress={loginWithBiometrics}
            activeOpacity={0.7}
          >
            <Text style={styles.biometricText}>Ingresar con Face ID / Huella</Text>
          </TouchableOpacity>

          <View style={styles.divider} />

          {onNavigateToRegister && (
            <TouchableOpacity
              style={styles.switchModeBtn}
              onPress={onNavigateToRegister}
              activeOpacity={0.7}
            >
              <Text style={styles.switchModeText}>
                ¿No tienes cuenta? <Text style={styles.switchModeBold}>Regístrate aquí</Text>
              </Text>
            </TouchableOpacity>
          )}
        </View>

        {/* Legal Disclaimer */}
        <Text style={styles.legalFooter}>
          AeroPark Smart Mobility · Datos protegidos con cifrado AES-256 y Argon2id
        </Text>
      </ScrollView>
    </KeyboardAvoidingView>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#FFFFFF',
  },
  scrollContent: {
    paddingHorizontal: 24,
    justifyContent: 'center',
    minHeight: '100%',
    width: '100%',
    maxWidth: 480,
    alignSelf: 'center',
  },
  brandHero: {
    alignItems: 'center',
    marginBottom: 2,
  },
  heroSub: {
    color: 'rgba(0, 0, 0, 0.55)',
    fontSize: 12,
    marginTop: 2,
    textAlign: 'center',
    letterSpacing: 0.3,
  },
  roleBanner: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#F0F6FF',
    borderWidth: 1,
    borderColor: '#CCE1FF',
    borderRadius: Radii.squircleMedium,
    padding: 2,
    marginBottom: 18,
    gap: 2,
  },
  roleBannerIcon: {
    color: '#0066CC',
    fontSize: 14,
  },
  roleBannerText: {
    flex: 1,
    color: '#0066CC',
    fontSize: 11,
    lineHeight: 16,
  },
  roleBold: {
    fontWeight: '700',
    color: '#1D1D1F',
  },
  formCard: {
    backgroundColor: '#FFFFFF',
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.08)',
    borderRadius: Radii.squircleMedium,
    padding: 22,
    marginBottom: 20,
    shadowColor: '#000000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.04,
    shadowRadius: 8,
    elevation: 1,
  },
  formTitle: {
    fontSize: 20,
    fontWeight: '700',
    color: '#1D1D1F',
    letterSpacing: -0.4,
  },
  formSubtitle: {
    fontSize: 13,
    color: 'rgba(0, 0, 0, 0.55)',
    marginTop: 4,
    marginBottom: 18,
    lineHeight: 18,
  },
  inputGroup: {
    marginBottom: 14,
  },
  label: {
    fontSize: 10,
    fontWeight: '700',
    color: 'rgba(0, 0, 0, 0.5)',
    letterSpacing: 0.8,
    marginBottom: 6,
  },
  input: {
    height: 46,
    backgroundColor: '#F5F5F7',
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.1)',
    borderRadius: Radii.squircleSmall,
    paddingHorizontal: 14,
    color: '#1D1D1F',
    fontSize: 14,
  },
  plateInput: {
    fontFamily: 'Courier',
    fontWeight: '700',
    fontSize: 16,
    letterSpacing: 1.5,
  },
  vehicleSection: {
    backgroundColor: '#F8F8FA',
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.06)',
    borderRadius: Radii.squircleSmall,
    padding: 14,
    marginVertical: 6,
    marginBottom: 16,
  },
  vehicleSectionHeader: {
    marginBottom: 12,
  },
  sectionBadge: {
    fontSize: 9,
    fontWeight: '700',
    color: '#0066CC',
    letterSpacing: 1,
    marginBottom: 2,
  },
  vehicleSectionTitle: {
    fontSize: 14,
    fontWeight: '700',
    color: '#1D1D1F',
  },
  platePreviewRow: {
    alignItems: 'center',
    marginBottom: 14,
    padding: 10,
    backgroundColor: '#FFFFFF',
    borderRadius: Radii.squircleSmall,
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.06)',
  },
  platePreviewLabel: {
    fontSize: 10,
    fontWeight: '600',
    color: 'rgba(0, 0, 0, 0.45)',
    marginBottom: 6,
  },
  submitBtn: {
    marginTop: 6,
  },
  biometricBtn: {
    marginTop: 12,
    alignItems: 'center',
    paddingVertical: 6,
  },
  biometricText: {
    color: '#0066CC',
    fontSize: 13,
    fontWeight: '500',
  },
  divider: {
    height: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.08)',
    marginVertical: 16,
  },
  switchModeBtn: {
    alignItems: 'center',
    paddingVertical: 4,
  },
  switchModeText: {
    fontSize: 13,
    color: 'rgba(0, 0, 0, 0.6)',
  },
  switchModeBold: {
    fontWeight: '700',
    color: '#0066CC',
  },
  legalFooter: {
    textAlign: 'center',
    color: 'rgba(0, 0, 0, 0.35)',
    fontSize: 10,
  },
});

export default LoginScreen;
