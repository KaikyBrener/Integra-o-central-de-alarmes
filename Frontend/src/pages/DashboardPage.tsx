import { useState } from 'react'
import { Link } from 'react-router-dom'
import ComCentralJfl from '../components/ComCentralJfl'
import { BarraStatus, GradeParticoes, GradePgms, GradeZonas } from '../components/Grades'
import { Info, PageHeader, Spinner, Vazio } from '../components/ui'
import { useJfl } from '../context/JflContext'
import { useJflStatus, usePolling } from '../hooks/usePolling'
import { centralHub } from '../services/api'
import type { Central, EventoCentral, JflSessao } from '../types'
import { dataHora, tempoDesde, tipoEvento } from '../utils/format'
import { formatarMac } from '../utils/masks'

function Indicador({ titulo, valor, icone, cor = 'primary', detalhe }: { titulo: string; valor: string | number; icone: string; cor?: string; detalhe?: string }) {
  return (
    <div className="col-6 col-md-4 col-xl-2">
      <div className="card card-indicador shadow-sm h-100">
        <div className="card-body py-3">
          <div className="d-flex justify-content-between text-body-secondary small">
            <span>{titulo}</span><i className={`bi ${icone} text-${cor}`} />
          </div>
          <div className="valor">{valor}</div>
          {detalhe && <div className="small text-body-secondary">{detalhe}</div>}
        </div>
      </div>
    </div>
  )
}

function PainelCentral({ central, sessao }: { central: Central; sessao: JflSessao }) {
  const [automatico, setAutomatico] = useState(true)
  const { status, erro, carregando, atualizar } = useJflStatus(sessao.serial, 10000, automatico)
  const [eventos, setEventos] = useState<EventoCentral[]>([])

  usePolling(async () => {
    // Pela série: inclui eventos recebidos antes de a central ser cadastrada (gravados sem CentralId).
    try { setEventos(await centralHub.eventos({ serial: sessao.serial, limite: 8 })) } catch { /* exibido no topo */ }
  }, 5000)

  const m = sessao.modelo
  const armadas = status?.particoes.filter(p => p.estado === 'ArmadaTotal' || p.estado === 'ArmadaStay').length ?? 0
  const programadas = status?.particoes.filter(p => p.programada).length ?? 0

  return (
    <>
      <div className="row g-3 mb-4">
        <div className="col-lg-4">
          <div className="card shadow-sm h-100">
            <div className="card-header fw-semibold"><i className="bi bi-cpu me-2" />Identificação</div>
            <div className="card-body row">
              <div className="col-6"><Info rotulo="Modelo">{m.nome}</Info></div>
              <div className="col-6"><Info rotulo="Placa">{m.placa}</Info></div>
              <div className="col-6"><Info rotulo="Número de série" mono>{sessao.serial}</Info></div>
              <div className="col-6"><Info rotulo="Firmware" mono>{sessao.firmware || '—'}</Info></div>
              <div className="col-6"><Info rotulo="MAC" mono>{formatarMac(sessao.mac)}</Info></div>
              <div className="col-6"><Info rotulo="Byte de modelo" mono>{m.byteModelo}</Info></div>
              <div className="col-6"><Info rotulo="Origem (IP:porta)" mono>{sessao.origem}</Info></div>
              <div className="col-6"><Info rotulo="Último quadro">{tempoDesde(sessao.ultimoQuadroEm)}</Info></div>
              <div className="col-12">
                <Info rotulo="Validação">
                  {m.validadoEmHardware
                    ? <span className="badge text-bg-success">Validado em hardware</span>
                    : <span className="badge text-bg-warning">Não validado em hardware</span>}
                </Info>
              </div>
            </div>
          </div>
        </div>
        <div className="col-lg-8">
          <BarraStatus status={status} carregando={carregando} erro={erro} onAtualizar={atualizar} automatico={automatico} onAutomatico={setAutomatico} />
          {!status ? <Spinner texto="Lendo status da central..." /> : (
            <div className="row g-3">
              <Indicador titulo="Partições armadas" valor={`${armadas}/${programadas}`} icone="bi-shield-lock" detalhe={`${m.particoes} no modelo`} />
              <Indicador titulo="Zonas abertas" valor={status.zonas.filter(z => z.estado === 'Open').length} icone="bi-door-open" cor="warning" detalhe={`${status.zonas.length} em uso de ${m.zonas}`} />
              <Indicador titulo="Disparos" valor={status.zonas.filter(z => z.estado === 'Triggered').length + status.particoes.filter(p => p.emAlarme).length} icone="bi-exclamation-octagon" cor="danger" />
              <Indicador titulo="Zonas anuladas" valor={status.zonas.filter(z => z.estado === 'Bypassed').length} icone="bi-slash-circle" cor="secondary" />
              <Indicador titulo="PGMs ligadas" valor={`${status.pgms.filter(p => p.ligada).length}/${m.pgms}`} icone="bi-toggles" cor="success" />
              <Indicador titulo="Problemas" valor={status.problemas.length} icone="bi-tools" cor={status.problemas.length ? 'danger' : 'success'} />
              <div className="col-md-6">
                <div className="card shadow-sm"><div className="card-body py-2 small">
                  <i className="bi bi-battery-charging me-1" />Bateria: <strong>{status.bateriaVolts.toFixed(1)} V</strong>
                  <span className="mx-2">·</span><i className="bi bi-clock me-1" />Relógio da central: <strong>{status.relogio || '—'}</strong>
                </div></div>
              </div>
              <div className="col-md-6">
                <div className="card shadow-sm"><div className="card-body py-2 small">
                  <i className="bi bi-lightning-charge me-1" />Eletrificador: <strong>{status.eletrificador}</strong>
                </div></div>
              </div>
            </div>
          )}
        </div>
      </div>

      {status && (
        <>
          {status.problemas.length > 0 && (
            <div className="alert alert-danger">
              <strong><i className="bi bi-tools me-2" />Problemas informados pela central:</strong>{' '}
              {status.problemas.map(p => p.descricao).join(' · ')}
            </div>
          )}
          <div className="row g-3">
            <div className="col-xl-6">
              <div className="card shadow-sm mb-3">
                <div className="card-header d-flex justify-content-between"><span className="fw-semibold"><i className="bi bi-shield-lock me-2" />Partições</span><Link to="/particoes" className="small">Operar</Link></div>
                <div className="card-body"><GradeParticoes modelo={m} status={status} /></div>
              </div>
              <div className="card shadow-sm mb-3">
                <div className="card-header d-flex justify-content-between"><span className="fw-semibold"><i className="bi bi-toggles me-2" />PGMs</span><Link to="/pgms" className="small">Operar</Link></div>
                <div className="card-body"><GradePgms status={status} /></div>
              </div>
            </div>
            <div className="col-xl-6">
              <div className="card shadow-sm mb-3">
                <div className="card-header d-flex justify-content-between"><span className="fw-semibold"><i className="bi bi-bell me-2" />Últimos eventos</span><Link to="/eventos" className="small">Ver todos</Link></div>
                <ul className="list-group list-group-flush">
                  {eventos.length === 0 && <li className="list-group-item text-body-secondary small">Nenhum evento recebido desta central.</li>}
                  {eventos.map(e => (
                    <li key={e.id} className="list-group-item small d-flex gap-2">
                      <span className={`badge text-bg-${tipoEvento(e.tipo).cor} align-self-start`}>{tipoEvento(e.tipo).texto}</span>
                      <span className="flex-grow-1">{e.descricao}</span>
                      <span className="text-body-secondary text-nowrap">{dataHora(e.dataHora)}</span>
                    </li>
                  ))}
                </ul>
              </div>
            </div>
          </div>
          <div className="card shadow-sm">
            <div className="card-header d-flex justify-content-between"><span className="fw-semibold"><i className="bi bi-grid-3x3-gap me-2" />Zonas ({m.zonas} no modelo)</span><Link to="/zonas" className="small">Detalhes e bypass</Link></div>
            <div className="card-body"><GradeZonas modelo={m} status={status} /></div>
          </div>
        </>
      )}
    </>
  )
}

