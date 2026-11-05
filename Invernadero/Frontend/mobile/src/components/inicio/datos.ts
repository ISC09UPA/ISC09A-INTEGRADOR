// Datos de ejemplo (en el front salen de los servicios/mocks).
export const PLANTAS = [
  {
    id: 'albahaca-1',
    nombre: 'Albahaca Genovesa',
    subtitulo: 'Sector A1 · RPi Activa',
    imagenUrl:
      'https://lh3.googleusercontent.com/aida-public/AB6AXuD5FyR3hRFpV6qdweT1PgXo8v2h_gwnM8Wjq_qkMx8Hw7ppX5B4ZhlPDnL2-XQJeV4KJKxftMb-vdK1d6nXBCq1HM5n9BfuRObRgmxG5iR5a2SfmNd0ZtjGh5v0HB99E14ZPMo_y8oQLPZSQTfN_aPV9a4J25Wg91Hlkl0hwABAjrKU7d19xAuuDnpToZRRbA5N-VjhHhOq191GBZRLeH8YB4EDO7riYWqtV-snFD2FR6CdauWfEnIO',
    vigorPct: 94,
    descripcion: 'Crecimiento óptimo · Estomas hidratados',
    humedad: 52,
    humedadOptima: [45, 65],
    riegoSugeridoMl: 180,
  },
  {
    id: 'tomate-1',
    nombre: 'Tomate Cherry',
    subtitulo: 'Sector B2',
    imagenUrl:
      'https://lh3.googleusercontent.com/aida-public/AB6AXuDdxpaV7c6uJtuQvvAoPuAuqPOOKK0F4q_EthNhLtsBgumBaOGCIJM1mlQ2uzxHG5HYBPvfqbvL-N7UlH2Es50t1flUV0qKJsKUJJ5pxZM1NiSNGDP6xrp0tWjdV7HAmB7rhluFNUZPb4x_u988pMCp4FpBZY9ubYseAz-3nUnPciE42T6LHNVK7M22mpaNFVFU_dMekjgCiBl40wHxLWVs21MnCwlKB9ctjnRns5eCt_EpPD379QgZ',
    vigorPct: 86,
    descripcion: 'Ligera clorosis en hojas basales · Vigilar y ajustar nutrientes',
    humedad: 41,
    humedadOptima: [50, 70],
    riegoSugeridoMl: 220,
  },
  {
    id: 'menta-1',
    nombre: 'Menta',
    subtitulo: 'Sector C1',
    imagenUrl:
      'https://lh3.googleusercontent.com/aida-public/AB6AXuCNzObIBCXmUZ5SRX1JHoOxBVG49ubcEezvINPemmNuD0GHQ0ECq4INsLFPvdmxbfYvFkV4HaJZkmfkqqf1nVKZl35hVrKlD_coCRkBsCoPP4Ullrwe_LvOAsXGejSH-ntQltUL2BSmX0MR_XAG3uP9gT-_qKoWFs910PUXnXewNbZOFQCGt4RwCyI-MEWzVTRaUJfsCKnoHV-q4Ft7F3omurZfkbyI9b66b2Kyt1Uts6NYqfDvM-dm',
    vigorPct: 90,
    descripcion: 'Follaje sano · Buena hidratación',
    humedad: 58,
    humedadOptima: [50, 70],
    riegoSugeridoMl: 150,
  },
];

export const TEMPERATURA = 24.5; // °C
export const LUZ = 12400; // lx
export const NIVEL_DEPOSITO = 72; // %

export type Planta = (typeof PLANTAS)[number];
