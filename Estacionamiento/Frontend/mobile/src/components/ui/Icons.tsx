import React from 'react';
import Svg, { Path, Rect, Circle, Line, Polyline, Polygon } from 'react-native-svg';

export interface IconProps {
  size?: number;
  color?: string;
  strokeWidth?: number;
  style?: any;
}

const defaultProps = {
  size: 20,
  color: '#0F172A',
  strokeWidth: 1.75,
};

// 1. Home / Inicio
export const IconHome: React.FC<IconProps> = ({ size = 20, color = '#0F172A', strokeWidth = 1.75, style }) => (
  <Svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round" style={style}>
    <Path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
    <Polyline points="9 22 9 12 15 12 15 22" />
  </Svg>
);

// 2. Ticket / Pase de Estancia
export const IconTicket: React.FC<IconProps> = ({ size = 20, color = '#0F172A', strokeWidth = 1.75, style }) => (
  <Svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round" style={style}>
    <Path d="M2 9a3 3 0 0 1 0 6v2a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-2a3 3 0 0 1 0-6V7a2 2 0 0 0-2-2H4a2 2 0 0 0-2 2z" />
    <Line x1="12" y1="9" x2="12" y2="15" strokeDasharray="2 2" />
  </Svg>
);

// 3. Parking / Reservar Cajón
export const IconParking: React.FC<IconProps> = ({ size = 20, color = '#0F172A', strokeWidth = 1.75, style }) => (
  <Svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round" style={style}>
    <Rect x="3" y="3" width="18" height="18" rx="4" />
    <Path d="M9 17V7h4a3 3 0 0 1 0 6H9" />
  </Svg>
);

// 4. Car / Vehículos
export const IconCar: React.FC<IconProps> = ({ size = 20, color = '#0F172A', strokeWidth = 1.75, style }) => (
  <Svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round" style={style}>
    <Path d="M19 17h2c.6 0 1-.4 1-1v-3c0-.9-.7-1.7-1.5-1.9C18.7 10.6 16 10 16 10s-1.3-1.4-2.2-2.3c-.5-.4-1.1-.7-1.8-.7H8c-.7 0-1.3.3-1.8.7C5.3 8.6 4 10 4 10s-2.7.6-4.5 1.1C-.3 11.3-.1 12.1 0 13v3c0 .6.4 1 1 1h2" />
    <Circle cx="7" cy="17" r="2" />
    <Path d="M9 17h6" />
    <Circle cx="17" cy="17" r="2" />
  </Svg>
);

// 5. CreditCard / Wallet
export const IconCreditCard: React.FC<IconProps> = ({ size = 20, color = '#0F172A', strokeWidth = 1.75, style }) => (
  <Svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round" style={style}>
    <Rect x="2" y="5" width="20" height="14" rx="3" />
    <Line x1="2" y1="10" x2="22" y2="10" />
    <Line x1="6" y1="15" x2="10" y2="15" />
  </Svg>
);

// 6. Shield / Seguridad
export const IconShield: React.FC<IconProps> = ({ size = 20, color = '#0F172A', strokeWidth = 1.75, style }) => (
  <Svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round" style={style}>
    <Path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
  </Svg>
);

// 7. AlertTriangle / Alerta de Choque
export const IconAlertTriangle: React.FC<IconProps> = ({ size = 20, color = '#DC2626', strokeWidth = 1.75, style }) => (
  <Svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round" style={style}>
    <Path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
    <Line x1="12" y1="9" x2="12" y2="13" />
    <Line x1="12" y1="17" x2="12.01" y2="17" />
  </Svg>
);

// 8. Clock / Cronómetro
export const IconClock: React.FC<IconProps> = ({ size = 20, color = '#0F172A', strokeWidth = 1.75, style }) => (
  <Svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round" style={style}>
    <Circle cx="12" cy="12" r="10" />
    <Polyline points="12 6 12 12 16 14" />
  </Svg>
);

// 9. Gate / Barrera de Acceso
export const IconGate: React.FC<IconProps> = ({ size = 20, color = '#0F172A', strokeWidth = 1.75, style }) => (
  <Svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round" style={style}>
    <Rect x="3" y="10" width="4" height="11" rx="1" />
    <Path d="M7 12h14" />
    <Path d="M10 9l3 3-3 3" />
    <Line x1="7" y1="15" x2="21" y2="15" />
    <Line x1="2" y1="21" x2="8" y2="21" />
  </Svg>
);

