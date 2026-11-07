import { EscaneoPlanta, EventoTimeline, Planta, Regla, Umbrales, Usuario, Vivero } from "../../types";



const IMG = {
  albahaca:
    'https://lh3.googleusercontent.com/aida-public/AB6AXuD5FyR3hRFpV6qdweT1PgXo8v2h_gwnM8Wjq_qkMx8Hw7ppX5B4ZhlPDnL2-XQJeV4KJKxftMb-vdK1d6nXBCq1HM5n9BfuRObRgmxG5iR5a2SfmNd0ZtjGh5v0HB99E14ZPMo_y8oQLPZSQTfN_aPV9a4J25Wg91Hlkl0hwABAjrKU7d19xAuuDnpToZRRbA5N-VjhHhOq191GBZRLeH8YB4EDO7riYWqtV-snFD2FR6CdauWfEnIO',
  tomate:
    'https://lh3.googleusercontent.com/aida-public/AB6AXuDdxpaV7c6uJtuQvvAoPuAuqPOOKK0F4q_EthNhLtsBgumBaOGCIJM1mlQ2uzxHG5HYBPvfqbvL-N7UlH2Es50t1flUV0qKJsKUJJ5pxZM1NiSNGDP6xrp0tWjdV7HAmB7rhluFNUZPb4x_u988pMCp4FpBZY9ubYseAz-3nUnPciE42T6LHNVK7M22mpaNFVFU_dMekjgCiBl40wHxLWVs21MnCwlKB9ctjnRns5eCt_EpPD379QgZ',
  menta:
    'https://lh3.googleusercontent.com/aida-public/AB6AXuCNzObIBCXmUZ5SRX1JHoOxBVG49ubcEezvINPemmNuD0GHQ0ECq4INsLFPvdmxbfYvFkV4HaJZkmfkqqf1nVKZl35hVrKlD_coCRkBsCoPP4Ullrwe_LvOAsXGejSH-ntQltUL2BSmX0MR_XAG3uP9gT-_qKoWFs910PUXnXewNbZOFQCGt4RwCyI-MEWzVTRaUJfsCKnoHV-q4Ft7F3omurZfkbyI9b66b2Kyt1Uts6NYqfDvM-dm',
  hojaAlerta:
    'https://lh3.googleusercontent.com/aida-public/AB6AXuA0_ahaqO2L5MArM1ymGnYrbyheq5tc0Ay4XzgXaaavZ59rEC-wsrr_pnpfSm6uhmQYQjBVXGJnL4PqZjWLTxSpig8nMXdMt3iEa41_ENG9eHzS1UYY1UmHtPD5XLPacWJISIKWa0j0QLGIPr2G2dbXYMTXkIL2hzx1VJGSuYCvx9xr-L_0_HuI7ji14eTZAeVW_-bmPuPqlleGjxGiMz7yF75sTv3XMPJhHqT5ph0xqjRcWJ5YKA52',
  basilVivo:
    'https://lh3.googleusercontent.com/aida-public/AB6AXuCEEDiT32I33dn49coYdCDPASJuFq1S9endItkzyLqhs-dM7kQhWH_KtfOPQNxdrCwHioPWip-YmtI6l78XajbwB7qvuvqlt4JyKtgE3oamhfH_V_56lgftT1-D246Z3AYbBVvHUHRHiWl0Ao9Gbt25tMEffj0pLj3C00FsJ-AU5KfiD_zvxKkMrP1SgBonnLjjoGe-unTTUmKIsui69SpWM7TAXPtVcDytwMe7GZhBDFGJPrxQQb_8',
  tomateMuestra:
    'https://lh3.googleusercontent.com/aida-public/AB6AXuCYmOsaLQaLsHnXsrZE6NIqxjqAbttHYFCGgJWOuzouo5lELGC8IQjlE258GZLgVqZdHHXcZv24hBY2qr18jOEl_wzHh-B3kesEJhNPewaI6raOpoY8DxhqUThZQYkfC9pV6GTw_AiwOxn1slaGIFP0Y8AHidUw85dX6FszeUDvyp0avZZ1efwLdjAVlbTXtsfMVySz8sZcNL77H3g2dJl6mR2iH7UDtEIeAUJ85ACRPMCDL5uFRETL',
  romero:
    'https://lh3.googleusercontent.com/aida-public/AB6AXuA0rzhAHbOh42i5_saiuR7pe6VeRL5vaZVAmMAwqBQ_SlCtLSuD9gR02aiu5w00SAJ7o1viFSgBrdcT8qyEZU_5jgX9Wd9kwpkt5DrP66ZRFd8FOF4EZDdmNqEj4pkkl-PIskT7mtfnti6RFynBqI-y4a6llqNFNpsriYmZVBbXq1iLauO_48Hd_V84G9exACQ2cRQxq2TmVHL3VRSjwgrIvTg1-g4TAiR6J9-jGJFtG-cHhRBgpuEs',
  mentaMuestra:
    'https://lh3.googleusercontent.com/aida-public/AB6AXuCrgFRIJiLSeHA_uxgQ-0UxcB0sgaC5JBKmQYMOJl1yelGX2Fyt0_vP_SN2TBqWWVAuX0RVMy4QQ8N0ScbpDEurEmJjBI6ukxazjl0btIlOoUCAXUDs2ZwqN68HV5Gm18BBVZilsRA8PqaKmloqvzhHn7Rtjyxzv2GzKMJ3DrS3QpXnqT-HPI2JKaEB_NV5-QP4-ieJroymL8Jo6YIlYTQEkix6b3_uCGT9anAlf7w8VoMaOna2MgI3',
};

