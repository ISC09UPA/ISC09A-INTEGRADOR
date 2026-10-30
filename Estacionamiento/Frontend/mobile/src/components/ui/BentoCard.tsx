import React from 'react';
import { View, StyleSheet, ViewStyle, StyleProp } from 'react-native';
import { ApplePalette, Radii } from '@/theme/tokens';

export interface BentoCardProps {
  children: React.ReactNode;
  style?: StyleProp<ViewStyle>;
  variant?: 'surface' | 'translucent' | 'elevated';
  highlightBorder?: boolean;
}

export const BentoCard: React.FC<BentoCardProps> = ({
  children,
  style,
  variant = 'surface',
  highlightBorder = false,
}) => {
  const getBackgroundColor = () => {
    switch (variant) {
      case 'translucent':
        return 'rgba(255, 255, 255, 0.85)';
      case 'elevated':
        return '#FFFFFF';
      case 'surface':
      default:
        return '#FFFFFF';
    }
  };

  return (
    <View
      style={[
        styles.card,
        {
          backgroundColor: getBackgroundColor(),
          borderColor: highlightBorder
            ? 'rgba(0, 113, 227, 0.4)'
            : 'rgba(0, 0, 0, 0.08)',
        },
        style,
      ]}
    >
      {children}
    </View>
  );
};

const styles = StyleSheet.create({
  card: {
    borderRadius: Radii.squircleLarge,
    borderWidth: 1,
    padding: 20,
    overflow: 'hidden',
    shadowColor: '#000000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.04,
    shadowRadius: 8,
    elevation: 1,
  },
});

export default BentoCard;
