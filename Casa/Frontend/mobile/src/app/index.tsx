import { useState } from 'react';
import { ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { Feather } from '@expo/vector-icons';
import { Background } from '@/components/background';
import { Card } from '@/components/card';
import { Dot } from '@/components/dot';
import { Link } from '@/components/link';
import { QuickAction, type QuickActionProps } from '@/components/quick-action';
import { SectionLabel } from '@/components/section-label';
import { TabBar } from '@/components/tab-bar';
import { alternar, casa, controlesIniciales, type Control } from '@/constants/casa';
import { colors, text } from '@/constants/casa-theme';

export default function HomeScreen() {
  const insets = useSafeAreaInsets();
  const [{ luz, armada, noche }, setControles] = useState(controlesIniciales);
  const toggle = (key: Control) => () => setControles((c) => alternar(c, key));

  const accesos: QuickActionProps[] = [
    { icon: 'sun', label: luz ? 'Apagar luces' : 'Encender luces', activo: luz, onPress: toggle('luz') },
    { icon: 'shield', label: armada ? 'Desarmar' : 'Armar', activo: armada, onPress: toggle('armada') },
    { icon: 'video', label: 'Cámara', activo: false },
    { icon: 'moon', label: 'Modo noche', activo: noche, onPress: toggle('noche') },
  ];

  return (
    <View style={styles.root}>
      <Background />

      <ScrollView
        contentContainerStyle={[styles.content, { paddingTop: insets.top + 24, paddingBottom: insets.bottom + 120 }]}
        showsVerticalScrollIndicator={false}
      >
        <Text style={styles.title}>{casa.nombre}</Text>
        <Text style={styles.subtitle}>Estado de la casa · actualizado hoy, {casa.actualizado}</Text>

        <View style={styles.connection}>
          <View style={styles.inline}>
            <Dot color={colors.green} />
            <Text style={styles.connectionText}>Raspberry conectada</Text>
          </View>
          <Text style={styles.connectionSub}>Último reporte {casa.actualizado}</Text>
        </View>

        <SectionLabel>Resumen</SectionLabel>

        <Card>
          <Text style={styles.cardTitle}>
            Sala <Text style={styles.sep}> · </Text> Luz
          </Text>
          <Text style={[styles.bigValue, !luz && styles.off]}>{luz ? 'Encendida' : 'Apagada'}</Text>
          <Text style={styles.meta}>
            {noche ? 'Modo noche' : 'Automático'}
            <Text style={styles.sep}>  ·  </Text>
            ambiente {casa.ambiente}%
          </Text>
          <Link style={{ marginTop: 14 }}>Ver iluminación</Link>
        </Card>

        <Card>
          <Text style={styles.cardTitle}>Seguridad</Text>
          <View style={styles.securityRow}>
            <View style={styles.inline}>
              <Dot color={armada ? colors.green : colors.faint} />
              <Text style={[styles.securityState, !armada && styles.off]}>{armada ? 'Armada' : 'Desarmada'}</Text>
            </View>
            <Text style={styles.metaRight}>{armada ? 'Sala vigilada' : 'Sin vigilancia'}</Text>
          </View>
        </Card>

        <View style={styles.row}>
          <Card style={styles.half}>
            <Text style={styles.cardTitle}>Cámara</Text>
            <Text style={styles.available}>Disponible</Text>
            <Text style={styles.inlineLink}>Ver en vivo →</Text>
          </Card>
          <Card style={styles.half}>
            <Text style={styles.eyebrow}>Último evento</Text>
            <Text style={styles.eventText}>
              {casa.ultimoEvento.tipo}
              {'\n'}en {casa.ultimoEvento.lugar} · {casa.ultimoEvento.hora}
            </Text>
          </Card>
        </View>

        <Link style={styles.activityLink} size={18}>Ver actividad</Link>

        <Card>
          <Text style={styles.cardLabel}>Accesos rápidos</Text>
          <View style={styles.quickGrid}>
            {accesos.map((a) => (
              <QuickAction key={a.icon} {...a} />
            ))}
          </View>
        </Card>

        <SectionLabel>Ambiente</SectionLabel>

        <Card>
          <View style={styles.sensorRow}>
            {casa.sensores.map((s, i) => (
              <View key={s.etiqueta} style={[styles.sensor, i > 0 && styles.sensorDivider]}>
                <Feather name={s.icon} size={18} color={colors.teal} />
                <Text style={styles.sensorValue}>{s.valor}</Text>
                <Text style={styles.sensorLabel}>{s.etiqueta}</Text>
              </View>
            ))}
          </View>
        </Card>
      </ScrollView>

      <TabBar bottom={insets.bottom} />
    </View>
  );
}

const styles = StyleSheet.create({
  root: { flex: 1, backgroundColor: '#ECEEF7' },
  content: { paddingHorizontal: 22 },
  title: { ...text('semibold', 32), letterSpacing: -0.6 },
  subtitle: { ...text('regular', 15, 'muted'), marginTop: 6 },
  connection: {
    marginTop: 22,
    padding: 16,
    borderRadius: 26,
    backgroundColor: colors.greenSoft,
    borderWidth: 1.5,
    borderColor: colors.glassBorder,
  },
  inline: { flexDirection: 'row', alignItems: 'center', gap: 10 },
  connectionText: text('medium', 18, 'green'),
  connectionSub: { ...text('regular', 14, 'muted'), marginTop: 4 },
  cardTitle: text('medium', 19),
  cardLabel: text('regular', 15, 'muted'),
  sep: { color: colors.faint },
  off: { color: colors.muted },
  bigValue: { ...text('medium', 30), letterSpacing: -0.4, marginTop: 14 },
  meta: { ...text('regular', 15, 'muted'), marginTop: 14 },
  metaRight: text('regular', 15, 'muted'),
  securityRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginTop: 14,
    marginBottom: 20,
    paddingRight: 40,
  },
  securityState: text('medium', 18, 'green'),
  row: { flexDirection: 'row', gap: 12 },
  half: { flex: 1, minHeight: 132 },
  available: { ...text('medium', 15, 'teal'), marginTop: 12 },
  inlineLink: { ...text('regular', 14), marginTop: 12 },
  eyebrow: { ...text('medium', 12, 'muted'), letterSpacing: 0.6, textTransform: 'uppercase' },
  eventText: { ...text('medium', 16), lineHeight: 22, marginTop: 14 },
  activityLink: { marginTop: 18, marginBottom: 24 },
  quickGrid: { flexDirection: 'row', justifyContent: 'space-between', marginTop: 16 },
  sensorRow: { flexDirection: 'row' },
  sensor: { flex: 1, alignItems: 'center', gap: 6 },
  sensorDivider: { borderLeftWidth: 1, borderLeftColor: 'rgba(22, 34, 46, 0.08)' },
  sensorValue: text('semibold', 18),
  sensorLabel: text('regular', 12, 'muted'),
});
