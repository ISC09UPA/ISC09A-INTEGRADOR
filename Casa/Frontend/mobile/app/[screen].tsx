import { useLocalSearchParams } from 'expo-router';
import { AppScreen } from '../src/features/app/AppScreen';
export default function Screen() { const { screen } = useLocalSearchParams<{ screen: string }>(); return <AppScreen key={screen} screen={screen} />; }