function SessoesConectadas() {
  const { sessoes, servidor } = useJfl()
  return (
    <div className="card shadow-sm mb-4">
      <div className="card-header d-flex flex-wrap justify-content-between gap-2">
        <span className="fw-semibold"><i className="bi bi-hdd-network me-2" />Centrais JFL conectadas ao CentralHub</span>
        {servidor && (
          <span className="small text-body-secondary">
            Servidor {servidor.habilitado && servidor.portaEmUso ? <>escutando em <strong>{servidor.bindAddress}:{servidor.portaEmUso}</strong></> : <strong>desligado</strong>}
            {' · '}keep-alive {servidor.keepAliveMinutes} min
            {servidor.aceitaCentraisDesconhecidas ? ' · aceita centrais não cadastradas' : ' · só centrais cadastradas'}
          </span>
        )}
      </div>
      {sessoes.length === 0 ? (
        <div className="card-body"><Vazio icone="bi-wifi-off">Nenhuma central JFL conectada no momento.</Vazio></div>
      ) : (
        <div className="table-responsive">
          <table className="table table-hover align-middle mb-0">
            <thead><tr><th>Série</th><th>Modelo</th><th>Firmware</th><th>Origem</th><th>Conectada</th><th>Último quadro</th><th>Cadastro</th></tr></thead>
            <tbody>
              {sessoes.map(s => (
                <tr key={s.serial}>
                  <td className="font-monospace">{s.serial}</td>
                  <td>{s.modelo.nome} <span className="text-body-secondary small">({s.modelo.byteModelo})</span>
                    {!s.modelo.comandosHabilitados && <span className="badge text-bg-secondary ms-1">sem comandos</span>}</td>
                  <td className="font-monospace">{s.firmware}</td>
                  <td className="font-monospace small">{s.origem}</td>
                  <td className="small">{dataHora(s.conectadaEm)}</td>
                  <td className="small">{tempoDesde(s.ultimoQuadroEm)}</td>
                  <td>{s.centralId
                    ? <span className="small">{s.centralNome}</span>
                    : <Link className="btn btn-sm btn-outline-primary" to={`/centrais?serial=${s.serial}&ip=${encodeURIComponent(s.origem.split(':')[0])}`}><i className="bi bi-plus-lg me-1" />Cadastrar</Link>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

export default function DashboardPage() {
  const { central } = useJfl()
  return (
    <div>
      <PageHeader titulo="Dashboard" icone="bi-speedometer2" migalhas={[{ texto: 'Dashboard' }]}
        subtitulo={central ? `Central selecionada: ${central.nome}` : undefined} />
      <SessoesConectadas />
      <ComCentralJfl>{(c, s) => <PainelCentral central={c} sessao={s} />}</ComCentralJfl>
    </div>
  )
}
