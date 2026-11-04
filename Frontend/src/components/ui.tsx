import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'

/** Classificação obrigatória de cada recurso da interface. */
export type Suporte = 'funcional' | 'consulta' | 'aguardando' | 'nao-suportado'

const SUPORTE: Record<Suporte, { texto: string; cor: string; icone: string; dica: string }> = {
  funcional: { texto: 'Funcional', cor: 'success', icone: 'bi-check-circle-fill', dica: 'Lê e/ou envia comandos reais à central.' },
  consulta: { texto: 'Somente consulta', cor: 'info', icone: 'bi-eye-fill', dica: 'Dado real lido da central; não há comando de alteração.' },
  aguardando: { texto: 'Aguardando suporte do protocolo', cor: 'warning', icone: 'bi-hourglass-split', dica: 'Exige comandos ainda não implementados (ex.: leitura/gravação da programação 0x44/0x45).' },
  'nao-suportado': { texto: 'Não suportado', cor: 'secondary', icone: 'bi-x-circle-fill', dica: 'Não existe para este modelo ou foi deliberadamente bloqueado.' },
}

export function SuporteBadge({ tipo }: { tipo: Suporte }) {
  const s = SUPORTE[tipo]
  return (
    <span className={`badge text-bg-${s.cor} badge-suporte`} title={s.dica}>
      <i className={`bi ${s.icone} me-1`} />{s.texto}
    </span>
  )
}

export interface Recurso {
  nome: string
  suporte: Suporte
  detalhe?: string
}

/** Tabela "o que esta tela faz de verdade" exibida em todos os módulos. */
export function RecursosCard({ recursos, titulo = 'Recursos deste módulo' }: { recursos: Recurso[]; titulo?: string }) {
  return (
    <div className="card shadow-sm mb-4">
      <div className="card-header bg-body-tertiary fw-semibold"><i className="bi bi-list-check me-2" />{titulo}</div>
      <div className="table-responsive">
        <table className="table table-sm align-middle mb-0">
          <tbody>
            {recursos.map(r => (
              <tr key={r.nome}>
                <td className="w-25 fw-medium">{r.nome}</td>
                <td style={{ width: 260 }}><SuporteBadge tipo={r.suporte} /></td>
                <td className="small text-body-secondary">{r.detalhe}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}

export interface Migalha {
  texto: string
  para?: string
}

export function Breadcrumbs({ itens }: { itens: Migalha[] }) {
  return (
    <nav aria-label="breadcrumb">
      <ol className="breadcrumb mb-1 small">
        <li className="breadcrumb-item"><Link to="/"><i className="bi bi-house-door" /> Início</Link></li>
        {itens.map((m, i) => (
          <li key={m.texto} className={`breadcrumb-item ${i === itens.length - 1 ? 'active' : ''}`} aria-current={i === itens.length - 1 ? 'page' : undefined}>
            {m.para && i < itens.length - 1 ? <Link to={m.para}>{m.texto}</Link> : m.texto}
          </li>
        ))}
      </ol>
    </nav>
  )
}

export function PageHeader({ titulo, icone, migalhas, children, subtitulo }: {
  titulo: string
  icone: string
  migalhas: Migalha[]
  subtitulo?: ReactNode
  children?: ReactNode
}) {
  return (
    <div className="mb-4">
      <Breadcrumbs itens={migalhas} />
      <div className="d-flex flex-wrap align-items-center justify-content-between gap-2">
        <div>
          <h1 className="h3 mb-0"><i className={`bi ${icone} me-2 text-primary`} />{titulo}</h1>
          {subtitulo && <div className="text-body-secondary small mt-1">{subtitulo}</div>}
        </div>
        {children && <div className="d-flex flex-wrap gap-2">{children}</div>}
      </div>
    </div>
  )
}

export function Vazio({ icone = 'bi-inbox', children }: { icone?: string; children: ReactNode }) {
  return (
    <div className="text-center text-body-secondary py-5">
      <i className={`bi ${icone} display-6 d-block mb-2`} />
      {children}
    </div>
  )
}

export function Spinner({ texto = 'Carregando...' }: { texto?: string }) {
  return (
    <div className="d-flex align-items-center gap-2 text-body-secondary py-3">
      <div className="spinner-border spinner-border-sm" role="status" />
      <span>{texto}</span>
    </div>
  )
}

export function Info({ rotulo, children, mono }: { rotulo: string; children: ReactNode; mono?: boolean }) {
  return (
    <div className="mb-2">
      <div className="text-body-secondary small">{rotulo}</div>
      <div className={`fw-medium ${mono ? 'font-monospace' : ''}`}>{children}</div>
    </div>
  )
}
