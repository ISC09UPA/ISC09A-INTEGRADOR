import { Stack, useLocalSearchParams } from 'expo-router';
import { useEffect, useState } from 'react';
import { ActivityIndicator, Platform, StyleSheet, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { WebView } from 'react-native-webview';

import { Aviso, Boton, Texto, avisar, mensajeDe } from '@/components/ui';
import { Spacing } from '@/constants/theme';
import { useColores } from '@/hooks/use-colores';
import type { ArchivoLocal } from '@/models/archivo-local.model';
import { archivosService } from '@/services';

/**
 * Visor de PDF dentro de la app, con Compartir y Descargar abajo.
 * iOS: el WebView abre el PDF directo. Android: el WebView no sabe mostrar PDF, así que se dibuja con pdf.js.
 */
export default function VisorPdf() {
  const c = useColores();
  const { nombre } = useLocalSearchParams<{ nombre: string }>();
  const archivo: ArchivoLocal = archivosService.local(nombre);
  const uri = archivo.uri;
  const [html, setHtml] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [ocupado, setOcupado] = useState<'compartir' | 'guardar' | null>(null);

  useEffect(() => {
    if (Platform.OS !== 'android') return;
    archivosService
      .base64(archivo)
      .then((b64) => setHtml(paginaPdfJs(b64, c.background)))
      .catch((e) => setError(mensajeDe(e)));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [nombre, c.background]);

  const accion = async (cual: 'compartir' | 'guardar') => {
    setOcupado(cual);
    try {
      if (cual === 'compartir') await archivosService.compartir(archivo);
      else if ((await archivosService.guardar(archivo)) && Platform.OS === 'android') avisar('PDF guardado', `${nombre} se guardó en la carpeta que elegiste.`);
    } catch (e) {
      avisar('No se pudo completar', mensajeDe(e));
    } finally {
      setOcupado(null);
    }
  };

  const fuente = Platform.OS === 'android' ? (html ? { html, baseUrl: '' } : null) : { uri };

  return (
    <SafeAreaView edges={['bottom', 'left', 'right']} style={{ flex: 1, backgroundColor: c.background }}>
      <Stack.Screen options={{ title: nombre ?? 'PDF' }} />
      <View style={{ flex: 1 }}>
        {error ? (
          <View style={{ padding: Spacing.three }}>
            <Aviso tipo="error" texto={`No se pudo mostrar el PDF: ${error}. Puedes compartirlo o descargarlo con los botones de abajo.`} />
          </View>
        ) : fuente ? (
          <WebView
            source={fuente}
            originWhitelist={['*']}
            allowFileAccess
            allowingReadAccessToURL={uri}
            style={{ flex: 1, backgroundColor: c.background }}
            startInLoadingState
            renderLoading={() => <Cargando />}
            onError={(e) => setError(e.nativeEvent.description)}
            onMessage={(e) => {
              if (e.nativeEvent.data.startsWith('error:')) setError(e.nativeEvent.data.slice(6));
            }}
          />
        ) : (
          <Cargando />
        )}
      </View>
      <View style={[estilos.barra, { borderTopColor: c.line, backgroundColor: c.surface }]}>
        <Boton texto="Compartir" icono="share" variante="contorno" cargando={ocupado === 'compartir'} alPresionar={() => accion('compartir')} style={{ flex: 1 }} />
        <Boton texto="Descargar" icono="download" cargando={ocupado === 'guardar'} alPresionar={() => accion('guardar')} style={{ flex: 1 }} />
      </View>
    </SafeAreaView>
  );
}

function Cargando() {
  const c = useColores();
  return (
    <View style={[StyleSheet.absoluteFill, { alignItems: 'center', justifyContent: 'center', gap: Spacing.two }]}>
      <ActivityIndicator color={c.primary} />
      <Texto tipo="secundario">Abriendo PDF…</Texto>
    </View>
  );
}

/** Página que dibuja cada hoja del PDF a lo ancho de la pantalla con pdf.js (se puede hacer zoom con los dedos). */
function paginaPdfJs(base64: string, fondo: string) {
  return `<!doctype html>
<html><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=5, user-scalable=yes">
<style>
  body { margin: 0; background: ${fondo}; }
  canvas { display: block; width: 100%; margin: 0 0 8px; background: #fff; box-shadow: 0 1px 3px rgba(0,0,0,.25); }
</style>
<script src="https://cdnjs.cloudflare.com/ajax/libs/pdf.js/3.11.174/pdf.min.js"></script>
</head><body>
<script>
  (async function () {
    try {
      pdfjsLib.GlobalWorkerOptions.workerSrc = 'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/3.11.174/pdf.worker.min.js';
      const datos = Uint8Array.from(atob('${base64}'), (ch) => ch.charCodeAt(0));
      const pdf = await pdfjsLib.getDocument({ data: datos }).promise;
      const escala = window.devicePixelRatio || 2;
      for (let n = 1; n <= pdf.numPages; n++) {
        const pagina = await pdf.getPage(n);
        const base = pagina.getViewport({ scale: 1 });
        const vista = pagina.getViewport({ scale: (window.innerWidth / base.width) * escala });
        const lienzo = document.createElement('canvas');
        lienzo.width = vista.width;
        lienzo.height = vista.height;
        document.body.appendChild(lienzo);
        await pagina.render({ canvasContext: lienzo.getContext('2d'), viewport: vista }).promise;
      }
    } catch (e) {
      window.ReactNativeWebView.postMessage('error:' + (e && e.message ? e.message : e));
    }
  })();
</script>
</body></html>`;
}

const estilos = StyleSheet.create({
  barra: {
    flexDirection: 'row',
    gap: Spacing.two,
    padding: Spacing.three,
    borderTopWidth: StyleSheet.hairlineWidth,
  },
});
