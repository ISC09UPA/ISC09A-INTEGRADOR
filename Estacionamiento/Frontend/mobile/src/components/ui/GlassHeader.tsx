import React from 'react';
import { View, Text, StyleSheet, TouchableOpacity } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { AeroLogo, AeroLogoHorizontal } from '@/components/brand/AeroLogo';
import { useAuthStore } from '@/store/authStore';
import { ApplePalette } from '@/theme/tokens';

export interface GlassHeaderProps {
  title?: string;
  showLogo?: boolean;
  showLogout?: boolean;
  logoVariant?: 'mark' | 'horizontal';
  leftAction?: {
    icon: React.ReactNode;
    onPress: () => void;
  };
  rightAction?: {
    icon: React.ReactNode;
    onPress: () => void;
  };
}

export const GlassHeader: React.FC<GlassHeaderProps> = ({
  title,
  showLogo = true,
  showLogout = true,
  logoVariant = 'mark',
  leftAction,
  rightAction,
}) => {
  const insets = useSafeAreaInsets();
  const { logout, user } = useAuthStore();

  return (
    <View style={[styles.container, { paddingTop: Math.max(insets.top, 16) }]}>
      <View style={styles.content}>
        {/* Left Side: Logo or Action */}
        <View style={styles.side}>
          {leftAction ? (
            <TouchableOpacity onPress={leftAction.onPress} style={styles.iconBtn}>
              {leftAction.icon}
            </TouchableOpacity>
          ) : showLogo && logoVariant === 'mark' ? (
            <AeroLogo size={28} color="#000000" />
          ) : null}
        </View>

        {/* Center: Title or Horizontal Lockup */}
        <View style={styles.center}>
          {showLogo && logoVariant === 'horizontal' ? (
            <AeroLogoHorizontal size={24} color="#000000" showTagline={false} />
          ) : title ? (
            <Text style={styles.title}>{title}</Text>
          ) : null}
        </View>

        {/* Right Side: Logout Button or Custom Right Action */}
        <View style={[styles.side, styles.sideRight]}>
          {rightAction ? (
            <TouchableOpacity onPress={rightAction.onPress} style={styles.iconBtn}>
              {rightAction.icon}
            </TouchableOpacity>
          ) : showLogout ? (
            <TouchableOpacity onPress={logout} style={styles.logoutBtn} activeOpacity={0.7}>
              <Text style={styles.logoutIcon}>⎋</Text>
              <Text style={styles.logoutText}>Salir</Text>
            </TouchableOpacity>
          ) : null}
        </View>
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    backgroundColor: 'rgba(255, 255, 255, 0.92)',
    borderBottomWidth: 0.5,
    borderBottomColor: 'rgba(0, 0, 0, 0.08)',
  },
  content: {
    height: 48,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 16,
  },
  side: {
    width: 80,
    flexDirection: 'row',
    alignItems: 'center',
  },
  sideRight: {
    justifyContent: 'flex-end',
  },
  center: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
  },
  title: {
    color: '#1D1D1F',
    fontSize: 16,
    fontWeight: '600',
    letterSpacing: -0.3,
  },
  iconBtn: {
    padding: 6,
  },
  logoutBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    backgroundColor: 'rgba(220, 38, 38, 0.08)',
    borderWidth: 0.5,
    borderColor: 'rgba(220, 38, 38, 0.25)',
    paddingVertical: 5,
    paddingHorizontal: 10,
    borderRadius: 980,
  },
  logoutIcon: {
    color: '#DC2626',
    fontSize: 13,
    fontWeight: '700',
  },
  logoutText: {
    color: '#DC2626',
    fontSize: 11,
    fontWeight: '600',
  },
});

export default GlassHeader;
