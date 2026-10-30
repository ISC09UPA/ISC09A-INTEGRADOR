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
import { PlateBadge } from '@/components/ui/PlateBadge';
import { useAuthStore } from '@/store/authStore';
import { useVehicleStore } from '@/store/vehicleStore';
import { useParkingStore } from '@/store/parkingStore';
import { Radii } from '@/theme/tokens';

export interface RegisterScreenProps {
  onNavigateToLogin?: () => void;
}

export const RegisterScreen: React.FC<RegisterScreenProps> = ({ onNavigateToLogin }) => {
  const [nombre, setNombre] = useState('');
  const [email, setEmail] = useState('');
  const [telefono, setTelefono] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [placa, setPlaca] = useState('');
  const [marcaModelo, setMarcaModelo] = useState('');

  const { register, isLoading } = useAuthStore();
  const insets = useSafeAreaInsets();

  const handleRegister = async () => {
    if (!nombre.trim() || !email.trim() || !password || !placa.trim()) {
      Alert.alert(
        'Datos requeridos',
        'Por favor completa tu nombre, correo, contraseña y placa vehicular.'
      );
      return;
    }

    if (password !== confirmPassword) {
      Alert.alert('Contraseña no coincide', 'Por favor confirma tu contraseña correctamente.');
      return;
    }

    const cleanPlate = placa.toUpperCase().trim();

    const res = await register({
      nombre: nombre.trim(),
      email: email.trim(),
      password: password,
      telefono: telefono.trim() || '+52 449 000 0000',
      placa: cleanPlate,
      marcaModelo: marcaModelo.trim() || 'Auto Particular',
    });

    if (res.success) {
      // Registrar vehículo localmente para simulación y UI del pase
      useVehicleStore.getState().addVehicle({
        plate: cleanPlate,
        alias: marcaModelo.trim() || 'Mi Auto Principal',
        brand: marcaModelo.trim().split(' ')[0] || 'Auto',
        model: marcaModelo.trim() || 'Sedan',
        color: 'Blanco',
        isPrimary: true,
        nfcTagLinked: true,
      });

      useParkingStore
        .getState()
        .startMockSession('TERMINAL 2 (4 CAJONES)', cleanPlate);

      Alert.alert(
        '¡Registro Exitoso!',
        `Bienvenido a AeroPark, ${nombre}. Tu placa ${cleanPlate} ha sido vinculada para reconocimiento automático LPR.`
      );
    } else {
      Alert.alert('Error al registrar', res.error || 'No se pudo crear la cuenta.');
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
        {/* Brand Header */}
        <View style={styles.brandHero}>
          <AeroLogoStacked size={280} color="#000000" showTagline={true} />
        </View>

        {/* Form Container Card */}
        <View style={styles.formCard}>
          <Text style={styles.formTitle}>Registro de Cliente</Text>
          <Text style={styles.formSubtitle}>
            Crea tu cuenta de alta fidelidad y vincula tu placa para acceso automático sin fricción
          </Text>

          {/* User Personal Data */}
          <View style={styles.inputGroup}>
            <Text style={styles.label}>NOMBRE COMPLETO</Text>
            <TextInput
              style={styles.input}
              value={nombre}
              onChangeText={setNombre}
              placeholder="ej. Laura Sánchez"
              placeholderTextColor="rgba(0, 0, 0, 0.35)"
              autoCapitalize="words"
            />
          </View>

          <View style={styles.inputGroup}>
            <Text style={styles.label}>CORREO ELECTRÓNICO</Text>
            <TextInput
              style={styles.input}
              value={email}
              onChangeText={setEmail}
              placeholder="tu@correo.com"
              placeholderTextColor="rgba(0, 0, 0, 0.35)"
              autoCapitalize="none"
              keyboardType="email-address"
            />
          </View>

          <View style={styles.inputGroup}>
            <Text style={styles.label}>TELÉFONO CELULAR</Text>
            <TextInput
              style={styles.input}
              value={telefono}
              onChangeText={setTelefono}
              placeholder="+52 449 123 4567"
              placeholderTextColor="rgba(0, 0, 0, 0.35)"
              keyboardType="phone-pad"
            />
          </View>

          <View style={styles.inputGroup}>
            <Text style={styles.label}>CONTRASEÑA</Text>
            <TextInput
              style={styles.input}
              value={password}
              onChangeText={setPassword}
              placeholder="Crea tu contraseña segura"
              placeholderTextColor="rgba(0, 0, 0, 0.35)"
              secureTextEntry
            />
          </View>

          <View style={styles.inputGroup}>
            <Text style={styles.label}>CONFIRMAR CONTRASEÑA</Text>
            <TextInput
              style={styles.input}
              value={confirmPassword}
              onChangeText={setConfirmPassword}
              placeholder="Repite tu contraseña"
              placeholderTextColor="rgba(0, 0, 0, 0.35)"
              secureTextEntry
            />
          </View>

          {/* Vehicle Registration Section */}
          <View style={styles.vehicleSection}>
            <View style={styles.vehicleSectionHeader}>
              <Text style={styles.sectionBadge}>RECONOCIMIENTO AUTOMÁTICO LPR</Text>
              <Text style={styles.vehicleSectionTitle}>Información de tu Vehículo</Text>
            </View>

            <View style={styles.inputGroup}>
              <Text style={styles.label}>PLACA VEHICULAR (MATRÍCULA)</Text>
              <TextInput
                style={[styles.input, styles.plateInput]}
                value={placa}
                onChangeText={setPlaca}
                placeholder="ej. ABC-123-D"
                placeholderTextColor="rgba(0, 0, 0, 0.35)"
                autoCapitalize="characters"
              />
            </View>

            {placa.trim().length > 0 && (
              <View style={styles.platePreviewRow}>
                <Text style={styles.platePreviewLabel}>Vista previa de placa LPR:</Text>
                <PlateBadge plateNumber={placa.toUpperCase().trim()} size="normal" />
              </View>
            )}

            <View style={styles.inputGroup}>
              <Text style={styles.label}>MARCA Y MODELO DEL VEHÍCULO</Text>
              <TextInput
                style={styles.input}
                value={marcaModelo}
                onChangeText={setMarcaModelo}
                placeholder="ej. Tesla Model 3 / BMW i4"
                placeholderTextColor="rgba(0, 0, 0, 0.35)"
              />
            </View>
          </View>

          <PillButton
            label={isLoading ? 'Registrando...' : 'Registrarme y Vincular Placa'}
            onPress={handleRegister}
            variant="primary"
            size="large"
            loading={isLoading}
            style={styles.submitBtn}
          />

          <View style={styles.divider} />

          {onNavigateToLogin && (
            <TouchableOpacity
              style={styles.switchModeBtn}
              onPress={onNavigateToLogin}
              activeOpacity={0.7}
            >
              <Text style={styles.switchModeText}>
                ¿Ya tienes una cuenta? <Text style={styles.switchModeBold}>Inicia Sesión aquí</Text>
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
    marginBottom: 10,
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
    fontSize: 10,
    fontWeight: '700',
    color: '#0066CC',
    letterSpacing: 0.8,
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

export default RegisterScreen;
