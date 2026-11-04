import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { centralHub, jfl, mensagemDeErro } from '../services/api'
import { usePolling } from '../hooks/usePolling'
import type { Central, JflServidor, JflSessao } from '../types'

const CHAVE = 'centralhub.centralJflSelecionada'

interface JflContextValue {
  /** Todas as centrais cadastradas. */
  centrais: Central[]
  /** Centrais cadastradas com Fabricante = JFL. */
  centraisJfl: Central[]
  /** Centrais JFL conectadas agora ao servidor do CentralHub (cadastradas ou não). */
  sessoes: JflSessao[]
  servidor: JflServidor | null
  erroApi: string | null
  selecionadaId: number | null
  selecionar: (id: number | null) => void
  central: Central | null
  sessao: JflSessao | null
  recarregar: () => Promise<void>
}

const Ctx = createContext<JflContextValue | null>(null)

export function JflProvider({ children }: { children: ReactNode }) {
  const [centrais, setCentrais] = useState<Central[]>([])
  const [sessoes, setSessoes] = useState<JflSessao[]>([])
  const [servidor, setServidor] = useState<JflServidor | null>(null)
  const [erroApi, setErroApi] = useState<string | null>(null)
  const [selecionadaId, setSelecionadaId] = useState<number | null>(() => {
    try {
      const v = localStorage.getItem(CHAVE)
      return v ? Number(v) : null
    } catch {
      return null
    }
  })

  const recarregar = useCallback(async () => {
    try {
      const [c, s, sv] = await Promise.all([centralHub.centrais(), jfl.sessoes(), jfl.servidor()])
      setCentrais(c)
      setSessoes(s)
      setServidor(sv)
      setErroApi(null)
    } catch (e) {
      setErroApi(mensagemDeErro(e))
    }
  }, [])

  usePolling(recarregar, 5000)

  const centraisJfl = useMemo(() => centrais.filter(c => c.fabricante?.toLowerCase() === 'jfl'), [centrais])

  // Seleciona automaticamente a primeira central JFL se nada estiver selecionado (ou a selecionada sumiu).
  useEffect(() => {
    if (centrais.length === 0) return
    if (selecionadaId && centraisJfl.some(c => c.id === selecionadaId)) return
    selecionar(centraisJfl[0]?.id ?? null)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [centraisJfl])

  const selecionar = (id: number | null) => {
    setSelecionadaId(id)
    try {
      if (id) localStorage.setItem(CHAVE, String(id)); else localStorage.removeItem(CHAVE)
    } catch { /* armazenamento indisponível: seleção vale só nesta sessão */ }
  }

  const central = centraisJfl.find(c => c.id === selecionadaId) ?? null
  const sessao = central ? sessoes.find(s => s.serial === central.usuario.trim()) ?? null : null

  const value: JflContextValue = {
    centrais, centraisJfl, sessoes, servidor, erroApi, selecionadaId, selecionar, central, sessao, recarregar,
  }
  return <Ctx.Provider value={value}>{children}</Ctx.Provider>
}

export function useJfl() {
  const v = useContext(Ctx)
  if (!v) throw new Error('useJfl fora do JflProvider')
  return v
}
