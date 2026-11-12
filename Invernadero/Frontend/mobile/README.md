# Welcome to your Expo app 👋

## Conectar con el backend

1. Copia `.env.example` a `.env.local`.
2. `EXPO_PUBLIC_TELEMETRIA_API_URL` ya apunta al App Service actual:
   `https://api-invernadero-kevin.azurewebsites.net`.
3. Configura `EXPO_PUBLIC_INVERNADERO_ID` con el identificador asignado al invernadero.
4. Reinicia Expo después de cambiar las variables: `npx expo start -c`.

Las variables `EXPO_PUBLIC_*` se incorporan al bundle y son visibles en el dispositivo: solo deben contener
valores públicos. No agregues aquí la API key, una cadena de conexión SQL ni credenciales de Azure.
El móvil consulta la API por HTTPS. En Web, además, el App Service debe permitir el origen web exacto mediante
`Cors__AllowedOrigins`; CORS no se aplica a la app nativa Android.

La app muestra datos de solo lectura recibidos por el backend. El sistema embebido publica en IoT Hub;
la ruta de IoT Hub entrega los mensajes a Service Bus, la Function los valida y registra en la API, y el móvil
consulta las lecturas y los riegos guardados. La API key de `X-Api-Key` es exclusivamente para Function → API
y no debe copiarse a esta app.

La cámara todavía no invoca Custom Vision. El recurso de predicción existe, pero falta entrenar y publicar
una iteración del modelo. Azure Machine Learning tampoco tiene un endpoint publicado; no hay predicciones
de IA reales que el frontend pueda mostrar todavía.

### Speech (solo backend local)

La pestaña Voz graba audio y envía el archivo junto con la transcripción de prueba a
`POST /api/speech/receive`. Este endpoint solo existe en el backend `Development` local. Para probarlo,
configura `EXPO_PUBLIC_TELEMETRIA_API_URL` con la URL local del backend (por ejemplo,
`http://localhost:5010`) y reinicia Expo. En un teléfono físico usa la IP local de tu computadora.
La API confirma la recepción, pero no procesa ni guarda el audio; Azure Speech sigue pendiente. No pongas
la API key de telemetría en `.env.local` ni en ninguna variable `EXPO_PUBLIC_*`.

This is an [Expo](https://expo.dev) project created with [`create-expo-app`](https://www.npmjs.com/package/create-expo-app).

## Get started

1. Install dependencies

   ```bash
   npm install
   ```

2. Start the app

   ```bash
   npx expo start
   ```

In the output, you'll find options to open the app in a

- [development build](https://docs.expo.dev/develop/development-builds/introduction/)
- [Android emulator](https://docs.expo.dev/workflow/android-studio-emulator/)
- [iOS simulator](https://docs.expo.dev/workflow/ios-simulator/)
- [Expo Go](https://expo.dev/go), a limited sandbox for trying out app development with Expo

You can start developing by editing the files inside the **app** directory. This project uses [file-based routing](https://docs.expo.dev/router/introduction).

## Get a fresh project

When you're ready, run:

```bash
npm run reset-project
```

This command will move the starter code to the **app-example** directory and create a blank **app** directory where you can start developing.

### Other setup steps

- To set up ESLint for linting, run `npx expo lint`, or follow our guide on ["Using ESLint and Prettier"](https://docs.expo.dev/guides/using-eslint/)
- If you'd like to set up unit testing, follow our guide on ["Unit Testing with Jest"](https://docs.expo.dev/develop/unit-testing/)
- Learn more about the TypeScript setup in this template in our guide on ["Using TypeScript"](https://docs.expo.dev/guides/typescript/)

## Learn more

To learn more about developing your project with Expo, look at the following resources:

- [Expo documentation](https://docs.expo.dev/): Learn fundamentals, or go into advanced topics with our [guides](https://docs.expo.dev/guides).
- [Learn Expo tutorial](https://docs.expo.dev/tutorial/introduction/): Follow a step-by-step tutorial where you'll create a project that runs on Android, iOS, and the web.

## Join the community

Join our community of developers creating universal apps.

- [Expo on GitHub](https://github.com/expo/expo): View our open source platform and contribute.
- [Discord community](https://chat.expo.dev): Chat with Expo users and ask questions.
