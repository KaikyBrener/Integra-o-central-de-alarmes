import { useState } from 'react'
import { Link } from 'react-router-dom'
import { PageHeader, SuporteBadge } from '../components/ui'
import { useJfl } from '../context/JflContext'

type Modelo = 'Active 100 Bus' | 'Active 20'

interface Endereco {
  endereco: string
  descricao: string
  /** Particularidade de modelo informada no manual. */
  nota?: Partial<Record<Modelo, string>>
}

interface Secao {
  id: string
  titulo: string
  icone: string
  enderecos: Endereco[]
  observacao?: string
}

/**
 * Mapa da programação das centrais Active, transcrito do "Resumo da programação" (seção 30) e dos
 * capítulos correspondentes do manual oficial JFL (linhas Active, fw 7.3+). É REFERÊNCIA: a leitura e
 * a gravação da programação (comandos 0x44/0x45) ainda não estão implementadas no CentralHub.
 */
const SECOES: Secao[] = [
  {
    id: 'gerais', titulo: 'Configurações gerais e tempos', icone: 'bi-gear',
    enderecos: [
      { endereco: '400', descricao: 'Trava de reset (159 trava; outro valor não trava)' },
      { endereco: '401 / 411', descricao: 'Tempo de entrada 1 e 2 (segundos)' },
      { endereco: '402 / 412', descricao: 'Tempo de saída 1 e 2 (segundos)' },
      { endereco: '403, 413, 405, 415', descricao: 'Tempo de disparo das partições', nota: { 'Active 100 Bus': 'usa 403xx (xx = partição)' } },
      { endereco: '404, 414, 406, 416', descricao: 'Tempo de arme por não movimento das partições', nota: { 'Active 100 Bus': 'usa 404xx' } },
      { endereco: '425', descricao: 'Tempo de zona inteligente (segundos)' },
      { endereco: '426', descricao: 'Tempo de falta de AC (minutos)' },
      { endereco: '427', descricao: 'Intervalo de autoteste (001-200 h ou 201-255 = 1-55 min)' },
      { endereco: '428', descricao: 'Tempo de falta de linha telefônica (minutos)' },
      { endereco: '429 a 431', descricao: 'Tempo para expirar as senhas temporárias (dias)' },
      { endereco: '434', descricao: 'Intervalo do autoteste do meio secundário (horas)' },
      { endereco: '435', descricao: 'Tempo de porta aberta (minutos; 000 desabilita)' },
      { endereco: '436', descricao: 'Tempo de reporte de inatividade de zona (horas; 000 desabilita)' },
    ],
  },
  {
    id: 'usuarios', titulo: 'Usuários e senhas', icone: 'bi-people',
    enderecos: [
      { endereco: '200', descricao: 'Senha do usuário mestre (4 ou 6 dígitos)' },
      { endereco: '201 a 298', descricao: 'Senhas dos usuários 01 a 98' },
      { endereco: '299', descricao: 'Senha do instalador (usuário 99)' },
      { endereco: '300', descricao: 'Permissões: programação, programação de usuário, armar, desarmar, operar PGM, inibir zonas' },
      { endereco: '301 a 398', descricao: 'Atributos dos usuários (partições, desarmar, arme AWAY, inibir, acesso remoto, ronda, PGMs, eletrificador)' },
      { endereco: '900 a 998', descricao: 'Nomes dos usuários' },
      { endereco: '801, tecla 1', descricao: 'Senhas com 4 ou 6 dígitos' },
    ],
    observacao: 'A central bloqueia senhas fracas (1234, 5678, 1111...). Eventos gerados por software aparecem como usuário 99.',
  },
  {
    id: 'controles', titulo: 'Controles remotos', icone: 'bi-broadcast',
    enderecos: [],
    observacao: 'Cadastro de controles remotos (Active 20: até 99; Active 100 Bus: requer MRF-01). Endereços não levantados nesta versão.',
  },
  {
    id: 'comunicacao', titulo: 'Comunicação (Ethernet, Wi-Fi, celular)', icone: 'bi-ethernet',
    enderecos: [
      { endereco: '700', descricao: 'Opções de reporte: linha telefônica, 4G, Ethernet (tecla 3), discadora, SMS, inverter prioridade, modo duplo (tecla 8), DHCP (tecla 9)' },
      { endereco: '701', descricao: 'Acesso remoto: SMS, telefone, celular, aplicativo, nuvem Ethernet, notificações, programador, nuvem 4G' },
      { endereco: '702 / 703', descricao: 'Endereço IP de destino 1 e 2 (até 36 caracteres) — servidor do CentralHub' },
      { endereco: '706 / 707', descricao: 'Porta de comunicação do IP1 e IP2 (4 dígitos)' },
      { endereco: '711 / 712', descricao: 'Telefones de reporte para a estação de monitoramento' },
      { endereco: '721 a 724 / 731 a 734', descricao: 'APN, login e senha da operadora' },
      { endereco: '740 a 743', descricao: 'Rede local: IP, máscara, gateway e DNS' },
      { endereco: '746 / 747', descricao: 'Wi-Fi: nome da rede (SSID) e senha' },
    ],
  },
  {
    id: 'reporte', titulo: 'Reporte e notificações', icone: 'bi-send',
    enderecos: [
      { endereco: '803, tecla 1', descricao: 'Reporta desarme somente após disparo' },
      { endereco: '803, tecla 2', descricao: 'Reporta acionar/desacionar PGM (a PGM vai no campo partição do evento)' },
      { endereco: '803, tecla 3', descricao: 'Restaura zonas somente após o tempo de sirene' },
      { endereco: '803, tecla 4', descricao: 'Autoteste somente com a central armada' },
      { endereco: '803, tecla 5', descricao: 'Reporta entrar, alterar e sair da programação' },
      { endereco: '803, tecla 6', descricao: 'Reporta ronda OK' },
      { endereco: '803, tecla 7', descricao: 'Gera evento de problema de comunicação IP' },
      { endereco: '710', descricao: 'Habilita notificações (armes, desarmes...)' },
    ],
    observacao: 'O protocolo padrão de envio de eventos é Contact ID (tabela de eventos na seção 29 do manual, usada pelo CentralHub para interpretar os eventos).',
  },
  {
    id: 'zonas', titulo: 'Zonas', icone: 'bi-grid-3x3-gap',
    enderecos: [
      { endereco: '001 a 099', descricao: 'Tipo da zona: desabilitada, imediata, temporizada 1/2, seguidora, 24 h, ronda, 24 h pânico, 24 h tamper' },
      { endereco: '100xx', descricao: 'Partição da zona' },
      { endereco: '101 a 199', descricao: 'Atributos: partições, STAY, inteligente, silenciosa, auto-anulável, permite inibir, sirene intermitente, inatividade, sensibilidade sem fio, chime, aviso de porta aberta' },
      { endereco: '601 a 699', descricao: 'Nome da zona' },
    ],
  },
  {
    id: 'pgms', titulo: 'PGMs', icone: 'bi-toggles',
    enderecos: [
      { endereco: '421 a 424', descricao: 'Tempo de acionamento das PGMs (001-200 min ou 201-255 = 1-55 s)', nota: { 'Active 100 Bus': 'usa 421xx' } },
      { endereco: '511 a 514', descricao: 'Horário para acionar as PGMs', nota: { 'Active 100 Bus': 'usa 511xx' } },
      { endereco: '515 a 518', descricao: 'Horário para desacionar as PGMs', nota: { 'Active 100 Bus': 'usa 515xx' } },
    ],
    observacao: 'Função 12/13 = acionada pelo usuário (com/sem retenção) — necessária para acionamento remoto. Função 18 (e 25, só Active 20) = eletrificador.',
  },
  {
    id: 'autoarme', titulo: 'Autoarme', icone: 'bi-alarm',
    enderecos: [
      { endereco: '521 a 524', descricao: 'Horário do autoarme das partições (00:00 desabilita)' },
      { endereco: '508 / 509', descricao: 'Início e término do arme por não movimento' },
    ],
  },
  {
    id: 'tarefas', titulo: 'Tarefas agendadas', icone: 'bi-calendar-check',
    enderecos: [
      { endereco: '561 a 576', descricao: 'Tarefas: arme/desarme de partição, arme/desarme total, eletrificador, acionar/desacionar PGM, arme por não movimento, reiniciar a central; frequência por dia da semana/feriado e horário' },
    ],
  },
  {
    id: 'feriados', titulo: 'Feriados', icone: 'bi-calendar-event',
    enderecos: [{ endereco: '541 a 556', descricao: 'Cadastro de feriados (DD/MM)' }],
  },
  {
    id: 'ronda', titulo: 'Ronda', icone: 'bi-person-walking',
    enderecos: [
      { endereco: '432', descricao: 'Intervalo de ronda (minutos)' },
      { endereco: '433', descricao: 'Tempo de duração da ronda (minutos; 000 desabilita)' },
      { endereco: '506 / 507', descricao: 'Horário de início e término da ronda' },
    ],
  },
  {
    id: 'datahora', titulo: 'Data, hora e autoteste', icone: 'bi-clock',
    enderecos: [
      { endereco: '500', descricao: 'Data e hora automáticas (fuso horário)' },
      { endereco: '501', descricao: 'Hora do alarme (HH:MM:SS)' },
      { endereco: '502', descricao: 'Data do alarme (DD/MM/AA)' },
      { endereco: '503', descricao: 'Hora do primeiro autoteste (HH:MM)' },
    ],
  },
  {
    id: 'sinalizacao', titulo: 'Sinalização (bipes e sirene)', icone: 'bi-volume-up',
    enderecos: [
      { endereco: '801, tecla 6', descricao: 'Bip na sirene ao armar e desarmar pelo teclado' },
      { endereco: '801, tecla 7', descricao: 'Bip na sirene ao armar e desarmar pelo controle remoto' },
      { endereco: '801, tecla 8', descricao: 'Bip de confirmação de eventos no teclado' },
      { endereco: '801, tecla 1 (pág. B)', descricao: 'Inverter bip da sirene' },
    ],
  },
  {
    id: 'dispositivos', titulo: 'Dispositivos', icone: 'bi-hdd-network',
    enderecos: [],
    observacao: 'Cadastro de sensores sem fio, teclados e módulos. Veja as capacidades de cada modelo em Dispositivos.',
  },
]

