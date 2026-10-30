import React, { useState, useEffect, useRef } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  Alert,
  useWindowDimensions,
  Platform,
  Animated,
} from 'react-native';
import { GlassHeader } from '@/components/ui/GlassHeader';
import { BentoCard } from '@/components/ui/BentoCard';
import { PillButton } from '@/components/ui/PillButton';
import { StatusBadge } from '@/components/ui/StatusBadge';
import { PlateBadge } from '@/components/ui/PlateBadge';
import { useAuthStore } from '@/store/authStore';
import { ApplePalette, Radii } from '@/theme/tokens';
import Svg, { Rect, Circle, Line, Polyline, Polygon, G, Path, Text as SvgText } from 'react-native-svg';
import {
  IconCheck,
  IconGate,
  IconCar,
  IconActivity,
  IconShield,
  IconCamera,
  IconX,
  IconParking,
  IconCreditCard,
  IconClock,
  IconAlertTriangle,
  IconVideo,
  IconPedestrian,
  IconPhone,
  IconBarChart,
  IconDownload,
  IconFileText,
  IconDollarSign,
  IconReceipt,
  IconLock,
  IconUnlock,
  IconSiren,
  IconVolume2,
  IconVolumeX,
} from '@/components/ui/Icons';
import { useEmergencyStore } from '@/store/emergencyStore';
import { EmergencyScreen } from '@/features/emergency/EmergencyScreen';

type AdminTab = 'bays' | 'barrier' | 'finance' | 'security' | 'audit' | 'emergency';

interface BayState {
  id: string;
  name: string;
  status: 'occupied' | 'free' | 'ev';
  plate: string;
  vehicle: string;
  sensorDistance: string;
  time: string;
}

const INITIAL_BAYS: BayState[] = [
  {
    id: 'bay-01',
    name: 'Cajón 01',
    status: 'occupied',
    plate: 'ABC-123-D',
    vehicle: 'Tesla Model 3',
    sensorDistance: '18 cm',
    time: '02:45:12',
  },
  {
    id: 'bay-02',
    name: 'Cajón 02',
    status: 'free',
    plate: '—',
    vehicle: 'Despejado',
    sensorDistance: '> 150 cm',
    time: '00:00:00',
  },
  {
    id: 'bay-03',
    name: 'Cajón 03',
    status: 'ev',
    plate: 'JLM-456-E',
    vehicle: 'Audi e-tron GT (EV)',
    sensorDistance: '22 cm',
    time: '01:12:30',
  },
  {
    id: 'bay-04',
    name: 'Cajón 04',
    status: 'occupied',
    plate: 'XYZ-789-A',
    vehicle: 'Porsche Macan',
    sensorDistance: '16 cm',
    time: '04:10:05',
  },
];

interface FinanceTransaction {
  id: string;
  cfdi: string;
  plate: string;
  vehicle: string;
  entryTime: string;
  exitTime: string;
  durationFormatted: string;
  amount: number;
  subtotal: number;
  iva: number;
  fee: number;
  net: number;
  method: 'Apple Pay' | 'Visa' | 'Mastercard' | 'AMEX' | 'Terminal NFC';
  status: 'liquidado' | 'conciliado';
}

const FINANCE_TRANSACTIONS: FinanceTransaction[] = [
  {
    id: 'TX-9842',
    cfdi: 'CFDI-4829-E1',
    plate: 'JLM-456-E',
    vehicle: 'Audi e-tron GT (EV)',
    entryTime: '15:10',
    exitTime: '17:48',
    durationFormatted: '2h 38m',
    amount: 135.0,
    subtotal: 116.38,
    iva: 18.62,
    fee: 4.41,
    net: 130.59,
    method: 'Apple Pay',
    status: 'liquidado',
  },
  {
    id: 'TX-9841',
    cfdi: 'CFDI-4828-A7',
    plate: 'ABC-123-D',
    vehicle: 'Tesla Model 3',
    entryTime: '14:02',
    exitTime: '16:47',
    durationFormatted: '2h 45m',
    amount: 135.0,
    subtotal: 116.38,
    iva: 18.62,
    fee: 4.41,
    net: 130.59,
    method: 'Visa',
    status: 'liquidado',
  },
  {
    id: 'TX-9840',
    cfdi: 'CFDI-4827-B9',
    plate: 'NUE-901-B',
    vehicle: 'BMW i4 M50',
    entryTime: '12:30',
    exitTime: '16:30',
    durationFormatted: '4h 00m',
    amount: 180.0,
    subtotal: 155.17,
    iva: 24.83,
    fee: 5.72,
    net: 174.28,
    method: 'Apple Pay',
    status: 'liquidado',
  },
  {
    id: 'TX-9839',
    cfdi: 'CFDI-4826-K4',
    plate: 'XYZ-789-A',
    vehicle: 'Porsche Macan',
    entryTime: '15:20',
    exitTime: '16:20',
    durationFormatted: '1h 00m',
    amount: 45.0,
    subtotal: 38.79,
    iva: 6.21,
    fee: 1.8,
    net: 43.2,
    method: 'Mastercard',
    status: 'liquidado',
  },
  {
    id: 'TX-9838',
    cfdi: 'CFDI-4825-M2',
    plate: 'MER-331-C',
    vehicle: 'Mercedes C200',
    entryTime: '09:15',
    exitTime: '15:45',
    durationFormatted: '6h 30m',
    amount: 292.5,
    subtotal: 252.16,
    iva: 40.34,
    fee: 8.98,
    net: 283.52,
    method: 'AMEX',
    status: 'liquidado',
  },
  {
    id: 'TX-9837',
    cfdi: 'CFDI-4824-H8',
    plate: 'GHI-554-K',
    vehicle: 'Volvo XC40 EV',
    entryTime: '14:10',
    exitTime: '14:55',
    durationFormatted: '0h 45m',
    amount: 45.0,
    subtotal: 38.79,
    iva: 6.21,
    fee: 1.8,
    net: 43.2,
    method: 'Apple Pay',
    status: 'liquidado',
  },
  {
    id: 'TX-9836',
    cfdi: 'CFDI-4823-P3',
    plate: 'CUP-772-L',
    vehicle: 'Cupra Formentor',
    entryTime: '11:00',
    exitTime: '13:30',
    durationFormatted: '2h 30m',
    amount: 112.5,
    subtotal: 96.98,
    iva: 15.52,
    fee: 3.76,
    net: 108.74,
    method: 'Terminal NFC',
    status: 'liquidado',
  },
  {
    id: 'TX-9835',
    cfdi: 'CFDI-4822-X9',
    plate: 'TYT-808-R',
    vehicle: 'Toyota RAV4 Hybrid',
    entryTime: '08:00',
    exitTime: '16:00',
    durationFormatted: '8h 00m',
    amount: 350.0,
    subtotal: 301.72,
    iva: 48.28,
    fee: 10.65,
    net: 339.35,
    method: 'Visa',
    status: 'liquidado',
  },
];

interface StayDistribution {
  range: string;
  label: string;
  percentage: number;
  count: number;
  avgAmount: string;
  color: string;
  isPeak?: boolean;
}

const STAY_DISTRIBUTIONS: StayDistribution[] = [
  { range: '< 1 hora', label: 'Pick-up / Exprés', percentage: 18, count: 8, avgAmount: '$45.00', color: '#38BDF8' },
  { range: '1 - 2 horas', label: 'Trámites / Vuelo corto', percentage: 34, count: 14, avgAmount: '$78.20', color: '#0066CC', isPeak: true },
  { range: '2 - 4 horas', label: 'Estancia media / Esperas', percentage: 28, count: 11, avgAmount: '$135.00', color: '#2563EB' },
  { range: '4 - 8 horas', label: 'Jornada laboral / Negocios', percentage: 14, count: 6, avgAmount: '$247.50', color: '#4F46E5' },
  { range: '> 8 horas', label: 'Larga estancia / Aeropuerto', percentage: 6, count: 2, avgAmount: '$350.00', color: '#7C3AED' },
];

