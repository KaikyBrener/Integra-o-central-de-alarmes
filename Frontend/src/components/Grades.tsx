import type { ReactNode } from 'react'
import type { JflModelo, JflStatus } from '../types'
import { pad2, particaoEstado, zonaEstado } from '../utils/format'

/** Grade 1..N zonas do modelo. Zona que a central não reporta = desabilitada/não usada. */
export function GradeZonas({ modelo, status, acao }: {
  modelo: JflModelo
  status: JflStatus
  acao?: (numero: number) => ReactNode
}) {
  const porNumero = new Map(status.zonas.map(z => [z.numero, z]))
  return (
    <div className="grade">
      {Array.from({ length: modelo.zonas }, (_, i) => i + 1).map(n => {
        const z = porNumero.get(n)
        if (!z) {
          return (
            <div key={n} className="grade-item inativa" title="Desabilitada na programação (a central não reporta esta zona)">
              <div className="numero">Z{pad2(n)}</div>
              <div className="text-body-secondary">Não usada</div>
            </div>
          )
        }
        const e = zonaEstado(z.estado)
        return (
          <div key={n} className={`grade-item border-${e.cor}`} title={z.permiteInibir ? 'Permite inibir' : 'Não permite inibir'}>
            <div className="d-flex justify-content-between">
              <span className="numero">Z{pad2(n)}</span>
              <i className={`bi ${e.icone} text-${e.cor === 'light' ? 'secondary' : e.cor}`} />
            </div>
            <div className={`text-${e.cor === 'light' ? 'secondary' : e.cor}-emphasis`}>{e.texto}</div>
            {acao?.(n)}
          </div>
        )
      })}
    </div>
  )
}

export function GradeParticoes({ modelo, status, acao }: {
  modelo: JflModelo
  status: JflStatus
  acao?: (numero: number) => ReactNode
}) {
  return (
    <div className="grade" style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(150px, 1fr))' }}>
      {status.particoes.slice(0, modelo.particoes).map(p => {
        const e = particaoEstado(p.estado)
        return (
          <div key={p.numero} className={`grade-item ${p.programada ? `border-${e.cor}` : 'inativa'}`}>
            <div className="d-flex justify-content-between">
              <span className="numero">Partição {pad2(p.numero)}</span>
              {p.emAlarme && <span className="badge text-bg-danger">ALARME</span>}
            </div>
            <div className="mt-1"><i className={`bi ${e.icone} me-1`} />{e.texto}</div>
            {p.programada && acao?.(p.numero)}
          </div>
        )
      })}
    </div>
  )
}

export function GradePgms({ status, acao }: { status: JflStatus; acao?: (numero: number) => ReactNode }) {
  return (
    <div className="grade" style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(150px, 1fr))' }}>
      {status.pgms.map(p => (
        <div key={p.numero} className={`grade-item ${p.ligada ? 'border-success' : ''}`}>
          <div className="d-flex justify-content-between align-items-center">
            <span className="numero">PGM {pad2(p.numero)}</span>
            <span className={`badge ${p.ligada ? 'text-bg-success' : 'text-bg-secondary'}`}>{p.ligada ? 'Ligada' : 'Desligada'}</span>
          </div>
          <div className="small mt-1">
            {p.permitida === true && <span className="text-success-emphasis"><i className="bi bi-unlock me-1" />Acionamento remoto permitido</span>}
            {p.permitida === false && <span className="text-body-secondary"><i className="bi bi-lock me-1" />Sem função "acionada pelo usuário"</span>}
            {p.permitida == null && <span className="text-body-secondary">Permissão não informada</span>}
          </div>
          {acao?.(p.numero)}
        </div>
      ))}
    </div>
  )
}

/** Barra de atualização do status (último quadro, aviso de cache, botão e alternância automática). */
export function BarraStatus({ status, carregando, erro, onAtualizar, automatico, onAutomatico }: {
  status: JflStatus | null
  carregando: boolean
  erro: string | null
  onAtualizar: () => void
  automatico: boolean
  onAutomatico: (v: boolean) => void
}) {
  return (
    <div className="d-flex flex-wrap align-items-center gap-2 mb-3">
      <button className="btn btn-sm btn-outline-primary" onClick={onAtualizar} disabled={carregando}>
        {carregando ? <span className="spinner-border spinner-border-sm me-1" /> : <i className="bi bi-arrow-clockwise me-1" />}
        Ler status da central
      </button>
      <div className="form-check form-switch mb-0">
        <input className="form-check-input" type="checkbox" id="auto-status" checked={automatico} onChange={e => onAutomatico(e.target.checked)} />
        <label className="form-check-label small" htmlFor="auto-status">Atualizar a cada 10 s</label>
      </div>
      {status?.lidoEm && <span className="small text-body-secondary">Último status: {new Date(status.lidoEm).toLocaleTimeString('pt-BR')} ({status.tamanhoQuadro} bytes)</span>}
      {status && !status.atualizado && <span className="badge text-bg-warning">Status em cache</span>}
      {status?.aviso && <span className="small text-warning-emphasis">{status.aviso}</span>}
      {erro && <span className="small text-danger"><i className="bi bi-exclamation-triangle me-1" />{erro}</span>}
    </div>
  )
}
