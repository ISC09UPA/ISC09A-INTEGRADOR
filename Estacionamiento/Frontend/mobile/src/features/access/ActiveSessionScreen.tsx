import React, { useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  Alert,
  TouchableOpacity,
  Modal,
} from 'react-native';
import { GlassHeader } from '@/components/ui/GlassHeader';
import { BentoCard } from '@/components/ui/BentoCard';
import { PillButton } from '@/components/ui/PillButton';
import { StatusBadge } from '@/components/ui/StatusBadge';
import { PlateBadge } from '@/components/ui/PlateBadge';
import { HUDTimer } from '@/components/ui/HUDTimer';
import { AeroLogo } from '@/components/brand/AeroLogo';
import { useParkingStore } from '@/store/parkingStore';
import { useVehicleStore } from '@/store/vehicleStore';
import { usePaymentStore, CreditCard } from '@/store/paymentStore';
import { ApplePalette, Radii } from '@/theme/tokens';
import {
  IconShield,
  IconAlertTriangle,
  IconActivity,
  IconGate,
  IconCheck,
  IconPhone,
  IconMapPin,
  IconCar,
  IconVideo,
} from '@/components/ui/Icons';

export const ActiveSessionScreen: React.FC = () => {
  const {
    activeSession,
    completeCheckout,
    triggerGateOpen,
    triggerImpactSimulation,
    resetImpactStatus,
  } = useParkingStore();

  const { vehicles, primaryVehicle } = useVehicleStore();
  const { cards } = usePaymentStore();
  const defaultCard = cards.find((c: CreditCard) => c.isDefault) || cards[0];

  const [isProcessing, setIsProcessing] = useState(false);
  const [gateLifted, setGateLifted] = useState(false);
  const [checkoutModalVisible, setCheckoutModalVisible] = useState(false);
  const [securityCallModal, setSecurityCallModal] = useState(false);

  // Vehículo asociado
  const linkedVehicle =
    vehicles.find((v) => v.plate === activeSession?.licensePlate) ||
    primaryVehicle ||
    vehicles[0];

  if (!activeSession) {
    return (
      <View style={styles.emptyContainer}>
        <GlassHeader title="Pase de Acceso" showLogo={true} />
        <View style={styles.emptyContent}>
          <AeroLogo size={56} color="rgba(0, 0, 0, 0.15)" />
          <Text style={styles.emptyTitle}>Sin Estancia Activa</Text>
          <Text style={styles.emptySubtitle}>
            Cuando ingreses a cualquier terminal del aeropuerto, tu vehículo será detectado
            automáticamente por el sensor de carril LPR y tu pase se activará aquí.
          </Text>
        </View>
      </View>
    );
  }

  const isImpactDetected = activeSession.vibrationStatus === 'IMPACT_DETECTED';

  const handleApplePayCheckout = async () => {
    setIsProcessing(true);
    const success = await triggerGateOpen();
    setIsProcessing(false);

    if (success) {
      setGateLifted(true);
      setCheckoutModalVisible(true);
    }
  };

  const handleFinishSession = () => {
    setCheckoutModalVisible(false);
    setGateLifted(false);
    completeCheckout();
  };

  return (
    <View style={styles.container}>
      <GlassHeader title="Pase de Estancia Activa" showLogo={true} />

      <ScrollView contentContainerStyle={styles.scrollContent} showsVerticalScrollIndicator={false}>
        {/* Status Pill & Lane Info */}
        <View style={styles.metaRow}>
          <StatusBadge
            label={gateLifted ? 'PLUMA ABIERTA' : isImpactDetected ? 'ALERTA DE IMPACTO' : 'ESTANCIA EN CURSO'}
            variant={gateLifted ? 'success' : isImpactDetected ? 'warning' : 'active'}
          />
          <Text style={styles.laneText}>{activeSession.laneName}</Text>
        </View>

        {/* =========================================================================
            CARD 1: CRONÓMETRO HUD & TIEMPO RESTANTE ANTES DE SIGUIENTE CARGO
            ========================================================================= */}
        <BentoCard variant="elevated" highlightBorder={true} style={styles.timerCard}>
          <HUDTimer
            startTime={activeSession.entryTimestamp}
            ratePerHour={activeSession.ratePerHour}
          />

          <View style={styles.divider} />

          {/* Ubicación y Cajón Asignado */}
          <View style={styles.bayGrid}>
            <View style={styles.bayItem}>
              <Text style={styles.bayLabel}>TERMINAL</Text>
              <Text style={styles.bayValue}>{activeSession.terminal}</Text>
            </View>
            <View style={styles.bayItem}>
              <Text style={styles.bayLabel}>NIVEL / ZONA</Text>
              <Text style={styles.bayValue}>
                {activeSession.level} · {activeSession.zone}
              </Text>
            </View>
            <View style={styles.bayItem}>
              <Text style={styles.bayLabel}>CAJÓN ASIGNADO</Text>
              <Text style={[styles.bayValue, styles.highlightValue]}>
                {activeSession.bayNumber}
              </Text>
            </View>
          </View>
        </BentoCard>

        {/* =========================================================================
            CARD 2: MONITOREO DE IMPACTOS Y CHOQUES (SENSORES DE VIBRACIÓN DE BAHÍA)
            ========================================================================= */}
        <BentoCard
          variant="elevated"
          style={[
            styles.securityCard,
            isImpactDetected && styles.securityCardAlert,
          ]}
        >
          <View style={styles.securityHeader}>
            <View style={styles.securityHeaderIconBox}>
              {isImpactDetected ? (
                <IconAlertTriangle size={20} color="#DC2626" />
              ) : (
                <IconShield size={20} color="#0066CC" />
              )}
            </View>
            <View style={{ flex: 1 }}>
              <Text style={styles.securityEyebrow}>SISTEMA DE SEGURIDAD PERIMETRAL</Text>
              <Text style={styles.securityTitle}>
                Monitoreo de Impactos en Bahía
              </Text>
              <Text style={styles.securitySubtitle}>
                Sensores de vibración y colisiones activos en {activeSession.bayNumber}
              </Text>
            </View>
            <View style={[styles.sensorStatusBadge, isImpactDetected ? styles.sensorStatusAlert : styles.sensorStatusOk]}>
              <View style={[styles.statusDot, isImpactDetected ? styles.dotRed : styles.dotGreen]} />
              <Text style={[styles.sensorStatusText, isImpactDetected ? styles.statusTextAlert : styles.statusTextOk]}>
                {isImpactDetected ? 'IMPACTO REGISTRADO' : 'PROTEGIDO'}
              </Text>
            </View>
          </View>

          {/* Estado Normal vs Alerta */}
          {isImpactDetected ? (
            <View style={styles.alertContentBox}>
              <View style={styles.alertTopBar}>
                <IconAlertTriangle size={14} color="#DC2626" />
                <Text style={styles.alertTopBarText}>
                  CONTACTO FÍSICO O VIBRACIÓN REGISTRADA
                </Text>
              </View>
              <Text style={styles.alertDescription}>
                El sensor de vibración de la bahía detectó un contacto de{' '}
                <Text style={styles.boldText}>{activeSession.impactMagnitudeG || 2.14} G</Text> a las{' '}
                <Text style={styles.boldText}>{activeSession.lastImpactTime || '14:22:10'} hrs</Text>. Las cámaras de seguridad han guardado automáticamente el video del incidente.
              </Text>

              <View style={styles.alertActionsRow}>
                <TouchableOpacity
                  onPress={resetImpactStatus}
                  style={styles.alertResolveBtn}
                  activeOpacity={0.7}
                >
                  <Text style={styles.alertResolveBtnText}>Marcar como Revisado</Text>
                </TouchableOpacity>

                <TouchableOpacity
                  onPress={() => setSecurityCallModal(true)}
                  style={styles.alertSecurityBtn}
                  activeOpacity={0.7}
                >
                  <View style={{ flexDirection: 'row', alignItems: 'center', gap: 6 }}>
                    <IconPhone size={13} color="#DC2626" />
                    <Text style={styles.alertSecurityBtnText}>Asistencia de Seguridad</Text>
                  </View>
                </TouchableOpacity>
              </View>
            </View>
          ) : (
            <View style={styles.secureContentBox}>
              <Text style={styles.secureDescription}>
                Tu vehículo está protegido. Si otro automóvil roza, golpea o genera una vibración anómala en tu cajón mientras estás ausente, recibirás una notificación al instante.
              </Text>

              {/* Métricas de Telemetría en Vivo */}
              <View style={styles.telemetryRow}>
                <View style={styles.telemetryItem}>
                  <Text style={styles.telemetryLabel}>LECTURA VIBRACIÓN</Text>
                  <Text style={styles.telemetryValue}>0.02 G (Estable)</Text>
                </View>
                <View style={styles.telemetryItem}>
                  <Text style={styles.telemetryLabel}>SENSOR PERIMETRAL</Text>
                  <Text style={[styles.telemetryValue, { color: '#16A34A' }]}>Calibrado · Activo</Text>
                </View>
                <View style={styles.telemetryItem}>
                  <Text style={styles.telemetryLabel}>VIGILANCIA LPR</Text>
                  <Text style={styles.telemetryValue}>Grabación 24/7</Text>
                </View>
              </View>

              {/* Botón de Prueba para Simular el Sensor de Choque */}
              <TouchableOpacity
                onPress={triggerImpactSimulation}
                style={styles.testSensorBtn}
                activeOpacity={0.7}
              >
                <View style={{ flexDirection: 'row', alignItems: 'center', gap: 6 }}>
                  <IconActivity size={13} color="#0066CC" />
                  <Text style={styles.testSensorBtnText}>
                    Probar Calibración de Sensor
                  </Text>
                </View>
              </TouchableOpacity>
            </View>
          )}

          {/* Vehículo Protegido */}
          <View style={styles.vehicleSummaryRow}>
            <View style={{ flex: 1 }}>
              <Text style={styles.vehicleSmallLabel}>VEHÍCULO PROTEGIDO EN ESTANCIA</Text>
              <Text style={styles.vehicleSmallName}>
                {linkedVehicle?.brand} {linkedVehicle?.model} ({linkedVehicle?.color})
              </Text>
            </View>
            <PlateBadge plateNumber={activeSession.licensePlate} size="normal" />
          </View>
        </BentoCard>


        {/* Express Checkout Action Button */}
        <View style={styles.actionContainer}>
          <PillButton
            label="Abrir Pluma de Salida LPR"
            onPress={handleApplePayCheckout}
            variant="primary"
            size="large"
            loading={isProcessing}
            style={styles.payBtn}
          />
          <Text style={styles.securityNote}>
            Cobro automático debitado a tu tarjeta al detectar la placa en el carril de salida
          </Text>
        </View>
      </ScrollView>

      {/* =========================================================================
          MODAL DE CONFIRMACIÓN DE PAGO Y APERTURA DE PLUMA
          ========================================================================= */}
      <Modal
        visible={checkoutModalVisible}
        transparent
        animationType="fade"
        onRequestClose={handleFinishSession}
      >
        <View style={styles.modalOverlay}>
          <View style={styles.modalCard}>
            <View style={styles.modalIconCircle}>
              <IconCheck size={26} color="#16A34A" />
            </View>

            <Text style={styles.modalTitle}>Pago y Despacho Autorizado</Text>
            <Text style={styles.modalSubtitle}>
              Cobro procesado en tu tarjeta {defaultCard?.brand || 'VISA'} terminada en {defaultCard?.last4 || '4821'}.
            </Text>

            <View style={styles.gateOpenNoticeBox}>
              <View style={{ flexDirection: 'row', alignItems: 'center', gap: 6, marginBottom: 4 }}>
                <IconGate size={16} color="#0066CC" />
                <Text style={{ fontSize: 11, fontWeight: '800', color: '#0066CC', letterSpacing: 0.5 }}>
                  CARRIL HABILITADO
                </Text>
              </View>
              <Text style={styles.gateOpenNoticeText}>
                La pluma del {activeSession.laneName} se ha levantado. Cuentas con 15 minutos de cortesía para cruzar la salida.
              </Text>
            </View>

            <TouchableOpacity
              onPress={handleFinishSession}
              style={styles.modalPrimaryBtn}
              activeOpacity={0.8}
            >
              <Text style={styles.modalPrimaryBtnText}>Finalizar Estancia y Salir</Text>
            </TouchableOpacity>
          </View>
        </View>
      </Modal>

      {/* =========================================================================
          MODAL DE ASISTENCIA DE SEGURIDAD
          ========================================================================= */}
      <Modal
        visible={securityCallModal}
        transparent
        animationType="fade"
        onRequestClose={() => setSecurityCallModal(false)}
      >
        <View style={styles.modalOverlay}>
          <View style={styles.modalCard}>
            <View style={[styles.modalIconCircle, { backgroundColor: '#F1F5F9' }]}>
              <IconShield size={26} color="#0066CC" />
            </View>

            <Text style={styles.modalTitle}>Seguridad Aeroportuaria</Text>
            <Text style={styles.modalSubtitle}>
              Personal de vigilancia asignado a la {activeSession.terminal} · {activeSession.zone}.
            </Text>

            <View style={styles.securityInfoBox}>
              <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
                <IconMapPin size={14} color="#475569" />
                <Text style={styles.securityInfoRow}>
                  <Text style={styles.boldText}>Ubicación:</Text> {activeSession.terminal}, {activeSession.bayNumber}
                </Text>
              </View>
              <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
                <IconCar size={14} color="#475569" />
                <Text style={styles.securityInfoRow}>
                  <Text style={styles.boldText}>Vehículo:</Text> {linkedVehicle?.brand} {linkedVehicle?.model} ({activeSession.licensePlate})
                </Text>
              </View>
              <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
                <IconVideo size={14} color="#475569" />
                <Text style={styles.securityInfoRow}>
                  <Text style={styles.boldText}>Grabación:</Text> Clip archivado en Servidor Seguro #402
                </Text>
              </View>
            </View>

            <TouchableOpacity
              onPress={() => setSecurityCallModal(false)}
              style={styles.modalPrimaryBtn}
              activeOpacity={0.8}
            >
              <Text style={styles.modalPrimaryBtnText}>Entendido, Cerrar</Text>
            </TouchableOpacity>
          </View>
        </View>
      </Modal>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#FFFFFF',
  },
  scrollContent: {
    paddingHorizontal: 16,
    paddingTop: 12,
    paddingBottom: 80,
    maxWidth: 640,
    width: '100%',
    alignSelf: 'center',
  },
  metaRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 12,
  },
  laneText: {
    color: 'rgba(0, 0, 0, 0.45)',
    fontSize: 11,
    fontWeight: '600',
  },

  timerCard: {
    marginBottom: 14,
  },
  divider: {
    height: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.06)',
    marginVertical: 14,
  },
  bayGrid: {
    flexDirection: 'row',
    justifyContent: 'space-between',
  },
  bayItem: {
    flex: 1,
  },
  bayLabel: {
    color: 'rgba(0, 0, 0, 0.45)',
    fontSize: 9,
    fontWeight: '800',
    letterSpacing: 0.8,
    marginBottom: 3,
  },
  bayValue: {
    color: '#1D1D1F',
    fontSize: 13,
    fontWeight: '700',
  },
  highlightValue: {
    color: '#0066CC',
    fontWeight: '800',
  },

  // Security & Vibration Sensor Card
  securityCard: {
    marginBottom: 14,
    borderWidth: 1.5,
    borderColor: 'rgba(0, 0, 0, 0.08)',
  },
  securityCardAlert: {
    borderColor: '#EF4444',
    backgroundColor: '#FFFBFB',
    shadowColor: '#EF4444',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.15,
    shadowRadius: 10,
  },
  securityHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    marginBottom: 12,
  },
  securityHeaderIconBox: {
    width: 38,
    height: 38,
    borderRadius: 10,
    backgroundColor: '#F5F5F7',
    alignItems: 'center',
    justifyContent: 'center',
  },
  securityHeaderIcon: {
    fontSize: 20,
  },
  securityEyebrow: {
    fontSize: 9,
    fontWeight: '800',
    color: '#0066CC',
    letterSpacing: 0.8,
  },
  securityTitle: {
    fontSize: 14,
    fontWeight: '800',
    color: '#1D1D1F',
  },
  securitySubtitle: {
    fontSize: 10,
    color: 'rgba(0, 0, 0, 0.5)',
    marginTop: 1,
  },
  sensorStatusBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 5,
    paddingHorizontal: 7,
    paddingVertical: 3,
    borderRadius: Radii.pill,
  },
  sensorStatusOk: {
    backgroundColor: 'rgba(52, 199, 89, 0.12)',
    borderWidth: 1,
    borderColor: 'rgba(52, 199, 89, 0.3)',
  },
  sensorStatusAlert: {
    backgroundColor: 'rgba(239, 68, 68, 0.12)',
    borderWidth: 1,
    borderColor: 'rgba(239, 68, 68, 0.3)',
  },
  statusDot: {
    width: 6,
    height: 6,
    borderRadius: 3,
  },
  dotGreen: {
    backgroundColor: '#16A34A',
  },
  dotRed: {
    backgroundColor: '#DC2626',
  },
  sensorStatusText: {
    fontSize: 8,
    fontWeight: '800',
    letterSpacing: 0.4,
  },
  statusTextOk: {
    color: '#248A3D',
  },
  statusTextAlert: {
    color: '#DC2626',
  },

  // Alert State Box
  alertContentBox: {
    backgroundColor: '#FEF2F2',
    borderWidth: 1,
    borderColor: 'rgba(239, 68, 68, 0.25)',
    borderRadius: Radii.squircleSmall,
    padding: 12,
    marginBottom: 12,
  },
  alertTopBar: {
    marginBottom: 4,
  },
  alertTopBarText: {
    fontSize: 11,
    fontWeight: '800',
    color: '#DC2626',
    letterSpacing: 0.5,
  },
  alertDescription: {
    fontSize: 11,
    color: '#7F1D1D',
    lineHeight: 16,
    marginBottom: 10,
  },
  alertActionsRow: {
    flexDirection: 'row',
    gap: 8,
  },
  alertResolveBtn: {
    flex: 1,
    backgroundColor: '#DC2626',
    paddingVertical: 9,
    borderRadius: Radii.pill,
    alignItems: 'center',
  },
  alertResolveBtnText: {
    color: '#FFFFFF',
    fontSize: 11,
    fontWeight: '700',
  },
  alertSecurityBtn: {
    flex: 1,
    backgroundColor: '#FFFFFF',
    borderWidth: 1,
    borderColor: '#DC2626',
    paddingVertical: 9,
    borderRadius: Radii.pill,
    alignItems: 'center',
  },
  alertSecurityBtnText: {
    color: '#DC2626',
    fontSize: 11,
    fontWeight: '700',
  },

  // Normal Secure State Box
  secureContentBox: {
    marginBottom: 12,
  },
  secureDescription: {
    fontSize: 11,
    color: 'rgba(0, 0, 0, 0.6)',
    lineHeight: 15,
    marginBottom: 10,
  },
  telemetryRow: {
    flexDirection: 'row',
    backgroundColor: '#F8F9FA',
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.06)',
    borderRadius: 10,
    padding: 8,
    gap: 8,
    marginBottom: 10,
  },
  telemetryItem: {
    flex: 1,
  },
  telemetryLabel: {
    fontSize: 8,
    fontWeight: '800',
    color: 'rgba(0, 0, 0, 0.45)',
    letterSpacing: 0.5,
  },
  telemetryValue: {
    fontSize: 11,
    fontWeight: '700',
    color: '#1D1D1F',
    marginTop: 1,
  },
  testSensorBtn: {
    alignSelf: 'flex-start',
    backgroundColor: '#F0F6FF',
    borderWidth: 1,
    borderColor: 'rgba(0, 102, 204, 0.25)',
    paddingHorizontal: 10,
    paddingVertical: 6,
    borderRadius: Radii.pill,
  },
  testSensorBtnText: {
    color: '#0066CC',
    fontSize: 10,
    fontWeight: '700',
  },

  // Vehicle info subrow
  vehicleSummaryRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingTop: 10,
    borderTopWidth: 1,
    borderTopColor: 'rgba(0, 0, 0, 0.06)',
  },
  vehicleSmallLabel: {
    fontSize: 8,
    fontWeight: '800',
    color: 'rgba(0, 0, 0, 0.45)',
    letterSpacing: 0.5,
  },
  vehicleSmallName: {
    fontSize: 12,
    fontWeight: '700',
    color: '#1D1D1F',
    marginTop: 1,
  },



  actionContainer: {
    gap: 6,
  },
  payBtn: {
    width: '100%',
  },
  securityNote: {
    color: 'rgba(0, 0, 0, 0.45)',
    fontSize: 10,
    textAlign: 'center',
    marginTop: 2,
  },

  // Modales
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.5)',
    justifyContent: 'center',
    alignItems: 'center',
    padding: 20,
  },
  modalCard: {
    backgroundColor: '#FFFFFF',
    borderRadius: 24,
    padding: 24,
    maxWidth: 400,
    width: '100%',
    alignItems: 'center',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 8 },
    shadowOpacity: 0.2,
    shadowRadius: 20,
  },
  modalIconCircle: {
    width: 56,
    height: 56,
    borderRadius: 28,
    backgroundColor: '#F0FDF4',
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 12,
  },
  modalCheckIcon: {
    fontSize: 26,
    fontWeight: '800',
    color: '#16A34A',
  },
  modalTitle: {
    fontSize: 18,
    fontWeight: '800',
    color: '#1D1D1F',
    textAlign: 'center',
  },
  modalSubtitle: {
    fontSize: 12,
    color: 'rgba(0, 0, 0, 0.55)',
    textAlign: 'center',
    marginTop: 4,
    marginBottom: 14,
  },
  gateOpenNoticeBox: {
    backgroundColor: '#F0F6FF',
    borderWidth: 1,
    borderColor: 'rgba(0, 102, 204, 0.2)',
    borderRadius: 12,
    padding: 12,
    marginBottom: 16,
    width: '100%',
  },
  gateOpenNoticeText: {
    fontSize: 12,
    color: '#0066CC',
    lineHeight: 16,
    textAlign: 'center',
  },
  modalPrimaryBtn: {
    width: '100%',
    backgroundColor: '#0066CC',
    paddingVertical: 12,
    borderRadius: Radii.pill,
    alignItems: 'center',
  },
  modalPrimaryBtnText: {
    color: '#FFFFFF',
    fontSize: 13,
    fontWeight: '700',
  },

  securityInfoBox: {
    backgroundColor: '#F8F9FA',
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.08)',
    borderRadius: 12,
    padding: 12,
    gap: 6,
    width: '100%',
    marginBottom: 16,
  },
  securityInfoRow: {
    fontSize: 11,
    color: '#1D1D1F',
  },
  boldText: {
    fontWeight: '700',
  },

  // Empty state
  emptyContainer: {
    flex: 1,
    backgroundColor: '#FFFFFF',
  },
  emptyContent: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 40,
  },
  emptyTitle: {
    color: '#1D1D1F',
    fontSize: 20,
    fontWeight: '700',
    marginTop: 16,
    marginBottom: 6,
  },
  emptySubtitle: {
    color: 'rgba(0, 0, 0, 0.55)',
    fontSize: 13,
    textAlign: 'center',
    lineHeight: 18,
  },
});

export default ActiveSessionScreen;
