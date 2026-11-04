import { useState } from 'react'
import Swal from 'sweetalert2'
import ComCentralJfl from '../components/ComCentralJfl'
import { BarraStatus } from '../components/Grades'
import { PageHeader, RecursosCard, Spinner } from '../components/ui'
import { useJfl } from '../context/JflContext'
import { useJflStatus } from '../hooks/usePolling'
import { centralHub } from '../services/api'
import type { Central, JflSessao } from '../types'
import { confirmar, esc, executarComando } from '../utils/alerts'
import { pad2 } from '../utils/format'

const PULSO_MAX_MS = 600000

function Conteudo({ central, sessao }: { central: Central; sessao: JflSessao }) {
  const { servidor } = useJfl()
  const [automatico, setAutomatico] = useState(true)
  const { status, erro, carregando, atualizar } = useJflStatus(sessao.serial, 10000, automatico)
  const m = sessao.modelo
  const podeComandar = m.comandosHabilitados && !servidor?.somenteLeitura

  const alerta = '<small>Se esta PGM aciona o eletrificador (funções 18/25) ou um portão, confirme que é seguro.</small>'

  const definir = async (pgm: number, ligar: boolean) => {
    if (!(await confirmar(`${ligar ? 'Ligar' : 'Desligar'} a PGM ${pad2(pgm)}?`, `${esc(central.nome)}.<br/>${alerta}`, ligar ? 'Ligar' : 'Desligar', ligar))) return
    if (await executarComando(ligar ? 'Ligando PGM...' : 'Desligando PGM...', () => centralHub.enviarPgm(central.id, pgm, ligar ? 'Ligar' : 'Desligar'))) atualizar()
  }

  const pulso = async (pgm: number) => {
    const r = await Swal.fire({
      title: `Pulso na PGM ${pad2(pgm)}`,
      html: `Liga, aguarda e desliga por software (o protocolo não tem comando de pulso).<br/>${alerta}`,
      input: 'number',
      inputLabel: 'Duração (ms)',
      inputValue: '1000',
      inputAttributes: { min: '1', max: String(PULSO_MAX_MS), step: '100' },
      showCancelButton: true,
      confirmButtonText: 'Enviar pulso',
      cancelButtonText: 'Cancelar',
      customClass: { confirmButton: 'btn btn-warning mx-1', cancelButton: 'btn btn-outline-secondary mx-1' },
      buttonsStyling: false,
      inputValidator: v => {
        const n = Number(v)
        return !Number.isInteger(n) || n < 1 || n > PULSO_MAX_MS ? `Informe de 1 a ${PULSO_MAX_MS} ms.` : undefined
      },
    })
    if (!r.isConfirmed) return
    if (await executarComando('Enviando pulso...', () => centralHub.enviarPgm(central.id, pgm, 'Pulso', Number(r.value)))) atualizar()
  }

  return (
    <>
      <BarraStatus status={status} carregando={carregando} erro={erro} onAtualizar={atualizar} automatico={automatico} onAutomatico={setAutomatico} />
      {!status ? <Spinner texto="Lendo status da central..." /> : (
        <div className="card shadow-sm mb-4">
          <div className="card-header fw-semibold">PGMs 1 a {m.pgms} — {m.nome}</div>
          <div className="table-responsive">
            <table className="table table-hover align-middle mb-0">
              <thead><tr><th>PGM</th><th>Estado</th><th>Acionamento remoto</th><th className="text-end">Comandos</th></tr></thead>
              <tbody>
                {status.pgms.map(p => {
                  const bloqueada = p.permitida === false
                  return (
                    <tr key={p.numero}>
                      <td className="fw-semibold">PGM {pad2(p.numero)}</td>
                      <td><span className={`badge ${p.ligada ? 'text-bg-success' : 'text-bg-secondary'}`}><i className={`bi ${p.ligada ? 'bi-toggle-on' : 'bi-toggle-off'} me-1`} />{p.ligada ? 'Ligada' : 'Desligada'}</span></td>
                      <td className="small">
                        {p.permitida === true && <span className="text-success-emphasis">Permitido</span>}
                        {p.permitida === false && <span className="text-body-secondary">Bloqueado: PGM não programada ou sem função "acionada pelo usuário"</span>}
                        {p.permitida == null && <span className="text-body-secondary">Não informado pela central</span>}
                      </td>
                      <td className="text-end">
                        {podeComandar && !bloqueada && (
                          <div className="btn-group btn-group-sm">
                            <button className="btn btn-outline-success" onClick={() => definir(p.numero, true)}>Ligar</button>
                            <button className="btn btn-outline-secondary" onClick={() => definir(p.numero, false)}>Desligar</button>
                            <button className="btn btn-outline-warning" onClick={() => pulso(p.numero)}>Pulso</button>
                          </div>
                        )}
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
          <div className="card-footer small text-body-secondary">
            {m.pgms > 4
              ? 'Active 100 Bus: 1 PGM na placa; as PGMs 2 a 16 dependem de módulos PGM Bus instalados.'
              : 'Active 20: 1 PGM na placa, expansível até 4.'}
            {' '}O CentralHub consulta o bit de permissão da central antes de enviar e só confirma o comando quando o status devolvido mostra a mudança.
          </div>
        </div>
      )}
      <RecursosCard recursos={[
        { nome: 'Estado da PGM', suporte: 'funcional', detalhe: 'Lido do status (PGMs 1-8 e 9-16).' },
        { nome: 'Ligar / desligar', suporte: podeComandar ? 'funcional' : 'nao-suportado', detalhe: podeComandar ? 'Comandos 0x50 / 0x51, confirmados pelo status.' : 'Comandos desabilitados para este modelo ou CentralHub em somente leitura.' },
        { nome: 'Pulso', suporte: podeComandar ? 'funcional' : 'nao-suportado', detalhe: 'Por software (ligar → esperar → desligar), até 10 min.' },
        { nome: 'Permissão de acionamento remoto', suporte: 'consulta', detalhe: 'Informada pela central (P-PGM).' },
        { nome: 'Função da PGM', suporte: 'aguardando', detalhe: 'Funções programáveis (com/sem retenção pelo usuário, junto com arme, problema, horário, eletrificador, disparo de zona...) ficam na programação (0x44).' },
        { nome: 'Tempo de acionamento', suporte: 'aguardando', detalhe: m.pgms > 4 ? 'Endereço 421xx (Active 100 Bus).' : 'Endereços 421 a 424.' },
        { nome: 'Agendamento (acionar/desacionar por horário)', suporte: 'aguardando', detalhe: m.pgms > 4 ? 'Endereços 511xx / 515xx (Active 100 Bus).' : 'Endereços 511 a 514 / 515 a 518.' },
        { nome: 'Nome da PGM', suporte: 'aguardando', detalhe: 'Leitura da programação (0x44).' },
      ]} />
    </>
  )
}

export default function PgmsPage() {
  return (
    <div>
      <PageHeader titulo="PGMs" icone="bi-toggles" migalhas={[{ texto: 'Central JFL' }, { texto: 'PGMs' }]} />
      <ComCentralJfl>{(c, s) => <Conteudo central={c} sessao={s} />}</ComCentralJfl>
    </div>
  )
}
