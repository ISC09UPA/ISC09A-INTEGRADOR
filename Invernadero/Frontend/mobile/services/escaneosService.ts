import type { EscaneoPlanta } from '../types';
import { espera, escribir, leer } from './bd';
import { nuevoIdEscaneo } from './ids';
import { IMG_VISTA_VIVA, PLANTAS_SEMILLA } from '@/mocks/datos';


// Identificación de plantas con foto (mock de Azure AI Vision).

const CATALOGO_MOCK: Omit<EscaneoPlanta, 'id' | 'fecha' | 'imagenUrl'>[] = [
  {
    especie: 'Albahaca Genovesa',
    nombreCientifico: 'Ocimum basilicum L.',
    confianzaPct: 98,
    salud: 'vigorosa_sana',
    descripcionSalud: 'Sin patógenos detectados · Clorofila excelente (94%)',
    sectorAsignado: undefined,
    condicionesOptimas: {
      humedadSustrato: '45% - 60%',
      temperatura: '20°C - 26°C',
      fotoperiodo: '6 - 8h Luz directa',
      riegoSugerido: '180 mL cada 14 h',
    },
    perfilAgronomico:
      'Herbácea aromática anual de follaje tierno y alta demanda osmótica. Muy sensible a bajas temperaturas y compactación radicular. Recomendado microgoteo focalizado en base para evitar moho foliar.',
    ubicacionCompra: 'Vivero La Cosecha · $45',
  },
  {
    especie: 'Tomate Cherry',
    nombreCientifico: 'Solanum lycopersicum',
    confianzaPct: 99.1,
    salud: 'vigorosa_sana',
    descripcionSalud: 'Vigorosa y sana · Sin patógenos detectados',
    sectorAsignado: undefined,
    condicionesOptimas: {
      humedadSustrato: '50% - 70%',
      temperatura: '21°C - 27°C',
      fotoperiodo: '6 - 8h Luz directa',
      riegoSugerido: '220 mL cada 12 h',
    },
    perfilAgronomico:
      'Cultivar precoz de fructificación en racimo. Requiere tutorado y riego localizado constante para evitar agrietamiento del fruto y apicalnecrosis.',
    ubicacionCompra: 'Vivero Hortícola Central · $38',
  },
  {
    especie: 'Menta Piperita',
    nombreCientifico: 'Mentha × piperita',
    confianzaPct: 96.5,
    salud: 'observar',
    descripcionSalud: 'Ligera pérdida de turgencia · Revisar riego en 2 días',
    sectorAsignado: undefined,
    condicionesOptimas: {
      humedadSustrato: '55% - 75%',
      temperatura: '18°C - 24°C',
      fotoperiodo: '4 - 6h Media sombra',
      riegoSugerido: '200 mL cada 16 h',
    },
    perfilAgronomico:
      'Perenne de alta demanda hídrica y crecimiento agresivo. Cultivar en contenedor; buena ventilación para evitar mildiu.',
    ubicacionCompra: 'Mesa Grow & Bonsái · $60',
  },
];

/** Análisis "IA": latencia + selección seudoaleatoria consistente por imagen. */
export async function identificarPlanta(urImagenLocal: string | null): Promise<EscaneoPlanta> {
  await espera(1400); // análisis simulado
  const indice = Math.floor(Math.random() * CATALOGO_MOCK.length);
  const base = CATALOGO_MOCK[indice];
  const escaneo: EscaneoPlanta = {
    ...base,
    id: nuevoIdEscaneo(),
    fecha: new Date().toISOString(),
    imagenUrl: urImagenLocal ?? PLANTAS_SEMILLA[indice]?.imagenUrl ?? IMG_VISTA_VIVA,
    confianzaPct: Math.min(99.8, base.confianzaPct + Math.random() * 1.5 - 0.7),
  };
  await escribir((bd) => {
    bd.escaneos.unshift(escaneo);
  });
  return escaneo;
}

export async function listarEscaneos(): Promise<EscaneoPlanta[]> {
  await espera(250);
  const bd = await leer();
  return [...bd.escaneos].sort((a, b) => b.fecha.localeCompare(a.fecha));
}

export async function asignarSector(escaneoId: string, sector: string): Promise<void> {
  await espera(200);
  await escribir((bd) => {
    const escaneo = bd.escaneos.find((s) => s.id === escaneoId);
    if (escaneo) escaneo.sectorAsignado = sector;
  });
}
