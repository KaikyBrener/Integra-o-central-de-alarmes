import axios from 'axios'
import type {
  Building, Central, ConexaoResult, EventoCentral, History,
  JflLog, JflModelo, JflServidor, JflSessao, JflStatus,
} from '../types'

/** URL da API; padrão http://localhost:5000/api, sobrescrevível com VITE_API_URL (ex.: no .env.local). */
export const API_URL: string = import.meta.env.VITE_API_URL ?? 'http://localhost:5000/api'

const api = axios.create({
  baseURL: API_URL,
})

export default api

/** Extrai a mensagem de erro padronizada da API ({ status, mensagem }) ou do axios. */
export function mensagemDeErro(e: unknown, padrao = 'Falha na comunicação com o CentralHub.'): string {
  if (axios.isAxiosError(e)) {
    const data = e.response?.data as { mensagem?: string; title?: string; errors?: Record<string, string[]> } | undefined
    if (data?.mensagem) return data.mensagem
    if (data?.errors) return Object.values(data.errors).flat().join(' ')
    if (data?.title) return data.title
    if (!e.response) return 'Não foi possível conectar à API do CentralHub (http://localhost:5000). Verifique se o backend está em execução.'
  }
  return padrao
}

export const centralHub = {
  buildings: () => api.get<Building[]>('/building').then(r => r.data),
  centrais: () => api.get<Central[]>('/central').then(r => r.data),
  testarConexao: (dados: { ip: string; porta: number; usuario: string; senha: string }) =>
    api.post<ConexaoResult>('/central/testar-conexao', dados).then(r => r.data),
  historico: (centralId?: number) =>
    api.get<History[]>('/operation/historico', { params: { centralId } }).then(r => r.data),
  eventos: (params: { centralId?: number; serial?: string; limite?: number }) =>
    api.get<EventoCentral[]>('/event', { params }).then(r => r.data),

  enviarPgm: (centralId: number, pgm: number, comando: 'Ligar' | 'Desligar' | 'Pulso', tempoPulsoMs = 0) =>
    api.post<History>('/operation/enviar', { centralId, pgm, comando, tempoPulsoMs }).then(r => r.data),
  particao: (centralId: number, particao: number, acao: 'Armar' | 'Desarmar') =>
    api.post<History>('/operation/particao', { centralId, particao, acao }).then(r => r.data),
  bypass: (centralId: number, zona: number, acao: 'Anular' | 'Reativar') =>
    api.post<History>('/operation/bypass', { centralId, zona, acao }).then(r => r.data),
}

export const jfl = {
  servidor: () => api.get<JflServidor>('/jfl/servidor').then(r => r.data),
  modelos: () => api.get<JflModelo[]>('/jfl/modelos').then(r => r.data),
  sessoes: () => api.get<JflSessao[]>('/jfl/sessoes').then(r => r.data),
  /** atualizar=true envia um pedido de status (0x4D) à central. */
  status: (serial: string, atualizar = true) =>
    api.get<JflStatus>(`/jfl/sessoes/${encodeURIComponent(serial)}/status`, { params: { atualizar } }).then(r => r.data),
  log: (params: { serial?: string; direcao?: string; tipo?: string; texto?: string; desdeId?: number; limite?: number }) =>
    api.get<JflLog[]>('/jfl/log', { params }).then(r => r.data),
  limparLog: () => api.delete('/jfl/log'),
}
