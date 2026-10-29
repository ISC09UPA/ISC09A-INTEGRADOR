import { StatusBar } from 'expo-status-bar';
import { ScrollView, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView, useSafeAreaInsets } from 'react-native-safe-area-context';

import { ActionButton } from '@/components/luces/action-button';
import { GlassCard } from '@/components/luces/glass-card';
import { ModeSelector, type ModeOption } from '@/components/luces/mode-selector';
import { PendingBanner } from '@/components/luces/pending-banner';
import { ScreenBackground } from '@/components/luces/screen-background';
import { ScreenHeader } from '@/components/luces/screen-header';
import { SectionLabel } from '@/components/luces/section-label';
import { LightingColors, LightingTypography } from '@/constants/lighting-theme';
import { BottomTabInset, MaxContentWidth, Spacing } from '@/constants/theme';
import { useLighting } from '@/hooks/use-lighting';
import type { LightingMode } from '@/services/lighting-api';
import { formatReportTime, formatTime } from '@/utils/format-time';

const MODE_OPTIONS: readonly ModeOption<LightingMode>[] = [
  { value: 'auto', label: 'Automático' },
  { value: 'manual', label: 'Manual' },
];

const MODE_HELP: Record<LightingMode, string> = {
  auto: 'La luz se ajusta según la iluminación ambiental.',
  manual: 'Tú decides cuándo encender o apagar la luz.',
};

const MANUAL_HINT = 'Cambia a Manual para encender o apagar la luz.';

export default function LucesScreen() {
  const insets = useSafeAreaInsets();
  const { mode, lightOn, pending, ambientLevel, lastUpdate, loaded, setMode, toggleLight } =
    useLighting();

  const isManual = mode === 'manual';
  const actionLabel = isManual
    ? lightOn
      ? 'Apagar luz'
      : 'Encender luz'
    : 'Control manual no disponible';
  const updatedAt = formatTime(lastUpdate);

  return (
    <ScreenBackground>
      <StatusBar style="dark" />
      <SafeAreaView edges={['top']} style={styles.safeArea}>
        <ScrollView
          contentContainerStyle={[
            styles.content,
            { paddingBottom: insets.bottom + BottomTabInset + Spacing.four },
          ]}
          showsVerticalScrollIndicator={false}>
          <ScreenHeader
            title="Iluminación"
            subtitle={loaded ? `Sala · último reporte ${formatReportTime(lastUpdate)}` : 'Sala'}
          />

          {loaded && (
            <>
              <GlassCard style={styles.firstCard}>
                <SectionLabel>Luz ambiental</SectionLabel>
                <Text style={styles.metric}>{ambientLevel} %</Text>
                <Text style={styles.caption}>Lectura ilustrativa · actualizada {updatedAt}</Text>
              </GlassCard>

              <SectionLabel style={styles.sectionLabel}>Estado aplicado</SectionLabel>
              <GlassCard>
                <Text style={styles.status}>{lightOn ? 'Encendida' : 'Apagada'}</Text>
                <Text style={styles.confirmed}>Confirmado por la casa · {updatedAt}</Text>
              </GlassCard>

              <SectionLabel style={styles.sectionLabel}>Modo de control</SectionLabel>
              <ModeSelector
                options={MODE_OPTIONS}
                value={mode}
                onChange={setMode}
                disabled={pending}
              />
              <Text style={styles.help}>{MODE_HELP[mode]}</Text>

              <View style={styles.action}>
                <ActionButton
                  label={actionLabel}
                  onPress={toggleLight}
                  disabled={!isManual || pending}
                  accessibilityHint={isManual ? undefined : MANUAL_HINT}
                />
                {!isManual && <Text style={styles.help}>{MANUAL_HINT}</Text>}
              </View>

              {pending && (
                <View style={styles.banner}>
                  <PendingBanner />
                </View>
              )}
            </>
          )}
        </ScrollView>
      </SafeAreaView>
    </ScreenBackground>
  );
}

const styles = StyleSheet.create({
  safeArea: {
    flex: 1,
  },
  content: {
    width: '100%',
    maxWidth: MaxContentWidth,
    alignSelf: 'center',
    paddingHorizontal: Spacing.four,
    paddingTop: Spacing.three,
  },
  firstCard: {
    marginTop: Spacing.four,
  },
  sectionLabel: {
    marginTop: Spacing.four,
    marginBottom: 12,
  },
  metric: {
    ...LightingTypography.metric,
    marginTop: Spacing.one,
  },
  caption: {
    ...LightingTypography.caption,
    marginTop: Spacing.one,
  },
  status: LightingTypography.status,
  confirmed: {
    ...LightingTypography.body,
    color: LightingColors.success,
    marginTop: Spacing.one,
  },
  help: {
    ...LightingTypography.caption,
    marginTop: 12,
  },
  action: {
    marginTop: Spacing.four,
  },
  banner: {
    marginTop: Spacing.four,
  },
});
