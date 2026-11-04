import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { useJfl } from '../context/JflContext'
import type { Central, JflSessao } from '../types'
import { dataHora } from '../utils/format'
import { Vazio } from './ui'

/**
 * Só renderiza o conteúdo quando há uma central JFL selecionada E conectada ao servidor do CentralHub.
 * Caso contrário explica o motivo — nunca mostra dados ou controles sem uma central real por trás.
 */
export default function ComCentralJfl({ children }: { children: (central: Central, sessao: JflSessao) => ReactNode }) {
  const { centraisJfl, central, sessao, servidor, erroApi } = useJfl()

  if (erroApi) {
    return <div className="alert alert-danger"><i className="bi bi-plug me-2" />{erroApi}</div>
  }

  if (centraisJfl.length === 0) {
    return (
      <Vazio icone="bi-cpu">
        Nenhuma central JFL cadastrada.<br />
        Conecte a central ao servidor do CentralHub e cadastre-a em <Link to="/centrais">Centrais</Link>
        {' '}(o número de série vai no campo Usuário).
      </Vazio>
    )
  }

  if (!central) {
    return <Vazio icone="bi-hand-index">Selecione uma central JFL no topo da página.</Vazio>
  }

  if (!sessao) {
    return (
      <div className="alert alert-warning">
        <h5 className="alert-heading"><i className="bi bi-wifi-off me-2" />Central offline</h5>
        <p className="mb-1">
          A central <strong>{central.nome}</strong> (série <span className="font-monospace">{central.usuario}</span>) não está
          conectada ao servidor do CentralHub{servidor?.portaEmUso ? <> (porta <strong>{servidor.portaEmUso}</strong>)</> : null}.
        </p>
        <p className="mb-1 small">
          A central JFL é quem conecta: confira o IP de destino (endereços 702/703), a porta (706/707), o reporte por
          Ethernet habilitado (endereço 700, tecla 3) e o firewall. Último status gravado: <strong>{central.status ?? '—'}</strong>.
        </p>
        <p className="mb-0 small">Acompanhe as tentativas de conexão em <Link to="/logs">Logs</Link>. Leituras e comandos ficam indisponíveis enquanto ela estiver offline.</p>
      </div>
    )
  }

  return (
    <>
      {!sessao.modelo.validadoEmHardware && sessao.modelo.comandosHabilitados && (
        <div className="alert alert-info py-2 small mb-3">
          <i className="bi bi-info-circle me-2" />
          <strong>{sessao.modelo.nome}</strong> ainda não foi validada em hardware. Leituras e comandos seguem o protocolo
          documentado; confira o resultado no local e registre os quadros na tela de <Link to="/logs">Logs</Link>.
          Conectada desde {dataHora(sessao.conectadaEm)}.
        </div>
      )}
      {!sessao.modelo.comandosHabilitados && (
        <div className="alert alert-secondary py-2 small mb-3">
          <i className="bi bi-lock me-2" />
          Modelo <strong>{sessao.modelo.nome}</strong> ({sessao.modelo.byteModelo}) reconhecido, mas sem comandos habilitados
          no CentralHub. Apenas eventos e leituras.
        </div>
      )}
      {children(central, sessao)}
    </>
  )
}
