import { StyleSheet, Text } from 'react-native';

import { GlassCard } from '@/components/luces/glass-card';
import { LightingColors, LightingTypography } from '@/constants/lighting-theme';
import { Spacing } from '@/constants/theme';
import type { ActivityEvent, ActivityStatus } from '@/services/activity-api';
import { formatTime } from '@/utils/format-time';

type StatusStyle = {
  detailColor: string;
  backgroundColor?: string;
};

// El estilo depende solo del estado del evento.
const STATUS_STYLES: Record<ActivityStatus, StatusStyle> = {
  normal: { detailColor: LightingColors.muted },
  success: { detailColor: LightingColors.success },
  warning: {
    detailColor: LightingColors.warningText,
    backgroundColor: LightingColors.warningBg,
  },
};

type ActivityEventCardProps = {
  event: ActivityEvent;
};

export function ActivityEventCard({ event }: ActivityEventCardProps) {
  const { detailColor, backgroundColor } = STATUS_STYLES[event.status];
  const time = formatTime(event.time);
  const detail = event.delayed
    ? `${event.detail} · recibido ${time} (retrasado)`
    : `${event.detail} · ${time}`;

  return (
    <GlassCard style={backgroundColor ? { backgroundColor } : undefined}>
      <Text style={styles.title}>{event.title}</Text>
      <Text style={[styles.detail, { color: detailColor }]}>{detail}</Text>
    </GlassCard>
  );
}

const styles = StyleSheet.create({
  title: {
    ...LightingTypography.button,
    color: LightingColors.navy,
  },
  detail: {
    ...LightingTypography.caption,
    marginTop: Spacing.one,
  },
});
