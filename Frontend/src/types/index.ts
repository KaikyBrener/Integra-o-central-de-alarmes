export interface Building {
  id: number
  nome: string
  descricao?: string
}

export interface Central {
  id: number
  nome: string
  ip: string
  porta: number
  usuario: string
  buildingId: number
  buildingNome?: string
  fabricante?: string
  modelo?: string
  firmware?: string
  status?: string
  latencia?: number
}

export interface History {
  id: number
  data: string
  centralId: number
  centralNome?: string
  pgm: number
  comando: string
  resultado: string
}

export interface ConexaoResult {
  sucesso: boolean
  fabricante: string
  modelo: string
  firmware: string
  status: string
  latenciaMs: number
  erro?: string
}

export interface EventoCentral {
  id: number
  dataHora: string
  centralId?: number
  centralNome?: string
  fabricante?: string
  serial?: string
  conta?: string
  codigo?: string
  tipo: string
  restauracao: boolean
  particao?: number
  zona?: number
  usuario?: number
  pgm?: number
  descricao?: string
}

// ---------------- JFL ----------------

export interface JflServidor {
  habilitado: boolean
  portaEmUso?: number
  portaConfigurada: number
  bindAddress: string
  keepAliveMinutes: number
  somenteLeitura: boolean
  aceitaCentraisDesconhecidas: boolean
  logQuadrosBrutos: boolean
  sessoesAtivas: number
  capacidadeLog: number
}

export interface JflModelo {
  byteModelo: string
  nome: string
  placa: string
  particoes: number
  zonas: number
  pgms: number
  eletrificador: boolean
  comandosHabilitados: boolean
  validadoEmHardware: boolean
  evidencia: string
}

export interface JflSessao {
  serial: string
  origem: string
  conectadaEm: string
  ultimoQuadroEm: string
  ultimoStatusEm?: string
  firmware: string
  mac: string
  imei: string
  sinal: number
  modelo: JflModelo
  centralId?: number
  centralNome?: string
}

export interface JflParticaoStatus {
  numero: number
  programada: boolean
  estado: 'NaoProgramada' | 'Desarmada' | 'ArmadaTotal' | 'ArmadaStay' | string
  emAlarme: boolean
  bruto: string
}

export type JflZonaEstado =
  | 'Bypassed' | 'Triggered' | 'NotCommunicating' | 'ShortCircuit'
  | 'Tamper' | 'LowBattery' | 'Open' | 'Closed'

export interface JflZonaStatus {
  numero: number
  estado: JflZonaEstado
  permiteInibir: boolean
}

export interface JflPgmStatus {
  numero: number
  ligada: boolean
  permitida?: boolean | null
}

export interface JflProblema {
  codigo: string
  descricao: string
  grupo: string
}

export interface JflStatus {
  serial: string
  lidoEm?: string
  atualizado: boolean
  aviso?: string
  relogio: string
  bateriaVolts: number
  eletrificador: string
  tamanhoQuadro: number
  particoes: JflParticaoStatus[]
  zonas: JflZonaStatus[]
  pgms: JflPgmStatus[]
  problemas: JflProblema[]
}

export interface JflLog {
  id: number
  dataHora: string
  serial?: string
  origem: string
  direcao: 'Rx' | 'Tx' | 'Sistema'
  tipo: string
  comando?: string
  hex?: string
  descricao: string
}
