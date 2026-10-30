import React from 'react';
import {
  View,
  TouchableOpacity,
  Text,
  StyleSheet,
  ViewStyle,
  TextStyle,
  ActivityIndicator,
  Animated,
} from 'react-native';
import * as Haptics from 'expo-haptics';
import { ApplePalette, Radii } from '@/theme/tokens';

export interface PillButtonProps {
  label: string;
  onPress: () => void;
  variant?: 'primary' | 'secondary' | 'glass' | 'danger';
  size?: 'normal' | 'large' | 'small';
  disabled?: boolean;
  loading?: boolean;
  style?: ViewStyle;
  textStyle?: TextStyle;
  icon?: React.ReactNode;
}

export const PillButton: React.FC<PillButtonProps> = ({
  label,
  onPress,
  variant = 'primary',
  size = 'normal',
  disabled = false,
  loading = false,
  style,
  textStyle,
  icon,
}) => {
  const scaleAnim = React.useRef(new Animated.Value(1)).current;

  const handlePressIn = () => {
    Animated.spring(scaleAnim, {
      toValue: 0.97,
      useNativeDriver: true,
      speed: 20,
    }).start();
  };

  const handlePressOut = () => {
    Animated.spring(scaleAnim, {
      toValue: 1,
      useNativeDriver: true,
      speed: 20,
    }).start();
  };

  const handlePress = () => {
    if (disabled || loading) return;
    try {
      Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Light);
    } catch {
      // Haptics not available in web/simulator
    }
    onPress();
  };

  const getContainerStyle = (): ViewStyle => {
    switch (variant) {
      case 'primary':
        return {
          backgroundColor: '#0066CC',
          borderWidth: 0,
        };
      case 'secondary':
        return {
          backgroundColor: '#E8E8ED',
          borderWidth: 0,
        };
      case 'glass':
        return {
          backgroundColor: 'rgba(0, 0, 0, 0.05)',
          borderWidth: 1,
          borderColor: 'rgba(0, 0, 0, 0.1)',
        };
      case 'danger':
        return {
          backgroundColor: '#DC2626',
          borderWidth: 0,
        };
    }
  };

  const getTextColor = (): string => {
    switch (variant) {
      case 'primary':
      case 'danger':
        return '#FFFFFF';
      case 'secondary':
      case 'glass':
        return '#1D1D1F';
    }
  };

  const getHeight = () => {
    switch (size) {
      case 'large':
        return 56;
      case 'small':
        return 38;
      case 'normal':
      default:
        return 48;
    }
  };

  return (
    <Animated.View style={{ transform: [{ scale: scaleAnim }] }}>
      <TouchableOpacity
        activeOpacity={0.85}
        onPressIn={handlePressIn}
        onPressOut={handlePressOut}
        onPress={handlePress}
        disabled={disabled || loading}
        style={[
          styles.base,
          getContainerStyle(),
          { height: getHeight() },
          disabled && styles.disabled,
          style,
        ]}
      >
        {loading ? (
          <ActivityIndicator color={getTextColor()} />
        ) : (
          <>
            {icon && <View style={styles.iconContainer}>{icon}</View>}
            <Text
              style={[
                styles.text,
                { color: getTextColor() },
                size === 'large' && styles.textLarge,
                size === 'small' && styles.textSmall,
                textStyle,
              ]}
            >
              {label}
            </Text>
          </>
        )}
      </TouchableOpacity>
    </Animated.View>
  );
};

const styles = StyleSheet.create({
  base: {
    borderRadius: Radii.pill,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 24,
  },
  iconContainer: {
    marginRight: 8,
  },
  text: {
    fontFamily: '-apple-system',
    fontSize: 16,
    fontWeight: '600',
    letterSpacing: -0.2,
  },
  textLarge: {
    fontSize: 17,
    fontWeight: '600',
  },
  textSmall: {
    fontSize: 14,
    fontWeight: '500',
  },
  disabled: {
    opacity: 0.4,
  },
});

export default PillButton;
