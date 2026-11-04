import { useEffect, useState } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router-dom'
import { useJfl } from '../context/JflContext'

interface Item {
  para: string
  texto: string
  icone: string
}

const GRUPOS: { titulo: string; itens: Item[] }[] = [
  {
    titulo: 'Monitoramento',
    itens: [
      { para: '/', texto: 'Dashboard', icone: 'bi-speedometer2' },
      { para: '/eventos', texto: 'Eventos', icone: 'bi-bell' },
      { para: '/logs', texto: 'Logs de protocolo', icone: 'bi-terminal' },
    ],
  },
  {
    titulo: 'Central JFL',
    itens: [
      { para: '/zonas', texto: 'Zonas', icone: 'bi-grid-3x3-gap' },
      { para: '/particoes', texto: 'Partições', icone: 'bi-shield-lock' },
      { para: '/pgms', texto: 'PGMs', icone: 'bi-toggles' },
      { para: '/dispositivos', texto: 'Dispositivos', icone: 'bi-hdd-network' },
      { para: '/programacao', texto: 'Programação', icone: 'bi-sliders' },
    ],
  },
  {
    titulo: 'Cadastros',
    itens: [
      { para: '/predios', texto: 'Prédios', icone: 'bi-buildings' },
      { para: '/centrais', texto: 'Centrais', icone: 'bi-cpu' },
      { para: '/operacao', texto: 'Comandos e histórico', icone: 'bi-clock-history' },
    ],
  },
]

function SeletorCentral() {
  const { centraisJfl, selecionadaId, selecionar, sessoes } = useJfl()
  if (centraisJfl.length === 0) {
    return <span className="small text-body-secondary">Nenhuma central JFL cadastrada</span>
  }
  return (
    <div className="d-flex align-items-center gap-2">
      <label htmlFor="seletor-central" className="small text-body-secondary d-none d-md-inline">Central</label>
      <select
        id="seletor-central"
        className="form-select form-select-sm"
        style={{ minWidth: 240 }}
        value={selecionadaId ?? ''}
        onChange={e => selecionar(e.target.value ? Number(e.target.value) : null)}
      >
        {centraisJfl.map(c => {
          const online = sessoes.some(s => s.serial === c.usuario.trim())
          return <option key={c.id} value={c.id}>{online ? '● ' : '○ '}{c.nome} — {c.usuario}</option>
        })}
      </select>
    </div>
  )
}

function EstadoServidor() {
  const { servidor, erroApi, sessao } = useJfl()
  if (erroApi) return <span className="badge text-bg-danger"><i className="bi bi-x-octagon me-1" />API indisponível</span>
  if (!servidor) return null
  if (!servidor.habilitado || !servidor.portaEmUso) {
    return <span className="badge text-bg-secondary"><i className="bi bi-power me-1" />Servidor JFL desligado</span>
  }
  return (
    <span className="d-none d-sm-flex align-items-center gap-2">
      <span className={`badge ${sessao ? 'text-bg-success' : 'text-bg-warning'}`}>
        <i className={`bi ${sessao ? 'bi-wifi' : 'bi-wifi-off'} me-1`} />{sessao ? 'Online' : 'Offline'}
      </span>
      <span className="badge text-bg-light border" title="Porta em que o CentralHub recebe as centrais JFL">
        <i className="bi bi-hdd-rack me-1" />TCP {servidor.portaEmUso}
      </span>
      {servidor.somenteLeitura && <span className="badge text-bg-dark">Somente leitura</span>}
    </span>
  )
}

export default function Layout() {
  const [aberto, setAberto] = useState(false)
  const local = useLocation()
  useEffect(() => setAberto(false), [local.pathname])

  return (
    <div className="app-shell">
      <aside className={`app-sidebar ${aberto ? 'aberto' : ''}`} aria-label="Menu principal">
        <div className="d-flex align-items-center justify-content-between px-3 py-3 border-bottom border-secondary-subtle">
          <span className="fs-5 fw-semibold text-white"><i className="bi bi-shield-check me-2" />CentralHub</span>
          <button className="btn btn-sm btn-outline-light d-lg-none" onClick={() => setAberto(false)} aria-label="Fechar menu">
            <i className="bi bi-x-lg" />
          </button>
        </div>
        <nav className="py-2">
          {GRUPOS.map(g => (
            <div key={g.titulo} className="mb-2">
              <div className="app-sidebar-grupo">{g.titulo}</div>
              {g.itens.map(i => (
                <NavLink key={i.para} to={i.para} end={i.para === '/'} className={({ isActive }) => `app-sidebar-link ${isActive ? 'ativo' : ''}`}>
                  <i className={`bi ${i.icone}`} /><span>{i.texto}</span>
                </NavLink>
              ))}
            </div>
          ))}
        </nav>
        <div className="mt-auto px-3 py-3 small text-white-50 border-top border-secondary-subtle">
          JFL Active 100 Bus · Active 20
        </div>
      </aside>
      {aberto && <div className="app-backdrop d-lg-none" onClick={() => setAberto(false)} />}

      <div className="app-main">
        <header className="app-topbar">
          <button className="btn btn-outline-secondary btn-sm d-lg-none" onClick={() => setAberto(true)} aria-label="Abrir menu">
            <i className="bi bi-list" />
          </button>
          <SeletorCentral />
          <div className="ms-auto"><EstadoServidor /></div>
        </header>
        <main className="app-content">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
