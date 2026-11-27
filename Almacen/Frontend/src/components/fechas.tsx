import { useEffect, useState } from 'react';
import { View } from 'react-native';

import { Campo, Fila, Interruptor } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { Fechas } from '@/lib/formato';

/** Rango de fechas en formato yyyy-MM-dd (el mismo que espera la API). */
export interface Rango {
  desde: string;
  hasta: string;
}

<<<<<<< HEAD
export const rangoMes = (): Rango => ({ desde: Fechas.inicioMes(), hasta: Fechas.hoy() });

const ATAJOS: { texto: string; rango: () => Rango }[] = [
  { texto: 'Hoy', rango: () => ({ desde: Fechas.hoy(), hasta: Fechas.hoy() }) },
  { texto: '7 días', rango: () => ({ desde: Fechas.restarDias(6), hasta: Fechas.hoy() }) },
  { texto: 'Este mes', rango: rangoMes },
  { texto: 'Mes pasado', rango: Fechas.mesAnterior },
  { texto: 'Este año', rango: () => ({ desde: Fechas.inicioAnio(), hasta: Fechas.hoy() }) },
];

/** Periodo con atajos y captura manual en formato AAAA-MM-DD. Solo avisa cuando ambas fechas son válidas. */
export function RangoFechas({ valor, alCambiar, opcional }: { valor: Rango | null; alCambiar: (r: Rango | null) => void; opcional?: boolean }) {
  const c = useColores();
  const [desde, setDesde] = useState(valor?.desde ?? '');
  const [hasta, setHasta] = useState(valor?.hasta ?? '');

  const aplicar = (d: string, h: string) => {
    setDesde(d);
    setHasta(h);
    if (Fechas.valida(d) && Fechas.valida(h)) alCambiar({ desde: d, hasta: h });
    else if (opcional && !d && !h) alCambiar(null);
  };

  const errorDesde = desde && !Fechas.valida(desde) ? 'Usa AAAA-MM-DD' : null;
  const errorHasta = hasta && !Fechas.valida(hasta) ? 'Usa AAAA-MM-DD' : desde && hasta && hasta < desde ? 'Antes de "desde"' : null;

  return (
    <View style={{ gap: Spacing.two }}>
      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 6 }}>
        {ATAJOS.map((a) => {
          const r = a.rango();
          const activo = valor?.desde === r.desde && valor?.hasta === r.hasta;
          return (
            <Pressable
              key={a.texto}
              onPress={() => aplicar(r.desde, r.hasta)}
              style={{ borderWidth: 1, borderColor: activo ? c.primary : c.line, backgroundColor: activo ? c.primary + '1A' : c.surface, borderRadius: 999, paddingHorizontal: 12, paddingVertical: 6 }}>
              <Text style={{ color: activo ? c.primary : c.text, fontSize: 13, fontWeight: activo ? '600' : '400' }}>{a.texto}</Text>
            </Pressable>
          );
        })}
        {opcional ? (
          <Pressable onPress={() => aplicar('', '')} style={{ borderWidth: 1, borderColor: !valor ? c.primary : c.line, borderRadius: 999, paddingHorizontal: 12, paddingVertical: 6 }}>
            <Text style={{ color: !valor ? c.primary : c.text, fontSize: 13 }}>Todo</Text>
          </Pressable>
        ) : null}
      </View>
      <Fila>
        <Campo etiqueta="Desde" value={desde} onChangeText={(t) => aplicar(t, hasta)} placeholder="AAAA-MM-DD" error={errorDesde} autoCapitalize="none" style={{ flex: 1 }} />
        <Campo etiqueta="Hasta" value={hasta} onChangeText={(t) => aplicar(desde, t)} placeholder="AAAA-MM-DD" error={errorHasta} autoCapitalize="none" style={{ flex: 1 }} />
      </Fila>
    </View>
  );
}

/** Una sola fecha (documentos). */
export function CampoFecha({ etiqueta, valor, alCambiar }: { etiqueta: string; valor: string; alCambiar: (v: string) => void }) {
=======
/** Fecha capturada como texto yyyy-MM-dd; marca el error si no es válida. */
export function CampoFecha({ etiqueta, valor, alCambiar }: { etiqueta: string; valor: string; alCambiar: (fecha: string) => void }) {
>>>>>>> origin/7jaylosmolcafriends-almacen-develop
  return (
    <Campo
      etiqueta={etiqueta}
      value={valor}
      onChangeText={alCambiar}
      placeholder="AAAA-MM-DD"
      autoCapitalize="none"
<<<<<<< HEAD
      error={valor && !Fechas.valida(valor) ? 'Usa el formato AAAA-MM-DD' : valor > Fechas.hoy() ? 'No puede ser una fecha futura' : null}
    />
  );
}
=======
      autoCorrect={false}
      keyboardType="numbers-and-punctuation"
      maxLength={10}
      error={valor && !Fechas.valida(valor) ? 'Usa el formato AAAA-MM-DD.' : null}
    />
  );
}

/**
 * Desde / hasta. Con <c>opcional</c> se activa con un interruptor; mientras está apagado el valor es null.
 * Solo avisa el cambio cuando ambas fechas son válidas y desde ≤ hasta.
 */
export function RangoFechas({ valor, alCambiar, opcional }: { valor: Rango | null; alCambiar: (r: Rango | null) => void; opcional?: boolean }) {
  const [activo, setActivo] = useState(!opcional || !!valor);
  const [desde, setDesde] = useState(valor?.desde ?? Fechas.inicioMes());
  const [hasta, setHasta] = useState(valor?.hasta ?? Fechas.hoy());

  useEffect(() => {
    if (!activo) {
      if (valor) alCambiar(null);
      return;
    }
    if (Fechas.valida(desde) && Fechas.valida(hasta) && desde <= hasta && (valor?.desde !== desde || valor?.hasta !== hasta))
      alCambiar({ desde, hasta });
  }, [activo, desde, hasta, valor, alCambiar]);

  return (
    <View style={{ gap: Spacing.two }}>
      {opcional ? <Interruptor etiqueta="Filtrar por fechas" valor={activo} alCambiar={setActivo} /> : null}
      {activo ? (
        <Fila>
          <View style={{ flex: 1 }}>
            <CampoFecha etiqueta="Desde" valor={desde} alCambiar={setDesde} />
          </View>
          <View style={{ flex: 1 }}>
            <CampoFecha etiqueta="Hasta" valor={hasta} alCambiar={setHasta} />
          </View>
        </Fila>
      ) : null}
    </View>
  );
}
>>>>>>> origin/7jaylosmolcafriends-almacen-develop
