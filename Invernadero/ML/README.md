# Machine Learning · Invernadero Inteligente

> Entrenado el 17 de noviembre de 2026 · Modelo versión 1.0.0 · Workspace de Azure: `ml-invernadero`

Este es el segundo sistema de IA del proyecto. Custom Vision trabaja con **fotos**; esto trabaja
con los **números de los sensores** y responde tres preguntas:

1. **¿Cuánto va a crecer la planta en los próximos 7 días?** (lo pide la rúbrica)
2. **¿Cuándo toca el próximo riego y cuánta agua se va a usar?**
3. **¿Qué umbral de humedad conviene cargarle a la Raspberry?** — con esto el riego deja de
   usar un umbral fijo (`HUMEDAD_MINIMA = 35`) y pasa a ser adaptativo.

```
Lecturas y riegos (Azure SQL)  ─┐
Mediciones de la planta (Juan) ─┼─▶ entrenar.py ─▶ modelo_invernadero.joblib
                                │                        │
                                │            score.py en Azure ML (endpoint)
                                │                        │
                                └──────▶ API Telemetria ─┴─▶ App movil
                                              │
                                              └─▶ umbral sugerido ─▶ IoT Hub ─▶ Raspberry
```

---

## 1. Los datos

> ### ⚠️ Hoy el historial es SIMULADO
> La Raspberry todavía no envía datos a IoT Hub, así que en producción solo hay 4 lecturas de
> prueba (`TEST-PORTAL-*`) y ningún riego. El historial con el que está entrenado este modelo lo
> genera `generar_datos.py` usando **la misma lógica de riego y los mismos parámetros** de
> `Invernadero/Embebidos/config.py`.
>
> El documento de contexto del proyecto lo permite de forma explícita: *"si no alcanzan los datos
> reales, se complementa con datos simulados y se documenta"*. Esto es esa documentación.
> **En cuanto lleguen datos reales hay que reentrenar** (sección 6); el código no cambia.

| Archivo | Qué es | De dónde sale cuando sea real |
|---|---|---|
| `datos/lecturas.csv` | 1 440 horas (60 días) × 7 sensores = 10 080 lecturas | `GET /api/Lecturas/exportar.csv` |
| `datos/riegos.csv` | 142 pulsos de bomba agrupados en 23 sesiones de riego | `GET /api/Riegos` |
| `datos/mediciones.csv` | 60 mediciones diarias: altura en cm y número de hojas | **medición manual de Juan con una regla** |
| `datos/metadatos.json` | Semilla, fechas y parámetros con los que se generó | — |

Los CSV tienen **exactamente las columnas que entrega la API**, para que el día que haya datos
reales se reentrene sin tocar una línea de código.

`mediciones.csv` es el único que la API no puede dar: la altura y las hojas se miden a mano. Es la
tarea diaria de Juan en el cronograma y **sin ella el modelo de crecimiento no se puede
reentrenar con datos reales.** Hoy esas mediciones no existen.

### Qué tan creíble es el historial simulado

Los sensores siguen el catálogo del contrato (`temperatura`, `humedad_aire`, `humedad_suelo`,
`luz`, `nivel_deposito`, `caudal`, `flotador_vacio`) con física sencilla pero coherente:

- temperatura con ciclo día/noche (12 – 32 °C) y frentes de varios días;
- luz con arco solar de 7:00 a 19:00 y nubosidad variable (máx. 1 471 lux);
- la humedad del suelo baja por evapotranspiración, **más rápido si hace calor y hay más luz**,
  y sube cuando riega la bomba;
- el riego usa el umbral, el objetivo, el tiempo máximo de bomba y la espera de `config.py`;
- el depósito se vacía con cada riego y se rellena a mano cuando baja de 15 % (pasó 2 veces);
- la planta crece con curva logística (6 → 39.5 cm en 60 días) frenada por falta de agua, calor
  fuera de 22 – 28 °C y poca luz;
- las mediciones de la planta traen error de medición, como una regla real.

Resultado: la humedad del suelo se mueve entre 35 y 63 % (media 49 %, desviación 8), hay una
sesión de riego cada 2.5 días en promedio y se usan unos 130 mL de agua al día.

### 🐛 Un hallazgo del simulador: `HUMEDAD_OBJETIVO` es inalcanzable hoy

Al simular con la lógica exacta de `Embebidos/main.py` salió que **la humedad nunca llega a
`HUMEDAD_OBJETIVO = 60 %`**. Un pulso de bomba de `TIEMPO_MAX_BOMBA_S = 10` segundos entrega unos
55 mL y sube la humedad apenas ~3.6 puntos; como el riego solo vuelve a arrancar cuando la humedad
baja de `HUMEDAD_MINIMA = 35`, la planta se queda oscilando entre 35 y 38 % para siempre y la
condición `humedad >= HUMEDAD_OBJETIVO` de `main.py` nunca se cumple.

El simulador asume la **intención** del diseño (una sesión da pulsos cada 5 minutos hasta llegar a
60 %), y por eso el historial sí tiene variación. **Hay que arreglar `main.py`** para que haga lo
mismo: mantener abierta la sesión de riego mientras la humedad siga por debajo del objetivo. Si no
se arregla, la maqueta real va a regar mucho menos de lo que supone este modelo.