export const PLANTAS_SEMILLA: Planta[] = [
  {
    id: 'albahaca-1',
    nombre: 'Albahaca Genovesa',
    nombreCientifico: 'Ocimum basilicum L.',
    sector: 'Sector A1',
    subtitulo: 'Sector A1 · RPi Activa',
    imagenUrl: IMG.albahaca,
    diagnostico: {
      vigorPct: 94,
      estado: 'sana',
      descripcion: 'Crecimiento óptimo · Estomas hidratados',
      fecha: new Date(Date.now() - 26 * 60 * 60 * 1000).toISOString(),
      humedadSueloPct: 52,
    },
    humedadOptima: [45, 65],
    temperaturaOptima: [20, 26],
    riegoSugeridoMl: 180,
    intervaloRiegoHoras: 14,
  },
  {
    id: 'tomate-1',
    nombre: 'Tomate Cherry',
    nombreCientifico: 'Solanum lycopersicum',
    sector: 'Sector B2',
    subtitulo: 'Sector B2',
    imagenUrl: IMG.tomate,
    diagnostico: {
      vigorPct: 86,
      estado: 'hoja_enferma_plaga',
      descripcion: 'Ligera clorosis en hojas basales · Vigilar y ajustar nutrientes',
      fecha: new Date(Date.now() - 15 * 60 * 1000).toISOString(),
      humedadSueloPct: 41,
    },
    humedadOptima: [50, 70],
    temperaturaOptima: [21, 27],
    riegoSugeridoMl: 220,
    intervaloRiegoHoras: 12,
  },
  {
    id: 'menta-1',
    nombre: 'Menta Piperita',
    nombreCientifico: 'Mentha × piperita',
    sector: 'Sector C1',
    subtitulo: 'Sector C1',
    imagenUrl: IMG.menta,
    diagnostico: {
      vigorPct: 91,
      estado: 'estres_hidrico',
      descripcion: 'Sustrato por debajo del rango ideal · Riego preventivo recomendado',
      fecha: new Date(Date.now() - 3 * 60 * 60 * 1000).toISOString(),
      humedadSueloPct: 34,
    },
    humedadOptima: [50, 70],
    temperaturaOptima: [18, 24],
    riegoSugeridoMl: 200,
    intervaloRiegoHoras: 16,
  },
];

export const USUARIOS_SEMILLA: Usuario[] = [
  { id: 'u-admin', nombre: 'Admin Demo', email: 'admin@demo.com', rol: 'admin' },
  { id: 'u-operador', nombre: 'Operador Demo', email: 'operador@demo.com', rol: 'operador' },
];

