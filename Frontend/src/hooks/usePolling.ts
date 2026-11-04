import { useCallback, useEffect, useRef, useState } from 'react'
import { jfl, mensagemDeErro } from '../services/api'
import type { JflStatus } from '../types'

/** Executa `fn` imediatamente e a cada `intervaloMs` enquanto `ativo` for verdadeiro. */
export function usePolling(fn: () => void | Promise<void>, intervaloMs: number, ativo = true) {
  const ref = useRef(fn)
  ref.current = fn

  useEffect(() => {
    if (!ativo) return
    let cancelado = false
    let timer: number | undefined
    const ciclo = async () => {
      try { await ref.current() } finally {
        if (!cancelado) timer = window.setTimeout(ciclo, intervaloMs)
      }
    }
    ciclo()
    return () => { cancelado = true; window.clearTimeout(timer) }
  }, [intervaloMs, ativo])
}

/**
 * Status lido da central JFL. Cada atualização envia um pedido de status (0x4D) à central —
 * o Active NET faz o mesmo a cada ~12,5 s; aqui o padrão é 10 s.
 */
export function useJflStatus(serial: string | undefined, intervaloMs = 10000, automatico = true) {
  const [status, setStatus] = useState<JflStatus | null>(null)
  const [erro, setErro] = useState<string | null>(null)
  const [carregando, setCarregando] = useState(false)

  const atualizar = useCallback(async () => {
    if (!serial) return
    setCarregando(true)
    try {
      setStatus(await jfl.status(serial, true))
      setErro(null)
    } catch (e) {
      setErro(mensagemDeErro(e, 'Não foi possível ler o status da central.'))
    } finally {
      setCarregando(false)
    }
  }, [serial])

  useEffect(() => { setStatus(null); setErro(null) }, [serial])
  usePolling(atualizar, intervaloMs, automatico && !!serial)

  return { status, erro, carregando, atualizar }
}