// 10. Pedestrian / Paso Peatonal
export const IconPedestrian: React.FC<IconProps> = ({ size = 20, color = '#0F172A', strokeWidth = 1.75, style }) => (
  <Svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round" style={style}>
    <Circle cx="12" cy="5" r="2" />
    <Path d="M10 22l2-7 3 3v4" />
    <Path d="M7 14l3-3 3-1 3 2 3 1" />
  </Svg>
);

// 11. Camera / Reconocimiento LPR
export const IconCamera: React.FC<IconProps> = ({ size = 20, color = '#0F172A', strokeWidth = 1.75, style }) => (
  <Svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round" style={style}>
    <Path d="M23 19a2 2 0 0 1-2 2H3a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4l2-3h6l2 3h4a2 2 0 0 1 2 2z" />
    <Circle cx="12" cy="13" r="4" />
  </Svg>
);

// 12. Activity / Vibración y Sensor Sísmico
export const IconActivity: React.FC<IconProps> = ({ size = 20, color = '#0F172A', strokeWidth = 1.75, style }) => (
  <Svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round" style={style}>
    <Polyline points="22 12 18 12 15 21 9 3 6 12 2 12" />
  </Svg>
);

// 13. MapPin / Ubicación
export const IconMapPin: React.FC<IconProps> = ({ size = 20, color = '#0F172A', strokeWidth = 1.75, style }) => (
  <Svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round" style={style}>
    <Path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" />
    <Circle cx="12" cy="10" r="3" />
  </Svg>
);

// 14. Phone / Asistencia
export const IconPhone: React.FC<IconProps> = ({ size = 20, color = '#0F172A', strokeWidth = 1.75, style }) => (
  <Svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round" style={style}>
    <Path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6 19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72 12.84 12.84 0 0 0 .7 2.81 2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45 12.84 12.84 0 0 0 2.81.7A2 2 0 0 1 22 16.92z" />
  </Svg>
);

// 15. Receipt / CFDI
export const IconReceipt: React.FC<IconProps> = ({ size = 20, color = '#0F172A', strokeWidth = 1.75, style }) => (
  <Svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round" style={style}>
    <Path d="M4 2v20l2-1 2 1 2-1 2 1 2-1 2 1 2-1 2 1V2l-2 1-2-1-2 1-2-1-2 1-2-1-2 1z" />
    <Line x1="8" y1="8" x2="16" y2="8" />
    <Line x1="8" y1="12" x2="16" y2="12" />
    <Line x1="8" y1="16" x2="12" y2="16" />
  </Svg>
);

// 16. Check
export const IconCheck: React.FC<IconProps> = ({ size = 20, color = '#15803D', strokeWidth = 2, style }) => (
  <Svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round" style={style}>
    <Polyline points="20 6 9 17 4 12" />
  </Svg>
);

// 17. Close / X
export const IconX: React.FC<IconProps> = ({ size = 20, color = '#0F172A', strokeWidth = 2, style }) => (
  <Svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round" style={style}>
    <Line x1="18" y1="6" x2="6" y2="18" />
    <Line x1="6" y1="6" x2="18" y2="18" />
  </Svg>
);

// 18. Arrow Right
export const IconArrowRight: React.FC<IconProps> = ({ size = 16, color = '#0F172A', strokeWidth = 1.75, style }) => (
  <Svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round" style={style}>
    <Line x1="5" y1="12" x2="19" y2="12" />
    <Polyline points="12 5 19 12 12 19" />
  </Svg>
);

// 19. Video / Grabación
export const IconVideo: React.FC<IconProps> = ({ size = 20, color = '#0F172A', strokeWidth = 1.75, style }) => (
  <Svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round" style={style}>
    <Polygon points="23 7 16 12 23 17 23 7" />
    <Rect x="1" y="5" width="15" height="14" rx="2" ry="2" />
  </Svg>
);

