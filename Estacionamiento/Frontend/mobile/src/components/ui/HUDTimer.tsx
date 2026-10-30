import React, { useEffect, useState } from 'react';
import { View, Text, StyleSheet } from 'react-native';
import { Radii } from '@/theme/tokens';

import { IconClock } from '@/components/ui/Icons';

export interface HUDTimerProps {
  startTime: Date;
  ratePerHour?: number;
}

export const HUDTimer: React.FC<HUDTimerProps> = ({
  startTime,
  ratePerHour = 45.0,
}) => {
  const [secondsElapsed, setSecondsElapsed] = useState(0);

  useEffect(() => {
    const updateElapsed = () => {
      const now = new Date().getTime();
      const diff = Math.max(0, Math.floor((now - startTime.getTime()) / 1000));
      setSecondsElapsed(diff);
    };

    updateElapsed();
    const interval = setInterval(updateElapsed, 1000);
    return () => clearInterval(interval);
  }, [startTime]);

  const hours = Math.floor(secondsElapsed / 3600);
  const minutes = Math.floor((secondsElapsed % 3600) / 60);
  const seconds = secondsElapsed % 60;

  const pad = (n: number) => n.toString().padStart(2, '0');

  // Cálculo de cobro actual (mínimo 1 hora)
  const billableHours = Math.max(1, Math.ceil(secondsElapsed / 3600));
  const estimatedCost = (billableHours * ratePerHour).toFixed(2);

  // Cálculo del tiempo restante antes de que se sume la siguiente hora
  const secondsIntoCurrentHour = secondsElapsed % 3600;
  const secondsUntilNextCharge = 3600 - secondsIntoCurrentHour;
  const remainingMinutes = Math.floor(secondsUntilNextCharge / 60);
  const remainingSeconds = secondsUntilNextCharge % 60;

  // Porcentaje consumido de la hora actual
  const progressPercent = Math.min(100, Math.max(0, (secondsIntoCurrentHour / 3600) * 100));

  // Hora exacta en la que se aplicará el siguiente cargo
  const nextChargeDate = new Date(Date.now() + secondsUntilNextCharge * 1000);
  const nextChargeTimeStr = nextChargeDate.toLocaleTimeString([], {
    hour: '2-digit',
    minute: '2-digit',
  });

  // Color adaptativo profesional y sobrio
  const isUrgent = remainingMinutes < 5;
  const isWarning = remainingMinutes < 15;
  const accentColor = isUrgent ? '#B91C1C' : isWarning ? '#B45309' : '#0066CC';

  return (
    <View style={styles.container}>
      {/* 1. Reloj de Tiempo Transcurrido Principal */}
      <Text style={styles.eyebrow}>TIEMPO TOTAL TRANSCURRIDO</Text>
      <Text style={styles.timerText}>
        {pad(hours)}:{pad(minutes)}:{pad(seconds)}
      </Text>

      {/* Tarifa Actual Acumulada */}
      <View style={styles.costRow}>
        <Text style={styles.costLabel}>TARIFA ACTUAL DEVENGADA:</Text>
        <Text style={styles.costValue}>${estimatedCost} MXN</Text>
        <View style={styles.hourPill}>
          <Text style={styles.hourPillText}>{billableHours}h facturadas</Text>
        </View>
      </View>

      {/* 2. Módulo Destacado: Tiempo Restante Antes del Siguiente Cargo */}
      <View style={[styles.nextChargeBox, { borderColor: isUrgent ? 'rgba(185, 28, 28, 0.25)' : 'rgba(15, 23, 42, 0.08)' }]}>
        <View style={styles.nextChargeHeader}>
          <View style={styles.nextChargeIconDot}>
            <IconClock size={15} color={accentColor} strokeWidth={2} />
          </View>
          <View style={{ flex: 1 }}>
            <Text style={styles.nextChargeTitle}>TIEMPO RESTANTE ANTES DE CARGO EXTRA</Text>
            <Text style={styles.nextChargeSub}>
              Próximo incremento de +${ratePerHour.toFixed(0)} MXN a las {nextChargeTimeStr} hrs
            </Text>
          </View>
          <View style={[styles.countdownBadge, { backgroundColor: isUrgent ? '#FEF2F2' : isWarning ? '#FFFBEB' : '#F1F5F9' }]}>
            <Text style={[styles.countdownBadgeText, { color: accentColor }]}>
              {pad(remainingMinutes)}m {pad(remainingSeconds)}s
            </Text>
          </View>
        </View>

        {/* Barra de Progreso de la Hora en Curso */}
        <View style={styles.progressBarTrack}>
          <View
            style={[
              styles.progressBarFill,
              {
                width: `${progressPercent}%`,
                backgroundColor: accentColor,
              },
            ]}
          />
        </View>

        <View style={styles.nextChargeFooter}>
          <Text style={styles.nextChargeFootText}>
            Tienes <Text style={[styles.boldText, { color: accentColor }]}>{remainingMinutes}m {remainingSeconds}s</Text> para retirar tu vehículo sin costo adicional en este periodo.
          </Text>
        </View>
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    alignItems: 'center',
    marginVertical: 4,
    width: '100%',
  },
  eyebrow: {
    color: 'rgba(0, 0, 0, 0.45)',
    fontSize: 10,
    fontWeight: '700',
    letterSpacing: 1.2,
    marginBottom: 4,
  },
  timerText: {
    color: '#1D1D1F',
    fontSize: 42,
    fontWeight: '800',
    letterSpacing: 1.5,
    fontVariant: ['tabular-nums'],
  },
  costRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginTop: 6,
    marginBottom: 14,
    backgroundColor: '#F8F9FA',
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.08)',
    paddingHorizontal: 12,
    paddingVertical: 5,
    borderRadius: Radii.pill,
    gap: 6,
  },
  costLabel: {
    color: 'rgba(0, 0, 0, 0.5)',
    fontSize: 10,
    fontWeight: '700',
    letterSpacing: 0.5,
  },
  costValue: {
    color: '#15803D',
    fontSize: 13,
    fontWeight: '800',
  },
  hourPill: {
    backgroundColor: 'rgba(0, 0, 0, 0.05)',
    paddingHorizontal: 6,
    paddingVertical: 2,
    borderRadius: Radii.pill,
  },
  hourPillText: {
    fontSize: 9,
    fontWeight: '700',
    color: 'rgba(0, 0, 0, 0.55)',
  },

  // Próximo Cargo Box
  nextChargeBox: {
    width: '100%',
    backgroundColor: '#F8F9FB',
    borderWidth: 1.5,
    borderRadius: 16,
    padding: 12,
    marginTop: 4,
  },
  nextChargeHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    marginBottom: 8,
  },
  nextChargeIconDot: {
    width: 28,
    height: 28,
    borderRadius: 14,
    backgroundColor: '#FFFFFF',
    borderWidth: 1,
    borderColor: 'rgba(0, 0, 0, 0.08)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  nextChargeTitle: {
    fontSize: 10,
    fontWeight: '800',
    color: '#1D1D1F',
    letterSpacing: 0.6,
  },
  nextChargeSub: {
    fontSize: 10,
    color: 'rgba(0, 0, 0, 0.5)',
    marginTop: 1,
  },
  countdownBadge: {
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: Radii.pill,
  },
  countdownBadgeText: {
    fontSize: 12,
    fontWeight: '800',
    fontVariant: ['tabular-nums'],
  },
  progressBarTrack: {
    height: 6,
    backgroundColor: 'rgba(0, 0, 0, 0.06)',
    borderRadius: 3,
    overflow: 'hidden',
    marginVertical: 4,
  },
  progressBarFill: {
    height: '100%',
    borderRadius: 3,
  },
  nextChargeFooter: {
    marginTop: 4,
  },
  nextChargeFootText: {
    fontSize: 10,
    color: 'rgba(0, 0, 0, 0.55)',
    lineHeight: 14,
  },
  boldText: {
    fontWeight: '700',
  },
});

export default HUDTimer;
