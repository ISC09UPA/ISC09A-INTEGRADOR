import { create } from 'zustand';

export interface Vehicle {
  id: string;
  plate: string;
  alias: string;
  brand: string;
  model: string;
  color: string;
  isPrimary: boolean;
  nfcTagLinked: boolean;
}

interface VehicleState {
  vehicles: Vehicle[];
  primaryVehicle: Vehicle | null;
  addVehicle: (vehicle: Omit<Vehicle, 'id'>) => void;
  removeVehicle: (id: string) => void;
  setPrimary: (id: string) => void;
}

const INITIAL_VEHICLES: Vehicle[] = [
  {
    id: 'veh_01',
    plate: 'ABC-123-D',
    alias: 'Tesla Model 3',
    brand: 'Tesla',
    model: 'Model 3 Performance',
    color: 'Blanco Perla',
    isPrimary: true,
    nfcTagLinked: true,
  },
  {
    id: 'veh_02',
    plate: 'XYZ-789-A',
    alias: 'Porsche Macan',
    brand: 'Porsche',
    model: 'Macan GTS',
    color: 'Gris Volcán',
    isPrimary: false,
    nfcTagLinked: false,
  },
];

export const useVehicleStore = create<VehicleState>((set, get) => ({
  vehicles: INITIAL_VEHICLES,
  primaryVehicle: INITIAL_VEHICLES[0],

  addVehicle: (veh) => {
    const newVeh: Vehicle = {
      ...veh,
      id: `veh_${Date.now()}`,
    };
    set((state) => {
      const updated = [...state.vehicles, newVeh];
      return {
        vehicles: updated,
        primaryVehicle: newVeh.isPrimary ? newVeh : state.primaryVehicle,
      };
    });
  },

  removeVehicle: (id) => {
    set((state) => {
      const updated = state.vehicles.filter((v) => v.id !== id);
      return {
        vehicles: updated,
        primaryVehicle: state.primaryVehicle?.id === id ? updated[0] || null : state.primaryVehicle,
      };
    });
  },

  setPrimary: (id) => {
    set((state) => {
      const updated = state.vehicles.map((v) => ({
        ...v,
        isPrimary: v.id === id,
      }));
      const primary = updated.find((v) => v.id === id) || null;
      return { vehicles: updated, primaryVehicle: primary };
    });
  },
}));