// 20. BarChart / Gráfica de Barras
export const IconBarChart: React.FC<IconProps> = ({ size = 20, color = '#0F172A', strokeWidth = 1.75, style }) => (
  <Svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round" style={style}>
    <Line x1="12" y1="20" x2="12" y2="10" />
    <Line x1="18" y1="20" x2="18" y2="4" />
    <Line x1="6" y1="20" x2="6" y2="16" />
  </Svg>
);

// 21. Download / Exportar
export const IconDownload: React.FC<IconProps> = ({ size = 20, color = '#0F172A', strokeWidth = 1.75, style }) => (
  <Svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round" style={style}>
    <Path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
    <Polyline points="7 10 12 15 17 10" />
    <Line x1="12" y1="15" x2="12" y2="3" />
  </Svg>
);

// 22. FileText / Facturación SAT
export const IconFileText: React.FC<IconProps> = ({ size = 20, color = '#0F172A', strokeWidth = 1.75, style }) => (
  <Svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round" style={style}>
    <Path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
    <Polyline points="14 2 14 8 20 8" />
    <Line x1="16" y1="13" x2="8" y2="13" />
    <Line x1="16" y1="17" x2="8" y2="17" />
    <Polyline points="10 9 9 9 8 9" />
  </Svg>
);

// 23. DollarSign / Flujo Monetario
export const IconDollarSign: React.FC<IconProps> = ({ size = 20, color = '#0F172A', strokeWidth = 1.75, style }) => (
  <Svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round" style={style}>
    <Line x1="12" y1="1" x2="12" y2="23" />
    <Path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6" />
  </Svg>
);

// 24. Lock / Candado
export const IconLock: React.FC<IconProps> = ({ size = 20, color = '#0F172A', strokeWidth = 1.75, style }) => (
  <Svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round" style={style}>
    <Rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
    <Path d="M7 11V7a5 5 0 0 1 10 0v4" />
  </Svg>
);

// 25. Unlock / Desbloqueo
export const IconUnlock: React.FC<IconProps> = ({ size = 20, color = '#0F172A', strokeWidth = 1.75, style }) => (
  <Svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round" style={style}>
    <Rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
    <Path d="M7 11V7a5 5 0 0 1 9.9-1" />
  </Svg>
);

// 26. Volume2 / Sirena Activa
export const IconVolume2: React.FC<IconProps> = ({ size = 20, color = '#0F172A', strokeWidth = 1.75, style }) => (
  <Svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round" style={style}>
    <Polygon points="11 5 6 9 2 9 2 15 6 15 11 19 11 5" />
    <Path d="M19.07 4.93a10 10 0 0 1 0 14.14M15.54 8.46a5 5 0 0 1 0 7.07" />
  </Svg>
);

// 27. VolumeX / Sirena Mute
export const IconVolumeX: React.FC<IconProps> = ({ size = 20, color = '#0F172A', strokeWidth = 1.75, style }) => (
  <Svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round" style={style}>
    <Polygon points="11 5 6 9 2 9 2 15 6 15 11 19 11 5" />
    <Line x1="23" y1="9" x2="17" y2="15" />
    <Line x1="17" y1="9" x2="23" y2="15" />
  </Svg>
);

// 28. Radio / Frecuencia Táctica
export const IconRadio: React.FC<IconProps> = ({ size = 20, color = '#0F172A', strokeWidth = 1.75, style }) => (
  <Svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round" style={style}>
    <Circle cx="12" cy="12" r="2" />
    <Path d="M16.24 7.76a6 6 0 0 1 0 8.49m-8.48-.01a6 6 0 0 1 0-8.49m11.31-2.82a10 10 0 0 1 0 14.14m-14.14 0a10 10 0 0 1 0-14.14" />
  </Svg>
);

// 29. Siren / Torreta Policial
export const IconSiren: React.FC<IconProps> = ({ size = 20, color = '#0F172A', strokeWidth = 1.75, style }) => (
  <Svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round" style={style}>
    <Path d="M7 18v-6a5 5 0 1 1 10 0v6" />
    <Path d="M5 21a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-1a2 2 0 0 0-2-2H7a2 2 0 0 0-2 2z" />
    <Line x1="12" y1="2" x2="12" y2="4" />
    <Line x1="4.93" y1="4.93" x2="6.34" y2="6.34" />
    <Line x1="19.07" y1="4.93" x2="17.66" y2="6.34" />
  </Svg>
);