export default function ProgramacaoPage() {
  const { sessao, servidor } = useJfl()
  const padrao: Modelo = sessao?.modelo.nome === 'Active 20' ? 'Active 20' : 'Active 100 Bus'
  const [modelo, setModelo] = useState<Modelo>(padrao)
  const [filtro, setFiltro] = useState('')

  const secoes = SECOES.map(s => ({
    ...s,
    enderecos: s.enderecos.filter(e => !filtro || `${e.endereco} ${e.descricao}`.toLowerCase().includes(filtro.toLowerCase())),
  })).filter(s => !filtro || s.enderecos.length > 0 || s.titulo.toLowerCase().includes(filtro.toLowerCase()))

  return (
    <div>
      <PageHeader titulo="Programação" icone="bi-sliders" migalhas={[{ texto: 'Central JFL' }, { texto: 'Programação' }]}
        subtitulo="Mapa de endereços das centrais Active, segundo o manual oficial JFL (fw 7.3+, seção 30 — Resumo da programação)." />

      <div className="alert alert-warning">
        <h5 className="alert-heading"><i className="bi bi-hourglass-split me-2" />Leitura e gravação da programação ainda não suportadas</h5>
        <p className="mb-1">
          O CentralHub ainda não implementa os comandos de leitura (<span className="font-monospace">0x44</span>) e gravação
          (<span className="font-monospace">0x45</span>) da programação. Por isso esta página é <strong>somente referência</strong>:
          não há campos editáveis nem botões de envio. Para programar a central, use o Programador JFL/Active NET ou o teclado.
        </p>
        <p className="mb-0 small">
          Risco conhecido: gravar uma janela de programação sobrescreve bytes que não foram exibidos (o próprio Active NET já apagou
          senhas assim) — a gravação só deve ser habilitada depois de leitura completa e backup.
        </p>
      </div>

      <div className="card shadow-sm mb-4">
        <div className="card-body row g-2 align-items-end">
          <div className="col-md-4">
            <label className="form-label small mb-1">Particularidades do modelo</label>
            <select className="form-select" value={modelo} onChange={e => setModelo(e.target.value as Modelo)}>
              <option value="Active 100 Bus">Active 100 Bus (PCI-350) — 99 zonas, 16 partições, 16 PGMs</option>
              <option value="Active 20">Active 20 (PCI-325) — 32 zonas, 2 partições, 4 PGMs</option>
            </select>
          </div>
          <div className="col-md-4">
            <label className="form-label small mb-1">Buscar endereço ou função</label>
            <input className="form-control" value={filtro} onChange={e => setFiltro(e.target.value)} placeholder="ex.: 702, ronda, PGM" />
          </div>
          <div className="col-md-4 small text-body-secondary">
            {sessao ? <>Central selecionada: <strong>{sessao.modelo.nome}</strong> (fw {sessao.firmware})</> : 'Nenhuma central selecionada conectada.'}
          </div>
        </div>
      </div>

      <div className="row g-3">
        <div className="col-lg-3">
          <div className="list-group sticky-top" style={{ top: 80 }}>
            {secoes.map(s => <a key={s.id} href={`#sec-${s.id}`} className="list-group-item list-group-item-action small"><i className={`bi ${s.icone} me-2`} />{s.titulo}</a>)}
          </div>
        </div>
        <div className="col-lg-9">
          {secoes.map(s => (
            <div key={s.id} id={`sec-${s.id}`} className="card shadow-sm mb-3">
              <div className="card-header d-flex flex-wrap justify-content-between align-items-center gap-2">
                <span className="fw-semibold"><i className={`bi ${s.icone} me-2`} />{s.titulo}</span>
                <SuporteBadge tipo="aguardando" />
              </div>
              {s.id === 'comunicacao' && (
                <div className="card-body border-bottom bg-body-tertiary small">
                  <div className="d-flex justify-content-between flex-wrap gap-2">
                    <span><strong>Servidor do CentralHub</strong> (destino a programar em 702/706 na central):</span>
                    <SuporteBadge tipo="consulta" />
                  </div>
                  {servidor?.portaEmUso
                    ? <>Porta TCP em uso: <strong>{servidor.portaEmUso}</strong> (interface {servidor.bindAddress}). Programe o IP desta máquina em 702/703 e a porta em 706/707; habilite o reporte Ethernet (700, tecla 3).</>
                    : <>Servidor JFL desligado ou API indisponível. Veja a seção <span className="font-monospace">Jfl</span> do appsettings.json.</>}
                </div>
              )}
              {s.enderecos.length > 0 && (
                <div className="table-responsive">
                  <table className="table table-sm mb-0">
                    <thead><tr><th style={{ width: 170 }}>Endereço</th><th>Descrição</th><th style={{ width: 200 }}>{modelo}</th></tr></thead>
                    <tbody>
                      {s.enderecos.map(e => (
                        <tr key={e.endereco + e.descricao}>
                          <td className="font-monospace">{e.endereco}</td>
                          <td className="small">{e.descricao}</td>
                          <td className="small text-body-secondary">{e.nota?.[modelo] ?? '—'}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
              {s.observacao && <div className="card-footer small text-body-secondary">{s.observacao}{s.id === 'dispositivos' && <> <Link to="/dispositivos">Abrir Dispositivos</Link>.</>}</div>}
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}