export const REGLAS_SEMILLA: Regla[] = [
  {
    id: 'r-riego-sequedad',
    nombre: 'Riego automático por sequedad',
    categoria: 'riego',
    activa: true,
    prioridad: 'alta',
    si: { sensor: 'humedadSuelo', operador: '<', umbral: '35', duranteMin: 10 },
    entonces: {
      actuador: 'bomba',
      accion: 'regar_volumen',
      volumenMl: 180,
      hastaValor: 60,
    },
    ultimaEjecucion: new Date(Date.now() - 6 * 60 * 60 * 1000).toISOString(),
  },
  {
    id: 'r-ventilador-calor',
    nombre: 'Control térmico de mediodía',
    categoria: 'clima',
    activa: true,
    prioridad: 'media',
    si: { sensor: 'temperatura', operador: '>=', umbral: '30' },
    entonces: { actuador: 'ventilador', accion: 'encender' },
    ultimaEjecucion: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(),
  },
  {
    id: 'r-ventilador-frio',
    nombre: 'Apagar ventilador al enfriarse',
    categoria: 'clima',
    activa: true,
    prioridad: 'baja',
    si: { sensor: 'temperatura', operador: '<=', umbral: '28' },
    entonces: { actuador: 'ventilador', accion: 'apagar' },
  },
  {
    id: 'r-bloqueo-deposito',
    nombre: 'Seguridad antibloqueo por depósito bajo',
    categoria: 'seguridad',
    activa: true,
    prioridad: 'alta',
    si: { sensor: 'nivelDeposito', operador: '<', umbral: '10' },
    entonces: { actuador: 'bomba', accion: 'bloquear' },
    bloqueadaPorSeguridad: true,
  },
  {
    id: 'r-max-bomba',
    nombre: 'Tiempo máximo de bomba',
    categoria: 'seguridad',
    activa: true,
    prioridad: 'alta',
    si: { sensor: 'humedadSuelo', operador: '<', umbral: '99' },
    entonces: { actuador: 'bomba', accion: 'bloquear' },
    bloqueadaPorSeguridad: true,
  },
  {
    id: 'r-luces-fotoperiodo',
    nombre: 'Ciclo diurno de crecimiento',
    categoria: 'iluminacion',
    activa: true,
    prioridad: 'baja',
    si: { sensor: 'horario', operador: 'entre', umbral: '07:00-21:00' },
    entonces: { actuador: 'leds', accion: 'encender' },
  },
];

export const UMBRALES_SEMILLA: Umbrales = {
  humedadMinimaPct: 35,
  humedadObjetivoPct: 60,
  temperaturaVentiladorC: 30,
  temperaturaApagadoVentiladorC: 28,
  depositoCriticoPct: 10,
  maxBombaSeg: 10,
  esperaMinRiegosMin: 45,
};

function iso(haceMinutos: number): string {
  return new Date(Date.now() - haceMinutos * 60 * 1000).toISOString();
}

export const EVENTOS_SEMILLA: EventoTimeline[] = [
  {
    id: 'e-1',
    tipo: 'alerta',
    titulo: 'Alerta Fitosanitaria · Azure AI',
    descripcion:
      'Ligera clorosis detectada en hojas basales con 86% de certeza mediante visión computacional.',
    fecha: iso(15),
    plantaId: 'tomate-1',
    plantaNombre: 'Tomate Cherry',
    imagenUrl: IMG.hojaAlerta,
    certezaVigorPct: 86,
  },
  {
    id: 'e-2',
    tipo: 'riego',
    titulo: 'Riego por Umbral Autónomo',
    descripcion:
      'Sensor capacitivo Z-01 activó electroválvula tras descender del umbral mínimo configurado.',
    fecha: iso(45),
    plantaId: 'albahaca-1',
    plantaNombre: 'Albahaca #1',
    humedadAntes: 38,
    humedadDespues: 54,
    volumenMl: 180,
    origen: 'automatico',
  },
  {
    id: 'e-3',
    tipo: 'clima',
    titulo: 'Ventilación forzada activada',
    descripcion:
      'Temperatura de dosel superó los 28.5 °C. Extractor RPi GPIO 27 encendido durante 15 minutos.',
    fecha: iso(150),
    plantaId: undefined,
    plantaNombre: 'Invernadero General',
    picoTemperaturaC: 29.1,
    origen: 'sistema',
  },
  {
    id: 'e-4',
    tipo: 'alerta',
    titulo: 'Nivel de Reserva Crítico',
    descripcion:
      'Sensor ultrasónico HC-SR04 reporta volumen inferior al 25% (aprox. 1.2 L restantes). Riegos futuros en riesgo.',
    fecha: iso(300),
    plantaNombre: 'Depósito Central',
    nivelDepositoPct: 24,
  },
  {
    id: 'e-5',
    tipo: 'riego',
    titulo: 'Riego Manual Aplicado',
    descripcion:
      'Dispensación preventiva de 200 mL solicitada por el usuario desde el módulo de Control Remoto.',
    fecha: iso(560),
    plantaId: 'menta-1',
    plantaNombre: 'Menta Piperita',
    volumenMl: 200,
    origen: 'manual',
  },
  {
    id: 'e-6',
    tipo: 'ia',
    titulo: 'Inspección Periódica Azure AI',
    descripcion:
      'Escaneo multiespectral de las 3 zonas. Follaje general evaluado con 94% de vigor biológico.',
    fecha: iso(24 * 60 + 90),
    plantaNombre: 'Inspección Global',
    certezaVigorPct: 94,
  },
];