export const AdminWorkspaceScreen: React.FC = () => {
  const [activeTab, setActiveTab] = useState<AdminTab>('bays');
  const [bays, setBays] = useState<BayState[]>(INITIAL_BAYS);
  const [selectedBayIndex, setSelectedBayIndex] = useState(0);
  const [barrierOpen, setBarrierOpen] = useState(false);
  const [revenue, setRevenue] = useState(180.0);
  const [policeAlertActive, setPoliceAlertActive] = useState(true);
  const [isBackendSynced, setIsBackendSynced] = useState(false);

  // Hook al Store Global de Emergencia (Enclavamiento de plumas, LEDs rojos, alerta autoridades)
  const {
    isEmergencyActive,
    emergencyType,
    barrierLocked,
    redLedsActive,
    authoritiesAlerted,
    triggerEmergency,
  } = useEmergencyStore();

  // Estados avanzados del Centro de Operaciones de Carril y Pluma
  const [selectedLane, setSelectedLane] = useState<'carril-01' | 'carril-02'>('carril-01');
  const [isCarPresent, setIsCarPresent] = useState(true); // Sensor de auto cerca
  const [isLoopActive, setIsLoopActive] = useState(true);
  const [detectedPlate, setDetectedPlate] = useState<string | null>('JLM-456-E');
  const [isSimulatingCycle, setIsSimulatingCycle] = useState(false);
  
  // Asistencia a clientes e Interfón de Caseta
  const [customerAlertActive, setCustomerAlertActive] = useState(false);
  const [savedCaptures, setSavedCaptures] = useState<string[]>([]);
  const [captureFlash, setCaptureFlash] = useState(false);
  const [armAngleVal, setArmAngleVal] = useState(barrierOpen ? -88 : 0);

  // Estados de Liquidaciones y Contabilidad
  const [financePeriod, setFinancePeriod] = useState<'today' | 'week' | 'month'>('today');
  const [financeSearch, setFinanceSearch] = useState('');

  const [barrierLogs, setBarrierLogs] = useState<
    Array<{ id: string; time: string; tag: string; message: string; type: 'info' | 'success' | 'warn' | 'error' }>
  >([
    { id: '1', time: '00:36:12', tag: 'BOOT_OK', message: 'Daemon GPIO Raspberry Pi 5 iniciado. PWM 50Hz listo.', type: 'info' },
    { id: '2', time: '00:36:15', tag: 'LPR_READY', message: 'Sensor LPR 4K UHD calibrado. Red neuronal OCR en línea.', type: 'info' },
    { id: '3', time: '00:36:20', tag: 'GATE_LOCKED', message: 'Servomotor calibrado en 0.0° (Pluma abajo / Carril cerrado).', type: 'warn' },
  ]);

  // Animación continua y fluida de la pluma (0° abajo -> -88° arriba vertical)
  useEffect(() => {
    // Si la emergencia está activa o la barrera enclavada, el ángulo objetivo forzado es 0° (enclavamiento total)
    const target = isEmergencyActive || barrierLocked ? 0 : barrierOpen ? -88 : 0;
    const start = armAngleVal;
    const startTime = Date.now();
    const duration = 480;

    const animInterval = setInterval(() => {
      const elapsed = Date.now() - startTime;
      const progress = Math.min(elapsed / duration, 1);
      const ease = progress < 0.5 ? 2 * progress * progress : -1 + (4 - 2 * progress) * progress;
      const current = start + (target - start) * ease;
      setArmAngleVal(current);
      if (progress >= 1) {
        clearInterval(animInterval);
        setArmAngleVal(target);
      }
    }, 16);

    return () => clearInterval(animInterval);
  }, [barrierOpen, isEmergencyActive, barrierLocked]);

  const { width } = useWindowDimensions();
  const isDesktopLayout = width >= 768;
  const { user } = useAuthStore();

  const selectedBay = bays[selectedBayIndex] || bays[0];

  const addLog = (tag: string, message: string, type: 'info' | 'success' | 'warn' | 'error' = 'info') => {
    const timeStr = new Date().toLocaleTimeString('es-MX', { hour12: false });
    setBarrierLogs((prev) => [
      { id: `${Date.now()}-${Math.random()}`, time: timeStr, tag, message, type },
      ...prev.slice(0, 25),
    ]);
  };

  // Sincronización con backend .NET 10 API
  useEffect(() => {
    const fetchDashboard = async () => {
      try {
        const res = await fetch('http://localhost:5000/api/v1/admin/dashboard');
        if (res.ok) {
          const data = await res.json();
          setIsBackendSynced(true);
          if (data.bays && data.bays.length > 0) {
            setBays(
              data.bays.map((b: any) => ({
                id: b.id,
                name: b.name,
                status: b.status.toLowerCase(),
                plate: b.plate,
                vehicle: b.vehicle,
                sensorDistance: b.sensorDistance,
                time: b.elapsedTime,
              }))
            );
          }
          if (data.todayCashlessRevenue) {
            setRevenue(data.todayCashlessRevenue);
          }
          if (data.barrierStatus) {
            setBarrierOpen(data.barrierStatus === 'OPEN');
          }
        }
      } catch {
        setIsBackendSynced(false);
      }
    };

    fetchDashboard();
    const interval = setInterval(fetchDashboard, 5000);
    return () => clearInterval(interval);
  }, []);

  // Simulación de entrada / salida de cajón
  const handleToggleBay = async () => {
    const bay = selectedBay;
    const isFree = bay.status === 'free';

    if (isBackendSynced) {
      try {
        const res = await fetch(`http://localhost:5000/api/v1/bays/${bay.id}/toggle`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            newStatus: isFree ? 'occupied' : 'free',
            plate: isFree ? 'NUE-901-B' : '—',
            vehicle: isFree ? 'BMW i4 M50' : 'Despejado',
          }),
        });
        if (res.ok) {
          const updated = await res.json();
          setBays((prev) =>
            prev.map((b) =>
              b.id === bay.id
                ? {
                    ...b,
                    status: updated.status.toLowerCase(),
                    plate: updated.plate,
                    vehicle: updated.vehicle,
                    time: updated.elapsedTime,
                    sensorDistance: updated.sensorDistance,
                  }
                : b
            )
          );
          return;
        }
      } catch {
        // Fallback local
      }
    }

    // Modo local autónomo
    setBays((prev) =>
      prev.map((b, idx) =>
        idx === selectedBayIndex
          ? {
              ...b,
              status: isFree ? 'occupied' : 'free',
              plate: isFree ? 'NUE-901-B' : '—',
              vehicle: isFree ? 'BMW i4 M50' : 'Despejado',
              sensorDistance: isFree ? '19 cm' : '> 150 cm',
              time: isFree ? '00:00:02' : '00:00:00',
            }
          : b
      )
    );

    if (!isFree) {
      setRevenue((r) => r + 45.0);
    }
  };

  // Control de Pluma con Enclavamiento de Seguridad
  const handleBarrierToggle = async (command: 'OPEN' | 'CLOSE') => {
    // ENCLAVAMIENTO DE SEGURIDAD: Bloqueo total de subida y cerrado
    if (isEmergencyActive || barrierLocked) {
      Alert.alert(
        '🚨 SISTEMA ENCLAVADO POR EMERGENCIA',
        'El mecanismo de la pluma (tanto subida como cerrado) se encuentra bloqueado por protocolo de Código Rojo.\n\nNo es posible operar los servomotores hasta que el supervisor desactive el estado de emergencia.'
      );
      addLog('INTERLOCK_BLOCK', `Intento de accionar ${command === 'OPEN' ? 'SUBIDA' : 'CIERRE'} interceptado: Sistema enclavado por emergencia.`, 'error');
      return;
    }

    const open = command === 'OPEN';
    setBarrierOpen(open);

    if (open) {
      addLog('PWM_SERVO', `GPIO 18: Pulso 2.0ms (90.0°). Pluma levantada en ${selectedLane === 'carril-01' ? 'Carril 01 (Entrada)' : 'Carril 02 (Salida)'}. Semáforo en VERDE.`, 'success');
    } else {
      addLog('PWM_SERVO', `GPIO 18: Pulso 1.0ms (0.0°). Pluma abajo / bloqueo activo en ${selectedLane === 'carril-01' ? 'Carril 01' : 'Carril 02'}. Semáforo en ROJO.`, 'warn');
    }

    if (isBackendSynced) {
      try {
        await fetch('http://localhost:5000/api/v1/access/barrier', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            laneId: selectedLane,
            command: command,
            reason: 'Acción manual desde consola React Native',
          }),
        });
      } catch {}
    }
  };

  // Alternar presencia de vehículo (Auto cerca o no)
  const handleToggleCarPresence = () => {
    const next = !isCarPresent;
    setIsCarPresent(next);
    setIsLoopActive(next);
    if (next) {
      setDetectedPlate('JLM-456-E');
      addLog('CAR_SENSOR', 'Sensor de Presencia: Automóvil aproximado a caseta (Audi e-tron GT). Lazo HIGH.', 'info');
    } else {
      setDetectedPlate(null);
      addLog('CAR_SENSOR', 'Sensor de Presencia: Carril despejado. Sin automóvil detectado en caseta.', 'info');
    }
  };

  // Botón de Atención a Clientes / Interfón de Caseta (Alerta si es empujado)
  const handleCustomerHelpPress = () => {
    setCustomerAlertActive(true);
    addLog('INTERCOM_ALERT', '🚨 BOTÓN DE INTERFÓN PRESIONADO: Conductor solicita atención a clientes en Carril 01.', 'error');
    Alert.alert(
      '🚨 Alerta de Atención a Clientes',
      'El conductor en Carril 01 ha presionado el botón de ayuda / interfón de la caseta.\n\n¿Deseas atender y silenciar la llamada?',
      [
        {
          text: 'Atender Asistencia',
          onPress: () => {
            setCustomerAlertActive(false);
            addLog('INTERCOM_ATTENDED', 'Llamada de atención a clientes atendida por operador de consola.', 'info');
          },
        },
      ]
    );
  };

  // Guardar Captura Manual de lo que se está visualizando en el streaming de la cámara
  const handleSaveCapture = () => {
    const snapId = `CAP-${new Date().toISOString().slice(0, 10).replace(/-/g, '')}-${Math.floor(1000 + Math.random() * 9000)}.jpg`;
    setCaptureFlash(true);
    setTimeout(() => setCaptureFlash(false), 200);
    setSavedCaptures((prev) => [snapId, ...prev.slice(0, 5)]);
    addLog('CAMERA_SNAPSHOT', `Captura manual de streaming archivada: ${snapId} (${detectedPlate || 'JLM-456-E'}).`, 'success');
    Alert.alert(
      'Captura Manual Guardada',
      `La imagen actual del streaming de cámara ha sido guardada exitosamente como evidencia fotográfica:\n\nArchivo: ${snapId}\nResolución: 3840x2160 UHD\nMatrícula: ${detectedPlate || 'JLM-456-E'}`
    );
  };

  // Simulación Automática Completa de Ciclo LPR
  const handleSimulateLPRCycle = () => {
    if (isEmergencyActive || barrierLocked) {
      Alert.alert(
        '🚨 Maniobra Bloqueada',
        'El ciclo automático del carril está deshabilitado mientras permanezca activo el enclavamiento de emergencia.'
      );
      return;
    }
    if (isSimulatingCycle) return;
    setIsSimulatingCycle(true);

    addLog('LOOP_SENSOR', 'GPIO 17 HIGH: Masa metálica detectada sobre lazo inductivo de carril.', 'info');
    setIsLoopActive(true);
    setDetectedPlate('JLM-456-E');

    // Paso 2: Cámara LPR detecta placa
    setTimeout(() => {
      addLog('LPR_OCR', "Reconocimiento óptico exitoso: Placa 'JLM-456-E' validada contra reserva activa #BK-9021. Confianza 99.4%.", 'success');

      // Paso 3: Elevar Pluma y Semáforo Verde
      setTimeout(() => {
        handleBarrierToggle('OPEN');

        // Paso 4: Vehículo cruza y despeja el lazo
        setTimeout(() => {
          addLog('LOOP_CLEAR', 'GPIO 17 LOW: Vehículo cruzó perímetro. Lazo inductivo despejado.', 'info');
          setIsLoopActive(false);
          setDetectedPlate(null);

          // Paso 5: Cerrar pluma automáticamente
          setTimeout(() => {
            handleBarrierToggle('CLOSE');
            setIsSimulatingCycle(false);
            addLog('CYCLE_END', 'Ciclo de paso automático completado satisfactoriamente. Carril asegurado.', 'info');
          }, 1200);
        }, 2400);
      }, 700);
    }, 1000);
  };

  // Handlers Contables y de Conciliación
  const handleExportCSV = () => {
    Alert.alert(
      '📊 Exportación Contable Completada',
      'El archivo CONCILIACION_AEROPARK_20261022.csv ha sido generado exitosamente:\n\n' +
      '• 41 transacciones conciliadas en el periodo actual\n' +
      '• Desglose fiscal SAT (Subtotal $4,189.66 + IVA $670.34)\n' +
      '• Comisiones de pasarela desglosadas por emisor\n' +
      '• Compatible con CONTPAQi, Aspel COI y SAP Business One.\n\n' +
      '¿Deseas descargar el archivo localmente?',
      [{ text: 'Descargar CSV' }]
    );
  };

  const handleDownloadTaxReport = () => {
    Alert.alert(
      '📄 Reporte Fiscal SAT CFDI 4.0',
      'Paquete fiscal PAQUETE_SAT_CFDI_OCT2026.zip listo:\n\n' +
      '• 41 Comprobantes Fiscales XML timbrados por PAC certificado\n' +
      '• Carátula y representación impresa PDF con código QR y sello SAT\n' +
      '• RFC Emisor: APK230815-XYZ (Régimen 601 General de Ley)\n' +
      '• Total IVA 16% Trasladado: $670.34 MXN',
      [{ text: 'Descargar Paquete Fiscal' }]
    );
  };

  const handleCierreDeCaja = () => {
    Alert.alert(
      '🔒 Arqueo & Cierre Contable de Turno',
      'Resumen de Cierre de Caja (Turno Matutino / Vespertino):\n\n' +
      '• Flujo Bruto Facturado: $4,860.00 MXN\n' +
      '• Base Gravable: $4,189.66 MXN\n' +
      '• IVA 16% Trasladado: $670.34 MXN\n' +
      '• Comisiones Stripe / Terminal (2.9% + $3): -$158.44 MXN\n' +
      '• Depósito Neto a Cuenta Concentradora: $4,701.56 MXN\n' +
      '• Discrepancia en Efectivo: $0.00 MXN (100% Cashless / Cero Faltantes)\n\n' +
      '¿Proceder con la firma digital y corte de terminales bancarias?',
      [
        { text: 'Cancelar', style: 'cancel' },
        {
          text: 'Confirmar Cierre',
          onPress: () => {
            Alert.alert('Cierre Exitoso', 'El arqueo contable ha sido sellado digitalmente y transmitido al ERP corporativo.');
          },
        },
      ]
    );
  };

  const handleViewTransactionDetail = (tx: FinanceTransaction) => {
    Alert.alert(
      `Liquidación Fiscal ${tx.id}`,
      `Vehículo: ${tx.vehicle} (${tx.plate})\n` +
      `Estancia: ${tx.entryTime} → ${tx.exitTime} (${tx.durationFormatted})\n` +
      `Método de Cobro: ${tx.method}\n\n` +
      `Monto Total: $${tx.amount.toFixed(2)} MXN\n` +
      `Subtotal Base: $${tx.subtotal.toFixed(2)} MXN\n` +
      `IVA Trasladado (16%): $${tx.iva.toFixed(2)} MXN\n` +
      `Comisión Bancaria: -$${tx.fee.toFixed(2)} MXN\n` +
      `Neto Liquidado: $${tx.net.toFixed(2)} MXN\n\n` +
      `Folio Fiscal: ${tx.cfdi} (SAT CFDI 4.0 Timbrado PAC)`
    );
  };

  const navItems = [
    { key: 'bays' as AdminTab, label: '4 Cajones IoT', iconComponent: IconParking },
    { key: 'barrier' as AdminTab, label: 'Plumas & Carril', iconComponent: IconGate },
    { key: 'finance' as AdminTab, label: 'Liquidaciones', iconComponent: IconCreditCard },
    { key: 'security' as AdminTab, label: 'Zero Trust', iconComponent: IconShield, badge: policeAlertActive ? 1 : undefined },
    { key: 'audit' as AdminTab, label: 'Auditoría', iconComponent: IconActivity },
    {
      key: 'emergency' as AdminTab,
      label: '🚨 Emergencia',
      iconComponent: IconAlertTriangle,
      badge: isEmergencyActive ? 'CÓDIGO ROJO' : undefined,
    },
  ];

  const freeBays = bays.filter((b) => b.status === 'free').length;

  return (
    <View style={styles.container}>
      {/* Top Bar Header with Built-in Logout Action */}
      <GlassHeader
        title="Mission Control · Centro de Operaciones"
        showLogo={true}
        showLogout={true}
      />

      {/* Barra de Alerta Global de Emergencia Persistente */}
      {isEmergencyActive && (
        <TouchableOpacity
          style={styles.globalEmergencyBar}
          onPress={() => setActiveTab('emergency')}
          activeOpacity={0.85}
        >
          <View style={styles.globalEmergencyInner}>
            <View style={styles.globalEmergencyLeft}>
              <View style={styles.globalEmergencyPulseDot} />
              <Text style={styles.globalEmergencyTitle}>
                🚨 CÓDIGO ROJO ACTIVO · PLUMAS ENCLAVADAS · POLICÍA DESPACHADA · LEDS ROJOS
              </Text>
            </View>
            <View style={styles.globalEmergencyBtnPill}>
              <Text style={styles.globalEmergencyBtnTxt}>VER PANTALLA DE EMERGENCIA →</Text>
            </View>
          </View>
        </TouchableOpacity>
      )}

      <View style={[styles.mainLayout, isDesktopLayout && styles.mainLayoutDesktop]}>
        {/* Dynamic Navigation: macOS Sidebar on Desktop vs iOS Horizontal Pill Bar on Mobile */}
        {isDesktopLayout ? (
          <View style={styles.sidebarDesktop}>
            <Text style={styles.sidebarHeader}>SECCIONES DE ADMINISTRACIÓN</Text>
            <View style={styles.sidebarNav}>
              {navItems.map((item) => {
                const isActive = activeTab === item.key;
                return (
                  <TouchableOpacity
                    key={item.key}
                    style={[styles.navBtn, isActive && styles.navBtnActive]}
                    onPress={() => setActiveTab(item.key)}
                    activeOpacity={0.7}
                  >
                    <View style={styles.navIconBox}>
                      <item.iconComponent size={16} color={isActive ? '#0066CC' : 'rgba(0, 0, 0, 0.55)'} />
                    </View>
                    <Text style={[styles.navLabel, isActive && styles.navLabelActive]}>
                      {item.label}
                    </Text>
                    {item.badge ? (
                      <View style={styles.badgePill}>
                        <Text style={styles.badgePillText}>{item.badge}</Text>
                      </View>
                    ) : null}
                  </TouchableOpacity>
                );
              })}
            </View>

            <View style={styles.sidebarFooter}>
              {/* Botón de Paro de Emergencia Rápido en Barra Lateral */}
              <TouchableOpacity
                style={[
                  styles.sidebarSosBtn,
                  isEmergencyActive ? styles.sidebarSosBtnActive : styles.sidebarSosBtnNormal,
                ]}
                onPress={() => {
                  if (!isEmergencyActive) {
                    triggerEmergency('intrusion');
                  }
                  setActiveTab('emergency');
                }}
                activeOpacity={0.8}
              >
                <IconAlertTriangle size={15} color="#FFFFFF" strokeWidth={2.2} />
                <Text style={styles.sidebarSosBtnTxt}>
                  {isEmergencyActive ? '🚨 EMERGENCIA ACTIVA' : '🚨 PARO DE EMERGENCIA'}
                </Text>
              </TouchableOpacity>

              <View style={styles.statusDotRow}>
                <View
                  style={[
                    styles.statusDot,
                    { backgroundColor: isBackendSynced ? '#16A34A' : '#D97706' },
                  ]}
                />
                <Text style={styles.statusFooterText}>
                  {isBackendSynced ? '.NET 10 Sincronizado' : 'Modo Autónomo Local'}
                </Text>
              </View>
            </View>
          </View>
        ) : (
          <View style={styles.mobileSegmentBar}>
            <ScrollView
              horizontal
              showsHorizontalScrollIndicator={false}
              contentContainerStyle={styles.mobileSegmentScroll}
            >
              {navItems.map((item) => {
                const isActive = activeTab === item.key;
                return (
                  <TouchableOpacity
                    key={item.key}
                    style={[styles.mobilePillBtn, isActive && styles.mobilePillBtnActive]}
                    onPress={() => setActiveTab(item.key)}
                    activeOpacity={0.7}
                  >
                    <item.iconComponent size={14} color={isActive ? '#FFFFFF' : '#1D1D1F'} />
                    <Text
                      style={[
                        styles.mobilePillLabel,
                        isActive && styles.mobilePillLabelActive,
                      ]}
                    >
                      {item.label}
                    </Text>
                    {item.badge ? (
                      <View style={styles.badgePill}>
                        <Text style={styles.badgePillText}>{item.badge}</Text>
                      </View>
                    ) : null}
                  </TouchableOpacity>
                );
              })}
            </ScrollView>
          </View>
        )}

        {/* Content Pane Area */}
        <ScrollView contentContainerStyle={styles.contentScroll} showsVerticalScrollIndicator={false}>
          {/* SECTION 1: 4 CAJONES IOT */}
          {activeTab === 'bays' && (
            <View style={styles.tabPane}>
              {/* Top Summary KPI Cards */}
              <View style={styles.kpiRow}>
                <BentoCard variant="elevated" style={styles.kpiCard}>
                  <Text style={styles.kpiLabel}>CAPACIDAD DE SIMULACIÓN</Text>
                  <Text style={styles.kpiHuge}>{freeBays} libres</Text>
                  <Text style={styles.kpiSub}>de 4 cajones disponibles</Text>
                </BentoCard>

                <BentoCard variant="elevated" style={styles.kpiCard}>
                  <Text style={styles.kpiLabel}>ESTADO DE PLUMA</Text>
                  <Text style={[styles.kpiHuge, barrierOpen ? styles.textGreen : styles.textRed]}>
                    {barrierOpen ? 'ABIERTA' : 'CERRADA'}
                  </Text>
                  <Text style={styles.kpiSub}>Servomotor SG90 PWM</Text>
                </BentoCard>

                <BentoCard variant="elevated" style={styles.kpiCard}>
                  <Text style={styles.kpiLabel}>INGRESOS CASHLESS</Text>
                  <Text style={[styles.kpiHuge, styles.textBlue]}>${revenue.toFixed(2)}</Text>
                  <Text style={styles.kpiSub}>MXN cobrados en línea</Text>
                </BentoCard>
              </View>

              {/* The 4 Bays Grid */}
              <BentoCard variant="elevated" highlightBorder={true} style={styles.baysGridCard}>
                <Text style={styles.sectionTitle}>Digital Twin · 4 Cajones con Sensores</Text>
                <Text style={styles.sectionSub}>
                  Toca cualquier cajón para inspeccionar el sensor HC-SR04 o alternar su estado.
                </Text>

                <View style={styles.baysGrid}>
                  {bays.map((bay, idx) => {
                    const isSelected = selectedBayIndex === idx;
                    return (
                      <TouchableOpacity
                        key={bay.id}
                        style={[
                          styles.baySlotCard,
                          bay.status === 'free' && styles.baySlotFree,
                          bay.status === 'ev' && styles.baySlotEv,
                          isSelected && styles.baySlotSelected,
                        ]}
                        onPress={() => setSelectedBayIndex(idx)}
                        activeOpacity={0.8}
                      >
                        <View style={styles.bayCardHeader}>
                          <Text style={styles.bayCardName}>{bay.name}</Text>
                          <StatusBadge
                            label={bay.status === 'free' ? 'LIBRE' : bay.status === 'ev' ? 'EV' : 'OCUPADO'}
                            variant={bay.status === 'free' ? 'success' : bay.status === 'ev' ? 'active' : 'neutral'}
                          />
                        </View>
                        <Text style={styles.bayCarTitle}>{bay.vehicle}</Text>
                        <PlateBadge plateNumber={bay.status !== 'free' ? bay.plate : 'DISPONIBLE'} />
                        <Text style={styles.baySensorInfo}>Sensor: {bay.sensorDistance}</Text>
                      </TouchableOpacity>
                    );
                  })}
                </View>

                {/* Selected Bay Inspector */}
                <View style={styles.inspectorCard}>
                  <View style={styles.inspRow}>
                    <View>
                      <Text style={styles.inspLabel}>CAJÓN SELECCIONADO</Text>
                      <Text style={styles.inspValue}>{selectedBay.name}</Text>
                    </View>
                    <View>
                      <Text style={styles.inspLabel}>DISTANCIA SENSOR</Text>
                      <Text style={styles.inspValue}>{selectedBay.sensorDistance}</Text>
                    </View>
                    <View>
                      <Text style={styles.inspLabel}>TIEMPO</Text>
                      <Text style={styles.inspValue}>{selectedBay.time}</Text>
                    </View>
                  </View>

                  <PillButton
                    label={selectedBay.status === 'free' ? 'Simular Entrada a Cajón' : 'Liberar Cajón (Salida)'}
                    onPress={handleToggleBay}
                    variant={selectedBay.status === 'free' ? 'primary' : 'glass'}
                    size="normal"
                    style={styles.inspActionBtn}
                  />
                </View>
              </BentoCard>
            </View>
          )}

          {/* SECTION 2: PLUMAS & CARRIL */}
          {activeTab === 'barrier' && (
            <View style={styles.tabPane}>
              {/* Main Lane Control Deck */}
              <BentoCard variant="elevated" style={styles.sectionCard}>
                {/* Header & Lane Selector */}
                <View style={styles.laneHeaderRow}>
                  <View style={{ flex: 1 }}>
                    <View style={styles.laneHeaderTitleRow}>
                      <IconGate size={22} color="#0066CC" strokeWidth={2} />
                      <Text style={styles.sectionTitle}>Accionamiento de Barrera & Control de Carril</Text>
                    </View>
                    <Text style={styles.sectionSub}>
                      Telemetría de Servomotor PWM SG90 · Lazo Inductivo GPIO 17 · LPR 4K UHD
                    </Text>
                  </View>

                  {/* Status Indicator Pill */}
                  <View
                    style={[
                      styles.gateLivePill,
                      isEmergencyActive || barrierLocked
                        ? styles.gateLivePillEmergency
                        : barrierOpen
                        ? styles.gateLivePillOpen
                        : styles.gateLivePillClosed,
                    ]}
                  >
                    <View
                      style={[
                        styles.gateStatusDot,
                        isEmergencyActive || barrierLocked
                          ? styles.gateStatusDotEmergency
                          : barrierOpen
                          ? styles.gateStatusDotOpen
                          : styles.gateStatusDotClosed,
                      ]}
                    />
                    <Text
                      style={[
                        styles.gateLivePillText,
                        isEmergencyActive || barrierLocked
                          ? styles.gateLivePillTextEmergency
                          : barrierOpen
                          ? styles.gateLivePillTextOpen
                          : styles.gateLivePillTextClosed,
                      ]}
                    >
                      {isEmergencyActive || barrierLocked
                        ? '🔒 ENCLAVAMIENTO DE EMERGENCIA (PLUMA BLOQUEADA EN 0.0°)'
                        : barrierOpen
                        ? 'PLUMA LEVANTADA (90° · PASO LIBRE)'
                        : 'PLUMA ABAJO (0° · BLOQUEO ACTIVO)'}
                    </Text>
                  </View>
                </View>

                {/* Lane Selector Tabs */}
                <View style={styles.laneTabsRow}>
                  <TouchableOpacity
                    style={[styles.laneTabBtn, selectedLane === 'carril-01' && styles.laneTabBtnActive]}
                    onPress={() => setSelectedLane('carril-01')}
                    activeOpacity={0.7}
                  >
                    <Text style={[styles.laneTabTxt, selectedLane === 'carril-01' && styles.laneTabTxtActive]}>
                      Carril 01 · Entrada Principal (LPR Automático)
                    </Text>
                  </TouchableOpacity>
                  <TouchableOpacity
                    style={[styles.laneTabBtn, selectedLane === 'carril-02' && styles.laneTabBtnActive]}
                    onPress={() => setSelectedLane('carril-02')}
                    activeOpacity={0.7}
                  >
                    <Text style={[styles.laneTabTxt, selectedLane === 'carril-02' && styles.laneTabTxtActive]}>
                      Carril 02 · Salida Express (Pase Digital)
                    </Text>
                  </TouchableOpacity>
                </View>

                {/* 2.5D Technical SCADA Canvas */}
                <View style={styles.scadaCanvas}>
                  {/* Canvas Top HUD */}
                  <View style={styles.scadaHudBar}>
                    <View style={styles.scadaHudLeft}>
                      <View style={styles.scadaRecDot} />
                      <IconCamera size={13} color="#94A3B8" />
                      <Text style={styles.scadaHudCode}>CAM-01 · 4K UHD AI SENSOR</Text>
                      <Text style={styles.scadaHudLatency}>3840x2160 · 60 FPS · 8ms</Text>
                    </View>

                    <View style={styles.scadaHudBadge}>
                      <Text style={styles.scadaHudBadgeTxt}>OCR AI: YOLOv8-LPR (99.4%)</Text>
                    </View>

                    <View style={styles.scadaHudRight}>
                      <Text style={styles.scadaHudAngle}>
                        PWM GPIO 18: {barrierOpen ? '2.0ms · 90.0°' : '1.0ms · 0.0°'}
                      </Text>
                    </View>
                  </View>

                  {/* SVG HIGH-CONTRAST FRONTAL BARRIER ACCESS SIMULATOR */}
                  <View style={styles.svgSimulatorWrapper}>
                    <Svg width="100%" height={260} viewBox="0 0 640 260">
                      {/* Dark Asphalt Pavement Background */}
                      <Rect x="0" y="0" width="640" height="260" fill="#0B1120" />

                      {/* Roadway Surface */}
                      <Rect x="0" y="170" width="640" height="90" fill="#141E33" />

                      {/* Road Border & Center Dashed Line */}
                      <Line x1="0" y1="170" x2="640" y2="170" stroke="#334155" strokeWidth="2" />
                      <Line x1="0" y1="215" x2="640" y2="215" stroke="rgba(255,255,255,0.2)" strokeWidth="2" strokeDasharray="16, 12" />

                      {/* Yellow Hazard Curb Stripes */}
                      {[...Array(16)].map((_, i) => (
                        <Rect
                          key={`curb-${i}`}
                          x={i * 40}
                          y={252}
                          width={40}
                          height={8}
                          fill={i % 2 === 0 ? '#EAB308' : '#0F172A'}
                        />
                      ))}

                      {/* Inductive Loop Sensor Embedded in Pavement */}
                      <Rect
                        x="150"
                        y="180"
                        width="320"
                        height="65"
                        rx="8"
                        fill={isLoopActive ? 'rgba(56, 189, 248, 0.12)' : 'rgba(234, 179, 8, 0.06)'}
                        stroke={isLoopActive ? '#38BDF8' : 'rgba(234, 179, 8, 0.4)'}
                        strokeWidth="2"
                        strokeDasharray={isLoopActive ? undefined : '6, 4'}
                      />
                      <SvgText
                        x="310"
                        y="222"
                        fill={isLoopActive ? '#38BDF8' : 'rgba(234, 179, 8, 0.8)'}
                        fontSize="9"
                        fontWeight="bold"
                        textAnchor="middle"
                      >
                        {isLoopActive
                          ? '◉ LAZO INDUCTIVO GPIO 17 · MASA METÁLICA HIGH (48.5 μH)'
                          : '○ LAZO INDUCTIVO GPIO 17 · EN ESPERA DE VEHÍCULO'}
                      </SvgText>

                      {/* Floor Guidance Light / Passage Indication */}
                      {barrierOpen ? (
                        <G>
                          {/* Green ground glow when open */}
                          <Rect x="195" y="172" width="135" height="6" fill="#22C55E" opacity={0.85} />
                          <SvgText x="262" y="196" fill="#22C55E" fontSize="10" fontWeight="bold" textAnchor="middle">
                            {'>>> PASO LIBRE AUTORIZADO >>>'}
                          </SvgText>
                        </G>
                      ) : (
                        <G>
                          {/* Red ground barrier beam when closed */}
                          <Line x1="190" y1="172" x2="330" y2="172" stroke="#EF4444" strokeWidth="3" strokeDasharray="4, 4" />
                          <SvgText x="260" y="196" fill="#EF4444" fontSize="10" fontWeight="bold" textAnchor="middle">
                            {'⛔ CARRIL BLOQUEADO · ALTO'}
                          </SvgText>
                        </G>
                      )}

                      {/* Vehicle in Lane (when car is present or loop active) */}
                      {(isCarPresent || isLoopActive) ? (
                        <G>
                          {/* Car body */}
                          <Rect x="360" y="165" width="105" height="42" rx="8" fill="#1E293B" stroke="#38BDF8" strokeWidth="2" />
                          <Rect x="380" y="147" width="65" height="22" rx="4" fill="#0F172A" stroke="#38BDF8" strokeWidth="1.5" />
                          {/* Wheels */}
                          <Circle cx="382" cy="207" r="9" fill="#0F172A" stroke="#64748B" strokeWidth="2" />
                          <Circle cx="445" cy="207" r="9" fill="#0F172A" stroke="#64748B" strokeWidth="2" />
                          {/* Headlights / Brake lights */}
                          <Rect x="358" y="175" width="4" height="12" rx="2" fill={barrierOpen ? '#60A5FA' : '#EF4444'} />
                          {/* OCR Plate Target Box on Vehicle */}
                          <Rect x="375" y="178" width="70" height="18" rx="3" fill="#090D16" stroke="#22C55E" strokeWidth="1.5" />
                          <SvgText x="410" y="191" fill="#22C55E" fontSize="9" fontWeight="bold" textAnchor="middle">
                            {detectedPlate || 'JLM-456-E'}
                          </SvgText>
                        </G>
                      ) : null}

                      {/* Traffic Light Semaphore Fixture */}
                      <G>
                        {/* Semaphore Housing */}
                        <Rect x="120" y="105" width="24" height="60" rx="5" fill="#090D16" stroke="#334155" strokeWidth="1.5" />
                        {/* Red Lamp */}
                        <Circle
                          cx="132"
                          cy="120"
                          r="8"
                          fill={barrierOpen ? '#3F1212' : '#EF4444'}
                          stroke={barrierOpen ? '#551B1B' : '#F87171'}
                          strokeWidth={barrierOpen ? 1 : 2}
                        />
                        {/* Green Lamp */}
                        <Circle
                          cx="132"
                          cy="148"
                          r="8"
                          fill={barrierOpen ? '#22C55E' : '#0D381E'}
                          stroke={barrierOpen ? '#4ADE80' : '#14532D'}
                          strokeWidth={barrierOpen ? 2 : 1}
                        />
                        {/* Pole */}
                        <Line x1="132" y1="165" x2="132" y2="210" stroke="#475569" strokeWidth="3" />
                      </G>

                      {/* Left Barrier Pedestal Tower (Tower Body) */}
                      <G>
                        <Rect x="165" y="100" width="48" height="110" rx="6" fill="#1E293B" stroke="#334155" strokeWidth="2" />
                        {/* Top Accent Stripe (Safety Amber) */}
                        <Rect x="165" y="100" width="48" height="8" rx="2" fill="#EAB308" />
                        {/* Strobe Beacon */}
                        <Circle cx="189" cy="90" r="7" fill={barrierOpen ? '#22C55E' : '#EAB308'} />
                        {/* Angle Gauge Digital Display on Tower */}
                        <Rect x="171" y="168" width="36" height="18" rx="3" fill="#090D16" stroke="#334155" strokeWidth="1" />
                        <SvgText
                          x="189"
                          y="181"
                          fill={barrierOpen ? '#22C55E' : '#EF4444'}
                          fontSize="9"
                          fontWeight="bold"
                          textAnchor="middle"
                        >
                          {barrierOpen ? '90.0°' : '0.0°'}
                        </SvgText>
                        <SvgText x="189" y="120" fill="#94A3B8" fontSize="7" fontWeight="bold" textAnchor="middle">
                          AERO-GATE
                        </SvgText>
                      </G>

                      {/* Right Support Pillar Cradle */}
                      <G>
                        <Rect x="320" y="138" width="16" height="72" rx="3" fill="#1E293B" stroke="#334155" strokeWidth="1.5" />
                        <Rect x="318" y="133" width="20" height="8" rx="2" fill="#475569" />
                        <Circle cx="328" cy="137" r="3" fill={barrierOpen ? '#334155' : '#EF4444'} />
                      </G>

                      {/* ROTATING BOOM ARM GROUP (Pivots at cx=189, cy=145) */}
                      <G transform={`rotate(${armAngleVal}, 189, 145)`}>
                        {/* Boom Arm Core Bar */}
                        <Rect x="189" y="139" width="134" height="12" rx="4" fill="#FFFFFF" stroke="#CBD5E1" strokeWidth="1" />

                        {/* Red Reflective Hazard Chevrons along arm */}
                        <Rect x="205" y="139" width="15" height="12" fill="#DC2626" />
                        <Rect x="235" y="139" width="15" height="12" fill="#DC2626" />
                        <Rect x="265" y="139" width="15" height="12" fill="#DC2626" />
                        <Rect x="295" y="139" width="15" height="12" fill="#DC2626" />

                        {/* Top LED Guide Line (Green when UP, Red when DOWN) */}
                        <Rect
                          x="189"
                          y="139"
                          width="134"
                          height="3"
                          fill={barrierOpen ? '#22C55E' : '#EF4444'}
                        />

                        {/* Arm Tip Bumper */}
                        <Rect x="319" y="139" width="5" height="12" rx="2" fill="#0F172A" />
                      </G>

                      {/* Mechanical Pivot Axle Hub Cap (Drawn on top of the arm base) */}
                      <Circle cx="189" cy="145" r="14" fill="#0F172A" stroke="#38BDF8" strokeWidth="3" />
                      <Circle cx="189" cy="145" r="5" fill="#38BDF8" />
                    </Svg>
                  </View>

                  {/* UNMISTAKABLE LARGE STATUS BANNER AT BASE OF SIMULATOR */}
                  <View
                    style={[
                      styles.simulatorBottomBanner,
                      isEmergencyActive || barrierLocked
                        ? styles.bannerEmergency
                        : barrierOpen
                        ? styles.bannerOpen
                        : styles.bannerClosed,
                    ]}
                  >
                    <View style={styles.bannerLeft}>
                      <View
                        style={[
                          styles.bannerDot,
                          isEmergencyActive || barrierLocked
                            ? styles.bannerDotEmergency
                            : barrierOpen
                            ? styles.bannerDotOpen
                            : styles.bannerDotClosed,
                        ]}
                      />
                      <Text
                        style={[
                          styles.bannerTitle,
                          isEmergencyActive || barrierLocked
                            ? styles.bannerTitleEmergency
                            : barrierOpen
                            ? styles.bannerTitleOpen
                            : styles.bannerTitleClosed,
                        ]}
                      >
                        {isEmergencyActive || barrierLocked
                          ? '🚨 BARRERA ENCLAVADA EN 0° · SUBIDA Y CERRADO BLOQUEADOS POR CÓDIGO ROJO'
                          : barrierOpen
                          ? 'BARRERA ELEVADA A 90° · PASO VEHICULAR LIBRE Y AUTORIZADO'
                          : 'BARRERA CERRADA EN 0° · BLOQUEO VEHICULAR ACTIVO'}
                      </Text>
                    </View>
                    <View style={styles.bannerRight}>
                      <Text
                        style={[
                          styles.bannerBadgeTxt,
                          isEmergencyActive || barrierLocked
                            ? styles.bannerTitleEmergency
                            : barrierOpen
                            ? styles.bannerTitleOpen
                            : styles.bannerTitleClosed,
                        ]}
                      >
                        {isEmergencyActive || barrierLocked
                          ? '🚨 SEMÁFORO: DOBLE DESTELLO ROJO · ALERTA C5'
                          : barrierOpen
                          ? 'SEMÁFORO: VERDE (PWM 2.0ms)'
                          : 'SEMÁFORO: ROJO (PWM 1.0ms)'}
                      </Text>
                    </View>
                  </View>
                </View>

                {/* BOTONES DE ACTIVACIÓN DE BARRERA & ATENCIÓN A CLIENTES (INMEDIATAMENTE DEBAJO DE LA ANIMACIÓN) */}
                <View style={styles.quickBarrierActionsRow}>
                  {/* Botón 1: Levantar Pluma */}
                  <TouchableOpacity
                    style={[
                      styles.quickBarrierBtn,
                      styles.quickBtnOpen,
                      (isEmergencyActive || barrierLocked) && styles.quickBtnEmergencyLock,
                      !isEmergencyActive && !barrierLocked && barrierOpen && styles.quickBtnOpenActive,
                    ]}
                    onPress={() => handleBarrierToggle('OPEN')}
                    activeOpacity={0.8}
                  >
                    {isEmergencyActive || barrierLocked ? (
                      <IconLock size={18} color="#DC2626" />
                    ) : (
                      <IconGate size={18} color={barrierOpen ? '#15803D' : '#047857'} />
                    )}
                    <View style={styles.quickBtnTextCol}>
                      <Text
                        style={[
                          styles.quickBtnTitle,
                          isEmergencyActive || barrierLocked ? styles.quickTitleEmergency : styles.quickTitleOpen,
                        ]}
                      >
                        {isEmergencyActive || barrierLocked ? '🔒 Subida Bloqueada' : '▲ Levantar Pluma (90°)'}
                      </Text>
                      <Text style={styles.quickBtnSub}>
                        {isEmergencyActive || barrierLocked ? 'Enclavado por Emergencia' : 'PWM 2.0ms · Paso Libre'}
                      </Text>
                    </View>
                    {!isEmergencyActive && !barrierLocked && barrierOpen && (
                      <View style={styles.activePillGreen}>
                        <Text style={styles.activePillTxt}>ACTIVO</Text>
                      </View>
                    )}
                    {(isEmergencyActive || barrierLocked) && (
                      <View style={styles.activePillEmergency}>
                        <Text style={styles.activePillTxt}>BLOQUEADO</Text>
                      </View>
                    )}
                  </TouchableOpacity>

                  {/* Botón 2: Bajar Pluma */}
                  <TouchableOpacity
                    style={[
                      styles.quickBarrierBtn,
                      styles.quickBtnClose,
                      (isEmergencyActive || barrierLocked) && styles.quickBtnEmergencyLock,
                      !isEmergencyActive && !barrierLocked && !barrierOpen && styles.quickBtnCloseActive,
                    ]}
                    onPress={() => handleBarrierToggle('CLOSE')}
                    activeOpacity={0.8}
                  >
                    {isEmergencyActive || barrierLocked ? (
                      <IconLock size={18} color="#DC2626" />
                    ) : (
                      <IconX size={18} color={!barrierOpen ? '#B91C1C' : '#991B1B'} />
                    )}
                    <View style={styles.quickBtnTextCol}>
                      <Text
                        style={[
                          styles.quickBtnTitle,
                          isEmergencyActive || barrierLocked ? styles.quickTitleEmergency : styles.quickTitleClose,
                        ]}
                      >
                        {isEmergencyActive || barrierLocked ? '🔒 Cierre Bloqueado' : '▼ Bajar Pluma (0°)'}
                      </Text>
                      <Text style={styles.quickBtnSub}>
                        {isEmergencyActive || barrierLocked ? 'Enclavado por Emergencia' : 'PWM 1.0ms · Bloqueo'}
                      </Text>
                    </View>
                    {!isEmergencyActive && !barrierLocked && !barrierOpen && (
                      <View style={styles.activePillRed}>
                        <Text style={styles.activePillTxt}>ACTIVO</Text>
                      </View>
                    )}
                    {(isEmergencyActive || barrierLocked) && (
                      <View style={styles.activePillEmergency}>
                        <Text style={styles.activePillTxt}>BLOQUEADO</Text>
                      </View>
                    )}
                  </TouchableOpacity>

                  {/* Botón 3: Ciclo Automático */}
                  <TouchableOpacity
                    style={[
                      styles.quickBarrierBtn,
                      styles.quickBtnCycle,
                      isSimulatingCycle && styles.quickBtnCycleActive,
                    ]}
                    onPress={handleSimulateLPRCycle}
                    disabled={isSimulatingCycle}
                    activeOpacity={0.8}
                  >
                    <IconCar size={18} color="#0066CC" />
                    <View style={styles.quickBtnTextCol}>
                      <Text style={[styles.quickBtnTitle, styles.quickTitleCycle]}>
                        {isSimulatingCycle ? 'Procesando...' : '⟳ Ciclo Automático'}
                      </Text>
                      <Text style={styles.quickBtnSub}>Lazo → LPR → Paso</Text>
                    </View>
                  </TouchableOpacity>

                  {/* Botón 4: Atención a Clientes / Interfón de Caseta */}
                  <TouchableOpacity
                    style={[
                      styles.quickBarrierBtn,
                      styles.quickBtnHelp,
                      customerAlertActive && styles.quickBtnHelpActive,
                    ]}
                    onPress={handleCustomerHelpPress}
                    activeOpacity={0.8}
                  >
                    <IconPhone size={18} color={customerAlertActive ? '#FFFFFF' : '#DC2626'} />
                    <View style={styles.quickBtnTextCol}>
                      <Text
                        style={[
                          styles.quickBtnTitle,
                          customerAlertActive ? styles.quickTitleHelpAlert : styles.quickTitleHelp,
                        ]}
                      >
                        🚨 Atención a Clientes
                      </Text>
                      <Text
                        style={[
                          styles.quickBtnSub,
                          customerAlertActive && { color: '#FEE2E2' },
                        ]}
                      >
                        {customerAlertActive ? '¡Alerta Emitida!' : 'Interfón de Caseta'}
                      </Text>
                    </View>
                    {customerAlertActive && (
                      <View style={styles.activePillEmergency}>
                        <Text style={styles.activePillEmergencyTxt}>SOS</Text>
                      </View>
                    )}
                  </TouchableOpacity>
                </View>

                {/* BANNER DE ALERTA DE ATENCIÓN A CLIENTES (SI FUE PRESIONADO) */}
                {customerAlertActive && (
                  <View style={styles.customerAlertBanner}>
                    <View style={styles.customerAlertLeft}>
                      <IconAlertTriangle size={24} color="#FFFFFF" />
                      <View style={{ flex: 1, marginLeft: 12 }}>
                        <Text style={styles.customerAlertTitle}>
                          🚨 ¡ALERTA DE ATENCIÓN A CLIENTES ACTIVA EN CASETA!
                        </Text>
                        <Text style={styles.customerAlertSub}>
                          El conductor en Carril 01 presionó el botón de ayuda/interfón. Requiere asistencia inmediata del operador.
                        </Text>
                      </View>
                    </View>
                    <TouchableOpacity
                      style={styles.customerAlertDismissBtn}
                      onPress={() => {
                        setCustomerAlertActive(false);
                        addLog('INTERCOM_ATTENDED', 'Llamada de atención a clientes atendida y silenciada por el operador.', 'info');
                      }}
                      activeOpacity={0.8}
                    >
                      <IconCheck size={16} color="#B91C1C" />
                      <Text style={styles.customerAlertDismissTxt}>Atender & Desactivar Alerta</Text>
                    </TouchableOpacity>
                  </View>
                )}

                {/* SENSORS ANALYSIS SECTION (PRESENCIA VEHICULAR & STREAMING DE CÁMARA) */}
                <View style={styles.sensorSectionHeaderRow}>
                  <Text style={styles.sensorSectionHeading}>ANÁLISIS DE SENSORES EN TIEMPO REAL · MISIÓN CRÍTICA</Text>
                  <Text style={styles.sensorSectionSubheading}>
                    Detección de presencia vehicular en caseta y streaming de cámara LPR con captura manual de evidencia
                  </Text>
                </View>

                <View style={[styles.sensorAnalysisGrid, isDesktopLayout && styles.sensorAnalysisGridDesktop]}>
                  {/* SENSOR 1: SENSOR DE PRESENCIA VEHICULAR (¿HAY AUTO O NO CERCA?) */}
                  <View style={[styles.sensorCard, isCarPresent ? styles.sensorCardActiveCar : styles.sensorCardClearCar]}>
                    <View style={styles.sensorCardHeader}>
                      <View style={styles.sensorCardTitleRow}>
                        <View style={[styles.sensorIconCircle, isCarPresent ? styles.sensorIconCarActive : styles.sensorIconCarClear]}>
                          <IconCar size={18} color={isCarPresent ? '#0284C7' : '#64748B'} />
                        </View>
                        <View>
                          <Text style={styles.sensorCardTitle}>Sensor de Presencia Vehicular</Text>
                          <Text style={styles.sensorCardTech}>Lazo Inductivo GPIO 17 + Radar Microondas</Text>
                        </View>
                      </View>
                      <View style={[styles.sensorStatusBadge, isCarPresent ? styles.badgeCarPresent : styles.badgeCarClear]}>
                        <Text style={[styles.sensorStatusBadgeTxt, isCarPresent ? styles.badgeTxtCarPresent : styles.badgeTxtCarClear]}>
                          {isCarPresent ? 'VEHÍCULO DETECTADO' : 'CARRIL DESPEJADO'}
                        </Text>
                      </View>
                    </View>

                    {/* Presencia State Box */}
                    <View style={styles.carPresenceDisplayBox}>
                      <View style={styles.carPresenceStatusLine}>
                        <View style={[styles.carPresenceIndicatorDot, isCarPresent ? styles.dotCarPresent : styles.dotCarClear]} />
                        <Text style={[styles.carPresenceStatusHeading, isCarPresent ? styles.txtCarPresent : styles.txtCarClear]}>
                          {isCarPresent ? 'AUTOMÓVIL EN APROXIMACIÓN A CASETA' : 'SIN AUTOMÓVIL EN LA ZONA DE CONTROL'}
                        </Text>
                      </View>

                      {/* Detail Metrics Grid */}
                      <View style={styles.carPresenceMetricsTable}>
                        <View style={styles.carPresenceRow}>
                          <Text style={styles.carPresenceLabel}>ESTADO DE APROXIMACIÓN:</Text>
                          <Text style={[styles.carPresenceValue, isCarPresent ? { color: '#0284C7' } : { color: '#64748B' }]}>
                            {isCarPresent ? 'Auto detenido frente a la barrera (0.85 m)' : 'Área libre (> 3.0 m)'}
                          </Text>
                        </View>
                        <View style={styles.carPresenceRow}>
                          <Text style={styles.carPresenceLabel}>MASA METÁLICA (GPIO 17):</Text>
                          <Text style={[styles.carPresenceValue, isCarPresent ? { color: '#16A34A' } : { color: '#94A3B8' }]}>
                            {isCarPresent ? '48.5 μH · DETECTADA (HIGH)' : '0.0 μH · EN REPOSO (LOW)'}
                          </Text>
                        </View>
                        <View style={styles.carPresenceRow}>
                          <Text style={styles.carPresenceLabel}>VEHÍCULO IDENTIFICADO:</Text>
                          <Text style={styles.carPresenceValue}>
                            {isCarPresent ? 'Audi e-tron GT · Particular EV' : 'Ninguno'}
                          </Text>
                        </View>
                        <View style={styles.carPresenceRow}>
                          <Text style={styles.carPresenceLabel}>MATRÍCULA ASOCIADA:</Text>
                          <Text style={[styles.carPresenceValue, { fontWeight: '700' }]}>
                            {isCarPresent ? (detectedPlate || 'JLM-456-E') : '—'}
                          </Text>
                        </View>
                      </View>
                    </View>

                    <Text style={styles.sensorExplanationTxt}>
                      {isCarPresent
                        ? '✓ Sensor activo: Se detectó masa vehicular a 0.85 m de la pluma. Sistema preparado para despacho y apertura de barrera.'
                        : '○ Sensor en espera: No hay vehículos en la zona de detección. El lazo inductivo y radar permanecen en reposo.'}
                    </Text>

                    {/* Interactive Button to toggle car presence */}
                    <TouchableOpacity
                      style={[styles.simCarPresenceBtn, isCarPresent ? styles.simCarBtnClear : styles.simCarBtnArrive]}
                      onPress={handleToggleCarPresence}
                      activeOpacity={0.8}
                    >
                      <IconCar size={16} color="#FFFFFF" />
                      <Text style={styles.simCarPresenceBtnTxt}>
                        {isCarPresent ? 'Simular Retiro / Salida de Automóvil' : 'Simular Llegada de Automóvil a Caseta'}
                      </Text>
                    </TouchableOpacity>
                  </View>

                  {/* SENSOR 2: STREAMING DE CÁMARA LPR 4K & GUARDAR CAPTURA MANUAL */}
                  <View style={styles.sensorCard}>
                    <View style={styles.sensorCardHeader}>
                      <View style={styles.sensorCardTitleRow}>
                        <View style={[styles.sensorIconCircle, styles.sensorIconCamera]}>
                          <IconCamera size={18} color="#0066CC" />
                        </View>
                        <View>
                          <Text style={styles.sensorCardTitle}>Streaming de Cámara LPR 4K</Text>
                          <Text style={styles.sensorCardTech}>Transmisión en Vivo RTSP UHD · YOLOv8 Edge AI</Text>
                        </View>
                      </View>
                      <View style={styles.badgeCamera}>
                        <Text style={styles.badgeCameraTxt}>STREAMING EN VIVO</Text>
                      </View>
                    </View>

                    {/* Camera Viewfinder Screen with Flash effect on capture */}
                    <View style={styles.cameraViewfinder}>
                      {/* Corner Targeting Brackets */}
                      <View style={styles.vfBracketTL} />
                      <View style={styles.vfBracketTR} />
                      <View style={styles.vfBracketBL} />
                      <View style={styles.vfBracketBR} />

                      {/* White flash overlay when snapshot is taken */}
                      {captureFlash && <View style={styles.cameraFlashOverlay} />}

                      <View style={styles.vfHeaderBar}>
                        <View style={styles.vfRecRow}>
                          <View style={styles.scadaRecDot} />
                          <Text style={styles.vfRecTxt}>LIVE STREAM · 4K UHD @ 60 FPS</Text>
                        </View>
                        <Text style={styles.vfExpoTxt}>EXP: 1/1000s · ISO 200</Text>
                      </View>

                      {/* Real-time Detections in Camera Frame */}
                      <View style={styles.cameraDetectionsContainer}>
                        {isCarPresent ? (
                          <>
                            {/* Detection 1: License Plate OCR */}
                            <View style={styles.cameraBoxPlate}>
                              <Text style={styles.cameraBoxTagPlate}>[ OCR MATRÍCULA: {detectedPlate || 'JLM-456-E'} ]</Text>
                              <Text style={styles.cameraBoxSubPlate}>Confianza: 99.6% · Match Reserva #BK-9021</Text>
                            </View>

                            {/* Detection 2: Vehicle Classification */}
                            <View style={styles.cameraBoxVehicle}>
                              <Text style={styles.cameraBoxTagVehicle}>[ AUTO: Audi e-tron GT · Particular ]</Text>
                              <Text style={styles.cameraBoxSubVehicle}>Clase: Sedán EV · Velocidad: 0 km/h (Detenido)</Text>
                            </View>
                          </>
                        ) : (
                          <View style={styles.cameraStandbyBox}>
                            <Text style={styles.cameraStandbyTxt}>[ CÁMARA TRANSMITIENDO EN VIVO · ESPERANDO VEHÍCULO EN ENCUADRE ]</Text>
                            <Text style={styles.cameraStandbySub}>Sensor óptico calibrado y listo para lectura OCR inmediata</Text>
                          </View>
                        )}
                      </View>
                    </View>

                    {/* Camera Specs & Saved Captures HUD */}
                    <View style={styles.cameraSpecsRow}>
                      <View style={styles.camSpecTile}>
                        <Text style={styles.camSpecLabel}>RESOLUCIÓN</Text>
                        <Text style={styles.camSpecVal}>3840 x 2160</Text>
                      </View>
                      <View style={styles.camSpecTile}>
                        <Text style={styles.camSpecLabel}>LATENCIA RTSP</Text>
                        <Text style={styles.camSpecVal}>8.2 ms</Text>
                      </View>
                      <View style={styles.camSpecTile}>
                        <Text style={styles.camSpecLabel}>EVIDENCIAS</Text>
                        <Text style={styles.camSpecVal}>{savedCaptures.length} Guardadas</Text>
                      </View>
                    </View>

                    {/* BOTÓN PARA GUARDAR CAPTURA MANUAL DE LO QUE SE VISUALIZA */}
                    <TouchableOpacity
                      style={styles.saveCaptureBtn}
                      onPress={handleSaveCapture}
                      activeOpacity={0.8}
                    >
                      <IconCamera size={18} color="#FFFFFF" />
                      <Text style={styles.saveCaptureBtnTxt}>Guardar Captura Manual de Evidencia</Text>
                    </TouchableOpacity>

                    {/* Saved Captures List (if any saved) */}
                    {savedCaptures.length > 0 && (
                      <View style={styles.savedCapturesContainer}>
                        <Text style={styles.savedCapturesLabel}>Capturas recientes archivadas:</Text>
                        <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.savedCapturesScroll}>
                          {savedCaptures.map((capId, idx) => (
                            <View key={idx} style={styles.savedCaptureChip}>
                              <IconCheck size={11} color="#16A34A" />
                              <Text style={styles.savedCaptureChipTxt}>{capId}</Text>
                            </View>
                          ))}
                        </ScrollView>
                      </View>
                    )}
                  </View>
                </View>

                {/* Hardware Telemetry & GPIO Matrix (Raspberry Pi 5) */}
                <View style={styles.telemetrySection}>
                  <Text style={styles.telemetryHeaderTitle}>TELEMETRÍA DE ACTUADORES & HARDWARE GPIO · RASPBERRY PI 5</Text>
                  
                  <View style={styles.telemetryGrid}>
                    {/* Pin 1: GPIO 18 */}
                    <View style={styles.telemetryTile}>
                      <View style={styles.telemetryTileHeader}>
                        <Text style={styles.telemetryPinName}>GPIO 18 · ACTUADOR</Text>
                        <View style={styles.telemetryStatusPillGreen}>
                          <Text style={styles.telemetryStatusTxtGreen}>PWM 50Hz</Text>
                        </View>
                      </View>
                      <Text style={styles.telemetryMetricMain}>
                        {barrierOpen ? '90.0° (Arriba)' : '0.0° (Horizontal)'}
                      </Text>
                      <Text style={styles.telemetryMetricSub}>
                        Pulso: {barrierOpen ? '2.0ms' : '1.0ms'} · Servomotor SG90
                      </Text>
                    </View>

                    {/* Pin 2: GPIO 17 */}
                    <View style={styles.telemetryTile}>
                      <View style={styles.telemetryTileHeader}>
                        <Text style={styles.telemetryPinName}>GPIO 17 · SENSOR</Text>
                        <View
                          style={
                            isLoopActive
                              ? styles.telemetryStatusPillGreen
                              : styles.telemetryStatusPillNeutral
                          }
                        >
                          <Text
                            style={
                              isLoopActive
                                ? styles.telemetryStatusTxtGreen
                                : styles.telemetryStatusTxtNeutral
                            }
                          >
                            {isLoopActive ? 'HIGH' : 'LOW'}
                          </Text>
                        </View>
                      </View>
                      <Text style={styles.telemetryMetricMain}>
                        {isLoopActive ? 'Masa Activa (Auto)' : 'Despejado'}
                      </Text>
                      <Text style={styles.telemetryMetricSub}>Lazo Inductivo · 45 μH Umbral</Text>
                    </View>

                    {/* Pin 3: GPIO 27 */}
                    <View style={styles.telemetryTile}>
                      <View style={styles.telemetryTileHeader}>
                        <Text style={styles.telemetryPinName}>GPIO 27 · SEGURIDAD</Text>
                        <View style={styles.telemetryStatusPillGreen}>
                          <Text style={styles.telemetryStatusTxtGreen}>OK</Text>
                        </View>
                      </View>
                      <Text style={styles.telemetryMetricMain}>Haz IR Conectado</Text>
                      <Text style={styles.telemetryMetricSub}>Fotocelda Anti-aplastamiento 38kHz</Text>
                    </View>

                    {/* Pin 4: I2C-1 Bus */}
                    <View style={styles.telemetryTile}>
                      <View style={styles.telemetryTileHeader}>
                        <Text style={styles.telemetryPinName}>I2C-1 (0x3C) · CASETA</Text>
                        <View style={styles.telemetryStatusPillBlue}>
                          <Text style={styles.telemetryStatusTxtBlue}>400 kHz</Text>
                        </View>
                      </View>
                      <Text style={styles.telemetryMetricMain}>OLED SSD1306</Text>
                      <Text style={styles.telemetryMetricSub}>Display Piloto: "PASE AUTORIZADO"</Text>
                    </View>
                  </View>

                  <View style={styles.healthRibbon}>
                    <Text style={styles.healthRibbonTxt}>
                      Alimentación: 5.12V PoE+ · Temp CPU: 41.2°C · Latencia Bus: 7ms · Sincronización .NET 10 API: Activa
                    </Text>
                  </View>
                </View>

                {/* Live Activity Audit Terminal */}
                <View style={styles.terminalBox}>
                  <View style={styles.terminalHeader}>
                    <View style={styles.terminalHeaderLeft}>
                      <View style={styles.terminalLedDot} />
                      <Text style={styles.terminalTitle}>TERMINAL DE EVENTOS Y AUDITORÍA EN TIEMPO REAL</Text>
                    </View>
                    <TouchableOpacity
                      onPress={() =>
                        setBarrierLogs([
                          {
                            id: Date.now().toString(),
                            time: new Date().toLocaleTimeString('es-MX', { hour12: false }),
                            tag: 'LOG_CLEARED',
                            message: 'Historial de consola reiniciado por el operador.',
                            type: 'info',
                          },
                        ])
                      }
                      style={styles.terminalClearBtn}
                    >
                      <Text style={styles.terminalClearTxt}>Limpiar</Text>
                    </TouchableOpacity>
                  </View>

                  <ScrollView style={styles.terminalScroll} nestedScrollEnabled={true}>
                    {barrierLogs.map((log) => (
                      <View key={log.id} style={styles.terminalRow}>
                        <Text style={styles.terminalTime}>[{log.time}]</Text>
                        <View
                          style={[
                            styles.terminalTagPill,
                            log.type === 'success' && styles.terminalTagSuccess,
                            log.type === 'warn' && styles.terminalTagWarn,
                            log.type === 'error' && styles.terminalTagError,
                          ]}
                        >
                          <Text
                            style={[
                              styles.terminalTagTxt,
                              log.type === 'success' && styles.terminalTagTxtSuccess,
                              log.type === 'warn' && styles.terminalTagTxtWarn,
                              log.type === 'error' && styles.terminalTagTxtError,
                            ]}
                          >
                            {log.tag}
                          </Text>
                        </View>
                        <Text style={styles.terminalMsg}>{log.message}</Text>
                      </View>
                    ))}
                  </ScrollView>
                </View>
              </BentoCard>
            </View>
          )}

          {/* SECTION 3: LIQUIDACIONES & AUDITORÍA CONTABLE */}
          {activeTab === 'finance' && (
            <View style={styles.tabPane}>
              {/* Main Financial Master BentoCard */}
              <BentoCard variant="elevated" style={styles.sectionCard}>
                {/* Header & Accounting Action Bar */}
                <View style={styles.financeHeaderRow}>
                  <View style={{ flex: 1, minWidth: 260 }}>
                    <View style={styles.financeTitleRow}>
                      <IconDollarSign size={22} color="#0066CC" strokeWidth={2} />
                      <Text style={styles.sectionTitle}>Centro de Liquidaciones & Auditoría Contable</Text>
                    </View>
                    <Text style={styles.sectionSub}>
                      Flujo de efectivo 100% Cashless · Conciliación bancaria en tiempo real · SAT CFDI 4.0
                    </Text>
                  </View>

                  {/* Accounting Export & Closure Action Buttons */}
                  <View style={styles.financeActionBtnsRow}>
                    <TouchableOpacity
                      style={styles.finActionBtn}
                      onPress={handleDownloadTaxReport}
                      activeOpacity={0.8}
                    >
                      <IconFileText size={15} color="#0066CC" />
                      <Text style={styles.finActionBtnTxt}>Reporte SAT (CFDI 4.0)</Text>
                    </TouchableOpacity>

                    <TouchableOpacity
                      style={styles.finActionBtn}
                      onPress={handleExportCSV}
                      activeOpacity={0.8}
                    >
                      <IconDownload size={15} color="#0F172A" />
                      <Text style={[styles.finActionBtnTxt, { color: '#0F172A' }]}>Exportar CSV</Text>
                    </TouchableOpacity>

                    <TouchableOpacity
                      style={[styles.finActionBtn, styles.finActionBtnClose]}
                      onPress={handleCierreDeCaja}
                      activeOpacity={0.8}
                    >
                      <IconCheck size={15} color="#FFFFFF" />
                      <Text style={[styles.finActionBtnTxt, { color: '#FFFFFF' }]}>Arqueo & Cierre</Text>
                    </TouchableOpacity>
                  </View>
                </View>

                {/* Period Selector Pills */}
                <View style={styles.finPeriodSelectorRow}>
                  <TouchableOpacity
                    style={[styles.finPeriodPill, financePeriod === 'today' && styles.finPeriodPillActive]}
                    onPress={() => setFinancePeriod('today')}
                    activeOpacity={0.7}
                  >
                    <Text style={[styles.finPeriodTxt, financePeriod === 'today' && styles.finPeriodTxtActive]}>
                      Hoy (22 Octubre) · En Vivo
                    </Text>
                  </TouchableOpacity>
                  <TouchableOpacity
                    style={[styles.finPeriodPill, financePeriod === 'week' && styles.finPeriodPillActive]}
                    onPress={() => setFinancePeriod('week')}
                    activeOpacity={0.7}
                  >
                    <Text style={[styles.finPeriodTxt, financePeriod === 'week' && styles.finPeriodTxtActive]}>
                      Esta Semana (W-43)
                    </Text>
                  </TouchableOpacity>
                  <TouchableOpacity
                    style={[styles.finPeriodPill, financePeriod === 'month' && styles.finPeriodPillActive]}
                    onPress={() => setFinancePeriod('month')}
                    activeOpacity={0.7}
                  >
                    <Text style={[styles.finPeriodTxt, financePeriod === 'month' && styles.finPeriodTxtActive]}>
                      Mes en Curso (Octubre 2026)
                    </Text>
                  </TouchableOpacity>
                </View>

                {/* 4 KEY FINANCIAL & ACCOUNTING METRICS (KPIs) */}
                <View style={[styles.finKpiGrid, isDesktopLayout && styles.finKpiGridDesktop]}>
                  {/* KPI 1: FLUJO TOTAL COBRADO */}
                  <View style={styles.finKpiCard}>
                    <View style={styles.finKpiHeader}>
                      <Text style={styles.finKpiLabel}>FLUJO BRUTO COBRADO</Text>
                      <View style={styles.finKpiIconWrapGreen}>
                        <IconDollarSign size={16} color="#16A34A" />
                      </View>
                    </View>
                    <Text style={styles.finKpiHuge}>$4,860.00</Text>
                    <Text style={styles.finKpiCurrency}>MXN · 100% Conciliado</Text>
                    <View style={styles.finKpiSubRow}>
                      <View style={styles.finTrendBadgeGreen}>
                        <Text style={styles.finTrendBadgeTxtGreen}>+14.2% vs ayer</Text>
                      </View>
                      <Text style={styles.finKpiFootnote}>41 autos despachados</Text>
                    </View>
                  </View>

                  {/* KPI 2: PROMEDIO DE DINERO POR CLIENTE (ARPU) */}
                  <View style={styles.finKpiCard}>
                    <View style={styles.finKpiHeader}>
                      <Text style={styles.finKpiLabel}>TICKET PROMEDIO / VEHÍCULO</Text>
                      <View style={styles.finKpiIconWrapBlue}>
                        <IconReceipt size={16} color="#0066CC" />
                      </View>
                    </View>
                    <Text style={[styles.finKpiHuge, { color: '#0066CC' }]}>$118.53</Text>
                    <Text style={styles.finKpiCurrency}>MXN por cliente atendido</Text>
                    <View style={styles.finKpiSubRow}>
                      <View style={styles.finRateBadge}>
                        <Text style={styles.finRateBadgeTxt}>Base: $45.00/h</Text>
                      </View>
                      <Text style={styles.finKpiFootnote}>2.6h permanencia prom.</Text>
                    </View>
                  </View>

                  {/* KPI 3: TIEMPO PROMEDIO DE ESTANCIA */}
                  <View style={styles.finKpiCard}>
                    <View style={styles.finKpiHeader}>
                      <Text style={styles.finKpiLabel}>TIEMPO PROMEDIO ESTANCIA</Text>
                      <View style={styles.finKpiIconWrapIndigo}>
                        <IconClock size={16} color="#4F46E5" />
                      </View>
                    </View>
                    <Text style={[styles.finKpiHuge, { color: '#4F46E5' }]}>2h 38m</Text>
                    <Text style={styles.finKpiCurrency}>158 minutos promedio</Text>
                    <View style={styles.finKpiSubRow}>
                      <View style={styles.finPeakBadge}>
                        <Text style={styles.finPeakBadgeTxt}>Pico: 1 a 2 horas (34%)</Text>
                      </View>
                      <Text style={styles.finKpiFootnote}>Rotación: 4.8 veh/cajón</Text>
                    </View>
                  </View>

                  {/* KPI 4: CONTABILIDAD NETA & SAT CFDI 4.0 */}
                  <View style={styles.finKpiCard}>
                    <View style={styles.finKpiHeader}>
                      <Text style={styles.finKpiLabel}>DEPÓSITO NETO & SAT</Text>
                      <View style={styles.finKpiIconWrapPurple}>
                        <IconShield size={16} color="#7C3AED" />
                      </View>
                    </View>
                    <Text style={[styles.finKpiHuge, { color: '#15803D' }]}>$4,701.56</Text>
                    <Text style={styles.finKpiCurrency}>MXN Neto Liquidable</Text>
                    <View style={styles.finKpiSubRow}>
                      <Text style={styles.finTaxBreakdownTxt}>
                        IVA 16%: $670.34 · Fee: $158.44
                      </Text>
                    </View>
                  </View>
                </View>

                {/* SENSATIONAL INTERACTIVE BAR CHART: DISTRIBUCIÓN DE HORAS DE ESTANCIA */}
                <View style={styles.stayChartCard}>
                  <View style={styles.stayChartHeader}>
                    <View style={{ flex: 1 }}>
                      <View style={styles.stayChartTitleRow}>
                        <IconBarChart size={18} color="#0066CC" />
                        <Text style={styles.stayChartTitle}>
                          Distribución de Permanencia · ¿Cuántas Horas se Queda el Cliente Normalmente?
                        </Text>
                      </View>
                      <Text style={styles.stayChartSub}>
                        Análisis estadístico de rotación por rangos de tiempo y ticket medio generado por vehículo
                      </Text>
                    </View>
                    <View style={styles.stayChartPill}>
                      <Text style={styles.stayChartPillTxt}>PROMEDIO GENERAL: 2h 38m</Text>
                    </View>
                  </View>

                  {/* Horizontal Bar Chart Rows */}
                  <View style={styles.chartBarsContainer}>
                    {STAY_DISTRIBUTIONS.map((item, idx) => (
                      <View key={idx} style={styles.chartBarRow}>
                        {/* Left Label */}
                        <View style={styles.chartBarLabelCol}>
                          <View style={styles.chartBarRangeRow}>
                            <Text style={styles.chartBarRange}>{item.range}</Text>
                            {item.isPeak && (
                              <View style={styles.peakTag}>
                                <Text style={styles.peakTagTxt}>PICO MÁXIMO</Text>
                              </View>
                            )}
                          </View>
                          <Text style={styles.chartBarSub}>{item.label}</Text>
                        </View>

                        {/* Middle Bar Visual */}
                        <View style={styles.chartBarTrack}>
                          <View
                            style={[
                              styles.chartBarFill,
                              {
                                width: `${item.percentage}%`,
                                backgroundColor: item.color,
                              },
                            ]}
                          />
                        </View>

                        {/* Right Metrics */}
                        <View style={styles.chartBarMetricsCol}>
                          <Text style={[styles.chartBarPct, { color: item.color }]}>
                            {item.percentage}%
                          </Text>
                          <Text style={styles.chartBarCount}>
                            {item.count} autos · {item.avgAmount}
                          </Text>
                        </View>
                      </View>
                    ))}
                  </View>

                  {/* Analytic Operational Insight Callout */}
                  <View style={styles.stayChartCallout}>
                    <IconCheck size={16} color="#15803D" />
                    <Text style={styles.stayChartCalloutTxt}>
                      <Text style={{ fontWeight: '700' }}>Hallazgo Operativo:</Text> El 62% del volumen de vehículos permanece entre 1 y 4 horas. La franja horaria pico de cobro y despacho se ubica entre las 13:00 y las 16:30 hrs, aportando el 58% de la facturación diaria.
                    </Text>
                  </View>
                </View>

                {/* ACCOUNTING AUDIT & TAX BREAKDOWN (TODO LO CONTABLE) */}
                <View style={[styles.accountingGrid, isDesktopLayout && styles.accountingGridDesktop]}>
                  {/* CARD A: ARQUEO DE CAJA Y CORTE CONTABLE */}
                  <View style={styles.accountingCard}>
                    <View style={styles.accHeaderRow}>
                      <View style={styles.accIconCircleBlue}>
                        <IconReceipt size={17} color="#0066CC" />
                      </View>
                      <View style={{ flex: 1 }}>
                        <Text style={styles.accCardTitle}>Corte de Caja Contable (Hoy)</Text>
                        <Text style={styles.accCardSub}>Conciliación 100% Cashless · Sin faltantes</Text>
                      </View>
                      <View style={styles.accBadgeGreen}>
                        <Text style={styles.accBadgeTxtGreen}>CUADRADO (0.00)</Text>
                      </View>
                    </View>

                    <View style={styles.accTable}>
                      <View style={styles.accRow}>
                        <Text style={styles.accRowLabel}>Ingreso Bruto Cobrado (41 Transacciones):</Text>
                        <Text style={[styles.accRowVal, { color: '#16A34A', fontWeight: '800' }]}>
                          +$4,860.00 MXN
                        </Text>
                      </View>
                      <View style={styles.accRow}>
                        <Text style={styles.accRowLabel}>Subtotal Base Gravable (sin IVA):</Text>
                        <Text style={styles.accRowVal}>$4,189.66 MXN</Text>
                      </View>
                      <View style={styles.accRow}>
                        <Text style={styles.accRowLabel}>IVA Trasladado (16% SAT Art. 1° LIVA):</Text>
                        <Text style={[styles.accRowVal, { color: '#0066CC', fontWeight: '700' }]}>
                          +$670.34 MXN
                        </Text>
                      </View>
                      <View style={styles.accRow}>
                        <Text style={styles.accRowLabel}>Comisión Pasarela Stripe / POS (2.9% + $3):</Text>
                        <Text style={[styles.accRowVal, { color: '#DC2626' }]}>
                          -$158.44 MXN
                        </Text>
                      </View>
                      <View style={styles.accDivider} />
                      <View style={styles.accRowHighlight}>
                        <Text style={styles.accHighlightLabel}>DEPÓSITO NETO A BANCO (SPEI):</Text>
                        <Text style={styles.accHighlightVal}>$4,701.56 MXN</Text>
                      </View>
                      <View style={styles.accRow}>
                        <Text style={styles.accRowLabel}>Efectivo Físico en Caseta (Cero Robo):</Text>
                        <Text style={[styles.accRowVal, { color: '#64748B' }]}>$0.00 MXN (Eliminado)</Text>
                      </View>
                    </View>
                  </View>

                  {/* CARD B: CANALES DE PAGO & CUMPLIMIENTO FISCAL SAT CFDI 4.0 */}
                  <View style={styles.accountingCard}>
                    <View style={styles.accHeaderRow}>
                      <View style={styles.accIconCirclePurple}>
                        <IconCreditCard size={17} color="#7C3AED" />
                      </View>
                      <View style={{ flex: 1 }}>
                        <Text style={styles.accCardTitle}>Canales de Pago & Régimen SAT</Text>
                        <Text style={styles.accCardSub}>CFDI 4.0 timbrado en tiempo real</Text>
                      </View>
                      <View style={styles.accBadgePurple}>
                        <Text style={styles.accBadgeTxtPurple}>PAC ACTIVO</Text>
                      </View>
                    </View>

                    <View style={styles.accChannelsBox}>
                      <View style={styles.channelRow}>
                        <View style={{ flex: 1 }}>
                          <Text style={styles.channelName}>Apple Pay / Google Pay Express</Text>
                          <Text style={styles.channelSub}>Despacho automatizado por LPR y wallet</Text>
                        </View>
                        <Text style={styles.channelAmount}>$2,818.80 (58%)</Text>
                      </View>

                      <View style={styles.channelRow}>
                        <View style={{ flex: 1 }}>
                          <Text style={styles.channelName}>Tarjetas Visa / Mastercard / AMEX</Text>
                          <Text style={styles.channelSub}>Cobro en app con tarjeta predeterminada</Text>
                        </View>
                        <Text style={styles.channelAmount}>$1,555.20 (32%)</Text>
                      </View>

                      <View style={styles.channelRow}>
                        <View style={{ flex: 1 }}>
                          <Text style={styles.channelName}>Terminal Contactless NFC de Caseta</Text>
                          <Text style={styles.channelSub}>Cobro de proximidad en pluma de salida</Text>
                        </View>
                        <Text style={styles.channelAmount}>$486.00 (10%)</Text>
                      </View>
                    </View>

                    <View style={styles.satComplianceBox}>
                      <Text style={styles.satComplianceTitle}>Certificación Fiscal CFDI 4.0:</Text>
                      <Text style={styles.satComplianceLine}>• RFC Emisor: APK230815-XYZ · AeroPark S.A.P.I. de C.V.</Text>
                      <Text style={styles.satComplianceLine}>• Régimen Fiscal: 601 General de Ley Personas Morales</Text>
                      <Text style={styles.satComplianceLine}>• Timbrado PAC: Enlace Fiscal · 100% CFDIs con UUID Fiscal</Text>
                    </View>
                  </View>
                </View>

                {/* REAL-TIME GENERAL LEDGER OF TRANSACTIONS (TODO EL DINERO QUE FLUYE) */}
                <View style={styles.ledgerSection}>
                  <View style={styles.ledgerHeaderRow}>
                    <View style={{ flex: 1 }}>
                      <Text style={styles.ledgerTitle}>Libro Mayor de Transacciones · Flujo en Tiempo Real</Text>
                      <Text style={styles.ledgerSub}>
                        Registro cronológico de vehículos despachados, tiempo computado y comprobante fiscal
                      </Text>
                    </View>
                    <View style={styles.ledgerCountBadge}>
                      <Text style={styles.ledgerCountTxt}>
                        {FINANCE_TRANSACTIONS.length} Transacciones Auditadas
                      </Text>
                    </View>
                  </View>

                  {/* Transactions Table / Card Rows */}
                  <View style={styles.ledgerList}>
                    {FINANCE_TRANSACTIONS.map((tx) => (
                      <TouchableOpacity
                        key={tx.id}
                        style={styles.ledgerRowCard}
                        onPress={() => handleViewTransactionDetail(tx)}
                        activeOpacity={0.7}
                      >
                        {/* Col 1: Plate & Vehicle */}
                        <View style={styles.ledgerPlateCol}>
                          <View style={styles.ledgerPlateBadge}>
                            <Text style={styles.ledgerPlateTxt}>{tx.plate}</Text>
                          </View>
                          <Text style={styles.ledgerVehicleTxt}>{tx.vehicle}</Text>
                        </View>

                        {/* Col 2: Times & Duration */}
                        <View style={styles.ledgerDurationCol}>
                          <Text style={styles.ledgerDurationTxt}>
                            ⏱ {tx.durationFormatted}
                          </Text>
                          <Text style={styles.ledgerTimesSub}>
                            {tx.entryTime} → {tx.exitTime}
                          </Text>
                        </View>

                        {/* Col 3: Payment Method & CFDI */}
                        <View style={styles.ledgerMethodCol}>
                          <Text style={styles.ledgerMethodTxt}>{tx.method}</Text>
                          <Text style={styles.ledgerCfdiTxt}>{tx.cfdi}</Text>
                        </View>

                        {/* Col 4: Amount & Tax */}
                        <View style={styles.ledgerAmountCol}>
                          <Text style={styles.ledgerAmountTxt}>
                            ${tx.amount.toFixed(2)} MXN
                          </Text>
                          <Text style={styles.ledgerTaxSub}>
                            IVA: ${tx.iva.toFixed(2)} · Neto: ${tx.net.toFixed(2)}
                          </Text>
                        </View>

                        {/* Col 5: Status Tag */}
                        <View style={styles.ledgerStatusPill}>
                          <IconCheck size={11} color="#16A34A" />
                          <Text style={styles.ledgerStatusTxt}>Liquidado</Text>
                        </View>
                      </TouchableOpacity>
                    ))}
                  </View>
                </View>
              </BentoCard>
            </View>
          )}

          {/* SECTION 4: SEGURIDAD ZERO TRUST */}
          {activeTab === 'security' && (
            <View style={styles.tabPane}>
              <BentoCard variant="elevated" style={styles.sectionCard}>
                <Text style={styles.sectionTitle}>Police Gateway · Alertas de Seguridad</Text>
                <Text style={styles.sectionSub}>
                  Zero Trust: La pluma jamás se bloquea sola; requiere dictamen humano documentado (ADR-08).
                </Text>

                {policeAlertActive ? (
                  <View style={styles.alertCard}>
                    <View style={styles.alertHeader}>
                      <Text style={styles.alertIcon}>▲</Text>
                      <View style={{ flex: 1 }}>
                        <Text style={styles.alertHeading}>Coincidencia C5 / Fiscalía</Text>
                        <Text style={styles.alertBody}>Reporte de Robo de Vehículo con Violencia</Text>
                      </View>
                      <PlateBadge plateNumber="ROB-404-MX" />
                    </View>

                    <Text style={styles.confidenceText}>Confianza del Algoritmo OCR: 94.0%</Text>

                    <View style={styles.alertBtnGroup}>
                      <PillButton
                        label="Confirmar y Alertar Seguridad"
                        onPress={() => {
                          setPoliceAlertActive(false);
                          Alert.alert('Dictamen Registrado', 'Alerta confirmada y transmitida a patrullas aeroportuarias.');
                        }}
                        variant="primary"
                        size="normal"
                        style={{ flex: 1 }}
                      />
                      <PillButton
                        label="Descartar (Falso Positivo)"
                        onPress={() => {
                          setPoliceAlertActive(false);
                          Alert.alert('Dictamen Registrado', 'Alerta descartada como falso positivo de lectura.');
                        }}
                        variant="glass"
                        size="normal"
                        style={{ flex: 1 }}
                      />
                    </View>
                  </View>
                ) : (
                  <View style={styles.emptyAlerts}>
                    <IconCheck size={14} color="#16A34A" strokeWidth={2.5} />
                    <Text style={styles.emptyAlertsText}>Sin alertas de seguridad pendientes.</Text>
                  </View>
                )}
              </BentoCard>
            </View>
          )}

          {/* SECTION 5: AUDITORÍA */}
          {activeTab === 'audit' && (
            <View style={styles.tabPane}>
              <BentoCard variant="elevated" style={styles.sectionCard}>
                <Text style={styles.sectionTitle}>Bitácora Inmutable de Eventos LPR</Text>
                <Text style={styles.sectionSub}>Registro estricto de accesos, decisiones y liquidaciones</Text>

                <View style={styles.auditTable}>
                  <View style={styles.auditRow}>
                    <Text style={styles.auditTime}>17:20:14</Text>
                    <Text style={styles.auditBay}>Cajón 02</Text>
                    <Text style={styles.auditEvent}>Salida · Apple Pay ($45.00)</Text>
                  </View>
                  <View style={styles.auditRow}>
                    <Text style={styles.auditTime}>17:15:22</Text>
                    <Text style={styles.auditBay}>Cajón 01</Text>
                    <Text style={styles.auditEvent}>Entrada LPR · Tesla Model 3</Text>
                  </View>
                  <View style={styles.auditRow}>
                    <Text style={styles.auditTime}>17:08:40</Text>
                    <Text style={styles.auditBay}>Cajón 03</Text>
                    <Text style={styles.auditEvent}>Carga EV · Audi e-tron</Text>
                  </View>
                </View>
              </BentoCard>
            </View>
          )}

          {/* SECTION 6: EMERGENCIA CÓDIGO ROJO */}
          {activeTab === 'emergency' && (
            <View style={styles.tabPane}>
              <EmergencyScreen isEmbedded={true} />
            </View>
          )}
        </ScrollView>
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#F5F5F7',
  },
  globalEmergencyBar: {
    backgroundColor: '#DC2626',
    paddingVertical: 10,
    paddingHorizontal: 16,
    borderBottomWidth: 1.5,
    borderBottomColor: '#B91C1C',
    zIndex: 999,
  },
  globalEmergencyInner: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    maxWidth: 1200,
    alignSelf: 'center',
    width: '100%',
    flexWrap: 'wrap',
    gap: 8,
  },
  globalEmergencyLeft: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    flex: 1,
    minWidth: 280,
  },
  globalEmergencyPulseDot: {
    width: 10,
    height: 10,
    borderRadius: 5,
    backgroundColor: '#FFFFFF',
  },
  globalEmergencyTitle: {
    color: '#FFFFFF',
    fontSize: 12,
    fontWeight: '800',
    letterSpacing: 0.4,
  },
  globalEmergencyBtnPill: {
    backgroundColor: 'rgba(255, 255, 255, 0.25)',
    paddingVertical: 5,
    paddingHorizontal: 12,
    borderRadius: 999,
  },
  globalEmergencyBtnTxt: {
    color: '#FFFFFF',
    fontSize: 11,
    fontWeight: '800',
    letterSpacing: 0.3,
  },
  sidebarSosBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 6,
    paddingVertical: 9,
    paddingHorizontal: 12,
    borderRadius: 10,
    marginBottom: 10,
  },
  sidebarSosBtnNormal: {
    backgroundColor: '#DC2626',
  },
  sidebarSosBtnActive: {
    backgroundColor: '#991B1B',
    borderWidth: 1.5,
    borderColor: '#EF4444',
  },
  sidebarSosBtnTxt: {
    color: '#FFFFFF',
    fontSize: 11,
    fontWeight: '800',
    letterSpacing: 0.4,
  },
  mainLayout: {
    flex: 1,
    flexDirection: 'column',
  },
  mainLayoutDesktop: {
    flexDirection: 'row',
  },
  sidebarDesktop: {
    width: 250,
    backgroundColor: '#F5F5F7',
    borderRightWidth: 1,
    borderRightColor: 'rgba(0, 0, 0, 0.08)',
    padding: 16,
  },
  mobileSegmentBar: {
    backgroundColor: '#FFFFFF',
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(0, 0, 0, 0.08)',
    paddingVertical: 10,
    paddingHorizontal: 12,
  },
  mobileSegmentScroll: {
    flexDirection: 'row',
    gap: 8,
    alignItems: 'center',
  },
  mobilePillBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    paddingHorizontal: 12,
    paddingVertical: 7,
    borderRadius: 980,
    backgroundColor: '#F5F5F7',
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.08)',
  },
  mobilePillBtnActive: {
    backgroundColor: '#0066CC',
    borderColor: '#0066CC',
  },
  mobilePillIcon: {
    fontSize: 14,
    color: '#1D1D1F',
  },
  mobilePillLabel: {
    fontSize: 12,
    fontWeight: '500',
    color: '#1D1D1F',
  },
  mobilePillLabelActive: {
    color: '#FFFFFF',
    fontWeight: '700',
  },
  sidebarHeader: {
    fontSize: 10,
    fontWeight: '700',
    color: 'rgba(0, 0, 0, 0.45)',
    letterSpacing: 0.8,
    marginBottom: 10,
    paddingHorizontal: 8,
  },
  sidebarNav: {
    gap: 4,
  },
  navBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 10,
    paddingHorizontal: 12,
    borderRadius: 10,
    gap: 10,
  },
  navBtnActive: {
    backgroundColor: '#FFFFFF',
    shadowColor: '#000000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.08,
    shadowRadius: 3,
    elevation: 2,
  },
  navIconBox: {
    width: 20,
    alignItems: 'center',
    justifyContent: 'center',
  },
  navIcon: {
    fontSize: 15,
    color: '#6E6E73',
  },
  navLabel: {
    fontSize: 13,
    color: '#1D1D1F',
    fontWeight: '500',
    flex: 1,
  },
  navLabelActive: {
    color: '#0066CC',
    fontWeight: '700',
  },
  badgePill: {
    backgroundColor: '#DC2626',
    paddingHorizontal: 6,
    paddingVertical: 2,
    borderRadius: 980,
  },
  badgePillText: {
    color: '#FFFFFF',
    fontSize: 10,
    fontWeight: '700',
  },
  sidebarFooter: {
    marginTop: 16,
    paddingTop: 12,
    borderTopWidth: 1,
    borderTopColor: 'rgba(0, 0, 0, 0.08)',
  },
  statusDotRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  statusDot: {
    width: 6,
    height: 6,
    borderRadius: 3,
  },
  statusFooterText: {
    color: 'rgba(0, 0, 0, 0.55)',
    fontSize: 11,
  },
  contentScroll: {
    flexGrow: 1,
    padding: 18,
    backgroundColor: '#FAFAFC',
  },
  tabPane: {
    gap: 18,
  },
  kpiRow: {
    flexDirection: 'row',
    gap: 12,
  },
  kpiCard: {
    flex: 1,
    padding: 14,
  },
  kpiLabel: {
    fontSize: 9,
    fontWeight: '700',
    color: 'rgba(0, 0, 0, 0.45)',
    letterSpacing: 0.6,
  },
  kpiHuge: {
    fontSize: 20,
    fontWeight: '700',
    color: '#1D1D1F',
    marginTop: 4,
  },
  kpiSub: {
    fontSize: 10,
    color: 'rgba(0, 0, 0, 0.5)',
    marginTop: 2,
  },
  textGreen: { color: '#16A34A' },
  textRed: { color: '#DC2626' },
  textBlue: { color: '#0066CC' },
  baysGridCard: {
    padding: 18,
  },
  sectionTitle: {
    fontSize: 18,
    fontWeight: '700',
    color: '#1D1D1F',
    letterSpacing: -0.3,
  },
  sectionSub: {
    fontSize: 12,
    color: 'rgba(0, 0, 0, 0.55)',
    marginTop: 4,
    marginBottom: 16,
  },
  baysGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 12,
    marginBottom: 16,
  },
  baySlotCard: {
    flex: 1,
    minWidth: 140,
    backgroundColor: '#F8F8FA',
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.08)',
    borderRadius: 14,
    padding: 12,
  },
  baySlotFree: {
    borderColor: 'rgba(22, 163, 74, 0.35)',
    backgroundColor: 'rgba(22, 163, 74, 0.05)',
  },
  baySlotEv: {
    borderColor: 'rgba(0, 102, 204, 0.35)',
    backgroundColor: 'rgba(0, 102, 204, 0.05)',
  },
  baySlotSelected: {
    borderColor: '#0066CC',
    borderWidth: 2,
  },
  bayCardHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 6,
  },
  bayCardName: {
    color: '#1D1D1F',
    fontSize: 12,
    fontWeight: '700',
  },
  bayCarTitle: {
    color: 'rgba(0, 0, 0, 0.75)',
    fontSize: 11,
    marginBottom: 8,
  },
  baySensorInfo: {
    color: 'rgba(0, 0, 0, 0.45)',
    fontSize: 9,
    marginTop: 6,
  },
  inspectorCard: {
    backgroundColor: '#F8F8FA',
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.06)',
    borderRadius: 14,
    padding: 14,
  },
  inspRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: 12,
  },
  inspLabel: {
    fontSize: 9,
    color: 'rgba(0, 0, 0, 0.45)',
    fontWeight: '700',
  },
  inspValue: {
    fontSize: 13,
    color: '#1D1D1F',
    fontWeight: '600',
    marginTop: 2,
  },
  inspActionBtn: {
    marginTop: 4,
  },
  sectionCard: {
    padding: 20,
  },
  // LANE CONTROLLER & SCADA SIMULATOR STYLES
  laneHeaderRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    marginBottom: 16,
    flexWrap: 'wrap',
    gap: 12,
  },
  laneHeaderTitleRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    marginBottom: 4,
  },
  gateLivePill: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    paddingHorizontal: 12,
    paddingVertical: 7,
    borderRadius: 980,
    borderWidth: 1,
  },
  gateLivePillOpen: {
    backgroundColor: 'rgba(22, 163, 74, 0.08)',
    borderColor: 'rgba(22, 163, 74, 0.3)',
  },
  gateLivePillClosed: {
    backgroundColor: 'rgba(220, 38, 38, 0.08)',
    borderColor: 'rgba(220, 38, 38, 0.3)',
  },
  gateLivePillEmergency: {
    backgroundColor: 'rgba(220, 38, 38, 0.16)',
    borderColor: '#DC2626',
  },
  gateStatusDot: {
    width: 8,
    height: 8,
    borderRadius: 4,
  },
  gateStatusDotOpen: {
    backgroundColor: '#16A34A',
  },
  gateStatusDotClosed: {
    backgroundColor: '#DC2626',
  },
  gateStatusDotEmergency: {
    backgroundColor: '#EF4444',
  },
  gateLivePillText: {
    fontSize: 11,
    fontWeight: '700',
    letterSpacing: 0.4,
  },
  gateLivePillTextOpen: {
    color: '#16A34A',
  },
  gateLivePillTextClosed: {
    color: '#DC2626',
  },
  gateLivePillTextEmergency: {
    color: '#DC2626',
    fontWeight: '800',
  },
  laneTabsRow: {
    flexDirection: 'row',
    gap: 8,
    marginBottom: 18,
    flexWrap: 'wrap',
  },
  laneTabBtn: {
    paddingHorizontal: 14,
    paddingVertical: 8,
    borderRadius: 10,
    backgroundColor: '#F1F5F9',
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.06)',
  },
  laneTabBtnActive: {
    backgroundColor: '#0F172A',
    borderColor: '#0F172A',
  },
  laneTabTxt: {
    fontSize: 12,
    fontWeight: '600',
    color: '#64748B',
  },
  laneTabTxtActive: {
    color: '#FFFFFF',
    fontWeight: '700',
  },
  scadaCanvas: {
    backgroundColor: '#0B1120',
    borderRadius: 16,
    borderWidth: 1,
    borderColor: '#1E293B',
    overflow: 'hidden',
    marginBottom: 20,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.15,
    shadowRadius: 10,
  },
  scadaHudBar: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingHorizontal: 14,
    paddingVertical: 10,
    backgroundColor: '#090D16',
    borderBottomWidth: 1,
    borderBottomColor: '#1E293B',
    flexWrap: 'wrap',
    gap: 8,
  },
  scadaHudLeft: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  scadaRecDot: {
    width: 7,
    height: 7,
    borderRadius: 3.5,
    backgroundColor: '#EF4444',
  },
  scadaHudCode: {
    color: '#F8FAFC',
    fontSize: 11,
    fontWeight: '700',
    letterSpacing: 0.5,
  },
  scadaHudLatency: {
    color: '#64748B',
    fontSize: 10,
    marginLeft: 4,
  },
  scadaHudBadge: {
    backgroundColor: 'rgba(56, 189, 248, 0.12)',
    borderWidth: 1,
    borderColor: 'rgba(56, 189, 248, 0.3)',
    paddingHorizontal: 10,
    paddingVertical: 3,
    borderRadius: 6,
  },
  scadaHudBadgeTxt: {
    color: '#38BDF8',
    fontSize: 10,
    fontWeight: '700',
    letterSpacing: 0.4,
  },
  scadaHudRight: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  scadaHudAngle: {
    color: '#94A3B8',
    fontSize: 11,
    fontWeight: '600',
  },
  svgSimulatorWrapper: {
    backgroundColor: '#0B1120',
    overflow: 'hidden',
  },
  simulatorBottomBanner: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingHorizontal: 16,
    paddingVertical: 12,
    borderTopWidth: 1,
    flexWrap: 'wrap',
    gap: 8,
  },
  bannerOpen: {
    backgroundColor: 'rgba(22, 163, 74, 0.12)',
    borderTopColor: 'rgba(22, 163, 74, 0.35)',
  },
  bannerClosed: {
    backgroundColor: 'rgba(220, 38, 38, 0.12)',
    borderTopColor: 'rgba(220, 38, 38, 0.35)',
  },
  bannerEmergency: {
    backgroundColor: 'rgba(220, 38, 38, 0.22)',
    borderTopColor: '#DC2626',
  },
  bannerLeft: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
  },
  bannerRight: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  bannerDot: {
    width: 10,
    height: 10,
    borderRadius: 5,
  },
  bannerDotOpen: {
    backgroundColor: '#16A34A',
    shadowColor: '#16A34A',
    shadowOpacity: 0.8,
    shadowRadius: 6,
  },
  bannerDotClosed: {
    backgroundColor: '#DC2626',
    shadowColor: '#DC2626',
    shadowOpacity: 0.8,
    shadowRadius: 6,
  },
  bannerDotEmergency: {
    backgroundColor: '#EF4444',
    shadowColor: '#EF4444',
    shadowOpacity: 1,
    shadowRadius: 8,
  },
  bannerTitle: {
    fontSize: 13,
    fontWeight: '800',
    letterSpacing: 0.3,
  },
  bannerTitleOpen: {
    color: '#16A34A',
  },
  bannerTitleClosed: {
    color: '#DC2626',
  },
  bannerTitleEmergency: {
    color: '#DC2626',
    fontWeight: '900',
  },
  bannerBadgeTxt: {
    fontSize: 11,
    fontWeight: '700',
    letterSpacing: 0.5,
  },

  // SENSORS ANALYSIS SECTION STYLES
  sensorSectionHeaderRow: {
    marginBottom: 12,
  },
  sensorSectionHeading: {
    fontSize: 11,
    fontWeight: '800',
    color: 'rgba(0, 0, 0, 0.5)',
    letterSpacing: 0.7,
  },
  sensorSectionSubheading: {
    fontSize: 11,
    color: 'rgba(0, 0, 0, 0.45)',
    marginTop: 2,
  },
  sensorAnalysisGrid: {
    flexDirection: 'column',
    gap: 14,
    marginBottom: 20,
  },
  sensorAnalysisGridDesktop: {
    flexDirection: 'row',
  },
  sensorCard: {
    flex: 1,
    backgroundColor: '#FFFFFF',
    borderRadius: 16,
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.08)',
    padding: 16,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.04,
    shadowRadius: 6,
  },
  // QUICK BARRIER ACTION CONTROLS (DIRECTLY BELOW ANIMATION)
  quickBarrierActionsRow: {
    flexDirection: 'row',
    gap: 10,
    marginTop: 14,
    marginBottom: 16,
    flexWrap: 'wrap',
  },
  quickBarrierBtn: {
    flex: 1,
    minWidth: 140,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    paddingVertical: 12,
    paddingHorizontal: 14,
    borderRadius: 12,
    borderWidth: 1.5,
    backgroundColor: '#FFFFFF',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.05,
    shadowRadius: 3,
  },
  quickBtnOpen: {
    borderColor: 'rgba(22, 163, 74, 0.3)',
    backgroundColor: '#FFFFFF',
  },
  quickBtnOpenActive: {
    backgroundColor: 'rgba(22, 163, 74, 0.08)',
    borderColor: '#16A34A',
  },
  quickBtnClose: {
    borderColor: 'rgba(220, 38, 38, 0.3)',
    backgroundColor: '#FFFFFF',
  },
  quickBtnCloseActive: {
    backgroundColor: 'rgba(220, 38, 38, 0.08)',
    borderColor: '#DC2626',
  },
  quickBtnEmergencyLock: {
    backgroundColor: 'rgba(220, 38, 38, 0.08)',
    borderColor: '#EF4444',
  },
  quickBtnCycle: {
    borderColor: 'rgba(0, 102, 204, 0.3)',
    backgroundColor: '#FFFFFF',
  },
  quickBtnCycleActive: {
    backgroundColor: 'rgba(0, 102, 204, 0.08)',
    borderColor: '#0066CC',
  },
  quickBtnHelp: {
    borderColor: 'rgba(220, 38, 38, 0.4)',
    backgroundColor: '#FFFFFF',
  },
  quickBtnHelpActive: {
    backgroundColor: '#DC2626',
    borderColor: '#B91C1C',
  },
  quickBtnTextCol: {
    flex: 1,
  },
  quickBtnTitle: {
    fontSize: 12,
    fontWeight: '700',
    color: '#1D1D1F',
  },
  quickTitleOpen: {
    color: '#15803D',
  },
  quickTitleClose: {
    color: '#B91C1C',
  },
  quickTitleEmergency: {
    color: '#DC2626',
    fontWeight: '800',
  },
  quickTitleCycle: {
    color: '#0066CC',
  },
  quickTitleHelp: {
    color: '#DC2626',
  },
  quickTitleHelpAlert: {
    color: '#FFFFFF',
  },
  quickBtnSub: {
    fontSize: 10,
    color: 'rgba(0, 0, 0, 0.5)',
    marginTop: 2,
  },
  activePillGreen: {
    backgroundColor: '#16A34A',
    paddingHorizontal: 6,
    paddingVertical: 2,
    borderRadius: 4,
  },
  activePillRed: {
    backgroundColor: '#DC2626',
    paddingHorizontal: 6,
    paddingVertical: 2,
    borderRadius: 4,
  },
  activePillTxt: {
    color: '#FFFFFF',
    fontSize: 8,
    fontWeight: '800',
    letterSpacing: 0.5,
  },
  activePillEmergency: {
    backgroundColor: '#FFFFFF',
    paddingHorizontal: 6,
    paddingVertical: 2,
    borderRadius: 4,
  },
  activePillEmergencyTxt: {
    color: '#DC2626',
    fontSize: 8,
    fontWeight: '800',
    letterSpacing: 0.5,
  },

  // CUSTOMER ASSISTANCE ALERT BANNER
  customerAlertBanner: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    backgroundColor: '#DC2626',
    borderRadius: 14,
    padding: 14,
    marginBottom: 16,
    flexWrap: 'wrap',
    gap: 12,
    shadowColor: '#DC2626',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.25,
    shadowRadius: 10,
  },
  customerAlertLeft: {
    flexDirection: 'row',
    alignItems: 'center',
    flex: 1,
    minWidth: 260,
  },
  customerAlertTitle: {
    color: '#FFFFFF',
    fontSize: 13,
    fontWeight: '800',
    letterSpacing: 0.3,
  },
  customerAlertSub: {
    color: '#FEE2E2',
    fontSize: 11,
    marginTop: 2,
  },
  customerAlertDismissBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    backgroundColor: '#FFFFFF',
    paddingVertical: 8,
    paddingHorizontal: 14,
    borderRadius: 8,
  },
  customerAlertDismissTxt: {
    color: '#B91C1C',
    fontSize: 12,
    fontWeight: '700',
  },

  // SENSOR CARDS (PRESENCE & CAMERA)
  sensorCardActiveCar: {
    borderColor: 'rgba(2, 132, 199, 0.35)',
    backgroundColor: '#FFFFFF',
  },
  sensorCardClearCar: {
    borderColor: 'rgba(0, 0, 0, 0.08)',
    backgroundColor: '#FFFFFF',
  },
  sensorCardHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 14,
  },
  sensorCardTitleRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
  },
  sensorIconCircle: {
    width: 36,
    height: 36,
    borderRadius: 18,
    justifyContent: 'center',
    alignItems: 'center',
  },
  sensorCardTitle: {
    fontSize: 14,
    fontWeight: '700',
    color: '#1D1D1F',
  },
  sensorCardTech: {
    fontSize: 10,
    color: 'rgba(0, 0, 0, 0.45)',
    marginTop: 1,
  },
  sensorStatusBadge: {
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 6,
  },
  sensorStatusBadgeTxt: {
    fontSize: 9,
    fontWeight: '800',
    letterSpacing: 0.4,
  },
  sensorIconCarActive: {
    backgroundColor: 'rgba(2, 132, 199, 0.12)',
  },
  sensorIconCarClear: {
    backgroundColor: 'rgba(100, 116, 139, 0.1)',
  },
  sensorIconCamera: {
    backgroundColor: 'rgba(0, 102, 204, 0.12)',
  },
  badgeCarPresent: {
    backgroundColor: 'rgba(2, 132, 199, 0.12)',
    borderWidth: 1,
    borderColor: 'rgba(2, 132, 199, 0.3)',
  },
  badgeTxtCarPresent: {
    color: '#0284C7',
    fontSize: 9,
    fontWeight: '800',
    letterSpacing: 0.4,
  },
  badgeCarClear: {
    backgroundColor: 'rgba(100, 116, 139, 0.1)',
  },
  badgeTxtCarClear: {
    color: '#64748B',
    fontSize: 9,
    fontWeight: '800',
    letterSpacing: 0.4,
  },
  badgeCamera: {
    backgroundColor: 'rgba(0, 102, 204, 0.1)',
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 6,
  },
  badgeCameraTxt: {
    color: '#0066CC',
    fontSize: 9,
    fontWeight: '800',
    letterSpacing: 0.5,
  },

  // CAR PRESENCE SENSOR DISPLAY
  carPresenceDisplayBox: {
    backgroundColor: '#F8F8FA',
    borderRadius: 12,
    padding: 12,
    marginBottom: 12,
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.05)',
  },
  carPresenceStatusLine: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    marginBottom: 10,
    paddingBottom: 8,
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(0, 0, 0, 0.06)',
  },
  carPresenceIndicatorDot: {
    width: 8,
    height: 8,
    borderRadius: 4,
  },
  dotCarPresent: {
    backgroundColor: '#0284C7',
  },
  dotCarClear: {
    backgroundColor: '#94A3B8',
  },
  carPresenceStatusHeading: {
    fontSize: 11,
    fontWeight: '800',
    letterSpacing: 0.5,
  },
  txtCarPresent: {
    color: '#0284C7',
  },
  txtCarClear: {
    color: '#64748B',
  },
  carPresenceMetricsTable: {
    gap: 6,
  },
  carPresenceRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  carPresenceLabel: {
    fontSize: 9,
    fontWeight: '700',
    color: 'rgba(0, 0, 0, 0.45)',
    letterSpacing: 0.4,
  },
  carPresenceValue: {
    fontSize: 11,
    color: '#1D1D1F',
    fontWeight: '600',
  },
  simCarPresenceBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
    paddingVertical: 11,
    borderRadius: 10,
  },
  simCarBtnArrive: {
    backgroundColor: '#0284C7',
  },
  simCarBtnClear: {
    backgroundColor: '#475569',
  },
  simCarPresenceBtnTxt: {
    color: '#FFFFFF',
    fontSize: 12,
    fontWeight: '700',
  },

  sensorExplanationTxt: {
    fontSize: 11,
    color: 'rgba(0, 0, 0, 0.6)',
    lineHeight: 16,
    marginBottom: 14,
  },

  // CAMERA LPR ANALYSIS VIEWPORT STYLES
  cameraViewfinder: {
    position: 'relative',
    height: 140,
    backgroundColor: '#090D16',
    borderRadius: 12,
    borderWidth: 1,
    borderColor: '#1E293B',
    padding: 10,
    justifyContent: 'space-between',
    marginBottom: 12,
    overflow: 'hidden',
  },
  cameraFlashOverlay: {
    ...StyleSheet.absoluteFillObject,
    backgroundColor: '#FFFFFF',
    zIndex: 99,
  },
  vfBracketTL: {
    position: 'absolute',
    top: 6,
    left: 6,
    width: 12,
    height: 12,
    borderTopWidth: 2,
    borderLeftWidth: 2,
    borderColor: '#38BDF8',
  },
  vfBracketTR: {
    position: 'absolute',
    top: 6,
    right: 6,
    width: 12,
    height: 12,
    borderTopWidth: 2,
    borderRightWidth: 2,
    borderColor: '#38BDF8',
  },
  vfBracketBL: {
    position: 'absolute',
    bottom: 6,
    left: 6,
    width: 12,
    height: 12,
    borderBottomWidth: 2,
    borderLeftWidth: 2,
    borderColor: '#38BDF8',
  },
  vfBracketBR: {
    position: 'absolute',
    bottom: 6,
    right: 6,
    width: 12,
    height: 12,
    borderBottomWidth: 2,
    borderRightWidth: 2,
    borderColor: '#38BDF8',
  },
  vfHeaderBar: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  vfRecRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  vfRecTxt: {
    color: '#94A3B8',
    fontSize: 9,
    fontWeight: '700',
    fontFamily: Platform.OS === 'ios' ? 'Courier' : 'monospace',
  },
  vfExpoTxt: {
    color: '#64748B',
    fontSize: 9,
    fontFamily: Platform.OS === 'ios' ? 'Courier' : 'monospace',
  },
  cameraDetectionsContainer: {
    gap: 4,
  },
  cameraBoxPlate: {
    backgroundColor: 'rgba(34, 197, 94, 0.12)',
    borderWidth: 1,
    borderColor: '#22C55E',
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: 4,
    alignSelf: 'flex-start',
  },
  cameraBoxTagPlate: {
    color: '#22C55E',
    fontSize: 10,
    fontWeight: '800',
    fontFamily: Platform.OS === 'ios' ? 'Courier' : 'monospace',
  },
  cameraBoxSubPlate: {
    color: '#E2E8F0',
    fontSize: 9,
    marginTop: 1,
  },
  cameraBoxVehicle: {
    backgroundColor: 'rgba(56, 189, 248, 0.1)',
    borderWidth: 1,
    borderColor: '#38BDF8',
    paddingHorizontal: 8,
    paddingVertical: 2,
    borderRadius: 4,
    alignSelf: 'flex-start',
  },
  cameraBoxTagVehicle: {
    color: '#38BDF8',
    fontSize: 9,
    fontWeight: '700',
  },
  cameraBoxSubVehicle: {
    color: '#94A3B8',
    fontSize: 8,
  },
  cameraStandbyBox: {
    backgroundColor: 'rgba(148, 163, 184, 0.1)',
    borderWidth: 1,
    borderColor: 'rgba(148, 163, 184, 0.3)',
    borderRadius: 6,
    padding: 8,
  },
  cameraStandbyTxt: {
    color: '#94A3B8',
    fontSize: 9,
    fontWeight: '700',
    fontFamily: Platform.OS === 'ios' ? 'Courier' : 'monospace',
  },
  cameraStandbySub: {
    color: '#64748B',
    fontSize: 8,
    marginTop: 2,
  },
  cameraSpecsRow: {
    flexDirection: 'row',
    gap: 8,
    marginBottom: 12,
  },
  camSpecTile: {
    flex: 1,
    backgroundColor: '#F8F8FA',
    borderRadius: 8,
    padding: 8,
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.05)',
  },
  camSpecLabel: {
    fontSize: 8,
    fontWeight: '700',
    color: 'rgba(0, 0, 0, 0.45)',
  },
  camSpecVal: {
    fontSize: 11,
    fontWeight: '700',
    color: '#1D1D1F',
    marginTop: 2,
  },
  saveCaptureBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
    paddingVertical: 11,
    borderRadius: 10,
    backgroundColor: '#0066CC',
    shadowColor: '#0066CC',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.2,
    shadowRadius: 4,
  },
  saveCaptureBtnTxt: {
    color: '#FFFFFF',
    fontSize: 12,
    fontWeight: '700',
  },
  savedCapturesContainer: {
    marginTop: 10,
    paddingTop: 8,
    borderTopWidth: 1,
    borderTopColor: 'rgba(0, 0, 0, 0.06)',
  },
  savedCapturesLabel: {
    fontSize: 9,
    fontWeight: '700',
    color: 'rgba(0, 0, 0, 0.45)',
    marginBottom: 6,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
  },
  savedCapturesScroll: {
    flexDirection: 'row',
  },
  savedCaptureChip: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    backgroundColor: '#F1F5F9',
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 6,
    marginRight: 6,
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.05)',
  },
  savedCaptureChipTxt: {
    fontSize: 9,
    fontFamily: Platform.OS === 'ios' ? 'Courier' : 'monospace',
    color: '#334155',
    fontWeight: '600',
  },
  telemetrySection: {
    backgroundColor: '#F8F8FA',
    borderRadius: 14,
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.06)',
    padding: 16,
    marginBottom: 20,
  },
  telemetryHeaderTitle: {
    fontSize: 10,
    fontWeight: '800',
    color: 'rgba(0, 0, 0, 0.5)',
    letterSpacing: 0.6,
    marginBottom: 12,
  },
  telemetryGrid: {
    flexDirection: 'row',
    gap: 10,
    flexWrap: 'wrap',
  },
  telemetryTile: {
    flex: 1,
    minWidth: 140,
    backgroundColor: '#FFFFFF',
    borderRadius: 10,
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.06)',
    padding: 12,
  },
  telemetryTileHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 6,
  },
  telemetryPinName: {
    fontSize: 9,
    fontWeight: '700',
    color: 'rgba(0, 0, 0, 0.45)',
  },
  telemetryStatusPillGreen: {
    backgroundColor: 'rgba(22, 163, 74, 0.12)',
    paddingHorizontal: 6,
    paddingVertical: 2,
    borderRadius: 4,
  },
  telemetryStatusTxtGreen: {
    color: '#16A34A',
    fontSize: 8,
    fontWeight: '800',
  },
  telemetryStatusPillNeutral: {
    backgroundColor: 'rgba(0, 0, 0, 0.06)',
    paddingHorizontal: 6,
    paddingVertical: 2,
    borderRadius: 4,
  },
  telemetryStatusTxtNeutral: {
    color: 'rgba(0, 0, 0, 0.5)',
    fontSize: 8,
    fontWeight: '800',
  },
  telemetryStatusPillBlue: {
    backgroundColor: 'rgba(0, 102, 204, 0.12)',
    paddingHorizontal: 6,
    paddingVertical: 2,
    borderRadius: 4,
  },
  telemetryStatusTxtBlue: {
    color: '#0066CC',
    fontSize: 8,
    fontWeight: '800',
  },
  telemetryMetricMain: {
    fontSize: 13,
    fontWeight: '700',
    color: '#1D1D1F',
    marginBottom: 2,
  },
  telemetryMetricSub: {
    fontSize: 10,
    color: 'rgba(0, 0, 0, 0.5)',
  },
  healthRibbon: {
    marginTop: 12,
    paddingTop: 10,
    borderTopWidth: 1,
    borderTopColor: 'rgba(0, 0, 0, 0.06)',
  },
  healthRibbonTxt: {
    fontSize: 10,
    color: 'rgba(0, 0, 0, 0.5)',
    textAlign: 'center',
  },
  terminalBox: {
    backgroundColor: '#090D16',
    borderRadius: 14,
    borderWidth: 1,
    borderColor: '#1E293B',
    padding: 14,
  },
  terminalHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 10,
  },
  terminalHeaderLeft: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  terminalLedDot: {
    width: 7,
    height: 7,
    borderRadius: 3.5,
    backgroundColor: '#22C55E',
  },
  terminalTitle: {
    color: '#94A3B8',
    fontSize: 10,
    fontWeight: '700',
    letterSpacing: 0.6,
  },
  terminalClearBtn: {
    paddingHorizontal: 8,
    paddingVertical: 3,
    borderRadius: 4,
    backgroundColor: '#1E293B',
  },
  terminalClearTxt: {
    color: '#94A3B8',
    fontSize: 10,
    fontWeight: '600',
  },
  terminalScroll: {
    maxHeight: 130,
  },
  terminalRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    paddingVertical: 3,
  },
  terminalTime: {
    color: '#64748B',
    fontSize: 10,
    fontFamily: Platform.OS === 'ios' ? 'Courier' : 'monospace',
  },
  terminalTagPill: {
    paddingHorizontal: 6,
    paddingVertical: 1,
    borderRadius: 4,
    backgroundColor: 'rgba(255, 255, 255, 0.08)',
  },
  terminalTagSuccess: {
    backgroundColor: 'rgba(34, 197, 94, 0.15)',
  },
  terminalTagWarn: {
    backgroundColor: 'rgba(234, 179, 8, 0.15)',
  },
  terminalTagError: {
    backgroundColor: 'rgba(239, 68, 68, 0.15)',
  },
  terminalTagTxt: {
    color: '#94A3B8',
    fontSize: 9,
    fontWeight: '700',
    fontFamily: Platform.OS === 'ios' ? 'Courier' : 'monospace',
  },
  terminalTagTxtSuccess: {
    color: '#22C55E',
  },
  terminalTagTxtWarn: {
    color: '#EAB308',
  },
  terminalTagTxtError: {
    color: '#EF4444',
  },
  terminalMsg: {
    color: '#CBD5E1',
    fontSize: 11,
    flex: 1,
    fontFamily: Platform.OS === 'ios' ? 'Courier' : 'monospace',
  },
  // LIQUIDACIONES & AUDITORÍA CONTABLE STYLES
  financeHeaderRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    marginBottom: 16,
    flexWrap: 'wrap',
    gap: 12,
  },
  financeTitleRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    marginBottom: 4,
  },
  financeActionBtnsRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    flexWrap: 'wrap',
  },
  finActionBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    paddingVertical: 8,
    paddingHorizontal: 12,
    borderRadius: 8,
    backgroundColor: '#FFFFFF',
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.1)',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.04,
    shadowRadius: 2,
  },
  finActionBtnClose: {
    backgroundColor: '#0066CC',
    borderColor: '#0052A3',
  },
  finActionBtnTxt: {
    fontSize: 11,
    fontWeight: '700',
    color: '#0066CC',
  },
  finPeriodSelectorRow: {
    flexDirection: 'row',
    gap: 8,
    marginBottom: 18,
    flexWrap: 'wrap',
  },
  finPeriodPill: {
    paddingVertical: 6,
    paddingHorizontal: 12,
    borderRadius: 8,
    backgroundColor: 'rgba(0, 0, 0, 0.04)',
    borderWidth: 1,
    borderColor: 'transparent',
  },
  finPeriodPillActive: {
    backgroundColor: '#FFFFFF',
    borderColor: '#0066CC',
    shadowColor: '#0066CC',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.1,
    shadowRadius: 3,
  },
  finPeriodTxt: {
    fontSize: 11,
    fontWeight: '600',
    color: 'rgba(0, 0, 0, 0.55)',
  },
  finPeriodTxtActive: {
    color: '#0066CC',
    fontWeight: '700',
  },

  // 4 TOP FINANCIAL KPIS GRID
  finKpiGrid: {
    flexDirection: 'column',
    gap: 12,
    marginBottom: 20,
  },
  finKpiGridDesktop: {
    flexDirection: 'row',
  },
  finKpiCard: {
    flex: 1,
    backgroundColor: '#FFFFFF',
    borderRadius: 14,
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.08)',
    padding: 14,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.03,
    shadowRadius: 4,
  },
  finKpiHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 8,
  },
  finKpiLabel: {
    fontSize: 9,
    fontWeight: '800',
    color: 'rgba(0, 0, 0, 0.45)',
    letterSpacing: 0.5,
  },
  finKpiIconWrapGreen: {
    width: 26,
    height: 26,
    borderRadius: 13,
    backgroundColor: 'rgba(22, 163, 74, 0.12)',
    justifyContent: 'center',
    alignItems: 'center',
  },
  finKpiIconWrapBlue: {
    width: 26,
    height: 26,
    borderRadius: 13,
    backgroundColor: 'rgba(0, 102, 204, 0.12)',
    justifyContent: 'center',
    alignItems: 'center',
  },
  finKpiIconWrapIndigo: {
    width: 26,
    height: 26,
    borderRadius: 13,
    backgroundColor: 'rgba(79, 70, 229, 0.12)',
    justifyContent: 'center',
    alignItems: 'center',
  },
  finKpiIconWrapPurple: {
    width: 26,
    height: 26,
    borderRadius: 13,
    backgroundColor: 'rgba(124, 58, 237, 0.12)',
    justifyContent: 'center',
    alignItems: 'center',
  },
  finKpiHuge: {
    fontSize: 24,
    fontWeight: '800',
    color: '#1D1D1F',
    letterSpacing: -0.5,
  },
  finKpiCurrency: {
    fontSize: 10,
    color: 'rgba(0, 0, 0, 0.5)',
    fontWeight: '600',
    marginTop: 2,
    marginBottom: 8,
  },
  finKpiSubRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingTop: 8,
    borderTopWidth: 1,
    borderTopColor: 'rgba(0, 0, 0, 0.05)',
  },
  finTrendBadgeGreen: {
    backgroundColor: 'rgba(22, 163, 74, 0.12)',
    paddingHorizontal: 6,
    paddingVertical: 2,
    borderRadius: 4,
  },
  finTrendBadgeTxtGreen: {
    color: '#16A34A',
    fontSize: 9,
    fontWeight: '700',
  },
  finRateBadge: {
    backgroundColor: 'rgba(0, 102, 204, 0.1)',
    paddingHorizontal: 6,
    paddingVertical: 2,
    borderRadius: 4,
  },
  finRateBadgeTxt: {
    color: '#0066CC',
    fontSize: 9,
    fontWeight: '700',
  },
  finPeakBadge: {
    backgroundColor: 'rgba(79, 70, 229, 0.1)',
    paddingHorizontal: 6,
    paddingVertical: 2,
    borderRadius: 4,
  },
  finPeakBadgeTxt: {
    color: '#4F46E5',
    fontSize: 9,
    fontWeight: '700',
  },
  finTaxBreakdownTxt: {
    fontSize: 9,
    color: 'rgba(0, 0, 0, 0.55)',
    fontWeight: '600',
  },
  finKpiFootnote: {
    fontSize: 9,
    color: 'rgba(0, 0, 0, 0.45)',
  },

  // STAY DURATION BAR CHART
  stayChartCard: {
    backgroundColor: '#FFFFFF',
    borderRadius: 16,
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.08)',
    padding: 16,
    marginBottom: 20,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.03,
    shadowRadius: 6,
  },
  stayChartHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    marginBottom: 16,
    flexWrap: 'wrap',
    gap: 8,
  },
  stayChartTitleRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    marginBottom: 2,
  },
  stayChartTitle: {
    fontSize: 14,
    fontWeight: '700',
    color: '#1D1D1F',
  },
  stayChartSub: {
    fontSize: 11,
    color: 'rgba(0, 0, 0, 0.45)',
  },
  stayChartPill: {
    backgroundColor: 'rgba(0, 102, 204, 0.08)',
    paddingHorizontal: 10,
    paddingVertical: 4,
    borderRadius: 6,
    borderWidth: 1,
    borderColor: 'rgba(0, 102, 204, 0.2)',
  },
  stayChartPillTxt: {
    color: '#0066CC',
    fontSize: 9,
    fontWeight: '800',
    letterSpacing: 0.5,
  },
  chartBarsContainer: {
    gap: 12,
    marginBottom: 14,
  },
  chartBarRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
  },
  chartBarLabelCol: {
    width: 140,
  },
  chartBarRangeRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  chartBarRange: {
    fontSize: 12,
    fontWeight: '700',
    color: '#1D1D1F',
  },
  peakTag: {
    backgroundColor: '#0066CC',
    paddingHorizontal: 4,
    paddingVertical: 1,
    borderRadius: 3,
  },
  peakTagTxt: {
    color: '#FFFFFF',
    fontSize: 7,
    fontWeight: '800',
    letterSpacing: 0.4,
  },
  chartBarSub: {
    fontSize: 10,
    color: 'rgba(0, 0, 0, 0.45)',
    marginTop: 1,
  },
  chartBarTrack: {
    flex: 1,
    height: 14,
    backgroundColor: '#F1F5F9',
    borderRadius: 7,
    overflow: 'hidden',
  },
  chartBarFill: {
    height: '100%',
    borderRadius: 7,
  },
  chartBarMetricsCol: {
    width: 130,
    alignItems: 'flex-end',
  },
  chartBarPct: {
    fontSize: 13,
    fontWeight: '800',
  },
  chartBarCount: {
    fontSize: 9,
    color: 'rgba(0, 0, 0, 0.45)',
    marginTop: 1,
  },
  stayChartCallout: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    gap: 8,
    backgroundColor: 'rgba(22, 163, 74, 0.08)',
    borderWidth: 1,
    borderColor: 'rgba(22, 163, 74, 0.25)',
    padding: 12,
    borderRadius: 10,
  },
  stayChartCalloutTxt: {
    flex: 1,
    fontSize: 11,
    color: '#166534',
    lineHeight: 16,
  },

  // ACCOUNTING AUDIT GRID
  accountingGrid: {
    flexDirection: 'column',
    gap: 14,
    marginBottom: 20,
  },
  accountingGridDesktop: {
    flexDirection: 'row',
  },
  accountingCard: {
    flex: 1,
    backgroundColor: '#FFFFFF',
    borderRadius: 16,
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.08)',
    padding: 16,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.03,
    shadowRadius: 6,
  },
  accHeaderRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    marginBottom: 14,
  },
  accIconCircleBlue: {
    width: 32,
    height: 32,
    borderRadius: 16,
    backgroundColor: 'rgba(0, 102, 204, 0.1)',
    justifyContent: 'center',
    alignItems: 'center',
  },
  accIconCirclePurple: {
    width: 32,
    height: 32,
    borderRadius: 16,
    backgroundColor: 'rgba(124, 58, 237, 0.1)',
    justifyContent: 'center',
    alignItems: 'center',
  },
  accCardTitle: {
    fontSize: 13,
    fontWeight: '700',
    color: '#1D1D1F',
  },
  accCardSub: {
    fontSize: 10,
    color: 'rgba(0, 0, 0, 0.45)',
    marginTop: 1,
  },
  accBadgeGreen: {
    backgroundColor: 'rgba(22, 163, 74, 0.12)',
    paddingHorizontal: 6,
    paddingVertical: 3,
    borderRadius: 4,
  },
  accBadgeTxtGreen: {
    color: '#16A34A',
    fontSize: 8,
    fontWeight: '800',
  },
  accBadgePurple: {
    backgroundColor: 'rgba(124, 58, 237, 0.12)',
    paddingHorizontal: 6,
    paddingVertical: 3,
    borderRadius: 4,
  },
  accBadgeTxtPurple: {
    color: '#7C3AED',
    fontSize: 8,
    fontWeight: '800',
  },
  accTable: {
    gap: 8,
  },
  accRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  accRowLabel: {
    fontSize: 11,
    color: 'rgba(0, 0, 0, 0.6)',
  },
  accRowVal: {
    fontSize: 12,
    fontWeight: '600',
    color: '#1D1D1F',
  },
  accDivider: {
    height: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.08)',
    marginVertical: 4,
  },
  accRowHighlight: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    backgroundColor: 'rgba(22, 163, 74, 0.08)',
    padding: 8,
    borderRadius: 6,
  },
  accHighlightLabel: {
    fontSize: 10,
    fontWeight: '800',
    color: '#15803D',
    letterSpacing: 0.4,
  },
  accHighlightVal: {
    fontSize: 14,
    fontWeight: '800',
    color: '#15803D',
  },
  accChannelsBox: {
    gap: 10,
    marginBottom: 12,
  },
  channelRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingBottom: 8,
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(0, 0, 0, 0.05)',
  },
  channelName: {
    fontSize: 11,
    fontWeight: '700',
    color: '#1D1D1F',
  },
  channelSub: {
    fontSize: 9,
    color: 'rgba(0, 0, 0, 0.45)',
    marginTop: 1,
  },
  channelAmount: {
    fontSize: 11,
    fontWeight: '700',
    color: '#0066CC',
  },
  satComplianceBox: {
    backgroundColor: '#F8F8FA',
    borderRadius: 8,
    padding: 10,
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.05)',
  },
  satComplianceTitle: {
    fontSize: 10,
    fontWeight: '700',
    color: '#1D1D1F',
    marginBottom: 4,
  },
  satComplianceLine: {
    fontSize: 9,
    color: 'rgba(0, 0, 0, 0.55)',
    marginVertical: 1,
  },

  // REAL-TIME GENERAL LEDGER
  ledgerSection: {
    backgroundColor: '#FFFFFF',
    borderRadius: 16,
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.08)',
    padding: 16,
  },
  ledgerHeaderRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 14,
    flexWrap: 'wrap',
    gap: 8,
  },
  ledgerTitle: {
    fontSize: 14,
    fontWeight: '700',
    color: '#1D1D1F',
  },
  ledgerSub: {
    fontSize: 11,
    color: 'rgba(0, 0, 0, 0.45)',
  },
  ledgerCountBadge: {
    backgroundColor: 'rgba(0, 0, 0, 0.05)',
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 6,
  },
  ledgerCountTxt: {
    fontSize: 9,
    fontWeight: '700',
    color: 'rgba(0, 0, 0, 0.6)',
  },
  ledgerList: {
    gap: 8,
  },
  ledgerRowCard: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    backgroundColor: '#F8F8FA',
    borderRadius: 10,
    padding: 12,
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.05)',
    flexWrap: 'wrap',
    gap: 10,
  },
  ledgerPlateCol: {
    width: 140,
  },
  ledgerPlateBadge: {
    backgroundColor: '#1E293B',
    paddingHorizontal: 6,
    paddingVertical: 2,
    borderRadius: 4,
    alignSelf: 'flex-start',
    marginBottom: 3,
  },
  ledgerPlateTxt: {
    color: '#38BDF8',
    fontSize: 10,
    fontWeight: '800',
    fontFamily: Platform.OS === 'ios' ? 'Courier' : 'monospace',
  },
  ledgerVehicleTxt: {
    fontSize: 10,
    color: 'rgba(0, 0, 0, 0.5)',
  },
  ledgerDurationCol: {
    width: 110,
  },
  ledgerDurationTxt: {
    fontSize: 11,
    fontWeight: '700',
    color: '#1D1D1F',
  },
  ledgerTimesSub: {
    fontSize: 9,
    color: 'rgba(0, 0, 0, 0.45)',
    marginTop: 1,
  },
  ledgerMethodCol: {
    width: 110,
  },
  ledgerMethodTxt: {
    fontSize: 11,
    fontWeight: '600',
    color: '#0066CC',
  },
  ledgerCfdiTxt: {
    fontSize: 9,
    color: 'rgba(0, 0, 0, 0.4)',
    fontFamily: Platform.OS === 'ios' ? 'Courier' : 'monospace',
    marginTop: 1,
  },
  ledgerAmountCol: {
    alignItems: 'flex-end',
    width: 130,
  },
  ledgerAmountTxt: {
    fontSize: 13,
    fontWeight: '800',
    color: '#15803D',
  },
  ledgerTaxSub: {
    fontSize: 8,
    color: 'rgba(0, 0, 0, 0.45)',
    marginTop: 1,
  },
  ledgerStatusPill: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    backgroundColor: 'rgba(22, 163, 74, 0.12)',
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 6,
  },
  ledgerStatusTxt: {
    color: '#16A34A',
    fontSize: 10,
    fontWeight: '700',
  },
  alertCard: {
    backgroundColor: '#FFF2F2',
    borderWidth: 1,
    borderColor: 'rgba(220, 38, 38, 0.25)',
    borderRadius: 14,
    padding: 16,
  },
  alertHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    marginBottom: 10,
  },
  alertIcon: {
    fontSize: 18,
    color: '#DC2626',
  },
  alertHeading: {
    color: '#DC2626',
    fontSize: 14,
    fontWeight: '700',
  },
  alertBody: {
    color: 'rgba(0, 0, 0, 0.7)',
    fontSize: 11,
  },
  confidenceText: {
    color: '#DC2626',
    fontSize: 11,
    fontWeight: '600',
    marginBottom: 14,
  },
  alertBtnGroup: {
    flexDirection: 'row',
    gap: 10,
  },
  emptyAlerts: {
    padding: 24,
    alignItems: 'center',
  },
  emptyAlertsText: {
    color: '#16A34A',
    fontSize: 14,
    fontWeight: '600',
  },
  auditTable: {
    gap: 8,
  },
  auditRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    backgroundColor: '#F8F8FA',
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.06)',
    padding: 12,
    borderRadius: 10,
  },
  auditTime: {
    color: 'rgba(0, 0, 0, 0.45)',
    fontSize: 11,
  },
  auditBay: {
    color: '#1D1D1F',
    fontWeight: '600',
    fontSize: 11,
  },
  auditEvent: {
    color: '#0066CC',
    fontSize: 11,
  },
});
