import { useEffect, useState } from 'react'
import { PageHeader, Vazio } from '../components/ui'
import api, { mensagemDeErro } from '../services/api'
import type { Building } from '../types'
import { confirmar, erro, esc, toast } from '../utils/alerts'

export default function BuildingsPage() {
  const [buildings, setBuildings] = useState<Building[]>([])
  const [nome, setNome] = useState('')
  const [descricao, setDescricao] = useState('')
  const [editId, setEditId] = useState<number | null>(null)
  const [tentouSalvar, setTentouSalvar] = useState(false)
  const [salvando, setSalvando] = useState(false)

  const carregar = async () => {
    try {
      const response = await api.get<Building[]>('/building')
      setBuildings(response.data)
    } catch (e) {
      erro('Não foi possível carregar os prédios', e)
    }
  }

  useEffect(() => {
    carregar()
  }, [])

  const limparForm = () => {
    setNome('')
    setDescricao('')
    setEditId(null)
    setTentouSalvar(false)
  }

  const salvar = async () => {
    setTentouSalvar(true)
    if (!nome.trim()) return

    setSalvando(true)
    try {
      if (editId) {
        await api.put(`/building/${editId}`, { id: editId, nome, descricao })
      } else {
        await api.post('/building', { nome, descricao })
      }
      toast.fire({ icon: 'success', title: editId ? 'Prédio atualizado' : 'Prédio cadastrado' })
      limparForm()
      carregar()
    } catch (e) {
      erro('Não foi possível salvar o prédio', e)
    } finally {
      setSalvando(false)
    }
  }

  const editar = (building: Building) => {
    setEditId(building.id)
    setNome(building.nome)
    setDescricao(building.descricao || '')
    setTentouSalvar(false)
  }

  const excluir = async (b: Building) => {
    if (!(await confirmar('Excluir prédio?', `<strong>${esc(b.nome)}</strong> será removido.`, 'Excluir', true))) return
    try {
      await api.delete(`/building/${b.id}`)
      toast.fire({ icon: 'success', title: 'Prédio excluído' })
      carregar()
    } catch (e) {
      erro('Não foi possível excluir', mensagemDeErro(e, 'Não é possível excluir um prédio que possui centrais cadastradas.'))
    }
  }

  return (
    <div>
      <PageHeader titulo="Prédios" icone="bi-buildings" migalhas={[{ texto: 'Cadastros' }, { texto: 'Prédios' }]} />

      <div className="card shadow-sm mb-4">
        <div className="card-header fw-semibold">{editId ? `Editar prédio #${editId}` : 'Novo prédio'}</div>
        <div className="card-body">
          <form className="row g-3" onSubmit={e => { e.preventDefault(); salvar() }} noValidate>
            <div className="col-md-4">
              <label className="form-label" htmlFor="predio-nome">Nome *</label>
              <input id="predio-nome" className={`form-control ${tentouSalvar && !nome.trim() ? 'is-invalid' : ''}`} value={nome} onChange={e => setNome(e.target.value)} maxLength={150} />
              <div className="invalid-feedback">Informe o nome do prédio.</div>
            </div>
            <div className="col-md-5">
              <label className="form-label" htmlFor="predio-desc">Descrição</label>
              <input id="predio-desc" className="form-control" value={descricao} onChange={e => setDescricao(e.target.value)} maxLength={500} />
            </div>
            <div className="col-md-3 d-flex align-items-end gap-2">
              <button type="submit" className="btn btn-primary" disabled={salvando}>
                <i className={`bi ${editId ? 'bi-check-lg' : 'bi-plus-lg'} me-1`} />{editId ? 'Atualizar' : 'Adicionar'}
              </button>
              {editId && <button type="button" className="btn btn-outline-secondary" onClick={limparForm}>Cancelar</button>}
            </div>
          </form>
        </div>
      </div>

      <div className="card shadow-sm">
        {buildings.length === 0 ? <Vazio icone="bi-buildings">Nenhum prédio cadastrado.</Vazio> : (
          <div className="table-responsive">
            <table className="table table-hover align-middle mb-0">
              <thead><tr><th>Id</th><th>Nome</th><th>Descrição</th><th className="text-end">Ações</th></tr></thead>
              <tbody>
                {buildings.map(b => (
                  <tr key={b.id}>
                    <td>{b.id}</td>
                    <td className="fw-medium">{b.nome}</td>
                    <td className="text-body-secondary">{b.descricao}</td>
                    <td className="text-end">
                      <button className="btn btn-sm btn-outline-primary me-1" onClick={() => editar(b)} title="Editar"><i className="bi bi-pencil" /></button>
                      <button className="btn btn-sm btn-outline-danger" onClick={() => excluir(b)} title="Excluir"><i className="bi bi-trash" /></button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  )
}
