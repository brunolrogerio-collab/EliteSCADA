import type { EngineeringLocale } from './i18n';

type ProtocolLabels = Readonly<{
  modbusArea: Readonly<Record<'coil' | 'discrete' | 'holding' | 'input', string>>;
  dnp3PointKind: Readonly<Record<
    'binaryInput' | 'doubleBitBinaryInput' | 'analogInput' | 'counter' | 'frozenCounter' | 'binaryOutputStatus' | 'analogOutputStatus',
    string
  >>;
  iec104CommandMode: Readonly<Record<'sbo' | 'direct', string>>;
}>;

const resources: Record<EngineeringLocale, ProtocolLabels> = {
  'pt-BR': {
    modbusArea: {
      coil: 'Bobina (Coil)',
      discrete: 'Entrada discreta',
      holding: 'Registrador holding',
      input: 'Registrador de entrada'
    },
    dnp3PointKind: {
      binaryInput: 'Entrada binária',
      doubleBitBinaryInput: 'Entrada binária de dois bits',
      analogInput: 'Entrada analógica',
      counter: 'Contador',
      frozenCounter: 'Contador congelado',
      binaryOutputStatus: 'Status de saída binária',
      analogOutputStatus: 'Status de saída analógica'
    },
    iec104CommandMode: {
      sbo: 'Selecionar antes de operar (SBO)',
      direct: 'Operação direta'
    }
  },
  en: {
    modbusArea: {
      coil: 'Coil',
      discrete: 'Discrete Input',
      holding: 'Holding Register',
      input: 'Input Register'
    },
    dnp3PointKind: {
      binaryInput: 'Binary input',
      doubleBitBinaryInput: 'Double-bit binary input',
      analogInput: 'Analog input',
      counter: 'Counter',
      frozenCounter: 'Frozen counter',
      binaryOutputStatus: 'Binary output status',
      analogOutputStatus: 'Analog output status'
    },
    iec104CommandMode: {
      sbo: 'Select before operate (SBO)',
      direct: 'Direct operate'
    }
  },
  es: {
    modbusArea: {
      coil: 'Bobina (Coil)',
      discrete: 'Entrada discreta',
      holding: 'Registro holding',
      input: 'Registro de entrada'
    },
    dnp3PointKind: {
      binaryInput: 'Entrada binaria',
      doubleBitBinaryInput: 'Entrada binaria de dos bits',
      analogInput: 'Entrada analógica',
      counter: 'Contador',
      frozenCounter: 'Contador congelado',
      binaryOutputStatus: 'Estado de salida binaria',
      analogOutputStatus: 'Estado de salida analógica'
    },
    iec104CommandMode: {
      sbo: 'Seleccionar antes de operar (SBO)',
      direct: 'Operación directa'
    }
  }
};

export function c04ProtocolLabels(locale: EngineeringLocale): ProtocolLabels {
  return resources[locale] ?? resources['pt-BR'];
}
