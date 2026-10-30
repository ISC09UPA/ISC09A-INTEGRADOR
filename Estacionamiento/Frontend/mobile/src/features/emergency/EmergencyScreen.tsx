import React, { useState, useEffect, useRef } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  TextInput,
  Alert,
  useWindowDimensions,
  Animated,
  Platform,
} from 'react-native';
import Svg, { Rect, Circle, Line, Polyline, Path, G, Polygon, Defs, RadialGradient, Stop, Text as SvgText } from 'react-native-svg';
import { useEmergencyStore, EmergencyType, LedStrobeMode } from '@/store/emergencyStore';
import { BentoCard } from '@/components/ui/BentoCard';
import { PillButton } from '@/components/ui/PillButton';
import {
  IconAlertTriangle,
  IconLock,
  IconUnlock,
  IconShield,
  IconGate,
  IconVolume2,
  IconVolumeX,
  IconRadio,
  IconActivity,
  IconCheck,
  IconX,
  IconClock,
  IconPhone,
  IconSiren,
} from '@/components/ui/Icons';

export interface EmergencyScreenProps {
  onDismiss?: () => void;
  isEmbedded?: boolean;
}

export const EmergencyScreen: React.FC<EmergencyScreenProps> = ({ onDismiss, isEmbedded = false }) => {
  const {
    isEmergencyActive,
    emergencyType,
    threatLevel,
    barrierLocked,
    redLedsActive,
    authoritiesAlerted,
    sirenActive,
    elapsedSeconds,
    ledStrobeMode,
    dispatchedAgencies,
    emergencyLogs,
    triggerEmergency,
    disarmEmergency,
    toggleSiren,
    setLedStrobeMode,
    confirmAuthorityArrival,
    tickElapsed,
  } = useEmergencyStore();

  const [pinInput, setPinInput] = useState('');
  const [showPinModal, setShowPinModal] = useState(false);
  const [selectedProtocol, setSelectedProtocol] = useState<EmergencyType>(emergencyType || 'intrusion');
  const [showTelemetryModal, setShowTelemetryModal] = useState(false);

  // Animaciones de balizas y estrobos
  const pulseAnim = useRef(new Animated.Value(1)).current;
  const strobeFlashAnim = useRef(new Animated.Value(0)).current;

  const { width } = useWindowDimensions();
  const isDesktop = width >= 768;

  // Temporizador de duración de incidente
  useEffect(() => {
    let timer: any = null;
    if (isEmergencyActive) {
      timer = setInterval(() => {
        tickElapsed();
      }, 1000);
    }
    return () => {
      if (timer) clearInterval(timer);
    };
  }, [isEmergencyActive, tickElapsed]);

  // Animación estroboscópica continua (3 Hz)
  useEffect(() => {
    if (!isEmergencyActive && !redLedsActive) return;

    const strobeLoop = Animated.loop(
      Animated.sequence([
        Animated.timing(strobeFlashAnim, {
          toValue: 1,
          duration: 160,
          useNativeDriver: Platform.OS !== 'web',
        }),
        Animated.timing(strobeFlashAnim, {
          toValue: 0,
          duration: 160,
          useNativeDriver: Platform.OS !== 'web',
        }),
      ])
    );

    const pulseLoop = Animated.loop(
      Animated.sequence([
        Animated.timing(pulseAnim, {
          toValue: 1.08,
          duration: 400,
          useNativeDriver: Platform.OS !== 'web',
        }),
        Animated.timing(pulseAnim, {
          toValue: 0.95,
          duration: 400,
          useNativeDriver: Platform.OS !== 'web',
        }),
      ])
    );

    strobeLoop.start();
    pulseLoop.start();

    return () => {
      strobeLoop.stop();
      pulseLoop.stop();
    };
  }, [isEmergencyActive, redLedsActive]);

  const formatElapsed = (sec: number) => {
    const mins = Math.floor(sec / 60);
    const remainingSec = sec % 60;
    return `${mins.toString().padStart(2, '0')}:${remainingSec.toString().padStart(2, '0')}`;
  };

  const handleTrigger = (type: EmergencyType) => {
    setSelectedProtocol(type);
    triggerEmergency(type);
  };

  const handleDisarmSubmit = async () => {
    if (!pinInput.trim()) {
      Alert.alert('PIN Requerido', 'Por favor ingrese el código de autorización del supervisor.');
      return;
    }
    const result = await disarmEmergency(pinInput);
    if (result.success) {
      setShowPinModal(false);
      setPinInput('');
      Alert.alert('Sistema Restablecido', 'El código de emergencia ha sido desactivado. Las plumas y sensores vuelven a operación estándar.');
    } else {
      Alert.alert('Acceso Denegado', result.error || 'PIN no válido.');
    }
  };

  return (
    <ScrollView
      style={[styles.container, isEmergencyActive && styles.containerEmergencyActive]}
      contentContainerStyle={[
        styles.scrollContent,
        isDesktop && styles.desktopContent,
      ]}
      showsVerticalScrollIndicator={false}
    >
      {/* 1. TOP HERO BANNER: ESTADO DE EMERGENCIA */}
      <View style={[styles.heroCard, isEmergencyActive ? styles.heroCardActive : styles.heroCardStandby]}>
        <View style={styles.heroTopRow}>
          <View style={styles.heroBadgeRow}>
            <View style={[styles.pulsingDot, isEmergencyActive ? styles.dotRed : styles.dotGreen]} />
            <Text style={[styles.heroBadgeText, isEmergencyActive ? styles.textRedLight : styles.textGreenLight]}>
              {isEmergencyActive ? 'PROTOCOLO DE EMERGENCIA ACTIVO' : 'SISTEMA DE SEGURIDAD EN ESPERA'}
            </Text>
          </View>

          <View style={styles.heroActionsRow}>
            {/* Botón Mute / Sonido de Sirena */}
            {isEmergencyActive && (
              <TouchableOpacity
                style={[styles.sirenBtn, sirenActive ? styles.sirenBtnActive : styles.sirenBtnMuted]}
                onPress={toggleSiren}
                activeOpacity={0.7}
              >
                {sirenActive ? (
                  <IconVolume2 size={16} color="#FFFFFF" />
                ) : (
                  <IconVolumeX size={16} color="rgba(255,255,255,0.7)" />
                )}
                <Text style={styles.sirenBtnText}>{sirenActive ? 'Sirena ON' : 'Sirena Mute'}</Text>
              </TouchableOpacity>
            )}

            {onDismiss && (
              <TouchableOpacity style={styles.closeBtn} onPress={onDismiss}>
                <IconX size={18} color="#FFFFFF" />
              </TouchableOpacity>
            )}
          </View>
        </View>

        <View style={styles.heroMainRow}>
          <View style={{ flex: 1 }}>
            <Text style={styles.heroTitle}>
              {isEmergencyActive ? '🚨 CÓDIGO ROJO AEROPORTUARIO' : 'Centro de Control de Emergencias'}
            </Text>
            <Text style={styles.heroSubtitle}>
              {isEmergencyActive
                ? 'Plumas enclavadas · Sistema de subida/cerrado bloqueado · Alerta C5/GN activa · Balizas rojas encendidas'
                : 'Monitoreo perimetral, despacho inter-agencias y enclavamiento de barreras mecánicas.'}
            </Text>
          </View>

          {isEmergencyActive && (
            <View style={styles.hudClockContainer}>
              <Text style={styles.hudClockLabel}>DURACIÓN INCIDENTE</Text>
              <Text style={styles.hudClockDigits}>{formatElapsed(elapsedSeconds)}</Text>
              <Text style={styles.hudClockSec}>minutos : segundos</Text>
            </View>
          )}
        </View>

        {/* Big Action Callouts */}
        {!isEmergencyActive ? (
          <View style={styles.triggerGroupRow}>
            <TouchableOpacity
              style={[styles.bigTriggerBtn, styles.btnRed]}
              onPress={() => handleTrigger('intrusion')}
              activeOpacity={0.8}
            >
              <IconAlertTriangle size={22} color="#FFFFFF" strokeWidth={2.2} />
              <View style={{ flex: 1 }}>
                <Text style={styles.bigTriggerTitle}>ACTIVAR CÓDIGO ROJO (PARO TOTAL)</Text>
                <Text style={styles.bigTriggerSub}>
                  Bloquea elevación/cierre de pluma, alerta a autoridades y enciende todos los LEDs rojos.
                </Text>
              </View>
            </TouchableOpacity>
          </View>
        ) : (
          <View style={styles.activeEmergencyBannerRow}>
            <View style={styles.activeAlertTextCol}>
              <Text style={styles.activeAlertMain}>AMENAZA: {emergencyType.toUpperCase()}</Text>
              <Text style={styles.activeAlertSub}>
                Enclavamiento forzado activo en Carril 01 y 02. No se permite maniobra de pluma.
              </Text>
            </View>

            <TouchableOpacity
              style={styles.disarmTriggerBtn}
              onPress={() => setShowPinModal(true)}
              activeOpacity={0.85}
            >
              <IconUnlock size={18} color="#15803D" strokeWidth={2.2} />
              <Text style={styles.disarmTriggerText}>DESARMAR Y RESTABLECER</Text>
            </TouchableOpacity>
          </View>
        )}
      </View>

      {/* 2. TRES PILARES CRÍTICOS: BLOQUEO BARRERA, LEDS ROJOS, AUTORIDADES */}
      <View style={[styles.gridThree, isDesktop && styles.gridThreeDesktop]}>
        
        {/* PILAR A: BLOQUEO SISTEMA DE SUBIDA Y CERRADO */}
        <BentoCard variant="elevated" style={[styles.pillarCard, isEmergencyActive && styles.pillarCardAlert]}>
          <View style={styles.cardHeaderRow}>
            <View style={[styles.iconPill, isEmergencyActive ? styles.pillRed : styles.pillGray]}>
              <IconLock size={18} color={isEmergencyActive ? '#EF4444' : '#64748B'} strokeWidth={2.2} />
            </View>
            <View style={{ flex: 1 }}>
              <Text style={styles.pillarTitle}>Sistema de Subida y Cerrado</Text>
              <Text style={styles.pillarStatusTag}>
                {barrierLocked ? 'ENCLAVAMIENTO ACTIVO (BLOQUEADO)' : 'OPERATIVO NORMAL'}
              </Text>
            </View>
          </View>

          {/* Diagrama Visual de Enclavamiento */}
          <View style={styles.barrierDiagramBox}>
            <Svg width="100%" height={120} viewBox="0 0 280 120">
              {/* Suelo / Carril */}
              <Line x1="10" y1="105" x2="270" y2="105" stroke="#334155" strokeWidth="2" strokeDasharray="4 4" />
              
              {/* Poste de la barrera */}
              <Rect x="40" y="35" width="28" height="70" rx="4" fill="#1E293B" stroke={barrierLocked ? '#EF4444' : '#64748B'} strokeWidth="2" />
              
              {/* Eje servomotor */}
              <Circle cx="54" cy="50" r="8" fill={barrierLocked ? '#EF4444' : '#3B82F6'} />
              
              {/* Brazo / Pluma (0° abajo cuando está en emergencia) */}
              <Rect
                x="54"
                y="46"
                width="160"
                height="8"
                rx="2"
                fill={barrierLocked ? '#DC2626' : '#94A3B8'}
                stroke={barrierLocked ? '#FCA5A5' : '#CBD5E1'}
                strokeWidth="1"
              />

              {/* Franjas diagonales de advertencia en la pluma */}
              {barrierLocked && (
                <>
                  <Line x1="80" y1="46" x2="90" y2="54" stroke="#FFFFFF" strokeWidth="2" />
                  <Line x1="110" y1="46" x2="120" y2="54" stroke="#FFFFFF" strokeWidth="2" />
                  <Line x1="140" y1="46" x2="150" y2="54" stroke="#FFFFFF" strokeWidth="2" />
                  <Line x1="170" y1="46" x2="180" y2="54" stroke="#FFFFFF" strokeWidth="2" />
                  <Line x1="200" y1="46" x2="210" y2="54" stroke="#FFFFFF" strokeWidth="2" />
                </>
              )}

              {/* Candado / Bloqueo mecánico sobre el eje */}
              {barrierLocked ? (
                <G>
                  <Circle cx="54" cy="50" r="14" fill="#7F1D1D" stroke="#EF4444" strokeWidth="2" />
                  {/* Icono de candado SVG */}
                  <Rect x="49" y="47" width="10" height="7" rx="1" fill="#FFFFFF" />
                  <Path d="M51 47V44a3 3 0 0 1 6 0v3" stroke="#FFFFFF" strokeWidth="1.5" fill="none" />
                </G>
              ) : null}

              {/* Texto de estado dentro del diagrama */}
              <SvgText
                x="140"
                y="85"
                textAnchor="middle"
                fill={barrierLocked ? '#F87171' : '#94A3B8'}
                fontSize="11"
                fontWeight="700"
              >
                {barrierLocked ? '🔒 SERVO BLOQUEADO · PWM INTERRUMPIDO' : 'PWM 50Hz · CONTROL LIBRE'}
              </SvgText>
            </Svg>
          </View>

          {/* Matriz de Canales Interceptados */}
          <View style={styles.interlockList}>
            <View style={styles.interlockRow}>
              <Text style={styles.interlockLabel}>Maniobra de Subida (90°):</Text>
              <View style={[styles.interlockBadge, barrierLocked ? styles.badgeLocked : styles.badgeUnlocked]}>
                <Text style={styles.interlockBadgeTxt}>{barrierLocked ? 'INTERCEPTADA' : 'HABILITADA'}</Text>
              </View>
            </View>

            <View style={styles.interlockRow}>
              <Text style={styles.interlockLabel}>Maniobra de Cierre (0°):</Text>
              <View style={[styles.interlockBadge, barrierLocked ? styles.badgeLocked : styles.badgeUnlocked]}>
                <Text style={styles.interlockBadgeTxt}>{barrierLocked ? 'INTERCEPTADA' : 'HABILITADA'}</Text>
              </View>
            </View>

            <View style={styles.interlockRow}>
              <Text style={styles.interlockLabel}>Freno Magnético de Eje:</Text>
              <View style={[styles.interlockBadge, barrierLocked ? styles.badgeActive : styles.badgeInactive]}>
                <Text style={styles.interlockBadgeTxt}>{barrierLocked ? 'ENCLAVADO (ENGAGED)' : 'LIBERADO'}</Text>
              </View>
            </View>
          </View>
        </BentoCard>

        {/* PILAR B: BALIZAS & LEDS ROJOS ESTROBOSCÓPICOS */}
        <BentoCard variant="elevated" style={[styles.pillarCard, isEmergencyActive && styles.pillarCardAlert]}>
          <View style={styles.cardHeaderRow}>
            <View style={[styles.iconPill, isEmergencyActive ? styles.pillRed : styles.pillGray]}>
              <IconSiren size={18} color={isEmergencyActive ? '#EF4444' : '#64748B'} strokeWidth={2} />
            </View>
            <View style={{ flex: 1 }}>
              <Text style={styles.pillarTitle}>Balizas y LEDs Rojos</Text>
              <Text style={styles.pillarStatusTag}>
                {redLedsActive ? `ESTROBO ${ledStrobeMode.toUpperCase()} (ALTA POTENCIA)` : 'LEDS DE TRÁFICO NORMAL'}
              </Text>
            </View>
          </View>

          {/* Simulación Gráfica de LEDs en Tiempo Real */}
          <View style={styles.ledsSimContainer}>
            {/* Torreta Central Estroboscópica */}
            <View style={styles.strobeBeaconRow}>
              <View style={[styles.beaconUnit, redLedsActive && styles.beaconUnitFlash]}>
                <View style={[styles.beaconCore, redLedsActive && styles.beaconCoreRed]} />
                <Text style={styles.beaconCaption}>ESTROBO IZQ</Text>
              </View>

              <View style={styles.beaconDivider} />

              <View style={[styles.beaconUnit, redLedsActive && styles.beaconUnitFlashAlt]}>
                <View style={[styles.beaconCore, redLedsActive && styles.beaconCoreRed]} />
                <Text style={styles.beaconCaption}>ESTROBO DER</Text>
              </View>
            </View>

            {/* Tira LED de Mástil y Semáforo */}
            <View style={styles.hardwareLedBox}>
              <Text style={styles.hardwareLedHeading}>TIRA DE MÁSTIL & SEMÁFORO DE CASETA</Text>
              <View style={styles.ledDiodesRow}>
                {[1, 2, 3, 4, 5, 6, 7, 8].map((i) => (
                  <View
                    key={i}
                    style={[
                      styles.ledDiode,
                      redLedsActive
                        ? i % 2 === 0
                          ? styles.diodeRedBright
                          : styles.diodeRedPulse
                        : styles.diodeDim,
                    ]}
                  />
                ))}
              </View>
            </View>

            {/* Matriz 4 Cajones IoT */}
            <View style={styles.bayLedsGrid}>
              <View style={[styles.bayLedPill, redLedsActive && styles.bayLedPillAlert]}>
                <View style={[styles.bayDot, redLedsActive ? styles.bayDotRed : styles.bayDotGreen]} />
                <Text style={styles.bayLedTxt}>Cajón 01: {redLedsActive ? 'LED ROJO' : 'Verde'}</Text>
              </View>
              <View style={[styles.bayLedPill, redLedsActive && styles.bayLedPillAlert]}>
                <View style={[styles.bayDot, redLedsActive ? styles.bayDotRed : styles.bayDotGreen]} />
                <Text style={styles.bayLedTxt}>Cajón 02: {redLedsActive ? 'LED ROJO' : 'Verde'}</Text>
              </View>
              <View style={[styles.bayLedPill, redLedsActive && styles.bayLedPillAlert]}>
                <View style={[styles.bayDot, redLedsActive ? styles.bayDotRed : styles.bayDotGreen]} />
                <Text style={styles.bayLedTxt}>Cajón 03: {redLedsActive ? 'LED ROJO' : 'Verde'}</Text>
              </View>
              <View style={[styles.bayLedPill, redLedsActive && styles.bayLedPillAlert]}>
                <View style={[styles.bayDot, redLedsActive ? styles.bayDotRed : styles.bayDotGreen]} />
                <Text style={styles.bayLedTxt}>Cajón 04: {redLedsActive ? 'LED ROJO' : 'Verde'}</Text>
              </View>
            </View>

            {/* Selector de Modos de Luz */}
            {redLedsActive && (
              <View style={styles.strobeSelectorRow}>
                <TouchableOpacity
                  style={[styles.strobeOptBtn, ledStrobeMode === 'flash_3hz' && styles.strobeOptActive]}
                  onPress={() => setLedStrobeMode('flash_3hz')}
                >
                  <Text style={[styles.strobeOptTxt, ledStrobeMode === 'flash_3hz' && styles.strobeOptTxtActive]}>
                    3.2 Hz Rápido
                  </Text>
                </TouchableOpacity>

                <TouchableOpacity
                  style={[styles.strobeOptBtn, ledStrobeMode === 'sos' && styles.strobeOptActive]}
                  onPress={() => setLedStrobeMode('sos')}
                >
                  <Text style={[styles.strobeOptTxt, ledStrobeMode === 'sos' && styles.strobeOptTxtActive]}>
                    Código SOS
                  </Text>
                </TouchableOpacity>

                <TouchableOpacity
                  style={[styles.strobeOptBtn, ledStrobeMode === 'steady' && styles.strobeOptActive]}
                  onPress={() => setLedStrobeMode('steady')}
                >
                  <Text style={[styles.strobeOptTxt, ledStrobeMode === 'steady' && styles.strobeOptTxtActive]}>
                    Fijo Continuo
                  </Text>
                </TouchableOpacity>
              </View>
            )}
          </View>
        </BentoCard>

        {/* PILAR C: ALERTA Y DESPACHO DE AUTORIDADES */}
        <BentoCard variant="elevated" style={[styles.pillarCard, isEmergencyActive && styles.pillarCardAlert]}>
          <View style={styles.cardHeaderRow}>
            <View style={[styles.iconPill, isEmergencyActive ? styles.pillRed : styles.pillGray]}>
              <IconShield size={18} color={isEmergencyActive ? '#EF4444' : '#64748B'} strokeWidth={2.2} />
            </View>
            <View style={{ flex: 1 }}>
              <Text style={styles.pillarTitle}>Alerta a Autoridades</Text>
              <Text style={styles.pillarStatusTag}>
                {authoritiesAlerted ? 'TELEMETRÍA ENVIADA (C5 & GUARDIA NACIONAL)' : 'CANALES SEGUROS EN LÍNEA'}
              </Text>
            </View>
          </View>

          {/* Lista de Corporaciones Despachadas */}
          <View style={styles.agenciesList}>
            {dispatchedAgencies.map((agency) => (
              <View key={agency.id} style={styles.agencyRow}>
                <View style={styles.agencyInfoCol}>
                  <Text style={styles.agencyName}>{agency.name}</Text>
                  <Text style={styles.agencyUnits}>
                    {agency.units} · Frec: {agency.channel}
                  </Text>
                </View>

                <View style={styles.agencyRightCol}>
                  <View style={[styles.agencyBadge, { backgroundColor: agency.badgeColor }]}>
                    <Text style={styles.agencyBadgeTxt}>{agency.status}</Text>
                  </View>
                  <Text style={styles.agencyEta}>ETA: {agency.eta}</Text>

                  {isEmergencyActive && agency.status === 'EN RUTA' && (
                    <TouchableOpacity
                      style={styles.arrivalBtn}
                      onPress={() => confirmAuthorityArrival(agency.id)}
                    >
                      <Text style={styles.arrivalBtnTxt}>Arribó</Text>
                    </TouchableOpacity>
                  )}
                </View>
              </View>
            ))}
          </View>

          {/* Botón para Ver Paquete de Telemetría mTLS */}
          <TouchableOpacity
            style={styles.telemetryBtn}
            onPress={() => setShowTelemetryModal(true)}
            activeOpacity={0.7}
          >
            <IconRadio size={14} color="#0066CC" />
            <Text style={styles.telemetryBtnTxt}>Ver Paquete Criptográfico Transmitido al C5</Text>
          </TouchableOpacity>
        </BentoCard>
      </View>

      {/* 3. PROTOCOLOS DE ACTIVACIÓN RÁPIDA (SI NO ESTÁ ACTIVO O PARA RE-CLASIFICAR) */}
      <BentoCard variant="elevated" style={styles.protocolsCard}>
        <Text style={styles.protocolsHeading}>PROTOCOLOS OPERATIVOS DE CONTENCIÓN</Text>
        <Text style={styles.protocolsSub}>
          Seleccione un escenario para ajustar el nivel de respuesta y enlace táctico con las fuerzas de seguridad:
        </Text>

        <View style={styles.protocolButtonsRow}>
          <TouchableOpacity
            style={[
              styles.protocolBtn,
              selectedProtocol === 'intrusion' && styles.protocolBtnActive,
            ]}
            onPress={() => handleTrigger('intrusion')}
          >
            <Text style={styles.protocolEmoji}>🛑</Text>
            <View style={{ flex: 1 }}>
              <Text style={styles.protocolName}>Intrusión Vehicular / Lista Negra</Text>
              <Text style={styles.protocolDesc}>Enclava plumas en 0° y activa alerta C5 de robo con violencia.</Text>
            </View>
          </TouchableOpacity>

          <TouchableOpacity
            style={[
              styles.protocolBtn,
              selectedProtocol === 'fire' && styles.protocolBtnActive,
            ]}
            onPress={() => handleTrigger('fire')}
          >
            <Text style={styles.protocolEmoji}>🔥</Text>
            <View style={{ flex: 1 }}>
              <Text style={styles.protocolName}>Incendio / Derrame en Bahía</Text>
              <Text style={styles.protocolDesc}>Despacho prioritario de Bomberos CREI y balizas rojas de desalojo.</Text>
            </View>
          </TouchableOpacity>

          <TouchableOpacity
            style={[
              styles.protocolBtn,
              selectedProtocol === 'panic' && styles.protocolBtnActive,
            ]}
            onPress={() => handleTrigger('panic')}
          >
            <Text style={styles.protocolEmoji}>🚨</Text>
            <View style={{ flex: 1 }}>
              <Text style={styles.protocolName}>Botón de Pánico en Caseta</Text>
              <Text style={styles.protocolDesc}>Ataque o asalto a personal. Notifica a patrullas aeroportuarias.</Text>
            </View>
          </TouchableOpacity>

          <TouchableOpacity
            style={[
              styles.protocolBtn,
              selectedProtocol === 'mechanical_estop' && styles.protocolBtnActive,
            ]}
            onPress={() => handleTrigger('mechanical_estop')}
          >
            <Text style={styles.protocolEmoji}>⚠️</Text>
            <View style={{ flex: 1 }}>
              <Text style={styles.protocolName}>Paro Mecánico Industrial (E-Stop)</Text>
              <Text style={styles.protocolDesc}>Corte inmediato de energía del servomotor para evitar atrapamiento.</Text>
            </View>
          </TouchableOpacity>
        </View>
      </BentoCard>

      {/* 4. BITÁCORA EN TIEMPO REAL DE LA EMERGENCIA */}
      <BentoCard variant="elevated" style={styles.logCard}>
        <View style={styles.logHeaderRow}>
          <IconActivity size={16} color="#DC2626" />
          <Text style={styles.logTitle}>Bitácora de Eventos de Crisis (Audit Trail)</Text>
        </View>

        <View style={styles.logList}>
          {emergencyLogs.map((log) => (
            <View key={log.id} style={styles.logRow}>
              <Text style={styles.logTime}>{log.time}</Text>
              <View
                style={[
                  styles.logTagBadge,
                  log.type === 'danger'
                    ? styles.tagDanger
                    : log.type === 'warning'
                    ? styles.tagWarning
                    : log.type === 'success'
                    ? styles.tagSuccess
                    : styles.tagInfo,
                ]}
              >
                <Text style={styles.logTagTxt}>{log.tag}</Text>
              </View>
              <Text style={styles.logMessage}>{log.message}</Text>
            </View>
          ))}
        </View>
      </BentoCard>

      {/* MODAL / OVERLAY: PIN DE DESARME DEL SUPERVISOR */}
      {showPinModal && (
        <View style={styles.modalBackdrop}>
          <View style={styles.modalCard}>
            <View style={styles.modalHeaderRow}>
              <IconLock size={20} color="#DC2626" />
              <Text style={styles.modalTitle}>Autorización de Supervisor</Text>
              <TouchableOpacity onPress={() => setShowPinModal(false)} style={styles.modalCloseBtn}>
                <IconX size={18} color="#64748B" />
              </TouchableOpacity>
            </View>

            <Text style={styles.modalDesc}>
              Para desbloquear el mecanismo de subida/cerrado y silenciar los estrobos rojos, ingrese su PIN de seguridad autorizado (Demo: 9911):
            </Text>

            <TextInput
              style={styles.pinInputField}
              value={pinInput}
              onChangeText={setPinInput}
              placeholder="••••"
              placeholderTextColor="#94A3B8"
              secureTextEntry
              keyboardType="numeric"
              maxLength={6}
              autoFocus
            />

            <View style={styles.modalActionsRow}>
              <PillButton
                label="Cancelar"
                onPress={() => setShowPinModal(false)}
                variant="glass"
                size="normal"
                style={{ flex: 1 }}
              />
              <PillButton
                label="Confirmar Desarme"
                onPress={handleDisarmSubmit}
                variant="primary"
                size="normal"
                style={{ flex: 1 }}
              />
            </View>
          </View>
        </View>
      )}

      {/* MODAL: TELEMETRÍA CRIPTOGRÁFICA TRANSMITIDA AL C5 */}
      {showTelemetryModal && (
        <View style={styles.modalBackdrop}>
          <View style={styles.modalCard}>
            <View style={styles.modalHeaderRow}>
              <IconRadio size={20} color="#0066CC" />
              <Text style={styles.modalTitle}>Telemetría Transmitida al C5</Text>
              <TouchableOpacity onPress={() => setShowTelemetryModal(false)} style={styles.modalCloseBtn}>
                <IconX size={18} color="#64748B" />
              </TouchableOpacity>
            </View>

            <View style={styles.jsonBox}>
              <Text style={styles.jsonText}>
{`{
  "incident_id": "INC-2026-1022-991",
  "facility": "Aeropuerto Internacional Lic. Jesús Terán Peredo (AGU)",
  "lane_id": "carril-01-entrada",
  "threat_level": "${threatLevel}",
  "trigger_protocol": "${emergencyType}",
  "barrier_state": "INTERLOCKED_0_DEGREES",
  "hardware_cut": "PWM_DISABLED_SOLENOID_ENGAGED",
  "visual_beacons": "RED_STROBE_3.2HZ_ACTIVE",
  "gps_coordinates": {
    "latitude": 21.7056,
    "longitude": -102.3178
  },
  "dispatched_units": [
    "GN-204",
    "GN-209",
    "CREI-R01"
  ],
  "c5_ticket": "C5-AERO-2026-991",
  "status": "${isEmergencyActive ? 'ACTIVE_EMERGENCY' : 'ALL_CLEAR'}"
}`}
              </Text>
            </View>

            <PillButton
              label="Cerrar Telemetría"
              onPress={() => setShowTelemetryModal(false)}
              variant="glass"
              size="normal"
              style={{ marginTop: 14 }}
            />
          </View>
        </View>
      )}
    </ScrollView>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#0F172A', // Fondo oscuro de misión crítica táctica
  },
  containerEmergencyActive: {
    backgroundColor: '#0B0F19',
  },
  scrollContent: {
    padding: 16,
    paddingBottom: 40,
  },
  desktopContent: {
    maxWidth: 1200,
    alignSelf: 'center',
    width: '100%',
  },

  // 1. Hero Card
  heroCard: {
    borderRadius: 20,
    padding: 20,
    marginBottom: 20,
    borderWidth: 1.5,
  },
  heroCardActive: {
    backgroundColor: 'rgba(220, 38, 38, 0.14)',
    borderColor: '#DC2626',
  },
  heroCardStandby: {
    backgroundColor: 'rgba(30, 41, 59, 0.7)',
    borderColor: 'rgba(255, 255, 255, 0.1)',
  },
  heroTopRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 12,
  },
  heroBadgeRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  pulsingDot: {
    width: 10,
    height: 10,
    borderRadius: 5,
  },
  dotRed: {
    backgroundColor: '#EF4444',
  },
  dotGreen: {
    backgroundColor: '#10B981',
  },
  heroBadgeText: {
    fontSize: 12,
    fontWeight: '800',
    letterSpacing: 0.6,
  },
  textRedLight: {
    color: '#F87171',
  },
  textGreenLight: {
    color: '#34D399',
  },
  heroActionsRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
  },
  sirenBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    paddingVertical: 6,
    paddingHorizontal: 12,
    borderRadius: 999,
  },
  sirenBtnActive: {
    backgroundColor: '#DC2626',
  },
  sirenBtnMuted: {
    backgroundColor: 'rgba(255, 255, 255, 0.15)',
  },
  sirenBtnText: {
    color: '#FFFFFF',
    fontSize: 11,
    fontWeight: '700',
  },
  closeBtn: {
    padding: 6,
    backgroundColor: 'rgba(255, 255, 255, 0.1)',
    borderRadius: 999,
  },

  heroMainRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    flexWrap: 'wrap',
    gap: 16,
    marginBottom: 16,
  },
  heroTitle: {
    fontSize: 24,
    fontWeight: '800',
    color: '#FFFFFF',
    letterSpacing: -0.5,
    marginBottom: 4,
  },
  heroSubtitle: {
    fontSize: 13,
    color: '#CBD5E1',
    lineHeight: 18,
    maxWidth: 620,
  },
  hudClockContainer: {
    backgroundColor: 'rgba(0, 0, 0, 0.45)',
    borderWidth: 1,
    borderColor: '#EF4444',
    borderRadius: 14,
    paddingVertical: 10,
    paddingHorizontal: 16,
    alignItems: 'center',
  },
  hudClockLabel: {
    color: '#F87171',
    fontSize: 10,
    fontWeight: '800',
    letterSpacing: 0.5,
  },
  hudClockDigits: {
    color: '#FFFFFF',
    fontSize: 28,
    fontWeight: '900',
    fontVariant: ['tabular-nums'],
    letterSpacing: 1.5,
    marginVertical: 2,
  },
  hudClockSec: {
    color: '#94A3B8',
    fontSize: 9,
    fontWeight: '600',
  },

  triggerGroupRow: {
    marginTop: 8,
  },
  bigTriggerBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 14,
    padding: 16,
    borderRadius: 14,
  },
  btnRed: {
    backgroundColor: '#DC2626',
  },
  bigTriggerTitle: {
    color: '#FFFFFF',
    fontSize: 16,
    fontWeight: '800',
    letterSpacing: 0.4,
  },
  bigTriggerSub: {
    color: 'rgba(255, 255, 255, 0.85)',
    fontSize: 12,
    marginTop: 2,
  },

  activeEmergencyBannerRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    backgroundColor: 'rgba(0, 0, 0, 0.3)',
    borderRadius: 12,
    padding: 12,
    flexWrap: 'wrap',
    gap: 10,
  },
  activeAlertTextCol: {
    flex: 1,
    minWidth: 240,
  },
  activeAlertMain: {
    color: '#FCA5A5',
    fontSize: 14,
    fontWeight: '800',
  },
  activeAlertSub: {
    color: '#E2E8F0',
    fontSize: 12,
    marginTop: 2,
  },
  disarmTriggerBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    backgroundColor: '#FFFFFF',
    paddingVertical: 10,
    paddingHorizontal: 16,
    borderRadius: 10,
  },
  disarmTriggerText: {
    color: '#15803D',
    fontSize: 13,
    fontWeight: '800',
    letterSpacing: 0.3,
  },

  // 2. Grid Three Pillars
  gridThree: {
    gap: 16,
    marginBottom: 20,
  },
  gridThreeDesktop: {
    flexDirection: 'row',
  },
  pillarCard: {
    flex: 1,
    backgroundColor: '#1E293B',
    borderColor: 'rgba(255, 255, 255, 0.08)',
    borderRadius: 18,
    padding: 16,
  },
  pillarCardAlert: {
    borderColor: 'rgba(239, 68, 68, 0.4)',
    backgroundColor: 'rgba(30, 41, 59, 0.95)',
  },
  cardHeaderRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    marginBottom: 14,
  },
  iconPill: {
    width: 36,
    height: 36,
    borderRadius: 10,
    alignItems: 'center',
    justifyContent: 'center',
  },
  pillRed: {
    backgroundColor: 'rgba(239, 68, 68, 0.16)',
  },
  pillGray: {
    backgroundColor: 'rgba(255, 255, 255, 0.08)',
  },
  pillarTitle: {
    color: '#FFFFFF',
    fontSize: 15,
    fontWeight: '700',
  },
  pillarStatusTag: {
    color: '#94A3B8',
    fontSize: 11,
    fontWeight: '600',
    marginTop: 1,
  },

  barrierDiagramBox: {
    backgroundColor: '#0F172A',
    borderRadius: 12,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.06)',
    padding: 8,
    alignItems: 'center',
    marginBottom: 14,
  },

  interlockList: {
    gap: 8,
  },
  interlockRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    backgroundColor: 'rgba(0, 0, 0, 0.25)',
    paddingVertical: 6,
    paddingHorizontal: 10,
    borderRadius: 8,
  },
  interlockLabel: {
    color: '#CBD5E1',
    fontSize: 12,
    fontWeight: '500',
  },
  interlockBadge: {
    paddingVertical: 3,
    paddingHorizontal: 8,
    borderRadius: 6,
  },
  badgeLocked: {
    backgroundColor: 'rgba(220, 38, 38, 0.25)',
    borderWidth: 1,
    borderColor: '#EF4444',
  },
  badgeUnlocked: {
    backgroundColor: 'rgba(16, 185, 129, 0.2)',
    borderWidth: 1,
    borderColor: '#10B981',
  },
  badgeActive: {
    backgroundColor: 'rgba(239, 68, 68, 0.3)',
    borderWidth: 1,
    borderColor: '#F87171',
  },
  badgeInactive: {
    backgroundColor: 'rgba(255, 255, 255, 0.08)',
  },
  interlockBadgeTxt: {
    color: '#FFFFFF',
    fontSize: 10,
    fontWeight: '800',
    letterSpacing: 0.3,
  },

  // LEDs Simulator
  ledsSimContainer: {
    gap: 12,
  },
  strobeBeaconRow: {
    flexDirection: 'row',
    justifyContent: 'space-around',
    alignItems: 'center',
    backgroundColor: '#0F172A',
    borderRadius: 12,
    padding: 12,
  },
  beaconUnit: {
    alignItems: 'center',
    padding: 8,
    borderRadius: 10,
  },
  beaconUnitFlash: {
    backgroundColor: 'rgba(239, 68, 68, 0.25)',
  },
  beaconUnitFlashAlt: {
    backgroundColor: 'rgba(220, 38, 38, 0.35)',
  },
  beaconCore: {
    width: 22,
    height: 22,
    borderRadius: 11,
    backgroundColor: '#334155',
    marginBottom: 4,
  },
  beaconCoreRed: {
    backgroundColor: '#EF4444',
    shadowColor: '#EF4444',
    shadowOffset: { width: 0, height: 0 },
    shadowOpacity: 0.9,
    shadowRadius: 10,
    elevation: 8,
  },
  beaconCaption: {
    color: '#94A3B8',
    fontSize: 9,
    fontWeight: '700',
    letterSpacing: 0.4,
  },
  beaconDivider: {
    width: 1,
    height: 30,
    backgroundColor: 'rgba(255, 255, 255, 0.1)',
  },

  hardwareLedBox: {
    backgroundColor: '#0F172A',
    borderRadius: 10,
    padding: 10,
  },
  hardwareLedHeading: {
    color: '#94A3B8',
    fontSize: 10,
    fontWeight: '700',
    letterSpacing: 0.4,
    marginBottom: 6,
  },
  ledDiodesRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
  },
  ledDiode: {
    width: 12,
    height: 12,
    borderRadius: 6,
  },
  diodeDim: {
    backgroundColor: '#334155',
  },
  diodeRedBright: {
    backgroundColor: '#EF4444',
    shadowColor: '#EF4444',
    shadowOffset: { width: 0, height: 0 },
    shadowOpacity: 0.8,
    shadowRadius: 6,
    elevation: 4,
  },
  diodeRedPulse: {
    backgroundColor: '#B91C1C',
  },

  bayLedsGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
  },
  bayLedPill: {
    flex: 1,
    minWidth: '45%',
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    backgroundColor: 'rgba(0, 0, 0, 0.25)',
    paddingVertical: 6,
    paddingHorizontal: 8,
    borderRadius: 8,
  },
  bayLedPillAlert: {
    backgroundColor: 'rgba(220, 38, 38, 0.18)',
    borderWidth: 0.5,
    borderColor: '#EF4444',
  },
  bayDot: {
    width: 8,
    height: 8,
    borderRadius: 4,
  },
  bayDotRed: {
    backgroundColor: '#EF4444',
  },
  bayDotGreen: {
    backgroundColor: '#10B981',
  },
  bayLedTxt: {
    color: '#E2E8F0',
    fontSize: 11,
    fontWeight: '600',
  },

  strobeSelectorRow: {
    flexDirection: 'row',
    gap: 6,
    marginTop: 4,
  },
  strobeOptBtn: {
    flex: 1,
    backgroundColor: 'rgba(255, 255, 255, 0.08)',
    paddingVertical: 6,
    borderRadius: 6,
    alignItems: 'center',
  },
  strobeOptActive: {
    backgroundColor: '#DC2626',
  },
  strobeOptTxt: {
    color: '#94A3B8',
    fontSize: 10,
    fontWeight: '700',
  },
  strobeOptTxtActive: {
    color: '#FFFFFF',
  },

  // Authorities
  agenciesList: {
    gap: 10,
    marginBottom: 12,
  },
  agencyRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    backgroundColor: 'rgba(0, 0, 0, 0.25)',
    borderRadius: 10,
    padding: 10,
  },
  agencyInfoCol: {
    flex: 1,
    paddingRight: 8,
  },
  agencyName: {
    color: '#FFFFFF',
    fontSize: 13,
    fontWeight: '700',
  },
  agencyUnits: {
    color: '#94A3B8',
    fontSize: 11,
    marginTop: 2,
  },
  agencyRightCol: {
    alignItems: 'flex-end',
    gap: 2,
  },
  agencyBadge: {
    paddingVertical: 2,
    paddingHorizontal: 6,
    borderRadius: 4,
  },
  agencyBadgeTxt: {
    color: '#FFFFFF',
    fontSize: 9,
    fontWeight: '800',
    letterSpacing: 0.4,
  },
  agencyEta: {
    color: '#E2E8F0',
    fontSize: 10,
    fontWeight: '600',
  },
  arrivalBtn: {
    backgroundColor: '#16A34A',
    paddingVertical: 2,
    paddingHorizontal: 6,
    borderRadius: 4,
    marginTop: 2,
  },
  arrivalBtnTxt: {
    color: '#FFFFFF',
    fontSize: 9,
    fontWeight: '700',
  },
  telemetryBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 6,
    backgroundColor: 'rgba(0, 102, 204, 0.15)',
    borderWidth: 1,
    borderColor: 'rgba(0, 102, 204, 0.4)',
    paddingVertical: 8,
    borderRadius: 8,
  },
  telemetryBtnTxt: {
    color: '#38BDF8',
    fontSize: 11,
    fontWeight: '700',
  },

  // 3. Protocols Card
  protocolsCard: {
    backgroundColor: '#1E293B',
    borderColor: 'rgba(255, 255, 255, 0.08)',
    borderRadius: 18,
    padding: 18,
    marginBottom: 20,
  },
  protocolsHeading: {
    color: '#FFFFFF',
    fontSize: 14,
    fontWeight: '800',
    letterSpacing: 0.6,
  },
  protocolsSub: {
    color: '#94A3B8',
    fontSize: 12,
    marginTop: 2,
    marginBottom: 14,
  },
  protocolButtonsRow: {
    gap: 10,
  },
  protocolBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    backgroundColor: 'rgba(0, 0, 0, 0.25)',
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.06)',
    padding: 12,
    borderRadius: 12,
  },
  protocolBtnActive: {
    borderColor: '#DC2626',
    backgroundColor: 'rgba(220, 38, 38, 0.15)',
  },
  protocolEmoji: {
    fontSize: 22,
  },
  protocolName: {
    color: '#FFFFFF',
    fontSize: 13,
    fontWeight: '700',
  },
  protocolDesc: {
    color: '#94A3B8',
    fontSize: 11,
    marginTop: 1,
  },

  // 4. Log Card
  logCard: {
    backgroundColor: '#1E293B',
    borderColor: 'rgba(255, 255, 255, 0.08)',
    borderRadius: 18,
    padding: 18,
  },
  logHeaderRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    marginBottom: 14,
  },
  logTitle: {
    color: '#FFFFFF',
    fontSize: 14,
    fontWeight: '800',
    letterSpacing: 0.4,
  },
  logList: {
    gap: 8,
  },
  logRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255, 255, 255, 0.05)',
    paddingBottom: 6,
  },
  logTime: {
    color: '#94A3B8',
    fontSize: 11,
    fontVariant: ['tabular-nums'],
    width: 60,
  },
  logTagBadge: {
    paddingVertical: 2,
    paddingHorizontal: 6,
    borderRadius: 4,
  },
  tagDanger: {
    backgroundColor: 'rgba(239, 68, 68, 0.25)',
  },
  tagWarning: {
    backgroundColor: 'rgba(245, 158, 11, 0.25)',
  },
  tagSuccess: {
    backgroundColor: 'rgba(16, 185, 129, 0.25)',
  },
  tagInfo: {
    backgroundColor: 'rgba(59, 130, 246, 0.25)',
  },
  logTagTxt: {
    color: '#FFFFFF',
    fontSize: 9,
    fontWeight: '800',
    letterSpacing: 0.3,
  },
  logMessage: {
    flex: 1,
    color: '#CBD5E1',
    fontSize: 11,
  },

  // Modales
  modalBackdrop: {
    position: 'absolute',
    top: 0,
    left: 0,
    right: 0,
    bottom: 0,
    backgroundColor: 'rgba(0, 0, 0, 0.8)',
    justifyContent: 'center',
    alignItems: 'center',
    padding: 20,
    zIndex: 9999,
  },
  modalCard: {
    width: '100%',
    maxWidth: 440,
    backgroundColor: '#1E293B',
    borderRadius: 20,
    padding: 22,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.15)',
  },
  modalHeaderRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    marginBottom: 12,
  },
  modalTitle: {
    flex: 1,
    color: '#FFFFFF',
    fontSize: 16,
    fontWeight: '800',
  },
  modalCloseBtn: {
    padding: 4,
  },
  modalDesc: {
    color: '#94A3B8',
    fontSize: 13,
    lineHeight: 18,
    marginBottom: 16,
  },
  pinInputField: {
    backgroundColor: '#0F172A',
    borderWidth: 1,
    borderColor: '#475569',
    borderRadius: 12,
    padding: 14,
    color: '#FFFFFF',
    fontSize: 24,
    textAlign: 'center',
    letterSpacing: 8,
    marginBottom: 20,
  },
  modalActionsRow: {
    flexDirection: 'row',
    gap: 12,
  },
  jsonBox: {
    backgroundColor: '#0F172A',
    borderRadius: 10,
    padding: 12,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.08)',
    maxHeight: 280,
  },
  jsonText: {
    color: '#38BDF8',
    fontFamily: Platform.OS === 'ios' ? 'Menlo' : 'monospace',
    fontSize: 11,
    lineHeight: 16,
  },
});
