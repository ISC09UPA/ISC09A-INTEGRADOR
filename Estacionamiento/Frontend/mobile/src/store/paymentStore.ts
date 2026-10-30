import { create } from 'zustand';

export interface CreditCard {
  id: string;
  brand: 'VISA' | 'MASTERCARD' | 'AMEX';
  cardNumberMasked: string;
  last4: string;
  holderName: string;
  expiry: string; // MM/YY
  isDefault: boolean;
  theme: 'black' | 'blue' | 'platinum';
}

export interface ChargeTransaction {
  id: string;
  concept: string;
  date: string;
  timestamp: number;
  amount: number;
  amountFormatted: string;
  plate: string;
  status: 'COBRADO' | 'PENDIENTE';
  paymentMethod: string;
  duration?: string;
  uuidSat?: string;
}

interface PaymentState {
  cards: CreditCard[];
  transactions: ChargeTransaction[];
  addCard: (card: Omit<CreditCard, 'id'>) => void;
  removeCard: (id: string) => void;
  setDefaultCard: (id: string) => void;
  addTransaction: (tx: Omit<ChargeTransaction, 'id' | 'timestamp'>) => void;
}

const INITIAL_CARDS: CreditCard[] = [
  {
    id: 'card_01',
    brand: 'VISA',
    cardNumberMasked: '•••• •••• •••• 4821',
    last4: '4821',
    holderName: 'Laura Sánchez',
    expiry: '09/28',
    isDefault: true,
    theme: 'black',
  },
  {
    id: 'card_02',
    brand: 'MASTERCARD',
    cardNumberMasked: '•••• •••• •••• 9104',
    last4: '9104',
    holderName: 'Laura Sánchez',
    expiry: '11/29',
    isDefault: false,
    theme: 'blue',
  },
  {
    id: 'card_03',
    brand: 'AMEX',
    cardNumberMasked: '•••• •••••• •3012',
    last4: '3012',
    holderName: 'Laura Sánchez',
    expiry: '04/27',
    isDefault: false,
    theme: 'platinum',
  },
];

const INITIAL_TRANSACTIONS: ChargeTransaction[] = [
  {
    id: 'TX-2026-0982',
    concept: 'Estancia Estacionamiento A · Cajón A02',
    date: 'Hoy · 14:32',
    timestamp: Date.now() - 3600 * 4 * 1000,
    amount: 220.4,
    amountFormatted: '$220.40 MXN',
    plate: 'ABC-123-D',
    status: 'COBRADO',
    paymentMethod: 'Visa •••• 4821 (Apple Pay)',
    duration: '4 horas 15 min',
    uuidSat: 'a4b8-2c9e-41f0-9281-cfdi',
  },
  {
    id: 'TX-2026-0914',
    concept: 'Reserva Fin de Semana (3 Días)',
    date: '18 Oct 2026 · 09:10',
    timestamp: Date.now() - 3600 * 24 * 3 * 1000,
    amount: 650.0,
    amountFormatted: '$650.00 MXN',
    plate: 'ABC-123-D',
    status: 'COBRADO',
    paymentMethod: 'Visa •••• 4821',
    duration: '72 horas',
    uuidSat: 'f812-7b01-49a3-8321-cfdi',
  },
  {
    id: 'TX-2026-0842',
    concept: 'Estancia Corta Nivel 1',
    date: '12 Oct 2026 · 17:45',
    timestamp: Date.now() - 3600 * 24 * 9 * 1000,
    amount: 90.0,
    amountFormatted: '$90.00 MXN',
    plate: 'XYZ-789-A',
    status: 'COBRADO',
    paymentMethod: 'Mastercard •••• 9104',
    duration: '2 horas',
    uuidSat: 'c391-9e44-12a8-7741-cfdi',
  },
];

export const usePaymentStore = create<PaymentState>((set, get) => ({
  cards: INITIAL_CARDS,
  transactions: INITIAL_TRANSACTIONS,

  addCard: (card) => {
    const newCard: CreditCard = {
      ...card,
      id: `card_${Date.now()}`,
    };
    set((state) => {
      // Si la nueva es default, quitar default a las demás
      const updatedExisting = card.isDefault
        ? state.cards.map((c) => ({ ...c, isDefault: false }))
        : state.cards;
      return {
        cards: [newCard, ...updatedExisting],
      };
    });
  },

  removeCard: (id) => {
    set((state) => {
      const remaining = state.cards.filter((c) => c.id !== id);
      // Si la eliminada era default y quedan tarjetas, hacer default a la primera
      if (remaining.length > 0 && !remaining.some((c) => c.isDefault)) {
        remaining[0].isDefault = true;
      }
      return { cards: remaining };
    });
  },

  setDefaultCard: (id) => {
    set((state) => ({
      cards: state.cards.map((c) => ({
        ...c,
        isDefault: c.id === id,
      })),
    }));
  },

  addTransaction: (tx) => {
    const newTx: ChargeTransaction = {
      ...tx,
      id: `TX-${new Date().getFullYear()}-${Math.floor(1000 + Math.random() * 9000)}`,
      timestamp: Date.now(),
    };
    set((state) => ({
      transactions: [newTx, ...state.transactions],
    }));
  },
}));
