import { LinearGradient } from 'expo-linear-gradient';
import { router } from 'expo-router';
import { SymbolView } from 'expo-symbols';
import { Pressable, ScrollView, StyleSheet, Text, View, type ColorValue } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { Casa } from '@/constants/casa';

function Arrow({ color = Casa.colors.arrow, size = 15 }: { color?: ColorValue; size?: number }) {
  return (
    <SymbolView
      name={{ ios: 'arrow.right', android: 'arrow_forward', web: 'arrow_forward' }}
      size={size}
      tintColor={color}
    />
  );
}

function SectionLabel({ children }: { children: string }) {
  return <Text style={styles.sectionLabel}>{children}</Text>;
}

export default function SeguridadScreen() {
  const insets = useSafeAreaInsets();

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

      <ScrollView
        showsVerticalScrollIndicator={false}
        contentContainerStyle={[
          styles.content,
          { paddingTop: insets.top + 14, paddingBottom: insets.bottom + Casa.tabBar.clearance },
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

          <Text style={styles.title}>Seguridad</Text>
          <Text style={styles.subtitle}>Casa de prueba · confirmado 18:40</Text>

          <LinearGradient
            colors={['#DCF3E9', '#EAF8F1']}
            start={{ x: 0, y: 0 }}
            end={{ x: 1, y: 1 }}
            style={styles.armedCard}>
            <View style={styles.statusRow}>
              <View style={styles.statusDotRing}>
                <View style={styles.statusDot} />
              </View>
              <Text style={styles.statusText}>ARMADA</Text>
            </View>
            <Text style={styles.armedTitle}>Sala vigilada</Text>
            <Text style={styles.armedNote}>Modo efectivo confirmado por la Pi · 18:40</Text>
          </LinearGradient>

          <Pressable
            accessibilityRole="button"
            onPress={() => {}}
            style={({ pressed }) => [styles.disarmButton, pressed && styles.pressed]}>
            <Text style={styles.disarmLabel}>Solicitar desarmado</Text>
            <Arrow size={16} />
          </Pressable>

          <SectionLabel>ÚLTIMO MOVIMIENTO</SectionLabel>

          <View style={styles.card}>
            <Text style={styles.cardTitle}>Movimiento detectado en Sala</Text>
            <Text style={styles.cardBody}>Mientras la seguridad estaba armada</Text>
            <Text style={styles.cardBody}>Hoy · 18:42</Text>
            <Text style={styles.cardCaption}>Captura disponible · análisis no iniciado</Text>
          </View>

          <Pressable
            accessibilityRole="button"
            onPress={() => router.push('/actividad')}
            style={({ pressed }) => [styles.linkRow, pressed && styles.pressed]}>
            <Text style={styles.linkLabel}>Ver incidente y evidencia</Text>
            <Arrow />
          </Pressable>

          <LinearGradient
            colors={['#FBF2D5', '#FDF6DE']}
            start={{ x: 0, y: 0 }}
            end={{ x: 1, y: 1 }}
            style={styles.pendingCard}>
            <Text style={styles.pendingTitle}>Variante · cambio pendiente</Text>
            <Text style={styles.pendingBody}>Esperando confirmación de la casa.</Text>
          </LinearGradient>

          <Pressable
            accessibilityRole="button"
            onPress={() => router.push('/actividad')}
            style={({ pressed }) => [styles.linkRow, pressed && styles.pressed]}>
            <Text style={styles.linkLabel}>Incidentes recientes</Text>
            <Arrow />
          </Pressable>
        </View>
      </ScrollView>
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
  streak: {
    position: 'absolute',
    left: -120,
    right: -120,
    top: 120,
    height: 320,
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
  pressed: {
    opacity: 0.72,
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
    marginTop: 10,
    fontSize: 32,
    lineHeight: 38,
    fontWeight: '700',
    letterSpacing: -0.6,
    color: Casa.colors.text,
  },
  subtitle: {
    marginTop: 4,
    fontSize: 13,
    lineHeight: 18,
    fontWeight: '500',
    color: Casa.colors.textMuted,
  },
  armedCard: {
    marginTop: 20,
    borderRadius: Casa.radius.card,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.85)',
    paddingHorizontal: 18,
    paddingTop: 16,
    paddingBottom: 18,
    ...cardShadow,
  },
  statusRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 7,
  },
  statusDotRing: {
    width: 14,
    height: 14,
    borderRadius: 7,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: 'rgba(15, 122, 100, 0.16)',
  },
  statusDot: {
    width: 7,
    height: 7,
    borderRadius: 4,
    backgroundColor: Casa.colors.teal,
  },
  statusText: {
    fontSize: 11,
    lineHeight: 14,
    fontWeight: '700',
    letterSpacing: 1.5,
    color: Casa.colors.tealDark,
  },
  armedTitle: {
    marginTop: 10,
    fontSize: 23,
    lineHeight: 29,
    fontWeight: '700',
    letterSpacing: -0.3,
    color: '#10201C',
  },
  armedNote: {
    marginTop: 8,
    fontSize: 12,
    lineHeight: 17,
    fontWeight: '500',
    color: '#5C7A72',
  },
  disarmButton: {
    marginTop: 12,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: 12,
    paddingHorizontal: 18,
    paddingVertical: 17,
    borderRadius: Casa.radius.card,
    borderWidth: 1,
    borderColor: Casa.colors.surfaceBorder,
    backgroundColor: Casa.colors.surface,
    ...cardShadow,
  },
  disarmLabel: {
    fontSize: 15,
    lineHeight: 20,
    fontWeight: '600',
    color: Casa.colors.teal,
  },
  sectionLabel: {
    marginTop: 22,
    marginBottom: 10,
    fontSize: 10.5,
    lineHeight: 14,
    fontWeight: '700',
    letterSpacing: 1.3,
    color: Casa.colors.label,
    alignSelf: 'flex-start',
  },
  card: {
    borderRadius: Casa.radius.card,
    borderWidth: 1,
    borderColor: Casa.colors.surfaceBorder,
    backgroundColor: Casa.colors.surface,
    paddingHorizontal: 18,
    paddingTop: 16,
    paddingBottom: 16,
    ...cardShadow,
  },
  cardTitle: {
    fontSize: 16,
    lineHeight: 22,
    fontWeight: '600',
    color: Casa.colors.text,
  },
  cardBody: {
    marginTop: 4,
    fontSize: 13,
    lineHeight: 18,
    fontWeight: '500',
    color: Casa.colors.textMuted,
  },
  cardCaption: {
    marginTop: 10,
    fontSize: 11.5,
    lineHeight: 16,
    fontWeight: '500',
    color: Casa.colors.textFaint,
  },
  linkRow: {
    marginTop: 14,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 7,
    paddingVertical: 4,
    alignSelf: 'flex-start',
  },
  linkLabel: {
    fontSize: 14,
    lineHeight: 20,
    fontWeight: '600',
    color: Casa.colors.teal,
  },
  pendingCard: {
    marginTop: 16,
    borderRadius: Casa.radius.small,
    borderWidth: 1,
    borderColor: 'rgba(246, 231, 184, 0.9)',
    paddingHorizontal: 16,
    paddingVertical: 14,
  },
  pendingTitle: {
    fontSize: 12.5,
    lineHeight: 17,
    fontWeight: '700',
    color: Casa.colors.amberText,
  },
  pendingBody: {
    marginTop: 3,
    fontSize: 13,
    lineHeight: 18,
    fontWeight: '500',
    color: Casa.colors.amberBody,
  },
});
