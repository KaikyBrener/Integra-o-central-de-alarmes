import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import MaskedInput from '../components/MaskedInput'
import { Info, PageHeader, Vazio } from '../components/ui'
import { useJfl } from '../context/JflContext'
import api, { centralHub, mensagemDeErro } from '../services/api'
import type { Building, Central, ConexaoResult } from '../types'
import { carregando, confirmar, erro, esc, fechar, toast } from '../utils/alerts'
import { ipValido, maskIp, maskPorta, maskSerial, SERIAL_TAMANHO } from '../utils/masks'

type Tipo = 'jfl' | 'tcp'

export default function CentralsPage() {
  const { centrais, sessoes, servidor, recarregar } = useJfl()
  const [params, setParams] = useSearchParams()
  const [buildings, setBuildings] = useState<Building[]>([])

  const [tipo, setTipo] = useState<Tipo>('jfl')
  const [nome, setNome] = useState('')
  const [ip, setIp] = useState('')
  const [porta, setPorta] = useState('')
  const [usuario, setUsuario] = useState('')
  const [senha, setSenha] = useState('')
  const [buildingId, setBuildingId] = useState<number | ''>('')
  const [editId, setEditId] = useState<number | null>(null)
  const [conexaoInfo, setConexaoInfo] = useState<ConexaoResult | null>(null)
  const [tentou, setTentou] = useState(false)

  useEffect(() => {
    centralHub.buildings().then(setBuildings).catch(e => erro('Não foi possível carregar os prédios', e))
  }, [])

  // Vindo do Dashboard ("Cadastrar" uma central JFL conectada): pré-preenche com os dados da sessão.
  useEffect(() => {
    const serial = params.get('serial')
    if (!serial) return
    limparForm()
    setTipo('jfl')
    setUsuario(maskSerial(serial))
    setIp(maskIp(params.get('ip') ?? ''))
    setParams({}, { replace: true })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [params])

  const sessaoDoSerial = tipo === 'jfl' ? sessoes.find(s => s.serial === usuario) : undefined

  // Modo JFL: IP/porta/senha não são usados pelo protocolo (a central conecta no servidor), mas a API os exige.
  useEffect(() => {
    if (tipo !== 'jfl') return
    if (sessaoDoSerial && !ip) setIp(maskIp(sessaoDoSerial.origem.split(':')[0]))
    if (!porta && servidor?.portaEmUso) setPorta(String(servidor.portaEmUso))
    if (!senha) setSenha('-')
  }, [tipo, sessaoDoSerial, servidor, ip, porta, senha])

  const limparForm = () => {
    setNome(''); setIp(''); setPorta(''); setUsuario(''); setSenha(''); setBuildingId('')
    setEditId(null); setConexaoInfo(null); setTentou(false)
  }

  const erros = {
    nome: !nome.trim(),
    ip: !ipValido(ip),
    porta: !porta || Number(porta) < 1,
    usuario: tipo === 'jfl' ? usuario.length !== SERIAL_TAMANHO : !usuario.trim(),
    senha: !senha,
    predio: !buildingId,
  }

  const testarConexao = async () => {
    setTentou(true)
    if (erros.ip || erros.porta || erros.usuario || erros.senha) {
      toast.fire({ icon: 'warning', title: 'Corrija os campos destacados' })
      return
    }
    setConexaoInfo(null)
    carregando('Testando conexão...', tipo === 'jfl' ? 'Procurando a sessão da central no servidor do CentralHub' : `Conectando em ${ip}:${porta}`)
    try {
      const r = await centralHub.testarConexao({ ip, porta: Number(porta), usuario, senha })
      fechar()
      setConexaoInfo(r)
      if (r.sucesso) toast.fire({ icon: 'success', title: `${r.fabricante} identificada` })
      else erro('Central não identificada', r.erro || 'Não foi possível conectar à central.')
    } catch (e) {
      fechar()
      erro('Falha ao testar a conexão', e)
    }
  }

  const salvar = async () => {
    setTentou(true)
    if (Object.values(erros).some(Boolean)) {
      toast.fire({ icon: 'warning', title: 'Corrija os campos destacados' })
      return
    }
    if (!conexaoInfo?.sucesso) {
      erro('Teste a conexão primeiro', 'O fabricante só é gravado depois de identificado por um teste de conexão real.')
      return
    }

    const payload = {
      id: editId || 0, nome, ip, porta: Number(porta), usuario, senha, buildingId,
      fabricante: conexaoInfo.fabricante, modelo: conexaoInfo.modelo, firmware: conexaoInfo.firmware,
      status: conexaoInfo.status, latencia: conexaoInfo.latenciaMs,
    }
    try {
      if (editId) await api.put(`/central/${editId}`, payload)
      else await api.post('/central', payload)
      toast.fire({ icon: 'success', title: editId ? 'Central atualizada' : 'Central cadastrada' })
      limparForm()
      recarregar()
    } catch (e) {
      erro('Não foi possível salvar a central', e)
    }
  }

  const editar = (c: Central) => {
    setEditId(c.id)
    setTipo(c.fabricante?.toLowerCase() === 'jfl' ? 'jfl' : 'tcp')
    setNome(c.nome)
    setIp(c.ip)
    setPorta(String(c.porta))
    setUsuario(c.usuario)
    // A senha nunca é retornada pela API; em centrais TCP ela precisa ser informada de novo.
    setSenha(c.fabricante?.toLowerCase() === 'jfl' ? '-' : '')
    setBuildingId(c.buildingId)
    setTentou(false)
    setConexaoInfo(c.fabricante ? {
      sucesso: true, fabricante: c.fabricante, modelo: c.modelo || '', firmware: c.firmware || '',
      status: c.status || '', latenciaMs: c.latencia || 0,
    } : null)
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  const excluir = async (c: Central) => {
    if (!(await confirmar('Excluir central?', `<strong>${esc(c.nome)}</strong> e seu histórico de comandos serão removidos. Os eventos recebidos são mantidos.`, 'Excluir', true))) return
    try {
      await api.delete(`/central/${c.id}`)
      toast.fire({ icon: 'success', title: 'Central excluída' })
      recarregar()
    } catch (e) {
      erro('Não foi possível excluir a central', mensagemDeErro(e))
    }
  }

  const invalido = (campo: keyof typeof erros) => tentou && erros[campo]

  return (
    <div>
      <PageHeader titulo="Centrais" icone="bi-cpu" migalhas={[{ texto: 'Cadastros' }, { texto: 'Centrais' }]} />

      <div className="card shadow-sm mb-4">
        <div className="card-header fw-semibold">{editId ? `Editar central #${editId}` : 'Nova central'}</div>
        <div className="card-body">
          <div className="btn-group mb-3" role="group">
            <input type="radio" className="btn-check" id="tipo-jfl" checked={tipo === 'jfl'} onChange={() => { setTipo('jfl'); setConexaoInfo(null) }} />
            <label className="btn btn-outline-primary" htmlFor="tipo-jfl"><i className="bi bi-box-arrow-in-down-right me-1" />JFL (a central conecta no CentralHub)</label>
            <input type="radio" className="btn-check" id="tipo-tcp" checked={tipo === 'tcp'} onChange={() => { setTipo('tcp'); setConexaoInfo(null); setSenha('') }} />
            <label className="btn btn-outline-primary" htmlFor="tipo-tcp"><i className="bi bi-box-arrow-up-right me-1" />Outra (CentralHub conecta por TCP)</label>
          </div>

          {tipo === 'jfl' && (
            <div className="alert alert-light border small">
              Na JFL quem disca é a central: programe nela o IP deste servidor (endereços 702/703) e a porta
              {servidor?.portaEmUso ? <> <strong>{servidor.portaEmUso}</strong></> : null} (706/707). Ela é identificada pelo
              <strong> número de série</strong>; IP, porta e senha abaixo são apenas informativos (os comandos JFL não usam senha).
            </div>
          )}

          <form className="row g-3" onSubmit={e => { e.preventDefault(); salvar() }} noValidate>
            <div className="col-md-6">
              <label className="form-label" htmlFor="c-nome">Nome *</label>
              <input id="c-nome" className={`form-control ${invalido('nome') ? 'is-invalid' : ''}`} value={nome} onChange={e => setNome(e.target.value)} maxLength={150} />
              <div className="invalid-feedback">Informe o nome.</div>
            </div>
            <div className="col-md-6">
              <label className="form-label" htmlFor="c-predio">Prédio *</label>
              <select id="c-predio" className={`form-select ${invalido('predio') ? 'is-invalid' : ''}`} value={buildingId} onChange={e => setBuildingId(e.target.value ? Number(e.target.value) : '')}>
                <option value="">Selecione...</option>
                {buildings.map(b => <option key={b.id} value={b.id}>{b.nome}</option>)}
              </select>
              <div className="invalid-feedback">Selecione o prédio.</div>
            </div>

            {tipo === 'jfl' ? (
              <>
                <div className="col-md-4">
                  <label className="form-label" htmlFor="c-serie">Número de série *</label>
                  <MaskedInput id="c-serie" value={usuario} onChange={v => { setUsuario(v); setConexaoInfo(null) }} mascara={maskSerial}
                    placeholder="0000000000" invalido={invalido('usuario')} list="series-conectadas" inputMode="numeric" />
                  <datalist id="series-conectadas">
                    {sessoes.map(s => <option key={s.serial} value={s.serial}>{s.modelo.nome}</option>)}
                  </datalist>
                  <div className="invalid-feedback">A série tem {SERIAL_TAMANHO} dígitos.</div>
                  <div className="form-text">
                    {sessaoDoSerial
                      ? <span className="text-success-emphasis"><i className="bi bi-wifi me-1" />Conectada agora: {sessaoDoSerial.modelo.nome}, fw {sessaoDoSerial.firmware}</span>
                      : usuario.length === SERIAL_TAMANHO ? <span className="text-warning-emphasis">Nenhuma central com esta série conectada.</span> : 'Aparece no Dashboard quando a central conecta.'}
                  </div>
                </div>
                <div className="col-md-4">
                  <label className="form-label" htmlFor="c-ip">IP de origem (informativo) *</label>
                  <MaskedInput id="c-ip" value={ip} onChange={setIp} mascara={maskIp} placeholder="0.0.0.0" invalido={invalido('ip')} inputMode="decimal" />
                  <div className="invalid-feedback">IP inválido.</div>
                </div>
                <div className="col-md-4">
                  <label className="form-label" htmlFor="c-porta">Porta do servidor (informativo) *</label>
                  <MaskedInput id="c-porta" value={porta} onChange={setPorta} mascara={v => maskPorta(v, 9999)} invalido={invalido('porta')} inputMode="numeric" />
                  <div className="form-text">A central aceita no máximo 4 dígitos.</div>
                </div>
              </>
            ) : (
              <>
                <div className="col-md-3">
                  <label className="form-label" htmlFor="c-ip2">IP *</label>
                  <MaskedInput id="c-ip2" value={ip} onChange={v => { setIp(v); setConexaoInfo(null) }} mascara={maskIp} placeholder="192.168.0.10" invalido={invalido('ip')} inputMode="decimal" />
                  <div className="invalid-feedback">IP inválido.</div>
                </div>
                <div className="col-md-2">
                  <label className="form-label" htmlFor="c-porta2">Porta *</label>
                  <MaskedInput id="c-porta2" value={porta} onChange={v => { setPorta(v); setConexaoInfo(null) }} mascara={v => maskPorta(v)} invalido={invalido('porta')} inputMode="numeric" />
                  <div className="invalid-feedback">Porta inválida.</div>
                </div>
                <div className="col-md-3">
                  <label className="form-label" htmlFor="c-usuario">Usuário *</label>
                  <input id="c-usuario" className={`form-control ${invalido('usuario') ? 'is-invalid' : ''}`} value={usuario} onChange={e => setUsuario(e.target.value)} maxLength={100} />
                </div>
                <div className="col-md-4">
                  <label className="form-label" htmlFor="c-senha">Senha *</label>
                  <input id="c-senha" type="password" className={`form-control ${invalido('senha') ? 'is-invalid' : ''}`} value={senha} onChange={e => setSenha(e.target.value)} maxLength={200} autoComplete="new-password" />
                  <div className="invalid-feedback">Informe a senha da central.</div>
                </div>
              </>
            )}

            <div className="col-12 d-flex flex-wrap gap-2">
              <button type="button" className="btn btn-outline-primary" onClick={testarConexao}><i className="bi bi-plug me-1" />Testar conexão</button>
              <button type="submit" className="btn btn-primary" disabled={!conexaoInfo?.sucesso}><i className="bi bi-check-lg me-1" />{editId ? 'Atualizar' : 'Salvar'}</button>
              {editId && <button type="button" className="btn btn-outline-secondary" onClick={limparForm}>Cancelar</button>}
            </div>
          </form>

          {conexaoInfo?.sucesso && (
            <div className="border rounded p-3 mt-3 bg-body-tertiary row">
              <div className="col-6 col-md-3"><Info rotulo="Fabricante">{conexaoInfo.fabricante}</Info></div>
              <div className="col-6 col-md-3"><Info rotulo="Modelo">{conexaoInfo.modelo}</Info></div>
              <div className="col-6 col-md-2"><Info rotulo="Firmware" mono>{conexaoInfo.firmware}</Info></div>
              <div className="col-6 col-md-2"><Info rotulo="Status"><span className="badge text-bg-success">{conexaoInfo.status}</span></Info></div>
              <div className="col-6 col-md-2"><Info rotulo="Latência">{conexaoInfo.latenciaMs} ms</Info></div>
            </div>
          )}
        </div>
      </div>

      <div className="card shadow-sm">
        {centrais.length === 0 ? <Vazio icone="bi-cpu">Nenhuma central cadastrada.</Vazio> : (
          <div className="table-responsive">
            <table className="table table-hover align-middle mb-0">
              <thead><tr><th>Id</th><th>Nome</th><th>Prédio</th><th>Identificação</th><th>Fabricante</th><th>Modelo</th><th>Status</th><th className="text-end">Ações</th></tr></thead>
              <tbody>
                {centrais.map(c => {
                  const ehJfl = c.fabricante?.toLowerCase() === 'jfl'
                  const online = ehJfl ? sessoes.some(s => s.serial === c.usuario.trim()) : c.status === 'Online'
                  return (
                    <tr key={c.id}>
                      <td>{c.id}</td>
                      <td className="fw-medium">{c.nome}</td>
                      <td className="small">{c.buildingNome}</td>
                      <td className="font-monospace small">{ehJfl ? `série ${c.usuario}` : `${c.ip}:${c.porta}`}</td>
                      <td>{c.fabricante ?? <span className="badge text-bg-secondary">Não identificado</span>}</td>
                      <td className="small">{c.modelo}</td>
                      <td><span className={`badge ${online ? 'text-bg-success' : 'text-bg-secondary'}`}>{ehJfl ? (online ? 'Online' : 'Offline') : (c.status ?? '—')}</span></td>
                      <td className="text-end text-nowrap">
                        <button className="btn btn-sm btn-outline-primary me-1" onClick={() => editar(c)} title="Editar"><i className="bi bi-pencil" /></button>
                        <button className="btn btn-sm btn-outline-danger" onClick={() => excluir(c)} title="Excluir"><i className="bi bi-trash" /></button>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  )
}
