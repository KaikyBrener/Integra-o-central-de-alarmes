/**
 * Máscaras de entrada. Cada função recebe o texto digitado e devolve o texto formatado,
 * aceitando apenas os caracteres válidos para o campo.
 */

/** IPv4: até 4 octetos de 0 a 255, separados por ponto. */
export function maskIp(valor: string): string {
  const limpo = valor.replace(/[^\d.]/g, '').replace(/\.{2,}/g, '.')
  const partes = limpo.split('.').slice(0, 4).map(p => p.slice(0, 3))
  return partes.map(p => (p !== '' && Number(p) > 255 ? '255' : p)).join('.')
}

export function ipValido(valor: string): boolean {
  const partes = valor.split('.')
  return partes.length === 4 && partes.every(p => p !== '' && Number(p) >= 0 && Number(p) <= 255)
}

/** MAC: 12 dígitos hexadecimais em pares separados por ":". */
export function maskMac(valor: string): string {
  const hex = valor.replace(/[^0-9a-fA-F]/g, '').toUpperCase().slice(0, 12)
  return hex.match(/.{1,2}/g)?.join(':') ?? ''
}

/** Telefone brasileiro: (00) 0000-0000 ou (00) 00000-0000. */
export function maskTelefone(valor: string): string {
  const d = valor.replace(/\D/g, '').slice(0, 11)
  if (d.length <= 2) return d.length ? `(${d}` : ''
  if (d.length <= 6) return `(${d.slice(0, 2)}) ${d.slice(2)}`
  if (d.length <= 10) return `(${d.slice(0, 2)}) ${d.slice(2, 6)}-${d.slice(6)}`
  return `(${d.slice(0, 2)}) ${d.slice(2, 7)}-${d.slice(7)}`
}

/** Número de série da central JFL: 10 dígitos (como informado no quadro de conexão 0x21). */
export function maskSerial(valor: string): string {
  return valor.replace(/\D/g, '').slice(0, 10)
}

export const SERIAL_TAMANHO = 10

/** Horário HH:MM ou HH:MM:SS (24 h). */
export function maskHorario(valor: string, comSegundos = true): string {
  const d = valor.replace(/\D/g, '').slice(0, comSegundos ? 6 : 4)
  const blocos = d.match(/.{1,2}/g) ?? []
  const limites = [23, 59, 59]
  return blocos.map((b, i) => (b.length === 2 && Number(b) > limites[i] ? String(limites[i]) : b)).join(':')
}

export function horarioValido(valor: string): boolean {
  return /^([01]\d|2[0-3]):[0-5]\d(:[0-5]\d)?$/.test(valor)
}

/** Porta TCP. A central JFL guarda a porta de destino com 4 dígitos (máx. 9999). */
export function maskPorta(valor: string, maximo = 65535): string {
  const d = valor.replace(/\D/g, '').slice(0, 5)
  if (d === '') return ''
  return String(Math.min(Number(d), maximo))
}

/** Formata MAC vindo da central ("FFFFFFFFFFFF" → "FF:FF:FF:FF:FF:FF"). */
export function formatarMac(valor?: string): string {
  if (!valor) return '—'
  return maskMac(valor) || valor
}

/** Agrupa hexadecimal em bytes separados por espaço. */
export function formatarHex(hex?: string): string {
  return hex ? (hex.match(/.{1,2}/g) ?? []).join(' ') : ''
}
