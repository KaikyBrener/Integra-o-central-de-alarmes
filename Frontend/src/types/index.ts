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
  pgm: number
  comando: string
  sucesso: boolean
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
