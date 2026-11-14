// El repo vive en C:\Dev\c#\...: el plugin de worklets lee los source maps cortando la ruta en el primer
// "#" (lo toma como sufijo de URL) y falla con ENOENT 'C:\Dev\c'. Se agrega el plugin a mano sin
// source maps de worklets; el resto del preset de Expo queda igual.
module.exports = function (api) {
  api.cache(true);
  return {
    presets: [['babel-preset-expo', { worklets: false, reanimated: false }]],
    plugins: [['react-native-worklets/plugin', { disableSourceMaps: true }]],
  };
};