export const ESCANEOS_SEMILLA: EscaneoPlanta[] = [
  {
    id: 's-1',
    fecha: iso(35),
    especie: 'Tomate Cherry',
    nombreCientifico: 'Solanum lycopersicum',
    confianzaPct: 99.1,
    salud: 'vigorosa_sana',
    descripcionSalud: 'Vigorosa y sana · Sin patógenos detectados',
    imagenUrl: IMG.tomateMuestra,
    sectorAsignado: 'Sector A-2',
    condicionesOptimas: {
      humedadSustrato: '50% - 70%',
      temperatura: '21°C - 27°C',
      fotoperiodo: '6 - 8h Luz directa',
      riegoSugerido: '220 mL',
    },
    perfilAgronomico:
      'Herbácea de crecimiento vigoroso con fructificación en racimo. Sensible a desajustes hídricos que causan agrietamiento del fruto. Recomendado riego localizado y drenaje eficiente.',
    ubicacionCompra: 'Vivero Hortícola Central',
  },
  {
    id: 's-2',
    fecha: iso(26 * 60),
    especie: 'Romero Aromático',
    nombreCientifico: 'Salvia rosmarinus',
    confianzaPct: 97.8,
    salud: 'vigorosa_sana',
    descripcionSalud: 'Follaje aromático denso · Turgencia ideal',
    imagenUrl: IMG.romero,
    sectorAsignado: 'Sector C-1',
    condicionesOptimas: {
      humedadSustrato: '30% - 45%',
      temperatura: '18°C - 28°C',
      fotoperiodo: '8 - 10h Luz directa',
      riegoSugerido: '120 mL',
    },
    perfilAgronomico:
      'Arbusto aromático perenne de raíz delicada en exceso de humedad. Prefiere sustrato liviano y bien drenado; tolera sequía moderada.',
    ubicacionCompra: 'Mesa Grow & Bonsái',
  },
  {
    id: 's-3',
    fecha: iso(30 * 24 * 60),
    especie: 'Menta Piperita',
    nombreCientifico: 'Mentha × piperita',
    confianzaPct: 96.5,
    salud: 'observar',
    descripcionSalud: 'Ligera pérdida de turgencia · Revisar riego',
    imagenUrl: IMG.mentaMuestra,
    sectorAsignado: 'Macetón #04',
    condicionesOptimas: {
      humedadSustrato: '55% - 75%',
      temperatura: '18°C - 24°C',
      fotoperiodo: '4 - 6h Media sombra',
      riegoSugerido: '200 mL',
    },
    perfilAgronomico:
      'Perenne invasiva con alta demanda hídrica. Cultivar en contenedores para controlar su expansión; evitar moho con buena ventilación.',
    ubicacionCompra: 'Vivero La Cosecha',
  },
];

export const IMG_VISTA_VIVA = IMG.basilVivo;

export const VIVEROS_SEMILLA: Vivero[] = [
  {
    id: 'v-1',
    nombre: 'Vivero La Cosecha',
    latitud: 19.4326 + 0.012,
    longitud: -99.1332 + 0.009,
    distanciaKm: 1.4,
    especialidades: ['Aromáticas', 'Hortalizas', 'Semillas'],
    precioMedio: 45,
    abierto: true,
    horario: '08:00 - 18:00',
  },
  {
    id: 'v-2',
    nombre: 'Mesa Grow & Bonsái',
    latitud: 19.4326 - 0.015,
    longitud: -99.1332 + 0.018,
    distanciaKm: 2.6,
    especialidades: ['Aromáticas', 'Ornamentales'],
    precioMedio: 60,
    abierto: true,
    horario: '09:00 - 19:00',
  },
  {
    id: 'v-3',
    nombre: 'Vivero Hortícola Central',
    latitud: 19.4326 + 0.024,
    longitud: -99.1332 - 0.011,
    distanciaKm: 3.3,
    especialidades: ['Hortalizas', 'Insumos de riego', 'Sustratos'],
    precioMedio: 38,
    abierto: false,
    horario: '07:00 - 17:00',
  },
];

export const UBICACION_USUARIO_MOCK = { latitud: 19.4326, longitud: -99.1332 };