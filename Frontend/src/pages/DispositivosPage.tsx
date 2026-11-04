import { useState } from 'react'
import ComCentralJfl from '../components/ComCentralJfl'
import { BarraStatus } from '../components/Grades'
import { PageHeader, RecursosCard, Spinner } from '../components/ui'
import { useJflStatus } from '../hooks/usePolling'
import type { JflSessao } from '../types'
import { pad2, zonaEstado } from '../utils/format'

/** Capacidades de dispositivos segundo os datasheets oficiais JFL (não lidas da central). */
const CAPACIDADES: Record<string, { item: string; valor: string }[]> = {
  'Active 100 Bus': [
    { item: 'Sensores no barramento Bus', valor: 'até 99' },
    { item: 'Módulos PGM Bus', valor: 'até 4 (PGMs expansíveis até 16)' },
    { item: 'Sirenes Bus', valor: 'até 16' },
    { item: 'Teclados com fio (LCD ou touchscreen)', valor: 'até 16' },
    { item: 'Zonas sem fio', valor: '32 por teclado (necessário MRF-01 para sensores e controles sem fio)' },
    { item: 'Módulos de comunicação', valor: 'ME-04, ME-05 WB, MGP-04, MGP-04 3G, MGP-04 V2, MGP-04 3G V2' },
    { item: 'Buffer de eventos', valor: '1024 eventos' },
  ],
  'Active 20': [
    { item: 'Sensores sem fio (433,92 MHz)', valor: 'até 32 (necessário MRF-01 ou TEC-500)' },
    { item: 'Controles remotos', valor: 'até 99' },
    { item: 'Teclados com fio', valor: 'até 4 (1 zona com fio por teclado)' },
    { item: 'Saídas PGM', valor: '1 na placa, expansível até 4' },
    { item: 'Módulos de comunicação', valor: 'ME-04, ME-05 WB, MGP-04, MGP-04 3G, MGP-04 V2, MGP-04 3G V2, MGP-04 4G' },
    { item: 'Buffer de eventos', valor: '256 eventos' },
  ],
}

const ZONA_PROBLEMA = ['Tamper', 'LowBattery', 'NotCommunicating', 'ShortCircuit']

function Conteudo({ sessao }: { sessao: JflSessao }) {
  const [automatico, setAutomatico] = useState(true)
  const { status, erro, carregando, atualizar } = useJflStatus(sessao.serial, 10000, automatico)
  const m = sessao.modelo
  const capacidades = CAPACIDADES[m.nome]
  const problemasDispositivos = status?.problemas.filter(p => p.grupo === 'Dispositivos') ?? []
  const zonasComProblema = status?.zonas.filter(z => ZONA_PROBLEMA.includes(z.estado)) ?? []

  return (
    <>
      <BarraStatus status={status} carregando={carregando} erro={erro} onAtualizar={atualizar} automatico={automatico} onAutomatico={setAutomatico} />
      <div className="row g-3 mb-4">
        <div className="col-lg-6">
          <div className="card shadow-sm h-100">
            <div className="card-header fw-semibold"><i className="bi bi-exclamation-diamond me-2" />Indicadores de dispositivos no status da central</div>
            <div className="card-body">
              {!status ? <Spinner /> : (
                <>
                  <h6 className="small text-body-secondary">Problemas sinalizados</h6>
                  {problemasDispositivos.length === 0
                    ? <p className="text-success-emphasis small"><i className="bi bi-check-circle me-1" />Nenhum problema de teclado, sirene, barramento, sensor ou tamper sinalizado.</p>
                    : <ul className="small">{problemasDispositivos.map(p => <li key={p.codigo}>{p.descricao}</li>)}</ul>}
                  <h6 className="small text-body-secondary">Zonas com problema de dispositivo</h6>
                  {zonasComProblema.length === 0
                    ? <p className="small text-success-emphasis mb-0"><i className="bi bi-check-circle me-1" />Nenhuma zona com tamper, bateria fraca, curto ou sem comunicação.</p>
                    : (
                      <ul className="small mb-0">
                        {zonasComProblema.map(z => <li key={z.numero}>Zona {pad2(z.numero)}: {zonaEstado(z.estado).texto}</li>)}
                      </ul>
                    )}
                </>
              )}
            </div>
          </div>
        </div>
        <div className="col-lg-6">
          <div className="card shadow-sm h-100">
            <div className="card-header fw-semibold"><i className="bi bi-journal-text me-2" />Capacidades do modelo {m.nome} (datasheet oficial)</div>
            {capacidades ? (
              <ul className="list-group list-group-flush small">
                {capacidades.map(c => (
                  <li key={c.item} className="list-group-item d-flex justify-content-between gap-3"><span>{c.item}</span><span className="text-end fw-medium">{c.valor}</span></li>
                ))}
              </ul>
            ) : <div className="card-body small text-body-secondary">Sem dados oficiais levantados para este modelo.</div>}
            <div className="card-footer small text-body-secondary">Valores máximos do fabricante; o que está instalado de fato só pode ser lido com o inventário de dispositivos.</div>
          </div>
        </div>
      </div>
      <RecursosCard recursos={[
        { nome: 'Problemas de dispositivos (teclado, sirene, barramento, sensor, tamper)', suporte: 'consulta', detalhe: 'Bits de problema do quadro de status.' },
        { nome: 'Zonas com tamper, bateria fraca, curto ou sem comunicação', suporte: 'consulta', detalhe: 'Estado de cada zona no status.' },
        { nome: 'Inventário de sensores sem fio (modelo, firmware, sinal, bateria)', suporte: 'aguardando', detalhe: 'Comando 0x59 não implementado.' },
        { nome: m.nome === 'Active 100 Bus' ? 'Sensores Bus, sirenes Bus, módulos PGM Bus' : 'Dispositivos do barramento', suporte: m.nome === 'Active 100 Bus' ? 'aguardando' : 'nao-suportado', detalhe: m.nome === 'Active 100 Bus' ? 'Leitura do inventário de teclados/módulos (0x47) não implementada.' : 'A Active 20 não tem barramento Bus.' },
        { nome: 'Teclados e anunciadores', suporte: 'aguardando', detalhe: 'Leitura do inventário de teclados/módulos (0x47) não implementada.' },
        { nome: 'Controles remotos', suporte: 'aguardando', detalhe: 'Cadastro na programação (0x44/0x45).' },
        { nome: 'Cadastrar / remover dispositivos', suporte: 'aguardando', detalhe: 'Gravação de programação (0x45) não implementada.' },
      ]} />
    </>
  )
}

export default function DispositivosPage() {
  return (
    <div>
      <PageHeader titulo="Dispositivos" icone="bi-hdd-network" migalhas={[{ texto: 'Central JFL' }, { texto: 'Dispositivos' }]} />
      <ComCentralJfl>{(_, s) => <Conteudo sessao={s} />}</ComCentralJfl>
    </div>
  )
}