---

## 2. Los modelos

Cuatro regresiones de scikit-learn, guardadas juntas en `modelos/modelo_invernadero.joblib`.

| Modelo | Qué predice | Unidad | Variables de entrada |
|---|---|---|---|
| `crecimiento_altura` | cuánto crece la planta en 7 días | cm | potencial logístico, altura, hojas, y de los últimos 7 días: temp. media y máxima, luz media, humedad de suelo media, horas de estrés y agua aplicada |
| `crecimiento_hojas` | hojas nuevas en 7 días | hojas | las mismas |
| `riego_horas` | horas hasta la próxima sesión de riego | horas | humedad de suelo, temperatura, humedad del aire, luz, nivel del depósito, hora del día (seno/coseno), velocidad de secado a 3 h y 6 h, temp. media 6 h, temp. máx. 24 h, luz media 6 h y 24 h, agua y riegos de las últimas 24 h |
| `riego_agua_24h` | agua que se va a usar en 24 h | mL | las mismas |

### Tres decisiones de método que conviene saber defender

**1. El objetivo del crecimiento es el incremento, no la altura.** Si el modelo predijera la
altura absoluta a 7 días saldría un R² de 0.99, pero sería un engaño: la altura de hoy ya explica
casi toda la altura de la semana que viene. Predecir **cuántos centímetros crece** es el problema
real y es mucho más difícil.

**2. La separación entrenamiento/prueba es temporal, nunca aleatoria.** Lo viejo entrena (75 %),
lo nuevo prueba (25 %). Con una serie de tiempo, repartir al azar le filtra el futuro al modelo y
las métricas salen infladas. La validación cruzada es `TimeSeriesSplit` por la misma razón.

**3. El crecimiento usa modelos lineales a propósito.** Un bosque aleatorio nunca predice fuera
del rango que vio al entrenar, y la planta siempre está creciendo hacia tamaños nuevos. Además se
le da al modelo la forma de la curva: `potencial_logistico = altura × (1 − altura / 45)`, que vale
casi cero cuando la planta es chica y cuando ya se saturó. El modelo aprende la **tasa** de
crecimiento por unidad de potencial, y eso sí se puede extrapolar. Con esto el error bajó de
1.45 cm a 0.63 cm.

---

## 3. Métricas

Medidas sobre el tramo de prueba, que el modelo **nunca vio al entrenar**. La última columna es la
referencia tonta: predecir siempre el promedio de lo que se vio al entrenar. Un modelo que no le
gane a esa referencia no sirve para nada.

| Modelo | Elegido | Filas (entrena/prueba) | MAE | RMSE | R² | MAE de la referencia |
|---|---|---|---|---|---|---|
| `crecimiento_altura` | Ridge (α=10) | 39 / 13 | **0.63 cm** | 0.73 | −0.46 | 2.53 cm |
| `crecimiento_hojas` | Ridge (α=3) | 39 / 13 | **1.28 hojas** | 1.48 | 0.27 | 2.09 hojas |
| `riego_horas` | Gradient Boosting | 1 064 / 355 | **11.4 h** | 14.9 | 0.59 | 20.0 h |
| `riego_agua_24h` | Random Forest | 1 053 / 352 | **29 mL** | 66.1 | 0.88 | 168.5 mL |

Todo el detalle (candidatos probados, importancia de cada variable, fechas de cada tramo, versiones
de las librerías) está en `modelos/metricas.json`.

**Cómo leer esto:**

- El modelo de **crecimiento** se equivoca por 0.63 cm al predecir el crecimiento de una semana,
  cuatro veces mejor que la referencia. Su **R² es negativo y eso no es un error**: el tramo de
  prueba cae en la fase en que la planta ya casi no crece, así que la variación real ahí es
  mínima (desviación 1.33 cm) y el R² deja de ser informativo. Con solo 52 mediciones diarias, el
  MAE es la métrica que vale.
- El modelo de **riego por horas** se equivoca por 11.4 horas sobre sesiones que ocurren cada 2.5
  días. Le gana a la referencia (20 h) y le gana por mucho a la regla física
  `(humedad − 35) ÷ velocidad de secado`, que se equivoca por **39 horas**. Es decir: aquí el ML
  aporta algo real, no es adorno.
- El modelo de **agua a 24 h** es el mejor (R² 0.88) porque depende casi solo de la humedad actual.
- En los dos modelos de riego, la variable más importante es la **humedad del suelo** (72 % y 86 %
  de la importancia). Tiene sentido y es buena señal de que el modelo no aprendió casualidades.

---

## 4. Riego adaptativo (el umbral sugerido)

`predecir.py` no usa una regla fija: le **pregunta al modelo de crecimiento**. Para cada umbral
candidato entre 30 % y 50 % estima cómo quedaría la semana con ese umbral (la humedad media
oscilaría entre el umbral y el objetivo de 60 %, y no habría horas de estrés) y se queda con el
umbral que da más crecimiento previsto. Cuando el modelo se reentrene con datos reales, el umbral
sugerido cambia solo.

