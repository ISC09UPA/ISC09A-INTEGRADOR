import React from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
} from 'react-native';
import { GlassHeader } from '@/components/ui/GlassHeader';
import { BentoCard } from '@/components/ui/BentoCard';
import { PlateBadge } from '@/components/ui/PlateBadge';
import { useAuthStore } from '@/store/authStore';
import { useVehicleStore } from '@/store/vehicleStore';
import { usePaymentStore } from '@/store/paymentStore';
import { Radii } from '@/theme/tokens';
import { TabKey } from '@/navigation/TabNavigator';

import {
  IconParking,
  IconTicket,
  IconCar,
  IconCreditCard,
  IconArrowRight,
} from '@/components/ui/Icons';

export interface WelcomeScreenProps {
  onNavigateToTab?: (tab: TabKey) => void;
}

export const WelcomeScreen: React.FC<WelcomeScreenProps> = ({ onNavigateToTab }) => {
  const { user } = useAuthStore();
  const { vehicles, primaryVehicle } = useVehicleStore();
  const { cards } = usePaymentStore();

  const firstName = user?.fullName ? user.fullName.split(' ')[0] : 'Viajero';
  const activeVehicle = primaryVehicle || vehicles[0];
  const defaultCard = cards.find((c) => c.isDefault) || cards[0];

  const screenLinks = [
    {
      tab: 'booking' as TabKey,
      IconComponent: IconParking,
      badge: 'TIEMPO REAL',
      title: 'Reservar Cajón',
      subtitle: 'Estacionamientos A y B · 40 Cajones',
      description:
        'Consulta cajones libres en el plano arquitectónico y reserva tu lugar con tarifas por hora, día o temporada.',
      actionText: 'Ir a Reservas →',
    },
    {
      tab: 'session' as TabKey,
      IconComponent: IconTicket,
      badge: 'MONITOREO ACTIVO',
      title: 'Pase de Estancia Activa',
      subtitle: 'Acceso y salida automatizada',
      description:
        'Revisa el estado de tu vehículo en tiempo real, sensores de impacto perimetral y cronómetro de tarifa.',
      actionText: 'Ver Pase Digital →',
    },
    {
      tab: 'vehicles' as TabKey,
      IconComponent: IconCar,
      badge: `${vehicles.length} VEHÍCULOS`,
      title: 'Mis Vehículos',
      subtitle: 'Flota vinculada para LPR',
      description:
        'Vincula placas vehiculares, agrega nuevos autos y establece el vehículo predeterminado para tus accesos.',
      actionText: 'Gestionar Vehículos →',
    },
    {
      tab: 'wallet' as TabKey,
      IconComponent: IconCreditCard,
      badge: `${cards.length} TARJETAS`,
      title: 'Wallet & Métodos de Pago',
      subtitle: 'Pagos Cashless y CFDI 4.0',
      description:
        'Administra tus tarjetas guardadas, elige la predeterminada para cobro en carril y consulta tus comprobantes fiscales.',
      actionText: 'Abrir Wallet →',
    },
  ];

  return (
    <View style={styles.container}>
      <GlassHeader title="Inicio" showLogo={true} showLogout={true} />

      <ScrollView
        contentContainerStyle={styles.scrollContent}
        showsVerticalScrollIndicator={false}
      >
        {/* Editorial Hero Header */}
        <View style={styles.heroSection}>
          <Text style={styles.heroEyebrow}>AEROPARK AUTOMATED ACCESS</Text>
          <Text style={styles.heroTitle}>Hola, {firstName}</Text>
          <Text style={styles.heroSubtitle}>
            Panel de control vehicular inteligente. Selecciona un módulo para gestionar tu estancia o servicios.
          </Text>
        </View>

        {/* Quick Status Bar */}
        <View style={styles.statusBarCard}>
          <View style={styles.statusItem}>
            <Text style={styles.statusLabel}>AUTO PREDETERMINADO</Text>
            <View style={styles.statusContentRow}>
              <PlateBadge plateNumber={activeVehicle?.plate || 'ABC-123-D'} size="normal" />
              <Text style={styles.statusVehicleText} numberOfLines={1}>
                {activeVehicle?.brand} {activeVehicle?.model}
              </Text>
            </View>
          </View>

          <View style={styles.statusDivider} />

          <View style={styles.statusItem}>
            <Text style={styles.statusLabel}>PAGO PREDETERMINADO</Text>
            <View style={{ flexDirection: 'row', alignItems: 'center', gap: 6, marginTop: 4 }}>
              <IconCreditCard size={16} color="#0066CC" />
              <Text style={styles.statusPaymentText} numberOfLines={1}>
                {defaultCard ? `${defaultCard.brand} •••• ${defaultCard.last4}` : 'Apple Pay'}
              </Text>
            </View>
            <Text style={styles.statusPaymentSub}>Cobro automático en carril de salida</Text>
          </View>
        </View>

        {/* Section Title */}
        <View style={styles.sectionHeaderRow}>
          <Text style={styles.sectionTitle}>MÓDULOS DEL SISTEMA</Text>
          <Text style={styles.sectionSub}>Acceso directo a pantallas</Text>
        </View>

        {/* Interactive Navigation Cards */}
        <View style={styles.cardsContainer}>
          {screenLinks.map((item) => (
            <TouchableOpacity
              key={item.tab}
              style={styles.navCardTouchable}
              onPress={() => onNavigateToTab?.(item.tab)}
              activeOpacity={0.7}
            >
              <BentoCard variant="surface" style={styles.navCard}>
                <View style={styles.cardHeader}>
                  <View style={styles.cardIconBox}>
                    <item.IconComponent size={20} color="#0066CC" strokeWidth={1.8} />
                  </View>
                  <View style={styles.badgePill}>
                    <Text style={styles.badgePillText}>{item.badge}</Text>
                  </View>
                </View>

                <Text style={styles.cardTitle}>{item.title}</Text>
                <Text style={styles.cardSubtitle}>{item.subtitle}</Text>
                <Text style={styles.cardDesc}>{item.description}</Text>

                <View style={styles.cardActionRow}>
                  <Text style={styles.cardActionText}>{item.actionText}</Text>
                </View>
              </BentoCard>
            </TouchableOpacity>
          ))}
        </View>

        <Text style={styles.footerHelp}>
          AeroPark Smart Parking · Acceso LPR y Pagos Cashless Automatizados
        </Text>
      </ScrollView>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#FFFFFF',
  },
  scrollContent: {
    paddingHorizontal: 20,
    paddingTop: 16,
    paddingBottom: 40,
    maxWidth: 600,
    width: '100%',
    alignSelf: 'center',
  },
  heroSection: {
    marginBottom: 18,
  },
  heroEyebrow: {
    fontSize: 11,
    fontWeight: '700',
    color: '#0066CC',
    letterSpacing: 1.5,
    marginBottom: 4,
  },
  heroTitle: {
    fontSize: 30,
    fontWeight: '700',
    color: '#1D1D1F',
    letterSpacing: -0.6,
  },
  heroSubtitle: {
    fontSize: 13,
    color: 'rgba(0, 0, 0, 0.55)',
    marginTop: 4,
    lineHeight: 18,
  },

  // Status Bar
  statusBarCard: {
    flexDirection: 'row',
    backgroundColor: '#F8F9FB',
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.08)',
    borderRadius: Radii.squircleSmall,
    padding: 14,
    marginBottom: 22,
    alignItems: 'center',
  },
  statusItem: {
    flex: 1,
  },
  statusLabel: {
    fontSize: 9,
    fontWeight: '700',
    color: 'rgba(0, 0, 0, 0.45)',
    letterSpacing: 0.6,
    marginBottom: 4,
  },
  statusContentRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    flexWrap: 'wrap',
  },
  statusVehicleText: {
    fontSize: 12,
    fontWeight: '700',
    color: '#1D1D1F',
    flex: 1,
  },
  statusDivider: {
    width: 1,
    height: 38,
    backgroundColor: 'rgba(0, 0, 0, 0.08)',
    marginHorizontal: 12,
  },
  statusPaymentText: {
    fontSize: 12,
    fontWeight: '700',
    color: '#1D1D1F',
  },
  statusPaymentSub: {
    fontSize: 10,
    color: '#248A3D',
    fontWeight: '600',
    marginTop: 2,
  },

  // Section Header
  sectionHeaderRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 12,
  },
  sectionTitle: {
    fontSize: 11,
    fontWeight: '700',
    color: 'rgba(0, 0, 0, 0.65)',
    letterSpacing: 0.8,
  },
  sectionSub: {
    fontSize: 11,
    color: 'rgba(0, 0, 0, 0.4)',
  },

  // Navigation Cards
  cardsContainer: {
    gap: 12,
    marginBottom: 20,
  },
  navCardTouchable: {
    borderRadius: Radii.squircleMedium,
  },
  navCard: {
    backgroundColor: '#FFFFFF',
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.08)',
    padding: 16,
    borderRadius: Radii.squircleMedium,
    shadowColor: '#000000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.04,
    shadowRadius: 8,
    elevation: 2,
  },
  cardHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 10,
  },
  cardIconBox: {
    width: 34,
    height: 34,
    borderRadius: 10,
    backgroundColor: '#F0F6FF',
    alignItems: 'center',
    justifyContent: 'center',
  },
  cardIconText: {
    fontSize: 16,
    color: '#0066CC',
    fontWeight: '700',
  },
  badgePill: {
    backgroundColor: 'rgba(0, 0, 0, 0.05)',
    borderRadius: Radii.pill,
    paddingHorizontal: 8,
    paddingVertical: 3,
  },
  badgePillText: {
    color: 'rgba(0, 0, 0, 0.65)',
    fontSize: 9,
    fontWeight: '700',
    letterSpacing: 0.5,
  },
  cardTitle: {
    fontSize: 16,
    fontWeight: '700',
    color: '#1D1D1F',
    marginBottom: 2,
  },
  cardSubtitle: {
    fontSize: 12,
    fontWeight: '600',
    color: '#0066CC',
    marginBottom: 6,
  },
  cardDesc: {
    fontSize: 12,
    color: 'rgba(0, 0, 0, 0.6)',
    lineHeight: 17,
    marginBottom: 10,
  },
  cardActionRow: {
    alignSelf: 'flex-start',
    paddingVertical: 2,
  },
  cardActionText: {
    fontSize: 12,
    fontWeight: '700',
    color: '#0066CC',
  },

  footerHelp: {
    textAlign: 'center',
    fontSize: 11,
    color: 'rgba(0, 0, 0, 0.35)',
    marginTop: 10,
  },
});

export default WelcomeScreen;
