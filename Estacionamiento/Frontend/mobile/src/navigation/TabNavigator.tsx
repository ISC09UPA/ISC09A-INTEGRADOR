import React, { useState } from 'react';
import { View, Text, StyleSheet, TouchableOpacity, useWindowDimensions } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { WelcomeScreen } from '@/features/home/WelcomeScreen';
import { ActiveSessionScreen } from '@/features/access/ActiveSessionScreen';
import { BookingScreen } from '@/features/booking/BookingScreen';
import { VehiclesScreen } from '@/features/vehicles/VehiclesScreen';
import { WalletScreen } from '@/features/payment/WalletScreen';
import { ApplePalette } from '@/theme/tokens';

import {
  IconHome,
  IconTicket,
  IconParking,
  IconCar,
  IconCreditCard,
  IconAlertTriangle,
} from '@/components/ui/Icons';
import { useEmergencyStore } from '@/store/emergencyStore';

export type TabKey = 'welcome' | 'session' | 'booking' | 'vehicles' | 'wallet';

export const TabNavigator: React.FC = () => {
  const [activeTab, setActiveTab] = useState<TabKey>('welcome');
  const insets = useSafeAreaInsets();
  const { width } = useWindowDimensions();
  const isDesktop = width >= 768;
  const { isEmergencyActive } = useEmergencyStore();

  const tabs: { key: TabKey; label: string; IconComponent: React.FC<any> }[] = [
    { key: 'welcome', label: 'Inicio', IconComponent: IconHome },
    { key: 'session', label: 'Pase', IconComponent: IconTicket },
    { key: 'booking', label: 'Reservar', IconComponent: IconParking },
    { key: 'vehicles', label: 'Vehículos', IconComponent: IconCar },
    { key: 'wallet', label: 'Wallet', IconComponent: IconCreditCard },
  ];

  const renderActiveScreen = () => {
    switch (activeTab) {
      case 'welcome':
        return <WelcomeScreen onNavigateToTab={(tab) => setActiveTab(tab)} />;
      case 'session':
        return <ActiveSessionScreen />;
      case 'booking':
        return <BookingScreen onNavigateToTab={(tab) => setActiveTab(tab)} />;
      case 'vehicles':
        return <VehiclesScreen />;
      case 'wallet':
        return <WalletScreen />;
    }
  };

  return (
    <View style={styles.container}>
      {/* Aviso de Emergencia Persistente en App de Pasajeros */}
      {isEmergencyActive && (
        <View style={styles.travelerEmergencyBanner}>
          <IconAlertTriangle size={16} color="#FFFFFF" />
          <Text style={styles.travelerEmergencyTxt}>
            🚨 ALERTA AEROPORTUARIA: Carriles de acceso enclavados bajo protocolo de seguridad.
          </Text>
        </View>
      )}

      <View style={[styles.screenContainer, isDesktop && styles.screenContainerDesktop]}>
        {renderActiveScreen()}
      </View>

      {/* Responsive Navigation Bar: Floating Dock on Desktop, Tab Bar on Mobile */}
      <View
        style={[
          styles.tabBar,
          isDesktop
            ? styles.tabBarDesktop
            : { paddingBottom: Math.max(insets.bottom, 14) },
        ]}
      >
        {tabs.map((tab) => {
          const isFocused = activeTab === tab.key;
          const IconComponent = tab.IconComponent;
          return (
            <TouchableOpacity
              key={tab.key}
              onPress={() => setActiveTab(tab.key)}
              style={styles.tabItem}
              activeOpacity={0.7}
            >
              <View style={styles.iconWrap}>
                <IconComponent
                  size={20}
                  color={isFocused ? '#0066CC' : '#64748B'}
                  strokeWidth={isFocused ? 2.2 : 1.75}
                />
              </View>
              <Text style={[styles.tabLabel, isFocused && styles.tabLabelActive]}>
                {tab.label}
              </Text>
            </TouchableOpacity>
          );
        })}
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#F5F5F7',
  },
  screenContainer: {
    flex: 1,
  },
  screenContainerDesktop: {
    width: '100%',
    maxWidth: 680,
    alignSelf: 'center',
  },
  tabBar: {
    flexDirection: 'row',
    backgroundColor: 'rgba(255, 255, 255, 0.94)',
    borderTopWidth: 1,
    borderTopColor: 'rgba(0, 0, 0, 0.08)',
    paddingTop: 10,
    justifyContent: 'space-around',
    alignItems: 'center',
  },
  tabBarDesktop: {
    width: '100%',
    maxWidth: 480,
    alignSelf: 'center',
    borderRadius: 24,
    marginBottom: 16,
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.08)',
    paddingBottom: 10,
    shadowColor: '#000000',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.06,
    shadowRadius: 10,
    elevation: 2,
  },
  tabItem: {
    alignItems: 'center',
    flex: 1,
  },
  iconWrap: {
    marginBottom: 3,
    alignItems: 'center',
    justifyContent: 'center',
    height: 22,
  },
  tabLabel: {
    color: '#64748B',
    fontSize: 11,
    fontWeight: '500',
    letterSpacing: 0.2,
  },
  tabLabelActive: {
    color: '#0066CC',
    fontWeight: '700',
  },
  travelerEmergencyBanner: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
    backgroundColor: '#DC2626',
    paddingVertical: 9,
    paddingHorizontal: 14,
  },
  travelerEmergencyTxt: {
    color: '#FFFFFF',
    fontSize: 11,
    fontWeight: '700',
    textAlign: 'center',
  },
});

export default TabNavigator;
