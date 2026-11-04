import { useMemo, useState } from 'react'
import ComCentralJfl from '../components/ComCentralJfl'
import { BarraStatus, GradeZonas } from '../components/Grades'
import { PageHeader, RecursosCard, Spinner } from '../components/ui'
import { useJfl } from '../context/JflContext'
import { useJflStatus } from '../hooks/usePolling'
import { centralHub } from '../services/api'
import type { Central, JflSessao } from '../types'
import { confirmar, executarComando } from '../utils/alerts'
import { pad2, ZONA_ESTADO, zonaEstado } from '../utils/format'

function Conteudo({ central, sessao }: { central: Central; sessao: JflSessao }) {
  const { servidor } = useJfl()
  const [automatico, setAutomatico] = useState(true)
  const { status, erro, carregando, atualizar } = useJflStatus(sessao.serial, 10000, automatico)
  const [filtro, setFiltro] = useState('')
  const [visao, setVisao] = useState<'grade' | 'tabela'>('tabela')
  const m = sessao.modelo
  const podeComandar = m.comandosHabilitados && !servidor?.somenteLeitura

  const zonas = useMemo(() => (status?.zonas ?? []).filter(z => !filtro || z.estado === filtro), [status, filtro])

  const bypass = async (zona: number, anular: boolean) => {
    const ok = await confirmar(
      anular ? `Anular a zona ${pad2(zona)}?` : `Reativar a zona ${pad2(zona)}?`,
      anular
        ? 'A zona deixa de disparar o alarme até ser reativada. O CentralHub envia o conjunto completo de zonas anuladas (comando 0x52) preservando as que já estão anuladas.'
        : 'A zona volta a disparar o alarme.',
      anular ? 'Anular' : 'Reativar', anular)
    if (!ok) return
    if (await executarComando(anular ? 'Anulando zona...' : 'Reativando zona...',
      () => centralHub.bypass(central.id, zona, anular ? 'Anular' : 'Reativar'))) {
      atualizar()
    }
  }

  return (
    <>
      <BarraStatus status={status} carregando={carregando} erro={erro} onAtualizar={atualizar} automatico={automatico} onAutomatico={setAutomatico} />
      {!status ? <Spinner texto="Lendo status da central..." /> : (
        <div className="card shadow-sm mb-4">
          <div className="card-header d-flex flex-wrap gap-2 align-items-center">
            <span className="fw-semibold me-auto">{status.zonas.length} zonas em uso · {m.zonas} no modelo {m.nome}</span>
            <select className="form-select form-select-sm w-auto" value={filtro} onChange={e => setFiltro(e.target.value)}>
              <option value="">Todos os estados</option>
              {Object.entries(ZONA_ESTADO).map(([k, v]) => <option key={k} value={k}>{v.texto}</option>)}
            </select>
            <div className="btn-group btn-group-sm">
              <button className={`btn ${visao === 'tabela' ? 'btn-primary' : 'btn-outline-primary'}`} onClick={() => setVisao('tabela')}><i className="bi bi-list-ul" /></button>
              <button className={`btn ${visao === 'grade' ? 'btn-primary' : 'btn-outline-primary'}`} onClick={() => setVisao('grade')}><i className="bi bi-grid-3x3-gap" /></button>
            </div>
          </div>
          {visao === 'grade' ? <div className="card-body"><GradeZonas modelo={m} status={status} /></div> : (
            <div className="table-responsive">
              <table className="table table-hover align-middle mb-0">
                <thead><tr><th>Zona</th><th>Estado</th><th>Permite inibir</th><th className="text-end">Bypass</th></tr></thead>
                <tbody>
                  {zonas.map(z => {
                    const e = zonaEstado(z.estado)
                    const anulada = z.estado === 'Bypassed'
                    return (
                      <tr key={z.numero}>
                        <td className="fw-semibold">Zona {pad2(z.numero)}</td>
                        <td><span className={`badge text-bg-${e.cor}`}><i className={`bi ${e.icone} me-1`} />{e.texto}</span></td>
                        <td>{z.permiteInibir ? <span className="text-success-emphasis">Sim</span> : <span className="text-body-secondary">Não</span>}</td>
                        <td className="text-end">
                          {podeComandar && (anulada
                            ? <button className="btn btn-sm btn-outline-success" onClick={() => bypass(z.numero, false)}><i className="bi bi-arrow-counterclockwise me-1" />Reativar</button>
                            : <button className="btn btn-sm btn-outline-secondary" disabled={!z.permiteInibir}
                                title={z.permiteInibir ? '' : 'Atributo "permite inibir" desligado na programação da central'}
                                onClick={() => bypass(z.numero, true)}><i className="bi bi-slash-circle me-1" />Anular</button>)}
                        </td>
                      </tr>
                    )
                  })}
                  {zonas.length === 0 && <tr><td colSpan={4} className="text-center text-body-secondary py-4">Nenhuma zona neste estado.</td></tr>}
                </tbody>
              </table>
            </div>
          )}
          <div className="card-footer small text-body-secondary">
            Zonas desabilitadas na programação não são reportadas pela central e aparecem como "Não usada" na grade.
            Zonas auto-anuladas continuam informando o estado físico (acompanhe os eventos 1573).
          </div>
        </div>
      )}
      <RecursosCard recursos={[
        { nome: 'Estado da zona', suporte: 'funcional', detalhe: 'Fechada, aberta, disparo, anulada, tamper, curto, bateria fraca, sem comunicação — lido do quadro de status.' },
        { nome: 'Anular / reativar (bypass)', suporte: podeComandar ? 'funcional' : 'nao-suportado', detalhe: podeComandar ? 'Comando 0x52, confirmado pelo status devolvido.' : 'Comandos desabilitados para este modelo ou CentralHub em somente leitura.' },
        { nome: 'Atributo "permite inibir"', suporte: 'consulta', detalhe: 'Informado pela própria central no status (P-INIB).' },
        { nome: 'Nome da zona', suporte: 'aguardando', detalhe: 'Endereços 601 a 699 da programação (leitura 0x44 não implementada).' },
        { nome: 'Tipo da zona', suporte: 'aguardando', detalhe: 'Endereços 001 a 099 (imediata, temporizada, seguidora, 24 h, ronda, pânico, tamper...).' },
        { nome: 'Partição da zona', suporte: 'aguardando', detalhe: m.particoes > 2 ? 'Endereço 100xx (zona → partição 01 a 16).' : 'Endereço 100xx.' },
        { nome: 'Atributos (STAY, inteligente, silenciosa, auto-anulável, chime, sensibilidade...)', suporte: 'aguardando', detalhe: 'Endereços 101 a 199 da programação.' },
        { nome: 'Edição de zonas', suporte: 'aguardando', detalhe: 'Gravação de programação (0x45) não implementada.' },
      ]} />
    </>
  )
}

export default function ZonasPage() {
  return (
    <div>
      <PageHeader titulo="Zonas" icone="bi-grid-3x3-gap" migalhas={[{ texto: 'Central JFL' }, { texto: 'Zonas' }]} />
      <ComCentralJfl>{(c, s) => <Conteudo central={c} sessao={s} />}</ComCentralJfl>
    </div>
  )
}
