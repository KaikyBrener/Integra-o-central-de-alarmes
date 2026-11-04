import { useMemo, useRef, useState } from 'react'
import MaskedInput from '../components/MaskedInput'
import { PageHeader, RecursosCard, Vazio } from '../components/ui'
import { useJfl } from '../context/JflContext'
import { usePolling } from '../hooks/usePolling'
import { jfl, mensagemDeErro } from '../services/api'
import type { JflLog } from '../types'
import { confirmar, erro, toast } from '../utils/alerts'
import { horaComMs } from '../utils/format'
import { formatarHex, horarioValido, maskHorario } from '../utils/masks'

const MAX_LINHAS = 3000

const TIPOS = ['Quadro', 'Ack', 'Evento', 'Status', 'Comando', 'Conexao', 'Desconexao', 'Reconexao', 'Timeout', 'Erro']
const TIPO_TEXTO: Record<string, string> = {
  Quadro: 'Quadro', Ack: 'ACK', Evento: 'Evento', Status: 'Status', Comando: 'Comando', Conexao: 'Conexão',
  Desconexao: 'Desconexão', Reconexao: 'Reconexão', Timeout: 'Timeout', Erro: 'Erro',
}
const DIRECAO: Record<string, { texto: string; cor: string; icone: string }> = {
  Rx: { texto: 'RX', cor: 'primary', icone: 'bi-arrow-down-left' },
  Tx: { texto: 'TX', cor: 'success', icone: 'bi-arrow-up-right' },
  Sistema: { texto: 'SIS', cor: 'secondary', icone: 'bi-gear' },
}

function segundosDoDia(iso: string): number {
  const d = new Date(iso)
  return d.getHours() * 3600 + d.getMinutes() * 60 + d.getSeconds()
}

function horarioEmSegundos(h: string): number {
  const [hh, mm, ss = '0'] = h.split(':')
  return Number(hh) * 3600 + Number(mm) * 60 + Number(ss)
}

function baixar(nome: string, conteudo: string, tipo: string) {
  const url = URL.createObjectURL(new Blob([conteudo], { type: tipo }))
  const a = document.createElement('a')
  a.href = url
  a.download = nome
  a.click()
  URL.revokeObjectURL(url)
}

