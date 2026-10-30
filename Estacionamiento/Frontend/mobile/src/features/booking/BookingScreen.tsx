import React, { useState, useEffect, useRef } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  Modal,
  Animated,
  Easing,
  useWindowDimensions,
} from 'react-native';
import { GlassHeader } from '@/components/ui/GlassHeader';
import { BentoCard } from '@/components/ui/BentoCard';
import { PillButton } from '@/components/ui/PillButton';
import { PlateBadge } from '@/components/ui/PlateBadge';
import { useParkingStore } from '@/store/parkingStore';
import { useVehicleStore, Vehicle } from '@/store/vehicleStore';
import { usePaymentStore, CreditCard } from '@/store/paymentStore';
import { ApplePalette, Radii } from '@/theme/tokens';
import { TabKey } from '@/navigation/TabNavigator';
import {
  IconGate,
  IconPedestrian,
  IconCreditCard,
  IconCheck,
  IconAlertTriangle,
  IconCar,
} from '@/components/ui/Icons';

export type BayStatus = 'LIBRE' | 'OCUPADO';

export interface ParkingSeatBay {
  id: string;
  code: string;       // e.g. "A1", "A2", "B3"
  row: string;        // "A", "B", "C", "D"
  col: number;        // 1 to 5
  status: BayStatus;
  carColor?: string;
  carType?: 'sedan' | 'suv' | 'coupe';
  carPlate?: string;
}

interface ParkingFacility {
  id: 'lot-a' | 'lot-b';
  name: string;
  badge: string;
  description: string;
  totalBays: number;
}

interface DurationOption {
  id: string;
  label: string;
  sublabel: string;
  days: number;
  hours: number;
  price: number;
}

export interface BookingScreenProps {
  onNavigateToTab?: (tab: TabKey) => void;
}

// ---------------------------------------------------------------------------
// Realistic Top-Down Parked Car Component (Pure Native Views)
// ---------------------------------------------------------------------------
interface TopDownCarProps {
  color: string;
  isFacingDown?: boolean;
}

const TopDownCar: React.FC<TopDownCarProps> = ({ color, isFacingDown = true }) => {
  return (
    <View style={[carStyles.container, isFacingDown ? carStyles.facingDown : carStyles.facingUp]}>
      {/* Side Mirrors */}
      <View style={[carStyles.mirrorLeft, { backgroundColor: color }]} />
      <View style={[carStyles.mirrorRight, { backgroundColor: color }]} />

      {/* Main Car Chassis */}
      <View style={[carStyles.chassis, { backgroundColor: color }]}>
        {/* Front Headlights */}
        <View style={carStyles.headlightsRow}>
          <View style={carStyles.headlight} />
          <View style={carStyles.headlight} />
        </View>

        {/* Front Windshield */}
        <View style={carStyles.windshieldFront} />

        {/* Roof / Cabin */}
        <View style={carStyles.roof}>
          <View style={carStyles.sunroof} />
        </View>

        {/* Rear Windshield */}
        <View style={carStyles.windshieldRear} />

        {/* Taillights */}
        <View style={carStyles.taillightsRow}>
          <View style={carStyles.taillight} />
          <View style={carStyles.taillight} />
        </View>
      </View>
    </View>
  );
};

const carStyles = StyleSheet.create({
  container: {
    width: 38,
    height: 56,
    alignItems: 'center',
    justifyContent: 'center',
    position: 'relative',
    marginVertical: 2,
  },
  facingDown: {
    transform: [{ rotate: '180deg' }],
  },
  facingUp: {
    transform: [{ rotate: '0deg' }],
  },
  chassis: {
    width: 32,
    height: 54,
    borderRadius: 7,
    borderTopLeftRadius: 10,
    borderTopRightRadius: 10,
    borderBottomLeftRadius: 8,
    borderBottomRightRadius: 8,
    overflow: 'hidden',
    position: 'relative',
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.2)',
    shadowColor: '#000000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.35,
    shadowRadius: 3,
  },
  mirrorLeft: {
    position: 'absolute',
    left: 0,
    top: 15,
    width: 3.5,
    height: 6,
    borderRadius: 2,
    zIndex: 2,
  },
  mirrorRight: {
    position: 'absolute',
    right: 0,
    top: 15,
    width: 3.5,
    height: 6,
    borderRadius: 2,
    zIndex: 2,
  },
  headlightsRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingHorizontal: 3,
    paddingTop: 1.5,
  },
  headlight: {
    width: 5,
    height: 3,
    backgroundColor: '#FEF08A',
    borderRadius: 1.5,
    opacity: 0.9,
  },
  windshieldFront: {
    marginHorizontal: 4,
    marginTop: 2,
    height: 8,
    backgroundColor: '#1E293B',
    borderRadius: 2,
    borderTopLeftRadius: 4,
    borderTopRightRadius: 4,
    borderWidth: 0.5,
    borderColor: 'rgba(255, 255, 255, 0.2)',
  },
  roof: {
    marginHorizontal: 3.5,
    height: 18,
    backgroundColor: 'rgba(0, 0, 0, 0.22)',
    borderRadius: 2,
    alignItems: 'center',
    justifyContent: 'center',
  },
  sunroof: {
    width: 16,
    height: 10,
    backgroundColor: '#0F172A',
    borderRadius: 2,
    borderWidth: 0.5,
    borderColor: 'rgba(255, 255, 255, 0.15)',
  },
  windshieldRear: {
    marginHorizontal: 4,
    height: 7,
    backgroundColor: '#1E293B',
    borderRadius: 2,
    borderBottomLeftRadius: 4,
    borderBottomRightRadius: 4,
    borderWidth: 0.5,
    borderColor: 'rgba(255, 255, 255, 0.2)',
  },
  taillightsRow: {
    position: 'absolute',
    bottom: 1.5,
    left: 0,
    right: 0,
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingHorizontal: 3,
  },
  taillight: {
    width: 5,
    height: 2.5,
    backgroundColor: '#EF4444',
    borderRadius: 1,
  },
});

