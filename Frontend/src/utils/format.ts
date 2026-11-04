import type { JflZonaEstado } from '../types'

export const ZONA_ESTADO: Record<JflZonaEstado, { texto: string; cor: string; icone: string }> = {
  Closed: { texto: 'Fechada', cor: 'success', icone: 'bi-door-closed' },
  Open: { texto: 'Aberta', cor: 'warning', icone: 'bi-door-open' },
  Triggered: { texto: 'Disparo', cor: 'danger', icone: 'bi-exclamation-octagon' },
  Bypassed: { texto: 'Anulada', cor: 'secondary', icone: 'bi-slash-circle' },
  Tamper: { texto: 'Tamper', cor: 'danger', icone: 'bi-shield-exclamation' },
  ShortCircuit: { texto: 'Curto-circuito', cor: 'danger', icone: 'bi-lightning' },
  LowBattery: { texto: 'Bateria fraca', cor: 'warning', icone: 'bi-battery-half' },
  NotCommunicating: { texto: 'Sem comunicação', cor: 'dark', icone: 'bi-wifi-off' },
}

export function zonaEstado(estado: string) {
  return ZONA_ESTADO[estado as JflZonaEstado] ?? { texto: estado, cor: 'light', icone: 'bi-question-circle' }
}

export const PARTICAO_ESTADO: Record<string, { texto: string; cor: string; icone: string }> = {
  Desarmada: { texto: 'Desarmada', cor: 'success', icone: 'bi-unlock' },
  ArmadaTotal: { texto: 'Armada', cor: 'danger', icone: 'bi-lock-fill' },
  ArmadaStay: { texto: 'Armada STAY', cor: 'warning', icone: 'bi-house-lock' },
  NaoProgramada: { texto: 'Não programada', cor: 'light', icone: 'bi-dash-circle' },
}

export function particaoEstado(estado: string) {
  return PARTICAO_ESTADO[estado] ?? { texto: estado, cor: 'secondary', icone: 'bi-question-circle' }
}

export function dataHora(valor?: string): string {
  if (!valor) return '—'
  const d = new Date(valor)
  return isNaN(d.getTime()) ? valor : d.toLocaleString('pt-BR')
}

export function horaComMs(valor: string): string {
  const d = new Date(valor)
  if (isNaN(d.getTime())) return valor
  return `${d.toLocaleTimeString('pt-BR')}.${String(d.getMilliseconds()).padStart(3, '0')}`
}

export function tempoDesde(valor?: string): string {
  if (!valor) return '—'
  const s = Math.max(0, Math.round((Date.now() - new Date(valor).getTime()) / 1000))
  if (s < 60) return `há ${s} s`
  if (s < 3600) return `há ${Math.floor(s / 60)} min`
  return `há ${Math.floor(s / 3600)} h ${Math.floor((s % 3600) / 60)} min`
}

export function pad2(n: number): string {
  return String(n).padStart(2, '0')
}

export const TIPO_EVENTO: Record<string, { texto: string; cor: string }> = {
  Alarme: { texto: 'Alarme', cor: 'danger' },
  Arme: { texto: 'Arme', cor: 'primary' },
  Desarme: { texto: 'Desarme', cor: 'success' },
  Energia: { texto: 'Energia', cor: 'warning' },
  Bateria: { texto: 'Bateria', cor: 'warning' },
  Comunicacao: { texto: 'Comunicação', cor: 'info' },
  Problema: { texto: 'Problema', cor: 'warning' },
  Pgm: { texto: 'PGM', cor: 'secondary' },
  Bypass: { texto: 'Bypass', cor: 'secondary' },
  Programacao: { texto: 'Programação', cor: 'dark' },
  Teste: { texto: 'Teste', cor: 'light' },
  Geral: { texto: 'Geral', cor: 'light' },
}

export function tipoEvento(tipo: string) {
  return TIPO_EVENTO[tipo] ?? { texto: tipo, cor: 'light' }
}
