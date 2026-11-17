# Registrar el modelo y publicar el endpoint en Azure ML

Workspace: **`ml-invernadero`** · Grupo de recursos: **`rg-invernadero`** · Suscripción: Azure for Students

> **Cuándo hacer esto:** un endpoint administrado **cobra por hora mientras exista**, aunque nadie
> lo llame. Conviene publicarlo uno o dos días antes de la demo y **borrarlo al terminar**
> (último comando de esta página). Mientras no esté publicado, la API puede usar el modelo con
> `predecir.py`, o la app puede mostrar la última predicción guardada en la base de datos.

## 0. Antes de empezar

```bash
az login
az account set --subscription e443b793-5077-45ef-b658-ff758ebbe189
az extension add --name ml --upgrade
az configure --defaults group=rg-invernadero workspace=ml-invernadero
```

Si el modelo se reentrenó, correr primero `python3 entrenar.py` desde `Invernadero/ML/` para que
`modelos/modelo_invernadero.joblib` esté al día.

## 1. Registrar el modelo

```bash
cd Invernadero/ML
az ml model create \
  --name invernadero-prediccion \
  --path modelos \
  --type custom_model \
  --description "Crecimiento a 7 dias, proximo riego y umbral sugerido"
```

Cada `az ml model create` crea una versión nueva; `deployment.yml` apunta a `@latest`.

## 2. Crear el endpoint (una sola vez)

```bash
az ml online-endpoint create -f azure/endpoint.yml
```

## 3. Publicar el despliegue

```bash
az ml online-deployment create -f azure/deployment.yml --all-traffic
```

Tarda varios minutos porque construye el contenedor con `conda.yaml`. Si falla:

```bash
az ml online-deployment get-logs --name azul --endpoint-name invernadero-prediccion
```

**Error de cuota.** Azure for Students normalmente **no trae cuota de cómputo para endpoints
administrados**. Si sale `SubscriptionNotRegistered` o un error de cuota en `Standard_DS2_v2`:

1. Revisar qué tamaños hay disponibles: `az ml compute list-usage`.
2. Pedir aumento de cuota desde el portal (Azure ML Studio → Quota), que tarda en aprobarse.
3. Si no se aprueba a tiempo: dejar el endpoint fuera y que la API use el modelo directamente
   (`predecir.py`). La predicción es la misma; lo único que falta es que viva en Azure. Esto hay
   que documentarlo en la presentación.

## 4. Probar el endpoint

```bash
az ml online-endpoint invoke \
  --name invernadero-prediccion \
  --request-file azure/ejemplo-peticion.json
```

Obtener la URL y la llave para la API:

```bash
az ml online-endpoint show -n invernadero-prediccion --query scoring_uri -o tsv
az ml online-endpoint get-credentials -n invernadero-prediccion --query primaryKey -o tsv
```

## 5. Conectarlo con la API de Telemetría

La llave **no va al repositorio ni a la app**: se configuran como app settings del App Service
`api-invernadero-kevin`, igual que el resto de los secretos.

| App setting | Valor |
|---|---|
| `AzureMl__ScoringUri` | lo que devolvió `--query scoring_uri` |
| `AzureMl__Key` | lo que devolvió `get-credentials` |

La API llama al endpoint con `Authorization: Bearer <llave>` y `Content-Type: application/json`,
manda el cuerpo del modo `historial` (lecturas, riegos y mediciones que ya tiene en la base) y
guarda la respuesta. Ese es el `PrediccionesController` que falta en el backend.

## 6. Borrar el endpoint después de la demo

```bash
az ml online-endpoint delete --name invernadero-prediccion --yes
```

El modelo registrado no cuesta nada, así que ese se puede dejar.
