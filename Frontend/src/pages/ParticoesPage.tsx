import { useState } from 'react'
import ComCentralJfl from '../components/ComCentralJfl'
import { BarraStatus } from '../components/Grades'
import { PageHeader, RecursosCard, Spinner } from '../components/ui'
import { useJfl } from '../context/JflContext'
import { useJflStatus } from '../hooks/usePolling'
import { centralHub } from '../services/api'
import type { Central, JflSessao } from '../types'
import { confirmar, esc, executarComando } from '../utils/alerts'
import { pad2, particaoEstado } from '../utils/format'

function Conteudo({ central, sessao }: { central: Central; sessao: JflSessao }) {
  const { servidor } = useJfl()
  const [automatico, setAutomatico] = useState(true)
  const { status, erro, carregando, atualizar } = useJflStatus(sessao.serial, 10000, automatico)
  const m = sessao.modelo
  const podeComandar = m.comandosHabilitados && !servidor?.somenteLeitura

  const comandar = async (particao: number, armar: boolean) => {
    const ok = await confirmar(
      `${armar ? 'Armar' : 'Desarmar'} a partição ${pad2(particao)}?`,
      `${esc(central.nome)} — ${m.nome}.<br/><small>O arme pode ser recusado pela central (zona aberta, tempo de saída ou restrição do usuário mestre ao monitoramento). O resultado só é confirmado pelo estado real da partição. O evento sai como usuário 99 (software).</small>`,
      armar ? 'Armar' : 'Desarmar', armar)
    if (!ok) return
    if (await executarComando(armar ? 'Armando...' : 'Desarmando...', () => centralHub.particao(central.id, particao, armar ? 'Armar' : 'Desarmar'))) {
      atualizar()
    }
  }

  return (
    <>
      <BarraStatus status={status} carregando={carregando} erro={erro} onAtualizar={atualizar} automatico={automatico} onAutomatico={setAutomatico} />
      {!status ? <Spinner texto="Lendo status da central..." /> : (
        <div className="card shadow-sm mb-4">
          <div className="card-header fw-semibold">{m.particoes} partições no modelo {m.nome}{m.eletrificador ? ' + eletrificador' : ''}</div>
          <div className="table-responsive">
            <table className="table table-hover align-middle mb-0">
              <thead><tr><th>Partição</th><th>Estado</th><th>Alarme</th><th>Byte</th><th className="text-end">Comandos</th></tr></thead>
              <tbody>
                {status.particoes.map(p => {
                  const e = particaoEstado(p.estado)
                  const armada = p.estado === 'ArmadaTotal' || p.estado === 'ArmadaStay'
                  return (
                    <tr key={p.numero} className={p.programada ? '' : 'text-body-secondary'}>
                      <td className="fw-semibold">Partição {pad2(p.numero)}</td>
                      <td><span className={`badge text-bg-${e.cor}`}><i className={`bi ${e.icone} me-1`} />{e.texto}</span></td>
                      <td>{p.emAlarme ? <span className="badge text-bg-danger">EM ALARME</span> : '—'}</td>
                      <td className="font-monospace small">{p.bruto}</td>
                      <td className="text-end">
                        {p.programada && podeComandar && (
                          <div className="btn-group btn-group-sm">
                            <button className="btn btn-outline-danger" disabled={armada} onClick={() => comandar(p.numero, true)}><i className="bi bi-lock me-1" />Armar</button>
                            <button className="btn btn-outline-success" disabled={!armada && !p.emAlarme} onClick={() => comandar(p.numero, false)}><i className="bi bi-unlock me-1" />Desarmar</button>
                          </div>
                        )}
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
          <div className="card-footer small">
            <i className="bi bi-lightning-charge me-1" />Eletrificador: <strong>{status.eletrificador}</strong>
          </div>
        </div>
      )}
      <RecursosCard recursos={[
        { nome: 'Estado das partições', suporte: 'funcional', detalhe: `Desarmada, armada, armada STAY e alarme — partições 1 a ${m.particoes}.` },
        { nome: 'Armar / desarmar', suporte: podeComandar ? 'funcional' : 'nao-suportado', detalhe: podeComandar ? 'Comandos 0x4E / 0x4F sem senha, confirmados pelo estado da partição.' : 'Comandos desabilitados para este modelo ou CentralHub em somente leitura.' },
        { nome: 'Armar STAY / AWAY por comando', suporte: 'aguardando', detalhe: 'Comandos 0x53/0x54 documentados mas sem captura real; não habilitados.' },
        { nome: 'Estado do eletrificador', suporte: m.eletrificador ? 'consulta' : 'nao-suportado', detalhe: m.eletrificador ? 'Lido do status.' : 'Modelo sem eletrificador.' },
        { nome: 'Armar / desarmar eletrificador', suporte: m.eletrificador ? 'aguardando' : 'nao-suportado', detalhe: 'Partição 99 nos comandos 0x4E/0x4F; não habilitado nesta versão (risco operacional).' },
        { nome: 'Tempos de entrada, saída e disparo', suporte: 'aguardando', detalhe: m.particoes > 4 ? 'Endereços 401/411, 402/412 e 403xx (Active 100 Bus).' : 'Endereços 401/411, 402/412, 403/413/405/415.' },
        { nome: 'Autoarme por horário e por não movimento', suporte: 'aguardando', detalhe: 'Endereços 521 a 524, 404xx, 508/509 (leitura 0x44 não implementada).' },
        { nome: 'Nomes das partições', suporte: 'aguardando', detalhe: 'Leitura da programação (0x44).' },
      ]} />
    </>
  )
}

export default function ParticoesPage() {
  return (
    <div>
      <PageHeader titulo="Partições" icone="bi-shield-lock" migalhas={[{ texto: 'Central JFL' }, { texto: 'Partições' }]} />
      <ComCentralJfl>{(c, s) => <Conteudo central={c} sessao={s} />}</ComCentralJfl>
    </div>
  )
}
