import React from 'react';
import { View, Text, StyleSheet } from 'react-native';
import { Radii } from '@/theme/tokens';

export interface PlateBadgeProps {
  plateNumber: string;
  stateOrCountry?: string;
  size?: 'normal' | 'large';
}

export const PlateBadge: React.FC<PlateBadgeProps> = ({
  plateNumber,
  stateOrCountry = 'MEX',
  size = 'normal',
}) => {
  return (
    <View style={[styles.plate, size === 'large' && styles.plateLarge]}>
      <View style={styles.header}>
        <Text style={styles.countryText}>{stateOrCountry}</Text>
      </View>
      <Text style={[styles.numberText, size === 'large' && styles.numberTextLarge]}>
        {plateNumber.toUpperCase()}
      </Text>
    </View>
  );
};

const styles = StyleSheet.create({
  plate: {
    backgroundColor: '#FFFFFF',
    borderColor: '#D2D3D8',
    borderWidth: 1.5,
    borderRadius: Radii.squircleSmall,
    paddingHorizontal: 12,
    paddingVertical: 6,
    alignItems: 'center',
    justifyContent: 'center',
    alignSelf: 'flex-start',
    shadowColor: '#000000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.08,
    shadowRadius: 3,
    elevation: 2,
  },
  plateLarge: {
    paddingHorizontal: 18,
    paddingVertical: 10,
    borderWidth: 2,
  },
  header: {
    marginBottom: 2,
  },
  countryText: {
    color: '#0066CC',
    fontSize: 9,
    fontWeight: '700',
    letterSpacing: 1.5,
  },
  numberText: {
    color: '#1D1D21',
    fontFamily: 'Courier',
    fontSize: 18,
    fontWeight: '800',
    letterSpacing: 2.5,
  },
  numberTextLarge: {
    fontSize: 24,
    letterSpacing: 3,
  },
});

export default PlateBadge;
