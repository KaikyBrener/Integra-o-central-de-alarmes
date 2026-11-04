import { useMemo, useState } from 'react'
import { PageHeader, Vazio } from '../components/ui'
import { useJfl } from '../context/JflContext'
import { usePolling } from '../hooks/usePolling'
import { centralHub, mensagemDeErro } from '../services/api'
import type { EventoCentral } from '../types'
import { dataHora, TIPO_EVENTO, tipoEvento } from '../utils/format'

export default function EventosPage() {
  const { centrais } = useJfl()
  const [eventos, setEventos] = useState<EventoCentral[]>([])
  const [centralId, setCentralId] = useState<number | ''>('')
  const [tipo, setTipo] = useState('')
  const [texto, setTexto] = useState('')
  const [limite, setLimite] = useState(200)
  const [falha, setFalha] = useState<string | null>(null)

  usePolling(async () => {
    try {
      setEventos(await centralHub.eventos({ centralId: centralId || undefined, limite }))
      setFalha(null)
    } catch (e) {
      setFalha(mensagemDeErro(e))
    }
  }, 5000)

  const filtrados = useMemo(() => eventos.filter(e =>
    (!centralId || e.centralId === centralId)
    && (!tipo || e.tipo === tipo)
    && (!texto || `${e.codigo} ${e.descricao} ${e.serial} ${e.conta}`.toLowerCase().includes(texto.toLowerCase()))), [eventos, centralId, tipo, texto])

  const exportar = () => {
    const esc = (v: unknown) => `"${String(v ?? '').replace(/"/g, '""')}"`
    const linhas = filtrados.map(e => [e.id, e.dataHora, e.centralNome, e.serial, e.conta, e.codigo, e.tipo, e.restauracao ? 'sim' : 'não', e.particao, e.zona, e.usuario, e.pgm, e.descricao].map(esc).join(';'))
    const csv = '﻿' + ['id;data_hora;central;serie;conta;codigo;tipo;restauracao;particao;zona;usuario;pgm;descricao', ...linhas].join('\r\n')
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }))
    const a = document.createElement('a')
    a.href = url
    a.download = `centralhub-eventos-${Date.now()}.csv`
    a.click()
    URL.revokeObjectURL(url)
  }

  return (
    <div>
      <PageHeader titulo="Eventos" icone="bi-bell" migalhas={[{ texto: 'Monitoramento' }, { texto: 'Eventos' }]}
        subtitulo="Eventos Contact ID recebidos das centrais, já confirmados (ACK) e gravados no banco. Atualização a cada 5 s.">
        <button className="btn btn-sm btn-outline-primary" onClick={exportar} disabled={!filtrados.length}><i className="bi bi-filetype-csv me-1" />Exportar CSV</button>
      </PageHeader>

      {falha && <div className="alert alert-danger py-2">{falha}</div>}

      <div className="card shadow-sm mb-3">
        <div className="card-body row g-2">
          <div className="col-md-4">
            <select className="form-select form-select-sm" value={centralId} onChange={e => setCentralId(e.target.value ? Number(e.target.value) : '')}>
              <option value="">Todas as centrais (inclusive não cadastradas)</option>
              {centrais.map(c => <option key={c.id} value={c.id}>{c.nome}</option>)}
            </select>
          </div>
          <div className="col-md-3">
            <select className="form-select form-select-sm" value={tipo} onChange={e => setTipo(e.target.value)}>
              <option value="">Todos os tipos</option>
              {Object.entries(TIPO_EVENTO).map(([k, v]) => <option key={k} value={k}>{v.texto}</option>)}
            </select>
          </div>
          <div className="col-md-3">
            <input className="form-control form-control-sm" placeholder="Código, descrição, série ou conta" value={texto} onChange={e => setTexto(e.target.value)} />
          </div>
          <div className="col-md-2">
            <select className="form-select form-select-sm" value={limite} onChange={e => setLimite(Number(e.target.value))}>
              {[100, 200, 500, 1000].map(n => <option key={n} value={n}>Últimos {n}</option>)}
            </select>
          </div>
        </div>
      </div>

      <div className="card shadow-sm">
        {filtrados.length === 0 ? <Vazio icone="bi-bell-slash">Nenhum evento.</Vazio> : (
          <div className="table-responsive">
            <table className="table table-sm table-hover align-middle mb-0">
              <thead><tr><th>Data/hora</th><th>Central</th><th>Código</th><th>Tipo</th><th>Descrição</th><th>Conta</th></tr></thead>
              <tbody>
                {filtrados.map(e => {
                  const t = tipoEvento(e.tipo)
                  return (
                    <tr key={e.id}>
                      <td className="text-nowrap small">{dataHora(e.dataHora)}</td>
                      <td className="small">{e.centralNome ?? <span className="font-monospace text-body-secondary">{e.serial ?? '—'}</span>}</td>
                      <td className="font-monospace">{e.codigo}</td>
                      <td><span className={`badge text-bg-${t.cor}`}>{t.texto}</span>{e.restauracao && <span className="badge text-bg-light border ms-1">restauração</span>}</td>
                      <td className="small">{e.descricao}</td>
                      <td className="font-monospace small">{e.conta}</td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  )
}