// ---------------------------------------------------------------------------
// Main BookingScreen Component
// ---------------------------------------------------------------------------
export const BookingScreen: React.FC<BookingScreenProps> = ({ onNavigateToTab }) => {
  const [selectedLotId, setSelectedLotId] = useState<'lot-a' | 'lot-b'>('lot-a');
  const [selectedBayCode, setSelectedBayCode] = useState<string>('A2');
  const [selectedDurationId, setSelectedDurationId] = useState<string>('1d');

  // Modales
  const [isCheckoutModalOpen, setIsCheckoutModalOpen] = useState(false);
  const [occupiedNoticeBay, setOccupiedNoticeBay] = useState<{
    code: string;
    plate?: string;
  } | null>(null);

  const [successReservation, setSuccessReservation] = useState<{
    bayCode: string;
    lotName: string;
    plate: string;
    vehicleName: string;
    cardBrand: string;
    cardLast4: string;
    duration: string;
    price: number;
    expiration: string;
  } | null>(null);

  const { startMockSession } = useParkingStore();
  const { vehicles, primaryVehicle } = useVehicleStore();
  const { cards } = usePaymentStore();
  const { width } = useWindowDimensions();
  const isDesktop = width >= 768;

  // Selección de vehículo y tarjeta dentro del pop up
  const [selectedVehicleId, setSelectedVehicleId] = useState<string>('');
  const [selectedCardId, setSelectedCardId] = useState<string>('');

  // Sincronizar automáticamente con las predeterminadas cada vez que se abre el modal
  useEffect(() => {
    if (isCheckoutModalOpen) {
      const defaultVeh = primaryVehicle || vehicles.find((v) => v.isPrimary) || vehicles[0];
      const defaultCard = cards.find((c) => c.isDefault) || cards[0];
      if (defaultVeh) setSelectedVehicleId(defaultVeh.id);
      if (defaultCard) setSelectedCardId(defaultCard.id);
    }
  }, [isCheckoutModalOpen, primaryVehicle, vehicles, cards]);

  // Instalaciones
  const facilities: ParkingFacility[] = [
    {
      id: 'lot-a',
      name: 'Estacionamiento A',
      badge: 'PLANTA BAJA',
      description: 'Edificio Principal · Sensores Láser en Vivo',
      totalBays: 20,
    },
    {
      id: 'lot-b',
      name: 'Estacionamiento B',
      badge: 'NIVEL 1 TECHADO',
      description: 'Nivel Superior Express · Capacidad Llena',
      totalBays: 20,
    },
  ];

  const currentFacility = facilities.find((f) => f.id === selectedLotId) || facilities[0];

  // Paleta realista de autos estacionados
  const carColors: Record<string, { color: string; plate: string }> = {
    A1: { color: '#334155', plate: 'JLS-491-K' },
    A3: { color: '#E2E8F0', plate: 'MZN-820-P' },
    A5: { color: '#1E3A8A', plate: 'DFK-334-L' },

    B1: { color: '#18181B', plate: 'PQR-119-M' },
    B2: { color: '#475569', plate: 'TRX-774-A' },
    B3: { color: '#94A3B8', plate: 'VRN-502-B' },
    B4: { color: '#7F1D1D', plate: 'KLE-883-X' },
    B5: { color: '#1E293B', plate: 'WTC-301-S' },

    C1: { color: '#0F172A', plate: 'GTH-902-D' },
    C2: { color: '#3B82F6', plate: 'LMN-411-V' },
    C3: { color: '#D4D4D8', plate: 'QWE-654-H' },
    C4: { color: '#365314', plate: 'ERT-992-P' },
    C5: { color: '#581C87', plate: 'ZXC-701-K' },

    D1: { color: '#1C1917', plate: 'BNM-341-T' },
    D2: { color: '#64748B', plate: 'YUI-882-C' },
    D3: { color: '#DC2626', plate: 'OPL-519-E' },
    D4: { color: '#1F2937', plate: 'HJK-230-R' },
    D5: { color: '#CBD5E1', plate: 'FGH-672-W' },

    // Para cuando se activa Estacionamiento B en A2 y A4
    A2_lotB: { color: '#1E3A5F', plate: 'NMV-912-Q' },
    A4_lotB: { color: '#78350F', plate: 'TXP-404-J' },
  };

  // Construcción de cajones
  const getBaysForLot = (lotId: 'lot-a' | 'lot-b'): ParkingSeatBay[] => {
    const list: ParkingSeatBay[] = [];
    const rows = ['A', 'B', 'C', 'D'];
    const cols = [1, 2, 3, 4, 5];

    rows.forEach((row) => {
      cols.forEach((col) => {
        const code = `${row}${col}`;
        let isLibre = false;
        let car = carColors[code];

        if (lotId === 'lot-a') {
          // A2 y A4 libres en el estacionamiento A
          isLibre = row === 'A' && (col === 2 || col === 4);
        } else {
          // Estacionamiento B completamente ocupado
          isLibre = false;
          if (code === 'A2') car = carColors['A2_lotB'];
          if (code === 'A4') car = carColors['A4_lotB'];
        }

        list.push({
          id: `${lotId}-${code}`,
          code,
          row,
          col,
          status: isLibre ? 'LIBRE' : 'OCUPADO',
          carColor: isLibre ? undefined : car?.color || '#334155',
          carPlate: isLibre ? undefined : car?.plate || 'AER-000-X',
        });
      });
    });

    return list;
  };

  const currentBays = getBaysForLot(selectedLotId);
  const selectedBay = currentBays.find((b) => b.code === selectedBayCode);

  // Opciones de Reserva
  const durationOptions: DurationOption[] = [
    { id: '2h', label: '2 Horas', sublabel: 'Corta Estancia', days: 0, hours: 2, price: 90 },
    { id: '1d', label: '1 Día', sublabel: '24 Horas', days: 1, hours: 24, price: 250 },
    { id: '3d', label: '3 Días', sublabel: 'Fin de Semana', days: 3, hours: 72, price: 650 },
    { id: '1w', label: '1 Semana', sublabel: 'Viaje Corto', days: 7, hours: 168, price: 1400 },
    { id: '1m', label: '1 Mes', sublabel: 'Viajero Frecuente', days: 30, hours: 720, price: 4500 },
    { id: '6m', label: '6 Meses', sublabel: 'Temporada', days: 180, hours: 4320, price: 22000 },
    { id: '1y', label: '1 Año', sublabel: 'Anualidad VIP', days: 365, hours: 8760, price: 38000 },
  ];

  const selectedOption = durationOptions.find((d) => d.id === selectedDurationId) || durationOptions[1];

  // Animación del radar del sensor LED verde en cajones libres
  const pulseAnim = useRef(new Animated.Value(0)).current;

  useEffect(() => {
    Animated.loop(
      Animated.timing(pulseAnim, {
        toValue: 1,
        duration: 1400,
        easing: Easing.out(Easing.ease),
        useNativeDriver: true,
      })
    ).start();
  }, []);

  const pulseScale = pulseAnim.interpolate({
    inputRange: [0, 1],
    outputRange: [1, 2.5],
  });

  const pulseOpacity = pulseAnim.interpolate({
    inputRange: [0, 0.4, 1],
    outputRange: [0.8, 0.3, 0],
  });

  const getExpirationDateString = (option: DurationOption) => {
    const end = new Date(Date.now() + option.hours * 3600 * 1000);
    const months = ['Ene', 'Feb', 'Mar', 'Abr', 'May', 'Jun', 'Jul', 'Ago', 'Sep', 'Oct', 'Nov', 'Dic'];
    return `${end.getDate()} ${months[end.getMonth()]} ${end.getFullYear()}`;
  };

  // Manejo de clic en cajón
  const handleSelectSeatBay = (bay: ParkingSeatBay) => {
    if (bay.status === 'OCUPADO') {
      setOccupiedNoticeBay({ code: bay.code, plate: bay.carPlate });
      return;
    }
    setSelectedBayCode(bay.code);
    setIsCheckoutModalOpen(true);
  };

  // Vehículo y tarjeta elegidos dentro del modal (con fallback a predeterminadas)
  const chosenVehicle =
    vehicles.find((v) => v.id === selectedVehicleId) ||
    primaryVehicle ||
    vehicles[0];

  const chosenCard =
    cards.find((c) => c.id === selectedCardId) ||
    cards.find((c) => c.isDefault) ||
    cards[0];

  // Ejecución de reserva
  const handleConfirmReservation = () => {
    if (!chosenVehicle || !chosenCard) return;

    const plate = chosenVehicle.plate;
    const cardDesc = `${chosenCard.brand} •••• ${chosenCard.last4}`;
    const expiration = getExpirationDateString(selectedOption);

    // 1. Iniciar sesión en el store
    startMockSession(`CAJÓN ${selectedBayCode}`, plate);

    // 2. Registrar cobro
    usePaymentStore.getState().addTransaction({
      concept: `Reserva ${currentFacility.name} · Cajón ${selectedBayCode}`,
      date: 'Hoy · En curso',
      amount: selectedOption.price,
      amountFormatted: `$${selectedOption.price.toLocaleString()}.00 MXN`,
      plate: plate,
      status: 'COBRADO',
      paymentMethod: cardDesc,
      duration: selectedOption.label,
      uuidSat: `sat-${Math.random().toString(36).substring(2, 9)}-cfdi`,
    });

    // 3. Cerrar checkout modal y abrir modal de éxito
    setIsCheckoutModalOpen(false);
    setSuccessReservation({
      bayCode: selectedBayCode,
      lotName: currentFacility.name,
      plate: plate,
      vehicleName: `${chosenVehicle.brand} ${chosenVehicle.model}`,
      cardBrand: chosenCard.brand,
      cardLast4: chosenCard.last4,
      duration: selectedOption.label,
      price: selectedOption.price,
      expiration: expiration,
    });
  };

  // Renderizador de un cajón individual con aspecto realista de estacionamiento
  const renderParkingStall = (bay: ParkingSeatBay, isTopRow: boolean) => {
    const isFree = bay.status === 'LIBRE';
    const isSelected = selectedBayCode === bay.code && isFree;

    return (
      <TouchableOpacity
        key={bay.id}
        onPress={() => handleSelectSeatBay(bay)}
        activeOpacity={isFree ? 0.75 : 0.9}
        style={[
          styles.stallBox,
          isFree ? styles.stallFree : styles.stallOccupied,
          isSelected && styles.stallSelected,
        ]}
      >
        {/* Tope de concreto / goma para llantas (Wheel Stop) */}
        <View
          style={[
            styles.wheelStop,
            isTopRow ? styles.wheelStopTop : styles.wheelStopBottom,
            isSelected && styles.wheelStopSelected,
          ]}
        >
          <View style={styles.wheelStopReflector} />
          <View style={styles.wheelStopReflector} />
        </View>

        {/* Sensor Láser de Techo (Park Assist Indicator LED: Verde o Rojo) */}
        <View
          style={[
            styles.sensorLedContainer,
            isTopRow ? styles.sensorLedAtBottom : styles.sensorLedAtTop,
          ]}
        >
          {isFree && (
            <Animated.View
              style={[
                styles.sensorPulseRing,
                {
                  transform: [{ scale: pulseScale }],
                  opacity: pulseOpacity,
                  backgroundColor: isSelected ? '#0066CC' : '#16A34A',
                },
              ]}
            />
          )}
          <View
            style={[
              styles.sensorLedDot,
              isFree
                ? isSelected
                  ? styles.sensorLedBlue
                  : styles.sensorLedGreen
                : styles.sensorLedRed,
            ]}
          />
        </View>

        {/* Contenido del Cajón: Auto estacionado O Espacio Libre */}
        {isFree ? (
          <View style={styles.freeStallContent}>
            <Text style={[styles.stallCodeTextFree, isSelected && styles.stallCodeTextSelected]}>
              {bay.code}
            </Text>
            <View style={[styles.freeTagBadge, isSelected && styles.freeTagBadgeSelected]}>
              <Text style={[styles.freeTagText, isSelected && styles.freeTagTextSelected]}>
                {isSelected ? 'ELEGIDO' : 'LIBRE'}
              </Text>
            </View>
          </View>
        ) : (
          <View style={styles.occupiedStallContent}>
            {/* Auto visto desde arriba */}
            <TopDownCar color={bay.carColor || '#334155'} isFacingDown={!isTopRow} />
            <Text style={styles.stallCodeTextOccupied}>{bay.code}</Text>
          </View>
        )}
      </TouchableOpacity>
    );
  };

  return (
    <View style={styles.container}>
      <GlassHeader title="Reservar Cajón" showLogo={true} showLogout={true} />

      <ScrollView contentContainerStyle={styles.scrollContent} showsVerticalScrollIndicator={false}>
        {/* Header Principal */}
        <View style={styles.headerBox}>
          <Text style={styles.eyebrow}>PLANO DE ESTACIONAMIENTO EN TIEMPO REAL</Text>
          <Text style={styles.headline}>Elige tu Cajón Disponible</Text>
          <Text style={styles.subtitle}>
            Toca cualquiera de los cajones libres marcados en verde para abrir los detalles y confirmar tu estancia.
          </Text>
        </View>

        {/* Selector de Estacionamiento A / B */}
        <View style={styles.facilitySelectorRow}>
          {facilities.map((fac) => {
            const isSelected = selectedLotId === fac.id;
            return (
              <TouchableOpacity
                key={fac.id}
                onPress={() => {
                  setSelectedLotId(fac.id);
                  if (fac.id === 'lot-a') setSelectedBayCode('A2');
                }}
                style={[
                  styles.facilityTab,
                  isSelected && styles.facilityTabSelected,
                ]}
                activeOpacity={0.7}
              >
                <View style={styles.facilityTopLine}>
                  <Text style={[styles.facilityName, isSelected && styles.facilityNameSelected]}>
                    {fac.name}
                  </Text>
                  <View
                    style={[
                      styles.facilityBadge,
                      isSelected ? styles.facilityBadgeSelected : styles.facilityBadgeDefault,
                    ]}
                  >
                    <Text
                      style={[
                        styles.facilityBadgeText,
                        isSelected ? styles.facilityBadgeTextSelected : styles.facilityBadgeTextDefault,
                      ]}
                    >
                      {fac.badge}
                    </Text>
                  </View>
                </View>
                <Text style={styles.facilitySubtitle}>{fac.description}</Text>
              </TouchableOpacity>
            );
          })}
        </View>

        {/* =========================================================================
            MAPA ARQUITECTÓNICO DE ESTACIONAMIENTO REAL (Asfalto, Carril, Autos)
            ========================================================================= */}
        <View style={styles.parkingLotCanvas}>
          {/* Barra Superior de Estado y Pluma LPR */}
          <View style={styles.garageStatusBar}>
            <View style={styles.gateIndicator}>
              <View style={styles.gateBarrierStripes}>
                <View style={styles.gateArmPill}>
                  <IconGate size={12} color="#94A3B8" />
                  <Text style={styles.gateArmText}>ACCESO VEHICULAR LPR</Text>
                </View>
              </View>
            </View>

            <View style={styles.garageLiveCounts}>
              <View style={styles.liveSensorPill}>
                <View style={styles.greenDot} />
                <Text style={styles.liveSensorText}>
                  {selectedLotId === 'lot-a' ? '2 Cajones Libres' : '0 Libres'}
                </Text>
              </View>
              <View style={styles.liveSensorPill}>
                <View style={styles.redDot} />
                <Text style={styles.liveSensorText}>
                  {selectedLotId === 'lot-a' ? '18 Ocupados' : '20 Ocupados'}
                </Text>
              </View>
            </View>
          </View>

          {/* ----------------------------------------------------
              SECCIÓN 1: PASILLO NORTE (Filas A y B)
              ---------------------------------------------------- */}
          <View style={styles.aisleSection}>
            <View style={styles.aisleHeaderRow}>
              <Text style={styles.aisleHeaderTitle}>SECCIÓN NORTE · BAHÍA 01</Text>
              <Text style={styles.aisleHeaderHint}>Carril de Acceso Inmediato</Text>
            </View>

            {/* Fila A (5 cajones arriba) */}
            <View style={styles.stallsRow}>
              {currentBays
                .filter((b) => b.row === 'A')
                .map((bay) => renderParkingStall(bay, true))}
            </View>

            {/* Carril de Circulación y Maniobra 1 */}
            <View style={styles.drivewayLane}>
              <View style={styles.drivewayDashedLine} />
              <View style={styles.drivewayContent}>
                <Text style={styles.drivewayArrowText}>➔ ➔ ➔</Text>
                <Text style={styles.drivewayLabel}>CARRIL 01 DE CIRCULACIÓN VEHICULAR</Text>
                <Text style={styles.drivewaySpeed}>MÁX 10 KM/H</Text>
                <Text style={styles.drivewayArrowText}>➔ ➔ ➔</Text>
              </View>
              <View style={styles.drivewayDashedLine} />
            </View>

            {/* Fila B (5 cajones abajo) */}
            <View style={styles.stallsRow}>
              {currentBays
                .filter((b) => b.row === 'B')
                .map((bay) => renderParkingStall(bay, false))}
            </View>
          </View>

          {/* ----------------------------------------------------
              SEPARADOR PEATONAL (Paso de Cebra hacia Terminal)
              ---------------------------------------------------- */}
          <View style={styles.crosswalkSeparator}>
            <View style={styles.zebraStripe} />
            <View style={styles.zebraStripe} />
            <View style={styles.zebraStripe} />
            <View style={styles.crosswalkCenterBox}>
              <IconPedestrian size={12} color="#94A3B8" />
              <Text style={styles.crosswalkText}>PASO PEATONAL · ACCESO A ELEVADORES Y TERMINAL</Text>
            </View>
            <View style={styles.zebraStripe} />
            <View style={styles.zebraStripe} />
            <View style={styles.zebraStripe} />
          </View>

          {/* ----------------------------------------------------
              SECCIÓN 2: PASILLO SUR (Filas C y D)
              ---------------------------------------------------- */}
          <View style={styles.aisleSection}>
            <View style={styles.aisleHeaderRow}>
              <Text style={styles.aisleHeaderTitle}>SECCIÓN SUR · BAHÍA 02</Text>
              <Text style={styles.aisleHeaderHint}>Carril de Retorno</Text>
            </View>

            {/* Fila C (5 cajones arriba) */}
            <View style={styles.stallsRow}>
              {currentBays
                .filter((b) => b.row === 'C')
                .map((bay) => renderParkingStall(bay, true))}
            </View>

            {/* Carril de Circulación y Maniobra 2 */}
            <View style={styles.drivewayLane}>
              <View style={styles.drivewayDashedLine} />
              <View style={styles.drivewayContent}>
                <Text style={styles.drivewayArrowText}>➔ ➔ ➔</Text>
                <Text style={styles.drivewayLabel}>CARRIL 02 DE CIRCULACIÓN VEHICULAR</Text>
                <Text style={styles.drivewaySpeed}>MÁX 10 KM/H</Text>
                <Text style={styles.drivewayArrowText}>➔ ➔ ➔</Text>
              </View>
              <View style={styles.drivewayDashedLine} />
            </View>

            {/* Fila D (5 cajones abajo) */}
            <View style={styles.stallsRow}>
              {currentBays
                .filter((b) => b.row === 'D')
                .map((bay) => renderParkingStall(bay, false))}
            </View>
          </View>

          {/* Leyenda Realista de Estacionamiento */}
          <View style={styles.parkingLegend}>
            <View style={styles.legendItem}>
              <View style={styles.legendDotGreen} />
              <Text style={styles.legendLabelText}>Disponible (Toca el cajón)</Text>
            </View>
            <View style={styles.legendItem}>
              <View style={styles.legendDotBlue} />
              <Text style={styles.legendLabelText}>Tu Selección</Text>
            </View>
            <View style={styles.legendItem}>
              <View style={styles.legendDotRed} />
              <Text style={styles.legendLabelText}>Ocupado</Text>
            </View>
          </View>
        </View>
      </ScrollView>

      {/* =========================================================================
          POP-UP MODAL DE RESERVA Y PAGO (Vehículo y Tarjeta con Predeterminadas)
          ========================================================================= */}
      <Modal
        visible={isCheckoutModalOpen}
        transparent
        animationType="slide"
        onRequestClose={() => setIsCheckoutModalOpen(false)}
      >
        <View style={styles.modalOverlay}>
          <View style={[styles.modalCard, isDesktop && styles.modalCardDesktop]}>
            {/* Header del Modal */}
            <View style={styles.modalHeaderRow}>
              <View style={{ flex: 1 }}>
                <Text style={styles.modalEyebrow}>DETALLES DE RESERVA</Text>
                <Text style={styles.modalTitle}>
                  Cajón {selectedBayCode} · {currentFacility.name}
                </Text>
                <Text style={styles.modalSubtitle}>
                  Selecciona la duración, vehículo y método de cobro deseados.
                </Text>
              </View>
              <TouchableOpacity
                onPress={() => setIsCheckoutModalOpen(false)}
                style={styles.modalCloseBtn}
                activeOpacity={0.7}
              >
                <Text style={styles.modalCloseBtnText}>✕</Text>
              </TouchableOpacity>
            </View>

            <ScrollView
              style={styles.modalScroll}
              contentContainerStyle={styles.modalScrollContent}
              showsVerticalScrollIndicator={false}
            >
              {/* 1. SELECCIÓN DE DURACIÓN */}
              <View style={styles.modalSection}>
                <View style={styles.modalSectionHeader}>
                  <Text style={styles.modalSectionLabel}>1. DURACIÓN DE LA RESERVA</Text>
                  <Text style={styles.modalSectionHint}>Vence: {getExpirationDateString(selectedOption)}</Text>
                </View>

                <ScrollView
                  horizontal
                  showsHorizontalScrollIndicator={false}
                  contentContainerStyle={styles.modalDurationScroll}
                >
                  {durationOptions.map((opt) => {
                    const isChosen = selectedDurationId === opt.id;
                    return (
                      <TouchableOpacity
                        key={opt.id}
                        onPress={() => setSelectedDurationId(opt.id)}
                        style={[
                          styles.modalDurationCard,
                          isChosen && styles.modalDurationCardChosen,
                        ]}
                        activeOpacity={0.7}
                      >
                        <Text style={[styles.modalDurationLabel, isChosen && styles.modalDurationLabelChosen]}>
                          {opt.label}
                        </Text>
                        <Text style={[styles.modalDurationPrice, isChosen && styles.modalDurationPriceChosen]}>
                          ${opt.price.toLocaleString()} MXN
                        </Text>
                        <Text style={[styles.modalDurationSub, isChosen && styles.modalDurationSubChosen]}>
                          {opt.sublabel}
                        </Text>
                      </TouchableOpacity>
                    );
                  })}
                </ScrollView>
              </View>

              {/* 2. SELECCIÓN DE VEHÍCULO (Predeterminado por defecto) */}
              <View style={styles.modalSection}>
                <View style={styles.modalSectionHeader}>
                  <Text style={styles.modalSectionLabel}>2. VEHÍCULO PARA ACCESO LPR</Text>
                  <Text style={styles.modalSectionHint}>Predeterminado seleccionado</Text>
                </View>

                <View style={styles.optionsList}>
                  {vehicles.map((veh) => {
                    const isSelected = (chosenVehicle?.id === veh.id);
                    const isPredeterminado = veh.isPrimary || (primaryVehicle?.id === veh.id);

                    return (
                      <TouchableOpacity
                        key={veh.id}
                        onPress={() => setSelectedVehicleId(veh.id)}
                        style={[
                          styles.optionItemCard,
                          isSelected && styles.optionItemCardSelected,
                        ]}
                        activeOpacity={0.7}
                      >
                        {/* Radio Dot */}
                        <View style={[styles.radioCircle, isSelected && styles.radioCircleSelected]}>
                          {isSelected && <View style={styles.radioInnerDot} />}
                        </View>

                        {/* Plate Badge */}
                        <PlateBadge plateNumber={veh.plate} size="normal" />

                        {/* Vehicle details */}
                        <View style={{ flex: 1 }}>
                          <View style={styles.optionTitleRow}>
                            <Text style={[styles.optionTitleText, isSelected && styles.optionTitleTextActive]}>
                              {veh.brand} {veh.model}
                            </Text>
                            {isPredeterminado && (
                              <View style={styles.defaultTagBadge}>
                                <Text style={styles.defaultTagBadgeText}>PREDETERMINADO</Text>
                              </View>
                            )}
                          </View>
                          <Text style={styles.optionSubtitleText}>
                            {veh.alias} · {veh.color}
                          </Text>
                        </View>

                        {/* Selected Indicator */}
                        {isSelected && (
                          <View style={styles.checkedBadge}>
                            <IconCheck size={11} color="#FFFFFF" strokeWidth={3} />
                          </View>
                        )}
                      </TouchableOpacity>
                    );
                  })}
                </View>

                <Text style={styles.plateNoteModal}>
                  La cámara en carril leerá la placa <Text style={styles.boldText}>{chosenVehicle?.plate}</Text> y levantará la pluma automáticamente.
                </Text>
              </View>

              {/* 3. SELECCIÓN DE TARJETA DE PAGO (Predeterminada por defecto) */}
              <View style={styles.modalSection}>
                <View style={styles.modalSectionHeader}>
                  <Text style={styles.modalSectionLabel}>3. TARJETA DE COBRO (WALLET)</Text>
                  <Text style={styles.modalSectionHint}>Predeterminada seleccionada</Text>
                </View>

                <View style={styles.optionsList}>
                  {cards.map((card) => {
                    const isSelected = (chosenCard?.id === card.id);
                    const isPredeterminada = card.isDefault;

                    return (
                      <TouchableOpacity
                        key={card.id}
                        onPress={() => setSelectedCardId(card.id)}
                        style={[
                          styles.optionItemCard,
                          isSelected && styles.optionItemCardSelected,
                        ]}
                        activeOpacity={0.7}
                      >
                        {/* Radio Dot */}
                        <View style={[styles.radioCircle, isSelected && styles.radioCircleSelected]}>
                          {isSelected && <View style={styles.radioInnerDot} />}
                        </View>

                        {/* Card Icon */}
                        <View style={styles.cardBrandIconBox}>
                          <IconCreditCard size={18} color="#0066CC" />
                        </View>

                        {/* Card details */}
                        <View style={{ flex: 1 }}>
                          <View style={styles.optionTitleRow}>
                            <Text style={[styles.optionTitleText, isSelected && styles.optionTitleTextActive]}>
                              {card.brand} •••• {card.last4}
                            </Text>
                            {isPredeterminada && (
                              <View style={styles.defaultTagBadge}>
                                <Text style={styles.defaultTagBadgeText}>PREDETERMINADA</Text>
                              </View>
                            )}
                          </View>
                          <Text style={styles.optionSubtitleText}>
                            Vence: {card.expiry} · Titular: {card.holderName}
                          </Text>
                        </View>

                        {/* Selected Indicator */}
                        {isSelected && (
                          <View style={styles.checkedBadge}>
                            <IconCheck size={11} color="#FFFFFF" strokeWidth={3} />
                          </View>
                        )}
                      </TouchableOpacity>
                    );
                  })}
                </View>
              </View>

              {/* Resumen de Total */}
              <View style={styles.checkoutBreakdownBox}>
                <View style={styles.breakdownRow}>
                  <Text style={styles.breakdownLabel}>Cajón Asignado</Text>
                  <Text style={styles.breakdownValue}>
                    {currentFacility.name} · Cajón {selectedBayCode}
                  </Text>
                </View>
                <View style={styles.breakdownRow}>
                  <Text style={styles.breakdownLabel}>Periodo</Text>
                  <Text style={styles.breakdownValue}>{selectedOption.label}</Text>
                </View>
                <View style={styles.breakdownRow}>
                  <Text style={styles.breakdownLabel}>Vehículo Autorizado</Text>
                  <Text style={styles.breakdownValue}>
                    {chosenVehicle?.plate} ({chosenVehicle?.brand})
                  </Text>
                </View>
                <View style={styles.breakdownRow}>
                  <Text style={styles.breakdownLabel}>Método a Debitar</Text>
                  <Text style={styles.breakdownValue}>
                    {chosenCard?.brand} •••• {chosenCard?.last4}
                  </Text>
                </View>
                <View style={styles.breakdownDivider} />
                <View style={styles.breakdownRow}>
                  <Text style={styles.breakdownTotalLabel}>Total a Pagar (IVA Incluido)</Text>
                  <Text style={styles.breakdownTotalValue}>
                    ${selectedOption.price.toLocaleString()}.00 MXN
                  </Text>
                </View>
              </View>
            </ScrollView>

            {/* Acciones del Modal */}
            <View style={styles.modalFooter}>
              <TouchableOpacity
                onPress={() => setIsCheckoutModalOpen(false)}
                style={styles.modalCancelBtn}
                activeOpacity={0.7}
              >
                <Text style={styles.modalCancelText}>Cancelar</Text>
              </TouchableOpacity>

              <TouchableOpacity
                onPress={handleConfirmReservation}
                style={styles.modalConfirmBtn}
                activeOpacity={0.8}
              >
                <Text style={styles.modalConfirmText}>
                  Confirmar y Pagar ${selectedOption.price.toLocaleString()} MXN
                </Text>
              </TouchableOpacity>
            </View>
          </View>
        </View>
      </Modal>

      {/* =========================================================================
          MODAL DE CONFIRMACIÓN EXITOSA
          ========================================================================= */}
      <Modal
        visible={!!successReservation}
        transparent
        animationType="fade"
        onRequestClose={() => setSuccessReservation(null)}
      >
        <View style={styles.successModalOverlay}>
          <View style={styles.successModalCard}>
            <View style={styles.successIconCircle}>
              <IconCheck size={28} color="#16A34A" />
            </View>

            <Text style={styles.successTitle}>Reserva Confirmada</Text>
            <Text style={styles.successSubtitle}>
              Tu lugar ha sido asignado y el sistema de acceso está sincronizado.
            </Text>

            <View style={styles.successDetailsBox}>
              <View style={styles.successDetailRow}>
                <Text style={styles.successDetailLabel}>Cajón Reservado:</Text>
                <Text style={styles.successDetailHighlight}>
                  {successReservation?.bayCode} ({successReservation?.lotName})
                </Text>
              </View>

              <View style={styles.successDetailRow}>
                <Text style={styles.successDetailLabel}>Placa Habilitada:</Text>
                {successReservation && (
                  <PlateBadge plateNumber={successReservation.plate} size="normal" />
                )}
              </View>

              <View style={styles.successDetailRow}>
                <Text style={styles.successDetailLabel}>Vehículo:</Text>
                <Text style={styles.successDetailValue}>{successReservation?.vehicleName}</Text>
              </View>

              <View style={styles.successDetailRow}>
                <Text style={styles.successDetailLabel}>Tarjeta de Cobro:</Text>
                <Text style={styles.successDetailValue}>
                  {successReservation?.cardBrand} •••• {successReservation?.cardLast4}
                </Text>
              </View>

              <View style={styles.successDetailRow}>
                <Text style={styles.successDetailLabel}>Vigencia:</Text>
                <Text style={styles.successDetailValue}>
                  {successReservation?.duration} (Hasta el {successReservation?.expiration})
                </Text>
              </View>

              <View style={styles.successDetailRow}>
                <Text style={styles.successDetailLabel}>Importe:</Text>
                <Text style={styles.successDetailPrice}>
                  ${successReservation?.price.toLocaleString()}.00 MXN
                </Text>
              </View>
            </View>

            <View style={styles.successLprBanner}>
              <Text style={styles.successLprText}>
                Al llegar al aeropuerto, la cámara LPR reconocerá la placa {successReservation?.plate} y abrirá la pluma automáticamente.
              </Text>
            </View>

            <View style={styles.successActionButtons}>
              {onNavigateToTab && (
                <TouchableOpacity
                  onPress={() => {
                    setSuccessReservation(null);
                    onNavigateToTab('session');
                  }}
                  style={styles.successPrimaryBtn}
                  activeOpacity={0.8}
                >
                  <Text style={styles.successPrimaryBtnText}>Ver Mi Pase de Acceso →</Text>
                </TouchableOpacity>
              )}

              <TouchableOpacity
                onPress={() => setSuccessReservation(null)}
                style={styles.successSecondaryBtn}
                activeOpacity={0.7}
              >
                <Text style={styles.successSecondaryBtnText}>Listo, Volver al Mapa</Text>
              </TouchableOpacity>
            </View>
          </View>
        </View>
      </Modal>

      {/* =========================================================================
          MODAL DE AVISO CAJÓN OCUPADO (Muestra placa del vehículo estacionado)
          ========================================================================= */}
      <Modal
        visible={!!occupiedNoticeBay}
        transparent
        animationType="fade"
        onRequestClose={() => setOccupiedNoticeBay(null)}
      >
        <View style={styles.occupiedModalOverlay}>
          <View style={styles.occupiedModalCard}>
            <View style={styles.occupiedIconCircle}>
              <IconAlertTriangle size={24} color="#DC2626" />
            </View>
            <Text style={styles.occupiedTitle}>Cajón {occupiedNoticeBay?.code} Ocupado</Text>
            <Text style={styles.occupiedMessage}>
              Este cajón se encuentra actualmente en uso por el vehículo con placa{' '}
              <Text style={styles.boldText}>{occupiedNoticeBay?.plate || 'AER-000-X'}</Text>.
              Por favor selecciona uno de los cajones libres marcados en verde (A2 o A4).
            </Text>
            <TouchableOpacity
              onPress={() => setOccupiedNoticeBay(null)}
              style={styles.occupiedBtn}
              activeOpacity={0.8}
            >
              <Text style={styles.occupiedBtnText}>Entendido</Text>
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
    paddingHorizontal: 14,
    paddingTop: 12,
    paddingBottom: 80,
    maxWidth: 680,
    width: '100%',
    alignSelf: 'center',
  },
  headerBox: {
    marginBottom: 12,
  },
  eyebrow: {
    fontSize: 10,
    fontWeight: '700',
    color: '#0066CC',
    letterSpacing: 1.2,
    marginBottom: 2,
  },
  headline: {
    fontSize: 25,
    fontWeight: '700',
    color: '#1D1D1F',
    letterSpacing: -0.5,
  },
  subtitle: {
    fontSize: 12,
    color: 'rgba(0, 0, 0, 0.55)',
    marginTop: 2,
    lineHeight: 16,
  },

  // 2 Estacionamientos Tabs
  facilitySelectorRow: {
    flexDirection: 'row',
    gap: 8,
    marginBottom: 12,
  },
  facilityTab: {
    flex: 1,
    backgroundColor: '#F8F8FA',
    borderWidth: 1.5,
    borderColor: 'rgba(0, 0, 0, 0.08)',
    borderRadius: Radii.squircleSmall,
    padding: 10,
  },
  facilityTabSelected: {
    backgroundColor: '#F0F6FF',
    borderColor: '#0066CC',
    shadowColor: '#0066CC',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 5,
  },
  facilityTopLine: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 3,
  },
  facilityName: {
    fontSize: 13,
    fontWeight: '700',
    color: '#1D1D1F',
  },
  facilityNameSelected: {
    color: '#0066CC',
  },
  facilityBadge: {
    paddingHorizontal: 6,
    paddingVertical: 1,
    borderRadius: Radii.pill,
  },
  facilityBadgeDefault: {
    backgroundColor: 'rgba(0, 0, 0, 0.06)',
  },
  facilityBadgeSelected: {
    backgroundColor: '#0066CC',
  },
  facilityBadgeText: {
    fontSize: 8,
    fontWeight: '700',
    letterSpacing: 0.5,
  },
  facilityBadgeTextDefault: {
    color: 'rgba(0, 0, 0, 0.55)',
  },
  facilityBadgeTextSelected: {
    color: '#FFFFFF',
  },
  facilitySubtitle: {
    fontSize: 10,
    color: 'rgba(0, 0, 0, 0.5)',
  },

  // =========================================================================
  // CANAVAS REALISTA DEL ESTACIONAMIENTO (Asfalto, Carriles, Cajones)
  // =========================================================================
  parkingLotCanvas: {
    backgroundColor: '#0B1120', // Asfalto arquitectónico profundo
    borderRadius: Radii.squircleMedium,
    padding: 12,
    marginBottom: 14,
    borderWidth: 1.5,
    borderColor: '#1E293B',
    shadowColor: '#000000',
    shadowOffset: { width: 0, height: 6 },
    shadowOpacity: 0.2,
    shadowRadius: 12,
  },
  garageStatusBar: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingBottom: 10,
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255, 255, 255, 0.08)',
    marginBottom: 10,
  },
  gateIndicator: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  gateBarrierStripes: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  gateArmPill: {
    backgroundColor: '#070B14',
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.15)',
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: Radii.pill,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  gateArmText: {
    fontSize: 9,
    fontWeight: '800',
    color: '#94A3B8',
    letterSpacing: 0.8,
  },
  garageLiveCounts: {
    flexDirection: 'row',
    gap: 6,
  },
  liveSensorPill: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    backgroundColor: 'rgba(255, 255, 255, 0.06)',
    paddingHorizontal: 7,
    paddingVertical: 3,
    borderRadius: Radii.pill,
  },
  greenDot: {
    width: 6,
    height: 6,
    borderRadius: 3,
    backgroundColor: '#10B981',
  },
  redDot: {
    width: 6,
    height: 6,
    borderRadius: 3,
    backgroundColor: '#EF4444',
  },
  liveSensorText: {
    fontSize: 9,
    fontWeight: '700',
    color: 'rgba(255, 255, 255, 0.8)',
  },

  // Pasillos (Secciones de cajones)
  aisleSection: {
    backgroundColor: '#0F172A',
    borderRadius: 12,
    padding: 8,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.06)',
  },
  aisleHeaderRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingHorizontal: 4,
    marginBottom: 6,
  },
  aisleHeaderTitle: {
    fontSize: 9,
    fontWeight: '800',
    color: 'rgba(255, 255, 255, 0.6)',
    letterSpacing: 0.8,
  },
  aisleHeaderHint: {
    fontSize: 8,
    color: '#38BDF8',
    fontWeight: '700',
  },

  // Fila de 5 cajones
  stallsRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    gap: 5,
  },

  // Cajón de estacionamiento (Stall)
  stallBox: {
    flex: 1,
    height: 98,
    borderRadius: 6,
    borderLeftWidth: 1.5,
    borderRightWidth: 1.5,
    borderColor: 'rgba(255, 255, 255, 0.16)', // Líneas arquitectónicas sobrias
    alignItems: 'center',
    justifyContent: 'center',
    position: 'relative',
    overflow: 'hidden',
  },
  stallOccupied: {
    backgroundColor: '#162032',
    borderTopWidth: 1,
    borderBottomWidth: 1,
    borderTopColor: 'rgba(255, 255, 255, 0.06)',
    borderBottomColor: 'rgba(255, 255, 255, 0.06)',
  },
  stallFree: {
    backgroundColor: '#064E3B', // Muted dark emerald
    borderColor: '#10B981',
    borderWidth: 2,
    shadowColor: '#10B981',
    shadowOffset: { width: 0, height: 0 },
    shadowOpacity: 0.3,
    shadowRadius: 5,
  },
  stallSelected: {
    backgroundColor: '#1E3A8A', // Deep navy
    borderColor: '#0066CC',
    borderWidth: 2.5,
    shadowColor: '#0066CC',
    shadowOffset: { width: 0, height: 0 },
    shadowOpacity: 0.5,
    shadowRadius: 6,
  },

  // Topes de llanta (Wheel Stop de concreto / caucho)
  wheelStop: {
    position: 'absolute',
    width: '68%',
    height: 4,
    backgroundColor: '#4B5563',
    borderRadius: 2,
    flexDirection: 'row',
    justifyContent: 'space-around',
    alignItems: 'center',
    zIndex: 2,
  },
  wheelStopTop: {
    top: 5,
  },
  wheelStopBottom: {
    bottom: 5,
  },
  wheelStopSelected: {
    backgroundColor: '#0066CC',
  },
  wheelStopReflector: {
    width: 6,
    height: 2,
    backgroundColor: '#F59E0B',
    borderRadius: 1,
  },

  // Sensor LED de Techo (Park Assist)
  sensorLedContainer: {
    position: 'absolute',
    width: 14,
    height: 14,
    alignItems: 'center',
    justifyContent: 'center',
    zIndex: 3,
  },
  sensorLedAtTop: {
    top: 4,
  },
  sensorLedAtBottom: {
    bottom: 4,
  },
  sensorPulseRing: {
    position: 'absolute',
    width: 14,
    height: 14,
    borderRadius: 7,
  },
  sensorLedDot: {
    width: 6,
    height: 6,
    borderRadius: 3,
  },
  sensorLedGreen: {
    backgroundColor: '#16A34A',
    shadowColor: '#16A34A',
    shadowOffset: { width: 0, height: 0 },
    shadowOpacity: 0.9,
    shadowRadius: 4,
  },
  sensorLedBlue: {
    backgroundColor: '#0066CC',
    shadowColor: '#0066CC',
    shadowOffset: { width: 0, height: 0 },
    shadowOpacity: 0.9,
    shadowRadius: 4,
  },
  sensorLedRed: {
    backgroundColor: '#DC2626',
  },

  // Contenido de Cajón Libre
  freeStallContent: {
    alignItems: 'center',
    justifyContent: 'center',
    gap: 3,
  },
  stallCodeTextFree: {
    fontSize: 16,
    fontWeight: '900',
    color: '#16A34A',
    letterSpacing: -0.5,
  },
  stallCodeTextSelected: {
    color: '#FFFFFF',
  },
  freeTagBadge: {
    backgroundColor: '#16A34A',
    paddingHorizontal: 5,
    paddingVertical: 1.5,
    borderRadius: Radii.pill,
  },
  freeTagBadgeSelected: {
    backgroundColor: '#0066CC',
  },
  freeTagText: {
    fontSize: 8,
    fontWeight: '800',
    color: '#FFFFFF',
    letterSpacing: 0.4,
  },
  freeTagTextSelected: {
    color: '#FFFFFF',
  },

  // Contenido de Cajón Ocupado
  occupiedStallContent: {
    alignItems: 'center',
    justifyContent: 'center',
    height: '100%',
    width: '100%',
  },
  stallCodeTextOccupied: {
    position: 'absolute',
    bottom: 2,
    fontSize: 9,
    fontWeight: '700',
    color: 'rgba(255, 255, 255, 0.35)',
  },

  // Carril Central de Circulación
  drivewayLane: {
    height: 38,
    backgroundColor: '#11141A', // Asfalto puro de tránsito
    marginVertical: 6,
    borderRadius: 4,
    justifyContent: 'space-between',
    paddingVertical: 2,
    borderTopWidth: 1,
    borderBottomWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.08)',
  },
  drivewayDashedLine: {
    height: 1,
    borderStyle: 'dashed',
    borderWidth: 0.8,
    borderColor: '#EAB308', // Línea amarilla vial
    opacity: 0.45,
  },
  drivewayContent: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 6,
  },
  drivewayArrowText: {
    color: '#EAB308',
    fontSize: 9,
    fontWeight: '900',
    letterSpacing: 1,
  },
  drivewayLabel: {
    color: 'rgba(255, 255, 255, 0.7)',
    fontSize: 8,
    fontWeight: '800',
    letterSpacing: 0.8,
  },
  drivewaySpeed: {
    backgroundColor: '#EF4444',
    color: '#FFFFFF',
    fontSize: 7,
    fontWeight: '800',
    paddingHorizontal: 4,
    paddingVertical: 1,
    borderRadius: 2,
  },

  // Paso Peatonal (Zebra Crosswalk)
  crosswalkSeparator: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    height: 24,
    marginVertical: 8,
    gap: 4,
  },
  zebraStripe: {
    width: 14,
    height: 14,
    backgroundColor: 'rgba(255, 255, 255, 0.2)',
    borderRadius: 1,
  },
  crosswalkCenterBox: {
    backgroundColor: '#11141A',
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: 4,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.12)',
  },
  crosswalkText: {
    fontSize: 8,
    fontWeight: '700',
    color: 'rgba(255, 255, 255, 0.7)',
    letterSpacing: 0.5,
  },

  // Leyenda
  parkingLegend: {
    flexDirection: 'row',
    justifyContent: 'center',
    gap: 16,
    paddingTop: 10,
    marginTop: 8,
    borderTopWidth: 1,
    borderTopColor: 'rgba(255, 255, 255, 0.08)',
  },
  legendItem: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 5,
  },
  legendDotGreen: {
    width: 8,
    height: 8,
    borderRadius: 4,
    backgroundColor: '#16A34A',
  },
  legendDotBlue: {
    width: 8,
    height: 8,
    borderRadius: 4,
    backgroundColor: '#0066CC',
  },
  legendDotRed: {
    width: 8,
    height: 8,
    borderRadius: 4,
    backgroundColor: '#DC2626',
  },
  legendLabelText: {
    fontSize: 10,
    color: 'rgba(255, 255, 255, 0.7)',
    fontWeight: '500',
  },

  // Card de Acción Inmediata
  actionPromptCard: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    backgroundColor: '#F0F6FF',
    borderWidth: 1.5,
    borderColor: '#0066CC',
    borderRadius: Radii.squircleMedium,
    padding: 14,
    gap: 12,
    shadowColor: '#0066CC',
    shadowOffset: { width: 0, height: 3 },
    shadowOpacity: 0.12,
    shadowRadius: 8,
  },
  actionPromptInfo: {
    flex: 1,
  },
  actionPromptBadge: {
    alignSelf: 'flex-start',
    backgroundColor: '#0066CC',
    paddingHorizontal: 7,
    paddingVertical: 2,
    borderRadius: Radii.pill,
    marginBottom: 4,
  },
  actionPromptBadgeText: {
    fontSize: 8,
    fontWeight: '800',
    color: '#FFFFFF',
    letterSpacing: 0.5,
  },
  actionPromptTitle: {
    fontSize: 14,
    fontWeight: '700',
    color: '#1D1D1F',
  },
  actionPromptSub: {
    fontSize: 11,
    color: 'rgba(0, 0, 0, 0.55)',
    marginTop: 2,
    lineHeight: 14,
  },
  actionPromptBtn: {
    backgroundColor: '#0066CC',
    paddingHorizontal: 16,
    paddingVertical: 10,
    borderRadius: Radii.pill,
  },
  actionPromptBtnText: {
    color: '#FFFFFF',
    fontSize: 12,
    fontWeight: '700',
  },

  // ================= MODAL DE CHECKOUT & PAGO =================
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.5)',
    justifyContent: 'flex-end',
  },
  modalCard: {
    backgroundColor: '#FFFFFF',
    borderTopLeftRadius: 24,
    borderTopRightRadius: 24,
    maxHeight: '90%',
    width: '100%',
    display: 'flex',
    flexDirection: 'column',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: -4 },
    shadowOpacity: 0.15,
    shadowRadius: 16,
  },
  modalCardDesktop: {
    maxWidth: 600,
    alignSelf: 'center',
    marginBottom: 40,
    borderRadius: 24,
  },
  modalHeaderRow: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    justifyContent: 'space-between',
    paddingHorizontal: 20,
    paddingTop: 18,
    paddingBottom: 12,
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(0, 0, 0, 0.06)',
  },
  modalEyebrow: {
    fontSize: 10,
    fontWeight: '800',
    color: '#0066CC',
    letterSpacing: 1,
    marginBottom: 2,
  },
  modalTitle: {
    fontSize: 18,
    fontWeight: '800',
    color: '#1D1D1F',
  },
  modalSubtitle: {
    fontSize: 12,
    color: 'rgba(0, 0, 0, 0.5)',
    marginTop: 2,
  },
  modalCloseBtn: {
    width: 32,
    height: 32,
    borderRadius: 16,
    backgroundColor: 'rgba(0, 0, 0, 0.05)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  modalCloseBtnText: {
    fontSize: 14,
    fontWeight: '700',
    color: '#1D1D1F',
  },
  modalScroll: {
    flex: 1,
  },
  modalScrollContent: {
    paddingHorizontal: 20,
    paddingVertical: 14,
    gap: 16,
  },

  modalSection: {
    gap: 8,
  },
  modalSectionHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  modalSectionLabel: {
    fontSize: 10,
    fontWeight: '800',
    color: 'rgba(0, 0, 0, 0.45)',
    letterSpacing: 0.8,
  },
  modalSectionHint: {
    fontSize: 10,
    fontWeight: '600',
    color: '#0066CC',
  },

  // Modal Durations
  modalDurationScroll: {
    gap: 8,
    paddingVertical: 2,
  },
  modalDurationCard: {
    backgroundColor: '#F8F8FA',
    borderWidth: 1.5,
    borderColor: 'rgba(0, 0, 0, 0.08)',
    borderRadius: Radii.squircleSmall,
    paddingVertical: 8,
    paddingHorizontal: 12,
    minWidth: 85,
    alignItems: 'center',
  },
  modalDurationCardChosen: {
    backgroundColor: '#0066CC',
    borderColor: '#0066CC',
  },
  modalDurationLabel: {
    fontSize: 12,
    fontWeight: '700',
    color: '#1D1D1F',
    marginBottom: 1,
  },
  modalDurationLabelChosen: {
    color: '#FFFFFF',
  },
  modalDurationPrice: {
    fontSize: 11,
    fontWeight: '700',
    color: '#0066CC',
    marginBottom: 1,
  },
  modalDurationPriceChosen: {
    color: 'rgba(255, 255, 255, 0.95)',
  },
  modalDurationSub: {
    fontSize: 9,
    color: 'rgba(0, 0, 0, 0.45)',
  },
  modalDurationSubChosen: {
    color: 'rgba(255, 255, 255, 0.75)',
  },

  // Options lists (Vehicles & Cards)
  optionsList: {
    gap: 8,
  },
  optionItemCard: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#F8F8FA',
    borderWidth: 1.5,
    borderColor: 'rgba(0, 0, 0, 0.08)',
    borderRadius: Radii.squircleSmall,
    padding: 10,
    gap: 10,
  },
  optionItemCardSelected: {
    backgroundColor: '#F0F6FF',
    borderColor: '#0066CC',
    shadowColor: '#0066CC',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.08,
    shadowRadius: 4,
  },
  radioCircle: {
    width: 18,
    height: 18,
    borderRadius: 9,
    borderWidth: 2,
    borderColor: 'rgba(0, 0, 0, 0.25)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  radioCircleSelected: {
    borderColor: '#0066CC',
  },
  radioInnerDot: {
    width: 9,
    height: 9,
    borderRadius: 4.5,
    backgroundColor: '#0066CC',
  },
  optionTitleRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    flexWrap: 'wrap',
  },
  optionTitleText: {
    fontSize: 12,
    fontWeight: '700',
    color: '#1D1D1F',
  },
  optionTitleTextActive: {
    color: '#0066CC',
  },
  defaultTagBadge: {
    backgroundColor: 'rgba(22, 163, 74, 0.1)',
    borderWidth: 1,
    borderColor: 'rgba(22, 163, 74, 0.25)',
    paddingHorizontal: 6,
    paddingVertical: 1,
    borderRadius: Radii.pill,
  },
  defaultTagBadgeText: {
    fontSize: 8,
    fontWeight: '800',
    color: '#15803D',
  },
  optionSubtitleText: {
    fontSize: 10,
    color: 'rgba(0, 0, 0, 0.5)',
    marginTop: 2,
  },
  checkedBadge: {
    width: 22,
    height: 22,
    borderRadius: 11,
    backgroundColor: '#0066CC',
    alignItems: 'center',
    justifyContent: 'center',
  },
  checkedBadgeText: {
    color: '#FFFFFF',
    fontSize: 11,
    fontWeight: '800',
  },
  cardBrandIconBox: {
    width: 32,
    height: 32,
    borderRadius: 8,
    backgroundColor: '#FFFFFF',
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.08)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  cardBrandEmoji: {
    fontSize: 16,
  },
  plateNoteModal: {
    fontSize: 10,
    color: 'rgba(0, 0, 0, 0.5)',
    marginTop: 2,
    lineHeight: 14,
  },
  boldText: {
    fontWeight: '700',
    color: '#1D1D1F',
  },

  // Breakdown Box
  checkoutBreakdownBox: {
    backgroundColor: '#F8F9FA',
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.06)',
    borderRadius: Radii.squircleSmall,
    padding: 12,
    gap: 6,
  },
  breakdownRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  breakdownLabel: {
    fontSize: 11,
    color: 'rgba(0, 0, 0, 0.55)',
  },
  breakdownValue: {
    fontSize: 11,
    fontWeight: '600',
    color: '#1D1D1F',
  },
  breakdownDivider: {
    height: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.06)',
    marginVertical: 4,
  },
  breakdownTotalLabel: {
    fontSize: 13,
    fontWeight: '700',
    color: '#1D1D1F',
  },
  breakdownTotalValue: {
    fontSize: 15,
    fontWeight: '800',
    color: '#0066CC',
  },

  // Modal Footer Actions
  modalFooter: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    paddingHorizontal: 20,
    paddingVertical: 14,
    borderTopWidth: 1,
    borderTopColor: 'rgba(0, 0, 0, 0.06)',
    backgroundColor: '#FFFFFF',
    borderBottomLeftRadius: 24,
    borderBottomRightRadius: 24,
  },
  modalCancelBtn: {
    paddingVertical: 12,
    paddingHorizontal: 16,
    borderRadius: Radii.pill,
    backgroundColor: '#F5F5F7',
  },
  modalCancelText: {
    fontSize: 13,
    fontWeight: '600',
    color: 'rgba(0, 0, 0, 0.6)',
  },
  modalConfirmBtn: {
    flex: 1,
    backgroundColor: '#0066CC',
    paddingVertical: 13,
    borderRadius: Radii.pill,
    alignItems: 'center',
    shadowColor: '#0066CC',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.25,
    shadowRadius: 6,
  },
  modalConfirmText: {
    color: '#FFFFFF',
    fontSize: 13,
    fontWeight: '700',
  },

  // ================= MODAL DE ÉXITO =================
  successModalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.55)',
    justifyContent: 'center',
    alignItems: 'center',
    padding: 20,
  },
  successModalCard: {
    backgroundColor: '#FFFFFF',
    borderRadius: 24,
    padding: 24,
    maxWidth: 440,
    width: '100%',
    alignItems: 'center',
    shadowColor: '#000000',
    shadowOffset: { width: 0, height: 10 },
    shadowOpacity: 0.2,
    shadowRadius: 24,
  },
  successIconCircle: {
    width: 60,
    height: 60,
    borderRadius: 30,
    backgroundColor: '#F0FDF4',
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 14,
  },
  successIconCheck: {
    fontSize: 28,
    fontWeight: '800',
    color: '#16A34A',
  },
  successTitle: {
    fontSize: 20,
    fontWeight: '800',
    color: '#1D1D1F',
    textAlign: 'center',
  },
  successSubtitle: {
    fontSize: 12,
    color: 'rgba(0, 0, 0, 0.55)',
    textAlign: 'center',
    marginTop: 4,
    marginBottom: 16,
  },
  successDetailsBox: {
    width: '100%',
    backgroundColor: '#F8F9FA',
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.06)',
    borderRadius: Radii.squircleSmall,
    padding: 12,
    gap: 8,
    marginBottom: 12,
  },
  successDetailRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  successDetailLabel: {
    fontSize: 11,
    color: 'rgba(0, 0, 0, 0.5)',
  },
  successDetailHighlight: {
    fontSize: 12,
    fontWeight: '800',
    color: '#0066CC',
  },
  successDetailValue: {
    fontSize: 11,
    fontWeight: '600',
    color: '#1D1D1F',
  },
  successDetailPrice: {
    fontSize: 12,
    fontWeight: '800',
    color: '#16A34A',
  },
  successLprBanner: {
    width: '100%',
    backgroundColor: '#F0F6FF',
    borderWidth: 1,
    borderColor: 'rgba(0, 102, 204, 0.2)',
    borderRadius: 10,
    padding: 10,
    marginBottom: 18,
  },
  successLprText: {
    fontSize: 11,
    color: '#0066CC',
    lineHeight: 15,
    textAlign: 'center',
  },
  successActionButtons: {
    width: '100%',
    gap: 8,
  },
  successPrimaryBtn: {
    width: '100%',
    backgroundColor: '#0066CC',
    paddingVertical: 13,
    borderRadius: Radii.pill,
    alignItems: 'center',
  },
  successPrimaryBtnText: {
    color: '#FFFFFF',
    fontSize: 13,
    fontWeight: '700',
  },
  successSecondaryBtn: {
    width: '100%',
    backgroundColor: '#F5F5F7',
    paddingVertical: 12,
    borderRadius: Radii.pill,
    alignItems: 'center',
  },
  successSecondaryBtnText: {
    color: '#1D1D1F',
    fontSize: 12,
    fontWeight: '600',
  },

  // ================= MODAL CAJÓN OCUPADO =================
  occupiedModalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.45)',
    justifyContent: 'center',
    alignItems: 'center',
    padding: 20,
  },
  occupiedModalCard: {
    backgroundColor: '#FFFFFF',
    borderRadius: 20,
    padding: 20,
    maxWidth: 360,
    width: '100%',
    alignItems: 'center',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 8 },
    shadowOpacity: 0.15,
    shadowRadius: 20,
  },
  occupiedIconCircle: {
    width: 48,
    height: 48,
    borderRadius: 24,
    backgroundColor: 'rgba(220, 38, 38, 0.12)',
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 10,
  },
  occupiedIcon: {
    fontSize: 20,
    color: '#DC2626',
  },
  occupiedTitle: {
    fontSize: 16,
    fontWeight: '700',
    color: '#1D1D1F',
    marginBottom: 6,
  },
  occupiedMessage: {
    fontSize: 12,
    color: 'rgba(0, 0, 0, 0.6)',
    textAlign: 'center',
    lineHeight: 16,
    marginBottom: 16,
  },
  occupiedBtn: {
    width: '100%',
    backgroundColor: '#1D1D1F',
    paddingVertical: 11,
    borderRadius: Radii.pill,
    alignItems: 'center',
  },
  occupiedBtnText: {
    color: '#FFFFFF',
    fontSize: 12,
    fontWeight: '700',
  },
});

export default BookingScreen;