export default function LogsPage() {
  const { sessoes, centraisJfl } = useJfl()
  const [linhas, setLinhas] = useState<JflLog[]>([])
  const ultimoId = useRef(0)
  const [pausado, setPausado] = useState(false)
  const [falha, setFalha] = useState<string | null>(null)

  const [serial, setSerial] = useState('')
  const [direcao, setDirecao] = useState('')
  const [tipo, setTipo] = useState('')
  const [texto, setTexto] = useState('')
  const [de, setDe] = useState('')
  const [ate, setAte] = useState('')
  const [mostrarHex, setMostrarHex] = useState(true)

  // Leitura incremental: só pede o que chegou depois do último id recebido.
  usePolling(async () => {
    try {
      const novas = await jfl.log({ desdeId: ultimoId.current, limite: 1000 })
      if (novas.length) {
        ultimoId.current = novas[novas.length - 1].id
        setLinhas(atual => [...atual, ...novas].slice(-MAX_LINHAS))
      } else if (ultimoId.current > 0) {
        // A API foi reiniciada (os ids recomeçam): volta a ler do início.
        const ultima = await jfl.log({ limite: 1 })
        if (ultima.length && ultima[0].id < ultimoId.current) {
          ultimoId.current = 0
          setLinhas([])
        }
      }
      setFalha(null)
    } catch (e) {
      setFalha(mensagemDeErro(e))
    }
  }, 2000, !pausado)

  const seriais = useMemo(() => {
    const s = new Set<string>([...sessoes.map(x => x.serial), ...centraisJfl.map(c => c.usuario), ...linhas.flatMap(l => (l.serial ? [l.serial] : []))])
    return [...s].sort()
  }, [sessoes, centraisJfl, linhas])

  const deValido = de === '' || horarioValido(de)
  const ateValido = ate === '' || horarioValido(ate)

  const filtradas = useMemo(() => {
    const t = texto.trim().toLowerCase()
    const tHex = t.replace(/\s/g, '')
    return linhas.filter(l =>
      (!serial || l.serial === serial)
      && (!direcao || l.direcao === direcao)
      && (!tipo || l.tipo === tipo)
      && (!t || l.descricao.toLowerCase().includes(t) || (l.hex?.toLowerCase().includes(tHex) ?? false) || (l.origem.includes(t)))
      && (!de || !deValido || segundosDoDia(l.dataHora) >= horarioEmSegundos(de))
      && (!ate || !ateValido || segundosDoDia(l.dataHora) <= horarioEmSegundos(ate)))
  }, [linhas, serial, direcao, tipo, texto, de, ate, deValido, ateValido])

  const contagem = useMemo(() => ({
    rx: filtradas.filter(l => l.direcao === 'Rx').length,
    tx: filtradas.filter(l => l.direcao === 'Tx').length,
    erros: filtradas.filter(l => l.tipo === 'Erro' || l.tipo === 'Timeout').length,
    reconexoes: filtradas.filter(l => l.tipo === 'Reconexao' || l.tipo === 'Desconexao').length,
  }), [filtradas])

  const exportarCsv = () => {
    const esc = (v: unknown) => `"${String(v ?? '').replace(/"/g, '""')}"`
    const cab = ['id', 'data_hora_utc', 'serie', 'origem', 'direcao', 'tipo', 'comando', 'descricao', 'hex']
    const corpo = filtradas.map(l => [l.id, l.dataHora, l.serial, l.origem, l.direcao, l.tipo, l.comando, l.descricao, l.hex].map(esc).join(';'))
    baixar(`centralhub-log-jfl-${Date.now()}.csv`, '﻿' + [cab.join(';'), ...corpo].join('\r\n'), 'text/csv;charset=utf-8')
  }

  const exportarJson = () =>
    baixar(`centralhub-log-jfl-${Date.now()}.json`, JSON.stringify(filtradas, null, 2), 'application/json')

  const limpar = async () => {
    if (!(await confirmar('Limpar log?', 'Apaga o registro de protocolo em memória no servidor (todas as centrais). Exporte antes se precisar.', 'Limpar', true))) return
    try {
      await jfl.limparLog()
      setLinhas([])
      toast.fire({ icon: 'success', title: 'Log limpo' })
    } catch (e) {
      erro('Não foi possível limpar o log', e)
    }
  }

  const copiar = async (l: JflLog) => {
    try {
      await navigator.clipboard.writeText(l.hex ?? l.descricao)
      toast.fire({ icon: 'success', title: 'Copiado' })
    } catch {
      toast.fire({ icon: 'error', title: 'Área de transferência indisponível' })
    }
  }

  return (
    <div>
      <PageHeader titulo="Logs de protocolo" icone="bi-terminal" migalhas={[{ texto: 'Monitoramento' }, { texto: 'Logs de protocolo' }]}
        subtitulo="Tudo o que passa pelo servidor JFL: quadros RX/TX em hexadecimal e interpretados, ACKs, erros, timeouts e reconexões.">
        <button className={`btn btn-sm ${pausado ? 'btn-success' : 'btn-outline-secondary'}`} onClick={() => setPausado(p => !p)}>
          <i className={`bi ${pausado ? 'bi-play-fill' : 'bi-pause-fill'} me-1`} />{pausado ? 'Retomar' : 'Pausar'}
        </button>
        <button className="btn btn-sm btn-outline-primary" onClick={exportarCsv} disabled={!filtradas.length}><i className="bi bi-filetype-csv me-1" />CSV</button>
        <button className="btn btn-sm btn-outline-primary" onClick={exportarJson} disabled={!filtradas.length}><i className="bi bi-filetype-json me-1" />JSON</button>
        <button className="btn btn-sm btn-outline-danger" onClick={limpar}><i className="bi bi-trash me-1" />Limpar</button>
      </PageHeader>

      {falha && <div className="alert alert-danger py-2">{falha}</div>}

      <div className="card shadow-sm mb-3">
        <div className="card-body">
          <div className="row g-2 align-items-end">
            <div className="col-6 col-md-3 col-xl-2">
              <label className="form-label small mb-1">Série</label>
              <select className="form-select form-select-sm" value={serial} onChange={e => setSerial(e.target.value)}>
                <option value="">Todas</option>
                {seriais.map(s => <option key={s} value={s}>{s}</option>)}
              </select>
            </div>
            <div className="col-6 col-md-3 col-xl-2">
              <label className="form-label small mb-1">Direção</label>
              <select className="form-select form-select-sm" value={direcao} onChange={e => setDirecao(e.target.value)}>
                <option value="">Todas</option>
                <option value="Rx">RX (central → CentralHub)</option>
                <option value="Tx">TX (CentralHub → central)</option>
                <option value="Sistema">Sistema</option>
              </select>
            </div>
            <div className="col-6 col-md-3 col-xl-2">
              <label className="form-label small mb-1">Tipo</label>
              <select className="form-select form-select-sm" value={tipo} onChange={e => setTipo(e.target.value)}>
                <option value="">Todos</option>
                {TIPOS.map(t => <option key={t} value={t}>{TIPO_TEXTO[t]}</option>)}
              </select>
            </div>
            <div className="col-6 col-md-3 col-xl-2">
              <label className="form-label small mb-1">Texto ou hex</label>
              <input className="form-control form-control-sm" value={texto} onChange={e => setTexto(e.target.value)} placeholder="ex.: 1130 ou 7B 06" />
            </div>
            <div className="col-6 col-md-3 col-xl-1">
              <label className="form-label small mb-1">De</label>
              <MaskedInput className="form-control-sm" value={de} onChange={setDe} mascara={v => maskHorario(v)} placeholder="HH:MM:SS" invalido={!deValido} />
            </div>
            <div className="col-6 col-md-3 col-xl-1">
              <label className="form-label small mb-1">Até</label>
              <MaskedInput className="form-control-sm" value={ate} onChange={setAte} mascara={v => maskHorario(v)} placeholder="HH:MM:SS" invalido={!ateValido} />
            </div>
            <div className="col-12 col-md-6 col-xl-2">
              <div className="form-check form-switch">
                <input className="form-check-input" type="checkbox" id="mostrar-hex" checked={mostrarHex} onChange={e => setMostrarHex(e.target.checked)} />
                <label className="form-check-label small" htmlFor="mostrar-hex">Mostrar hexadecimal</label>
              </div>
            </div>
          </div>
          <div className="small text-body-secondary mt-2">
            {filtradas.length} de {linhas.length} entradas · RX {contagem.rx} · TX {contagem.tx} · erros/timeouts {contagem.erros} · desconexões/reconexões {contagem.reconexoes}
            {' · '}horários no fuso local do navegador · o servidor guarda as últimas entradas em memória (reiniciar a API limpa)
          </div>
        </div>
      </div>

      <div className="card shadow-sm mb-4">
        {filtradas.length === 0 ? <Vazio icone="bi-terminal">Nenhuma entrada de log {linhas.length ? 'para os filtros escolhidos' : 'ainda'}.</Vazio> : (
          <div className="table-responsive" style={{ maxHeight: '65vh' }}>
            <table className="table table-sm table-hover log-tabela mb-0">
              <thead className="sticky-top bg-body"><tr><th>Hora</th><th>Dir.</th><th>Tipo</th><th>Série</th><th>Interpretação</th><th></th></tr></thead>
              <tbody>
                {[...filtradas].reverse().map(l => {
                  const d = DIRECAO[l.direcao] ?? DIRECAO.Sistema
                  return (
                    <tr key={l.id} className={`log-${l.direcao} log-${l.tipo}`}>
                      <td className="text-nowrap font-monospace">{horaComMs(l.dataHora)}</td>
                      <td><span className={`badge text-bg-${d.cor}`}><i className={`bi ${d.icone} me-1`} />{d.texto}</span></td>
                      <td className="text-nowrap">{TIPO_TEXTO[l.tipo] ?? l.tipo}{l.comando && <span className="text-body-secondary"> {l.comando}</span>}</td>
                      <td className="font-monospace text-nowrap">{l.serial ?? <span className="text-body-secondary">{l.origem}</span>}</td>
                      <td>
                        <div>{l.descricao}</div>
                        {mostrarHex && l.hex && <div className="log-hex text-body-secondary">{formatarHex(l.hex)}</div>}
                      </td>
                      <td><button className="btn btn-sm btn-link p-0" title="Copiar" onClick={() => copiar(l)}><i className="bi bi-clipboard" /></button></td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>

      <RecursosCard recursos={[
        { nome: 'Quadros RX/TX em hexadecimal', suporte: 'funcional', detalhe: 'Todos os quadros trocados com as centrais JFL, como chegaram/saíram.' },
        { nome: 'Interpretação dos quadros', suporte: 'funcional', detalhe: 'Conexão 0x21, eventos 0x24, keep-alive 0x40, status, comandos 0x4D/0x4E/0x4F/0x50/0x51/0x52 e seus ACKs.' },
        { nome: 'Erros, timeouts e reconexões', suporte: 'funcional', detalhe: 'Watchdog de inatividade, comando sem resposta, quadro inválido, ressincronização, geração de protocolo não suportada, substituição de sessão.' },
        { nome: 'Filtros e exportação CSV/JSON', suporte: 'funcional', detalhe: 'Exporta exatamente as linhas filtradas, para anexar a relatórios de teste em hardware.' },
        { nome: 'Histórico persistente do log', suporte: 'nao-suportado', detalhe: 'O log de protocolo fica em memória no servidor (capacidade limitada). Eventos de alarme são persistidos em Eventos.' },
      ]} />
    </div>
  )
}
