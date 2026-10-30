const { getDefaultConfig } = require('expo/metro-config');

module.exports = (() => {
  const config = getDefaultConfig(__dirname);

  const { transformer } = config;
  transformer.assetExts = [...(transformer.assetExts ?? []), 'svg'];

  const { withNativeWind } = require('nativewind/metro');
  return withNativeWind(config, { input: './src/global.css' });
})();
