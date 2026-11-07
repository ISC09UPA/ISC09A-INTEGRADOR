import { StatusBar } from 'expo-status-bar';
import { useMemo, useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView, useSafeAreaInsets } from 'react-native-safe-area-context';

import { ActivityEventCard } from '@/components/actividad/activity-event-card';
import { FilterBar, type FilterOption } from '@/components/actividad/filter-bar';
import { ScreenBackground } from '@/components/luces/screen-background';
import { ScreenHeader } from '@/components/luces/screen-header';
import { SectionLabel } from '@/components/luces/section-label';
import { LightingColors, LightingTypography } from '@/constants/lighting-theme';
import { BottomTabInset, MaxContentWidth, Spacing } from '@/constants/theme';
import { useActivity } from '@/hooks/use-activity';
import type { ActivityCategory, ActivityEvent } from '@/services/activity-api';
import { formatDayLabel } from '@/utils/format-time';

type ActivityFilter = 'todo' | ActivityCategory;

const FILTER_OPTIONS: readonly FilterOption<ActivityFilter>[] = [
  { value: 'todo', label: 'Todo' },
  { value: 'luz', label: 'Luz' },
  { value: 'seguridad', label: 'Seguridad' },
  { value: 'accesos', label: 'Accesos' },
];

type ActivityLink = {
  key: string;
  label: string;
  onPress?: () => void;
};

// TODO: navegar a las pantallas correspondientes cuando existan.
const LINKS: readonly ActivityLink[] = [
  { key: 'personas', label: 'Personas autorizadas' },
  { key: 'avisos', label: 'Avisos y preferencias' },
];

type DayGroup = {
  key: string;
  label: string;
  events: ActivityEvent[];
};

// Agrupa eventos (ya ordenados) por día, conservando el orden.
function groupByDay(events: readonly ActivityEvent[]): DayGroup[] {
  const groups: DayGroup[] = [];
  for (const event of events) {
    const key = event.time.toDateString();
    const last = groups[groups.length - 1];
    if (last?.key === key) {
      last.events.push(event);
    } else {
      groups.push({ key, label: formatDayLabel(event.time), events: [event] });
    }
  }
  return groups;
}

export default function ActividadScreen() {
  const insets = useSafeAreaInsets();
  const { events, loaded } = useActivity();
  const [filter, setFilter] = useState<ActivityFilter>('todo');

  const groups = useMemo(
    () => groupByDay(filter === 'todo' ? events : events.filter((e) => e.category === filter)),
    [events, filter],
  );

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
          <ScreenHeader title="Actividad" subtitle="Casa de prueba · eventos recientes" />

          <View style={styles.filters}>
            <FilterBar options={FILTER_OPTIONS} value={filter} onChange={setFilter} />
          </View>

          {loaded && (
            <>
              {groups.map((group) => (
                <View key={group.key}>
                  <SectionLabel style={styles.sectionLabel}>{group.label}</SectionLabel>
                  <View style={styles.list}>
                    {group.events.map((event) => (
                      <ActivityEventCard key={event.id} event={event} />
                    ))}
                  </View>
                </View>
              ))}

              {groups.length === 0 && (
                <Text style={styles.empty}>No hay eventos recientes en esta categoría.</Text>
              )}

              <View style={styles.links}>
                {LINKS.map((link) => (
                  <Pressable
                    key={link.key}
                    accessibilityRole="link"
                    onPress={link.onPress}
                    style={({ pressed }) => pressed && styles.pressed}>
                    <Text style={styles.link}>{link.label}</Text>
                  </Pressable>
                ))}
              </View>
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
  filters: {
    marginTop: Spacing.four,
  },
  sectionLabel: {
    marginTop: Spacing.four,
    marginBottom: 12,
  },
  list: {
    gap: 12,
  },
  empty: {
    ...LightingTypography.caption,
    marginTop: Spacing.four,
  },
  links: {
    marginTop: Spacing.four,
    gap: Spacing.three,
  },
  link: {
    ...LightingTypography.body,
    color: LightingColors.accent,
    fontWeight: '500',
  },
  pressed: {
    opacity: 0.6,
  },
});
