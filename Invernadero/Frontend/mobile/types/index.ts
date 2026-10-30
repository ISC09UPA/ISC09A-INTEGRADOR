export type Rol = 'admin' | 'operador';

export interface Usuario {
  id: string;
  nombre: string;
  email: string;
  rol: Rol;
  avatarUrl?: string;
}

export type EstadoSaludPlanta = 'sana' | 'estres_hidrico' | 'hoja_enferma_plaga';

export interface Diagnostico {
  vigorPct: number;
  estado: EstadoSaludPlanta;
  descripcion: string;
  fecha: string;
  humedadSueloPct: number;
}

export interface Planta {
  id: string;
  nombre: string;
  nombreCientifico: string;
  sector: string;
  subtitulo: string;
  imagenUrl: string;
  diagnostico: Diagnostico;
  humedadOptima: [number, number];
  temperaturaOptima: [number, number];
  riegoSugeridoMl: number;
  intervaloRiegoHoras: number;
}

export interface LecturaSensor {
  humedadSuelo: number;
  temperatura: number;
  humedadAmbiente: number;
  luz: number;
  nivelDeposito: number;
  caudalLmin: number;
  flotadorVacio: boolean;
  timestamp: string;
}

export type OrigenRiego = 'automatico' | 'manual' | 'ia';

export interface Riego {
  id: string;
  plantaId: string;
  inicio: string;
  duracionSeg: number;
  volumenMl: number;
  origen: OrigenRiego;
  humedadAntes?: number;
  humedadDespues?: number;
  operadorNombre?: string;
}

export interface EstadoDispositivo {
  online: boolean;
  ultimaSync: string;
  bomba: boolean;
  ventilador: boolean;
  leds: boolean;
  valvulaLlenado: boolean;
  ledIndicador: 'verde' | 'rojo' | 'azul' | 'amarillo';
  modoAuto: boolean;
}

export type TendenciaCrecimiento = 'creciente' | 'estable' | 'decreciente';

export interface Prediccion {
  proximoRiegoEstimado: string;
  volumenSugeridoMl: number;
  tendencia: TendenciaCrecimiento;
  resumen: string;
}

export type CategoriaRegla = 'riego' | 'clima' | 'iluminacion' | 'seguridad';
export type PrioridadRegla = 'alta' | 'media' | 'baja';
export type SensorRegla = 'humedadSuelo' | 'temperatura' | 'luz' | 'nivelDeposito' | 'horario';
export type ActuadorRegla = 'bomba' | 'ventilador' | 'leds' | 'valvulaLlenado';

export interface Regla {
  id: string;
  nombre: string;
  categoria: CategoriaRegla;
  activa: boolean;
  prioridad: PrioridadRegla;
  si: {
    sensor: SensorRegla;
    operador: '<' | '>' | '<=' | '>=' | 'entre';
    umbral: string;
    duranteMin?: number;
  };
  entonces: {
    actuador: ActuadorRegla;
    accion: 'encender' | 'apagar' | 'regar_volumen' | 'bloquear';
    volumenMl?: number;
    hastaValor?: number;
  };
  ultimaEjecucion?: string;
  bloqueadaPorSeguridad?: boolean;
}

export type TipoEventoTimeline = 'riego' | 'alerta' | 'clima' | 'ia';

export interface EventoTimeline {
  id: string;
  tipo: TipoEventoTimeline;
  titulo: string;
  descripcion: string;
  fecha: string;
  plantaId?: string;
  plantaNombre?: string;
  imagenUrl?: string;
  humedadAntes?: number;
  humedadDespues?: number;
  volumenMl?: number;
  nivelDepositoPct?: number;
  picoTemperaturaC?: number;
  certezaVigorPct?: number;
  origen?: OrigenRiego | 'sistema';
}

export type EstadoSaludEscaneo = 'vigorosa_sana' | 'observar' | 'plaga_detectada';

export interface EscaneoPlanta {
  id: string;
  fecha: string;
  especie: string;
  nombreCientifico: string;
  confianzaPct: number;
  salud: EstadoSaludEscaneo;
  descripcionSalud: string;
  imagenUrl: string;
  sectorAsignado?: string;
  condicionesOptimas: {
    humedadSustrato: string;
    temperatura: string;
    fotoperiodo: string;
    riegoSugerido: string;
  };
  perfilAgronomico: string;
  ubicacionCompra?: string;
}

export interface Vivero {
  id: string;
  nombre: string;
  latitud: number;
  longitud: number;
  distanciaKm: number;
  especialidades: string[];
  precioMedio: number;
  abierto: boolean;
  horario: string;
}

export interface Umbrales {
  humedadMinimaPct: number;
  humedadObjetivoPct: number;
  temperaturaVentiladorC: number;
  temperaturaApagadoVentiladorC: number;
  depositoCriticoPct: number;
  maxBombaSeg: number;
  esperaMinRiegosMin: number;
}

export type TipoNotificacion =
  | 'deposito_vacio'
  | 'estres_hidrico'
  | 'enfermedad_detectada'
  | 'riego_realizado'
  | 'dispositivo_desconectado';

export interface NotificacionApp {
  id: string;
  tipo: TipoNotificacion;
  titulo: string;
  cuerpo: string;
  fecha: string;
  leida: boolean;
}
