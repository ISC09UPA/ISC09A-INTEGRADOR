import React from 'react';
import { View, Text, StyleSheet } from 'react-native';
import { Radii } from '@/theme/tokens';

export interface StatusBadgeProps {
  label: string;
  variant?: 'success' | 'active' | 'warning' | 'neutral';
  showDot?: boolean;
}

export const StatusBadge: React.FC<StatusBadgeProps> = ({
  label,
  variant = 'active',
  showDot = true,
}) => {
  const getBadgeStyle = () => {
    switch (variant) {
      case 'success':
        return {
          textColor: '#16A34A',
          dotColor: '#16A34A',
          bg: 'rgba(22, 163, 74, 0.12)',
        };
      case 'active':
        return {
          textColor: '#0066CC',
          dotColor: '#0066CC',
          bg: 'rgba(0, 102, 204, 0.12)',
        };
      case 'warning':
        return {
          textColor: '#D97706',
          dotColor: '#D97706',
          bg: 'rgba(217, 119, 6, 0.12)',
        };
      case 'neutral':
      default:
        return {
          textColor: '#64748B',
          dotColor: '#94A3B8',
          bg: 'rgba(0, 0, 0, 0.05)',
        };
    }
  };

  const { textColor, dotColor, bg } = getBadgeStyle();

  return (
    <View style={[styles.container, { backgroundColor: bg }]}>
      {showDot && <View style={[styles.dot, { backgroundColor: dotColor }]} />}
      <Text style={[styles.label, { color: textColor }]}>{label}</Text>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 9,
    paddingVertical: 3,
    borderRadius: Radii.pill,
    alignSelf: 'flex-start',
  },
  dot: {
    width: 6,
    height: 6,
    borderRadius: 3,
    marginRight: 6,
  },
  label: {
    fontSize: 11,
    fontWeight: '600',
    letterSpacing: 0.3,
  },
});

export default StatusBadge;
