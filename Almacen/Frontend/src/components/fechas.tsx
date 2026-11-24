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

/** Fecha capturada como texto yyyy-MM-dd; marca el error si no es válida. */
export function CampoFecha({ etiqueta, valor, alCambiar }: { etiqueta: string; valor: string; alCambiar: (fecha: string) => void }) {
  return (
    <Campo
      etiqueta={etiqueta}
      value={valor}
      onChangeText={alCambiar}
      placeholder="AAAA-MM-DD"
      autoCapitalize="none"
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
