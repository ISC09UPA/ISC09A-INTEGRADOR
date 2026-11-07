import { LinearGradient } from 'expo-linear-gradient';
import { router } from 'expo-router';
import { SymbolView } from 'expo-symbols';
import { useState } from 'react';
import {
  KeyboardAvoidingView,
  Platform,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { Casa } from '@/constants/casa';

export default function LoginScreen() {
  const insets = useSafeAreaInsets();
  const [email, setEmail] = useState('sam.torres@example.com');
  const [password, setPassword] = useState('CasaSegura2024');

  const handleSubmit = () => {
    router.replace('/seguridad');
  };

  return (
    <View style={styles.root}>
      <LinearGradient
        colors={Casa.gradient}
        start={{ x: 0.05, y: 0 }}
        end={{ x: 0.95, y: 1 }}
        style={StyleSheet.absoluteFill}
      />
      <LinearGradient
        colors={['rgba(255,255,255,0)', 'rgba(255,255,255,0.55)', 'rgba(255,255,255,0)']}
        start={{ x: 0, y: 0.5 }}
        end={{ x: 1, y: 0.5 }}
        style={styles.streak}
        pointerEvents="none"
      />

      <KeyboardAvoidingView
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        style={styles.flex}>
        <ScrollView
          keyboardShouldPersistTaps="handled"
          showsVerticalScrollIndicator={false}
          contentContainerStyle={[
            styles.content,
            { paddingTop: insets.top + 22, paddingBottom: insets.bottom + 28 },
          ]}>
          <View style={styles.inner}>
            <View style={styles.eyebrowRow}>
              <SymbolView
                name={{ ios: 'house', android: 'home', web: 'home' }}
                size={13}
                tintColor={Casa.colors.tealDark}
              />
              <Text style={styles.eyebrow}>CASA DOMÓTICA</Text>
            </View>

            <Text style={styles.title}>Bienvenido a casa</Text>
            <Text style={styles.subtitle}>Accede para consultar y controlar tu hogar.</Text>

            <Text style={styles.houseLabel}>Casa de prueba</Text>

            <View style={styles.field}>
              <Text style={styles.fieldLabel}>CORREO ELECTRÓNICO</Text>
              <TextInput
                accessibilityLabel="Correo electrónico"
                autoCapitalize="none"
                autoComplete="email"
                keyboardType="email-address"
                onChangeText={setEmail}
                placeholder="tu@correo.com"
                placeholderTextColor={Casa.colors.textFaint}
                selectionColor={Casa.colors.teal}
                style={styles.fieldValue}
                value={email}
              />
            </View>

            <View style={[styles.field, styles.fieldSpacing]}>
              <Text style={styles.fieldLabel}>CONTRASEÑA</Text>
              <TextInput
                accessibilityLabel="Contraseña"
                autoCapitalize="none"
                autoComplete="current-password"
                onChangeText={setPassword}
                placeholder="••••••••••"
                placeholderTextColor={Casa.colors.textFaint}
                secureTextEntry
                selectionColor={Casa.colors.teal}
                style={styles.fieldValue}
                value={password}
              />
            </View>

            <Pressable
              accessibilityRole="button"
              onPress={handleSubmit}
              style={({ pressed }) => [styles.submitWrap, pressed && styles.pressed]}>
              <LinearGradient
                colors={['#0D6E5C', '#0F7A64']}
                start={{ x: 0, y: 0 }}
                end={{ x: 1, y: 1 }}
                style={styles.submit}>
                <Text style={styles.submitLabel}>Iniciar sesión</Text>
              </LinearGradient>
            </Pressable>

            <Text style={styles.helper}>Al iniciar sesión, verás el estado de la solicitud.</Text>

            <Text style={styles.footer}>No hay registro público ni acceso social.</Text>
          </View>
        </ScrollView>
      </KeyboardAvoidingView>
    </View>
  );
}

const cardShadow = {
  shadowColor: '#53627F',
  shadowOpacity: 0.08,
  shadowRadius: 18,
  shadowOffset: { width: 0, height: 8 },
  elevation: 3,
} as const;

const styles = StyleSheet.create({
  root: {
    flex: 1,
    backgroundColor: '#E4EBFA',
    overflow: 'hidden',
  },
  flex: {
    flex: 1,
  },
  streak: {
    position: 'absolute',
    left: -120,
    right: -120,
    top: 140,
    height: 340,
    transform: [{ rotate: '-20deg' }],
  },
  content: {
    flexGrow: 1,
    paddingHorizontal: 20,
    alignItems: 'center',
  },
  inner: {
    width: '100%',
    maxWidth: 520,
  },
  eyebrowRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  eyebrow: {
    fontSize: 10.5,
    lineHeight: 14,
    fontWeight: '700',
    letterSpacing: 1.4,
    color: Casa.colors.label,
  },
  title: {
    marginTop: 18,
    fontSize: 34,
    lineHeight: 40,
    fontWeight: '700',
    letterSpacing: -0.7,
    color: Casa.colors.text,
  },
  subtitle: {
    marginTop: 6,
    fontSize: 14,
    lineHeight: 20,
    fontWeight: '500',
    color: Casa.colors.textMuted,
  },
  houseLabel: {
    marginTop: 34,
    marginBottom: 10,
    fontSize: 13,
    lineHeight: 18,
    fontWeight: '600',
    color: Casa.colors.textMuted,
  },
  field: {
    borderRadius: Casa.radius.small,
    borderWidth: 1,
    borderColor: Casa.colors.surfaceBorder,
    backgroundColor: Casa.colors.surface,
    paddingHorizontal: 16,
    paddingTop: 12,
    paddingBottom: 14,
    ...cardShadow,
  },
  fieldSpacing: {
    marginTop: 12,
  },
  fieldLabel: {
    fontSize: 10,
    lineHeight: 13,
    fontWeight: '700',
    letterSpacing: 1.2,
    color: Casa.colors.label,
  },
  fieldValue: {
    marginTop: 4,
    padding: 0,
    fontSize: 15.5,
    lineHeight: 21,
    fontWeight: '600',
    color: Casa.colors.text,
  },
  submitWrap: {
    marginTop: 30,
    borderRadius: Casa.radius.card,
    ...cardShadow,
  },
  submit: {
    borderRadius: Casa.radius.card,
    paddingVertical: 18,
    alignItems: 'center',
    justifyContent: 'center',
  },
  submitLabel: {
    fontSize: 16,
    lineHeight: 21,
    fontWeight: '700',
    letterSpacing: 0.1,
    color: '#FFFFFF',
  },
  pressed: {
    opacity: 0.85,
  },
  helper: {
    marginTop: 14,
    fontSize: 12.5,
    lineHeight: 17,
    fontWeight: '500',
    color: Casa.colors.textMuted,
  },
  footer: {
    marginTop: 34,
    fontSize: 12,
    lineHeight: 17,
    fontWeight: '500',
    color: Casa.colors.textFaint,
  },
});
