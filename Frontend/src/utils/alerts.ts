import Swal from 'sweetalert2'
import { mensagemDeErro } from '../services/api'
import type { History } from '../types'

const base = Swal.mixin({
  confirmButtonText: 'OK',
  cancelButtonText: 'Cancelar',
  customClass: {
    confirmButton: 'btn btn-primary mx-1',
    cancelButton: 'btn btn-outline-secondary mx-1',
    denyButton: 'btn btn-danger mx-1',
  },
  buttonsStyling: false,
})

/** Escapa texto vindo do usuário/API antes de inserir no html dos alertas. */
export function esc(texto: string): string {
  return texto.replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]!))
}

export const toast = Swal.mixin({
  toast: true,
  position: 'top-end',
  showConfirmButton: false,
  timer: 3500,
  timerProgressBar: true,
})

export async function confirmar(titulo: string, texto: string, botao = 'Confirmar', perigo = false): Promise<boolean> {
  const r = await base.fire({
    title: titulo,
    html: texto,
    icon: perigo ? 'warning' : 'question',
    showCancelButton: true,
    confirmButtonText: botao,
    focusCancel: perigo,
    customClass: {
      confirmButton: `btn ${perigo ? 'btn-danger' : 'btn-primary'} mx-1`,
      cancelButton: 'btn btn-outline-secondary mx-1',
    },
  })
  return r.isConfirmed
}

export function carregando(titulo: string, texto = 'Aguardando resposta da central...') {
  base.fire({ title: titulo, text: texto, allowOutsideClick: false, allowEscapeKey: false, didOpen: () => Swal.showLoading() })
}

export function fechar() {
  Swal.close()
}

export function erro(titulo: string, e: unknown) {
  return base.fire({ title: titulo, text: typeof e === 'string' ? e : mensagemDeErro(e), icon: 'error' })
}

export function sucesso(titulo: string, texto?: string) {
  return base.fire({ title: titulo, text: texto, icon: 'success' })
}

export function info(titulo: string, html: string) {
  return base.fire({ title: titulo, html, icon: 'info' })
}

/**
 * Executa um comando real na central exibindo carregamento e depois o resultado devolvido pelo
 * CentralHub. Um resultado "FALHA: ..." é exibido como erro (o CentralHub só confirma sucesso quando
 * o status devolvido pela central comprova a mudança).
 */
export async function executarComando(titulo: string, acao: () => Promise<History>): Promise<boolean> {
  carregando(titulo)
  try {
    const h = await acao()
    const falhou = h.resultado.startsWith('FALHA:')
    await base.fire({
      title: falhou ? 'Comando não confirmado' : 'Comando confirmado',
      text: falhou ? h.resultado.replace(/^FALHA:\s*/, '') : h.resultado,
      icon: falhou ? 'error' : 'success',
    })
    return !falhou
  } catch (e) {
    await erro('Falha ao enviar o comando', e)
    return false
  }
}
