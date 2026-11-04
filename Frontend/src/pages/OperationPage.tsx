import { useEffect, useState } from 'react'
import { PageHeader, Vazio } from '../components/ui'
import { useJfl } from '../context/JflContext'
import { usePolling } from '../hooks/usePolling'
import { centralHub, mensagemDeErro } from '../services/api'
import type { Building, History } from '../types'
import { confirmar, erro, esc, executarComando } from '../utils/alerts'
import { dataHora } from '../utils/format'

type Comando = 'Ligar' | 'Desligar' | 'Pulso'

export default function OperationPage() {
  const { centrais, sessoes } = useJfl()
  const [buildings, setBuildings] = useState<Building[]>([])
  const [history, setHistory] = useState<History[]>([])
  const [filtroCentral, setFiltroCentral] = useState<number | ''>('')

  const [buildingId, setBuildingId] = useState<number | ''>('')
  const [centralId, setCentralId] = useState<number | ''>('')
  const [pgm, setPgm] = useState(1)
  const [comando, setComando] = useState<Comando>('Ligar')
  const [tempoPulsoMs, setTempoPulsoMs] = useState(1000)

  useEffect(() => {
    centralHub.buildings().then(setBuildings).catch(e => erro('Não foi possível carregar os prédios', e))
  }, [])

  const carregarHistorico = async () => {
    try {
      setHistory(await centralHub.historico(filtroCentral || undefined))
    } catch (e) {
      console.warn(mensagemDeErro(e))
    }
  }
  usePolling(carregarHistorico, 10000)
  useEffect(() => { carregarHistorico() }, [filtroCentral]) // eslint-disable-line react-hooks/exhaustive-deps

  const centralsDoPredio = centrais.filter(c => c.buildingId === buildingId)
  const central = centrais.find(c => c.id === centralId)
  const sessao = central?.fabricante?.toLowerCase() === 'jfl' ? sessoes.find(s => s.serial === central.usuario.trim()) : undefined
  const pgmMax = sessao?.modelo.pgms ?? 16
  const pgmInvalida = !Number.isInteger(pgm) || pgm < 1 || pgm > pgmMax
  const pulsoInvalido = comando === 'Pulso' && (!Number.isInteger(tempoPulsoMs) || tempoPulsoMs < 1 || tempoPulsoMs > 600000)

  const enviar = async () => {
    if (!central || pgmInvalida || pulsoInvalido) return
    const detalhe = comando === 'Pulso' ? ` por ${tempoPulsoMs} ms` : ''
    if (!(await confirmar(`${comando} PGM ${pgm}${detalhe}?`, `Central <strong>${esc(central.nome)}</strong>.`, 'Enviar', comando !== 'Desligar'))) return
    await executarComando('Enviando comando...', () => centralHub.enviarPgm(central.id, pgm, comando, tempoPulsoMs))
    carregarHistorico()
  }

  return (
    <div>
      <PageHeader titulo="Comandos e histórico" icone="bi-clock-history" migalhas={[{ texto: 'Cadastros' }, { texto: 'Comandos e histórico' }]}
        subtitulo="Envio rápido de comandos de PGM para qualquer central cadastrada e histórico de todas as operações (PGM, arme/desarme, bypass)." />

      <div className="card shadow-sm mb-4">
        <div className="card-header fw-semibold">Comando de PGM</div>
        <div className="card-body">
          <form className="row g-3" onSubmit={e => { e.preventDefault(); enviar() }}>
            <div className="col-md-3">
              <label className="form-label">Prédio</label>
              <select className="form-select" value={buildingId} onChange={e => { setBuildingId(e.target.value ? Number(e.target.value) : ''); setCentralId('') }}>
                <option value="">Selecione...</option>
                {buildings.map(b => <option key={b.id} value={b.id}>{b.nome}</option>)}
              </select>
            </div>
            <div className="col-md-3">
              <label className="form-label">Central</label>
              <select className="form-select" value={centralId} disabled={!buildingId} onChange={e => setCentralId(e.target.value ? Number(e.target.value) : '')}>
                <option value="">{centralsDoPredio.length ? 'Selecione...' : 'Nenhuma central neste prédio'}</option>
                {centralsDoPredio.map(c => <option key={c.id} value={c.id}>{c.nome}{c.fabricante ? '' : ' (não identificada)'}</option>)}
              </select>
              {sessao && <div className="form-text">{sessao.modelo.nome}: PGM 1 a {sessao.modelo.pgms}</div>}
              {central?.fabricante?.toLowerCase() === 'jfl' && !sessao && <div className="form-text text-warning-emphasis">Central JFL offline.</div>}
            </div>
            <div className="col-md-2">
              <label className="form-label">PGM</label>
              <input type="number" className={`form-control ${pgmInvalida ? 'is-invalid' : ''}`} min={1} max={pgmMax} value={pgm} onChange={e => setPgm(Number(e.target.value))} />
              <div className="invalid-feedback">De 1 a {pgmMax}.</div>
            </div>
            <div className="col-md-2">
              <label className="form-label">Comando</label>
              <select className="form-select" value={comando} onChange={e => setComando(e.target.value as Comando)}>
                <option value="Ligar">Ligar</option>
                <option value="Desligar">Desligar</option>
                <option value="Pulso">Pulso</option>
              </select>
            </div>
            {comando === 'Pulso' && (
              <div className="col-md-2">
                <label className="form-label">Pulso (ms)</label>
                <input type="number" className={`form-control ${pulsoInvalido ? 'is-invalid' : ''}`} min={1} max={600000} value={tempoPulsoMs} onChange={e => setTempoPulsoMs(Number(e.target.value))} />
                <div className="invalid-feedback">De 1 a 600000 ms.</div>
              </div>
            )}
            <div className="col-12">
              <button type="submit" className="btn btn-primary" disabled={!central || pgmInvalida || pulsoInvalido}><i className="bi bi-send me-1" />Enviar</button>
            </div>
          </form>
        </div>
      </div>

      <div className="card shadow-sm">
        <div className="card-header d-flex flex-wrap gap-2 align-items-center">
          <span className="fw-semibold me-auto">Histórico</span>
          <select className="form-select form-select-sm w-auto" value={filtroCentral} onChange={e => setFiltroCentral(e.target.value ? Number(e.target.value) : '')}>
            <option value="">Todas as centrais</option>
            {centrais.map(c => <option key={c.id} value={c.id}>{c.nome}</option>)}
          </select>
        </div>
        {history.length === 0 ? <Vazio icone="bi-clock-history">Nenhum comando enviado.</Vazio> : (
          <div className="table-responsive">
            <table className="table table-sm table-hover align-middle mb-0">
              <thead><tr><th>Data</th><th>Central</th><th>PGM</th><th>Comando</th><th>Resultado</th></tr></thead>
              <tbody>
                {history.map(h => {
                  const falhou = h.resultado.startsWith('FALHA:')
                  return (
                    <tr key={h.id}>
                      <td className="text-nowrap small">{dataHora(h.data)}</td>
                      <td className="small">{h.centralNome ?? centrais.find(c => c.id === h.centralId)?.nome ?? h.centralId}</td>
                      <td>{h.pgm || '—'}</td>
                      <td>{h.comando}</td>
                      <td className="small">
                        <i className={`bi ${falhou ? 'bi-x-circle-fill text-danger' : 'bi-check-circle-fill text-success'} me-1`} />
                        {falhou ? h.resultado.replace(/^FALHA:\s*/, '') : h.resultado}
                      </td>
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