> **Limitación honesta:** hoy el modelo aprendió que más humedad siempre es más crecimiento, así
> que el umbral sugerido se va al techo del rango (**50 %**). Un modelo lineal no puede encontrar
> un punto óptimo intermedio. Con datos reales —donde el exceso de agua **sí** hace daño— la curva
> debería dejar de ser monótona y el óptimo caer dentro del rango. Por eso el umbral está limitado
> a 50 % por código (`UMBRAL_MAXIMO`) y **no debe mandarse a la Raspberry sin revisarlo.**

---

## 5. Cómo se usa

```bash
cd Invernadero/ML
pip install -r requirements.txt

python3 generar_datos.py              # historial simulado (no hace falta si ya hay datos reales)
python3 entrenar.py                   # entrena y escribe modelos/ + metricas.json
python3 predecir.py                   # predicción con los últimos datos disponibles
python3 predecir.py --json            # lo mismo en JSON, para consumirlo desde la API
python3 score.py                      # prueba el contrato del endpoint de Azure, sin Azure
```

Ejemplo de salida de `predecir.py` con el historial simulado:

```
  Humedad de suelo   59.3 %
  Planta             39.5 cm, 49 hojas

  Proximo riego en   72.6 h (2026-11-19T22:33:25Z)
  Agua proximas 24 h 0 mL
  Crecera            1.58 cm en 7 dias (hasta 41.1 cm)
  Umbral sugerido    50.0 %
```

Para publicarlo en Azure ML: **`azure/DESPLIEGUE-AZURE-ML.md`**. Ahí están los comandos, la
advertencia de cuota de Azure for Students y el recordatorio de borrar el endpoint después de la
demo (cobra por hora mientras exista).

---

## 6. Reentrenar con datos reales (lo que hay que hacer en cuanto la Raspberry envíe)

```bash
cd Invernadero/ML
mkdir -p datos-reales

# 1. Lecturas desde la API
curl "https://api-invernadero-kevin.azurewebsites.net/api/Lecturas/exportar.csv" \
  -o datos-reales/lecturas.csv

# 2. Riegos: la API los da en JSON, hay que pasarlos a CSV
curl "https://api-invernadero-kevin.azurewebsites.net/api/Riegos?limite=1000" \
  -o datos-reales/riegos.json
python3 -c "import json,csv,sys; d=json.load(open('datos-reales/riegos.json')); \
w=csv.DictWriter(open('datos-reales/riegos.csv','w',newline='',encoding='utf-8'), \
fieldnames=d[0].keys()); w.writeheader(); w.writerows(d)"

# 3. Mediciones de la planta: las pasa Juan a mano, con estas columnas
#    fecha,altura_cm,hojas,observaciones
#    2026-11-17,12.4,14,

# 4. Reentrenar y comparar contra las métricas de hoy
python3 entrenar.py --datos datos-reales --salida modelos
```

**Mínimos para que el reentrenamiento sirva de algo:**

| Modelo | Mínimo razonable | Hoy (simulado) |
|---|---|---|
| `riego_horas` y `riego_agua_24h` | 10 días de lecturas y ~8 sesiones de riego | 60 días, 23 sesiones |
| `crecimiento_altura` y `crecimiento_hojas` | 25 mediciones diarias seguidas | 60 mediciones |

Si llegan datos reales de riego pero no mediciones de la planta, se puede reentrenar **solo** la
parte de riego y dejar la de crecimiento con el modelo simulado; hay que decirlo en la
presentación.

---

## 7. Lo que falta para cerrar el ML del proyecto

| Pendiente | Quién | Nota |
|---|---|---|
| `PrediccionesController` en la API que llame al modelo y guarde la predicción | Back | Contrato de entrada y salida ya definido en `score.py` |
| Pantalla de predicción en la app conectada a ese endpoint | Front | Hoy usa `mocks/simulador` |
| Publicar el endpoint en Azure ML | Kevin | Dejarlo para uno o dos días antes de la demo |
| Mandar el umbral sugerido a la Raspberry por IoT Hub | Juan y Kevin | Depende de que la Raspberry se conecte a IoT Hub |
| Arreglar la sesión de riego en `Embebidos/main.py` | Juan | Ver el hallazgo de la sección 1 |
| Reentrenar con datos reales | Danna | Sección 6 |

---

## 8. Archivos

```
Invernadero/ML/
├── README.md               este documento
├── requirements.txt        versiones exactas con las que se entrenó
├── generar_datos.py        historial simulado (sin dependencias)
├── entrenar.py             variables, entrenamiento, métricas
├── predecir.py             predicción + umbral adaptativo
├── score.py                script de scoring para el endpoint de Azure ML
├── datos/                  historial simulado (lecturas, riegos, mediciones, metadatos)
├── modelos/                modelo_invernadero.joblib y metricas.json
└── azure/
    ├── DESPLIEGUE-AZURE-ML.md   comandos para registrar y publicar
    ├── conda.yaml               entorno del endpoint
    ├── endpoint.yml             definición del endpoint
    ├── deployment.yml           definición del despliegue
    └── ejemplo-peticion.json    petición de prueba
```
