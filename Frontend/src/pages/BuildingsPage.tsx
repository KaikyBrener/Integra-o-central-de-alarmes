import { useEffect, useState } from 'react'
import api from '../services/api'
import { Building } from '../types'
import {
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow,
  Paper, TextField, Button, Stack, IconButton, Typography, Alert
} from '@mui/material'
import EditIcon from '@mui/icons-material/Edit'
import DeleteIcon from '@mui/icons-material/Delete'

export default function BuildingsPage() {
  const [buildings, setBuildings] = useState<Building[]>([])
  const [nome, setNome] = useState('')
  const [descricao, setDescricao] = useState('')
  const [editId, setEditId] = useState<number | null>(null)
  const [erro, setErro] = useState<string | null>(null)
  const [carregando, setCarregando] = useState(false)

  const carregar = async () => {
    try {
      const response = await api.get<Building[]>('/building')
      setBuildings(response.data)
    } catch {
      setErro('Não foi possível carregar os prédios.')
    }
  }

  useEffect(() => {
    carregar()
  }, [])

  const limparForm = () => {
    setNome('')
    setDescricao('')
    setEditId(null)
    setErro(null)
  }

  const salvar = async () => {
    if (!nome.trim()) {
      setErro('Informe o nome do prédio.')
      return
    }

    setErro(null)
    setCarregando(true)
    try {
      if (editId) {
        await api.put(`/building/${editId}`, { id: editId, nome, descricao })
      } else {
        await api.post('/building', { nome, descricao })
      }
      limparForm()
      carregar()
    } catch {
      setErro('Não foi possível salvar o prédio.')
    } finally {
      setCarregando(false)
    }
  }

  const editar = (building: Building) => {
    setEditId(building.id)
    setNome(building.nome)
    setDescricao(building.descricao || '')
    setErro(null)
  }

  const excluir = async (id: number) => {
    setErro(null)
    try {
      await api.delete(`/building/${id}`)
      carregar()
    } catch {
      setErro('Não é possível excluir um prédio que possui centrais cadastradas.')
    }
  }

  return (
    <div>
      <Typography variant="h5" gutterBottom>Prédios</Typography>

      {erro && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setErro(null)}>{erro}</Alert>}

      <Stack direction="row" spacing={2} sx={{ mb: 3 }} alignItems="flex-start">
        <TextField
          label="Nome"
          value={nome}
          onChange={e => setNome(e.target.value)}
          error={!nome.trim() && erro !== null}
        />
        <TextField label="Descrição" value={descricao} onChange={e => setDescricao(e.target.value)} />
        <Button variant="contained" onClick={salvar} disabled={carregando}>
          {editId ? 'Atualizar' : 'Adicionar'}
        </Button>
        {editId && <Button onClick={limparForm}>Cancelar</Button>}
      </Stack>

      <TableContainer component={Paper}>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>Id</TableCell>
              <TableCell>Nome</TableCell>
              <TableCell>Descrição</TableCell>
              <TableCell align="right">Ações</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {buildings.map(b => (
              <TableRow key={b.id}>
                <TableCell>{b.id}</TableCell>
                <TableCell>{b.nome}</TableCell>
                <TableCell>{b.descricao}</TableCell>
                <TableCell align="right">
                  <IconButton onClick={() => editar(b)}><EditIcon /></IconButton>
                  <IconButton onClick={() => excluir(b.id)}><DeleteIcon /></IconButton>
                </TableCell>
              </TableRow>
            ))}
            {buildings.length === 0 && (
              <TableRow>
                <TableCell colSpan={4} align="center">Nenhum prédio cadastrado.</TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </TableContainer>
    </div>
  )
}
