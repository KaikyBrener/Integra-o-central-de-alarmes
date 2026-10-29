import { useEffect, useState } from 'react'
import api, { mensagemDeErro } from '../services/api'
import { Building, Central, ConexaoResult } from '../types'
import StatusChip from '../components/StatusChip'
import {
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow,
  Paper, TextField, Button, Stack, IconButton, Typography, MenuItem, Select,
  InputLabel, FormControl, Alert, Chip
} from '@mui/material'
import EditIcon from '@mui/icons-material/Edit'
import DeleteIcon from '@mui/icons-material/Delete'

export default function CentralsPage() {
  const [centrals, setCentrals] = useState<Central[]>([])
  const [buildings, setBuildings] = useState<Building[]>([])

  const [nome, setNome] = useState('')
  const [ip, setIp] = useState('')
  const [porta, setPorta] = useState(0)
  const [usuario, setUsuario] = useState('')
  const [senha, setSenha] = useState('')
  const [buildingId, setBuildingId] = useState<number | ''>('')
  const [editId, setEditId] = useState<number | null>(null)

  const [conexaoInfo, setConexaoInfo] = useState<ConexaoResult | null>(null)
  const [testando, setTestando] = useState(false)
  const [erro, setErro] = useState<string | null>(null)

  const carregar = async () => {
    const response = await api.get<Central[]>('/central')
    setCentrals(response.data)
  }

  const carregarBuildings = async () => {
    const response = await api.get<Building[]>('/building')
    setBuildings(response.data)
  }

  useEffect(() => {
    carregar()
    carregarBuildings()
  }, [])

  const limparForm = () => {
    setNome('')
    setIp('')
    setPorta(0)
    setUsuario('')
    setSenha('')
    setBuildingId('')
    setEditId(null)
    setConexaoInfo(null)
    setErro(null)
  }

  const testarConexao = async () => {
    if (!ip.trim() || !porta) {
      setErro('Informe IP e Porta para testar a conexão.')
      return
    }

    setErro(null)
    setConexaoInfo(null)
    setTestando(true)
    try {
      const response = await api.post<ConexaoResult>('/central/testar-conexao', { ip, porta, usuario, senha })
      setConexaoInfo(response.data)
      if (!response.data.sucesso) {
        setErro(response.data.erro || 'Não foi possível conectar à central.')
      }
    } catch (e) {
      setErro(mensagemDeErro(e, 'Falha ao testar a conexão com a central.'))
    } finally {
      setTestando(false)
    }
  }

  const salvar = async () => {
    if (!nome.trim() || !ip.trim() || !buildingId) {
      setErro('Preencha Nome, IP e Prédio.')
      return
    }

    if (!conexaoInfo || !conexaoInfo.sucesso) {
      setErro('Teste a conexão com sucesso antes de salvar.')
      return
    }

    setErro(null)

    const payload = {
      id: editId || 0,
      nome,
      ip,
      porta,
      usuario,
      senha,
      buildingId,
      fabricante: conexaoInfo?.fabricante,
      modelo: conexaoInfo?.modelo,
      firmware: conexaoInfo?.firmware,
      status: conexaoInfo?.status,
      latencia: conexaoInfo?.latenciaMs,
    }

    try {
      if (editId) {
        await api.put(`/central/${editId}`, payload)
      } else {
        await api.post('/central', payload)
      }
      limparForm()
      carregar()
    } catch (e) {
      setErro(mensagemDeErro(e, 'Não foi possível salvar a central.'))
    }
  }

  const editar = (central: Central) => {
    setEditId(central.id)
    setNome(central.nome)
    setIp(central.ip)
    setPorta(central.porta)
    setUsuario(central.usuario)
    // A senha nunca é retornada pela API por segurança; ao editar,
    // o usuário precisa informá-la novamente e testar a conexão.
    setSenha('')
    setBuildingId(central.buildingId)
    setErro(null)
    setConexaoInfo(central.fabricante ? {
      sucesso: true,
      fabricante: central.fabricante || '',
      modelo: central.modelo || '',
      firmware: central.firmware || '',
      status: central.status || '',
      latenciaMs: central.latencia || 0
    } : null)
  }

  const excluir = async (id: number) => {
    setErro(null)
    try {
      await api.delete(`/central/${id}`)
      carregar()
    } catch (e) {
      setErro(mensagemDeErro(e, 'Não foi possível excluir a central.'))
    }
  }

  return (
    <div>
      <Typography variant="h5" gutterBottom>Centrais</Typography>

      {erro && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setErro(null)}>{erro}</Alert>}

      <Stack spacing={2} sx={{ mb: 3, maxWidth: 500 }}>
        <TextField label="Nome" value={nome} onChange={e => setNome(e.target.value)} />
        <TextField label="IP" value={ip} onChange={e => setIp(e.target.value)} />
        <TextField label="Porta" type="number" value={porta} onChange={e => setPorta(Number(e.target.value))} />
        <TextField label="Usuário" value={usuario} onChange={e => setUsuario(e.target.value)} />
        <TextField label="Senha" type="password" value={senha} onChange={e => setSenha(e.target.value)} />

        <FormControl>
          <InputLabel>Prédio</InputLabel>
          <Select
            label="Prédio"
            value={buildingId}
            onChange={e => setBuildingId(e.target.value as number)}
          >
            {buildings.map(b => (
              <MenuItem key={b.id} value={b.id}>{b.nome}</MenuItem>
            ))}
          </Select>
        </FormControl>

        <Button variant="outlined" onClick={testarConexao} disabled={testando}>
          {testando ? 'Testando...' : 'Testar Conexão'}
        </Button>

        {conexaoInfo && conexaoInfo.sucesso && (
          <Paper sx={{ p: 2 }}>
            <Stack direction="row" spacing={1} sx={{ mb: 1 }}>
              <Chip label={conexaoInfo.status} color="success" size="small" />
            </Stack>
            <Typography variant="body2">Fabricante: {conexaoInfo.fabricante}</Typography>
            <Typography variant="body2">Modelo: {conexaoInfo.modelo}</Typography>
            <Typography variant="body2">Firmware: {conexaoInfo.firmware}</Typography>
            <Typography variant="body2">Latência: {conexaoInfo.latenciaMs}ms</Typography>
          </Paper>
        )}

        <Stack direction="row" spacing={2}>
          <Button variant="contained" onClick={salvar} disabled={!conexaoInfo?.sucesso}>
            {editId ? 'Atualizar' : 'Salvar'}
          </Button>
          {editId && <Button onClick={limparForm}>Cancelar</Button>}
        </Stack>
      </Stack>

      <TableContainer component={Paper}>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>Id</TableCell>
              <TableCell>Nome</TableCell>
              <TableCell>IP</TableCell>
              <TableCell>Fabricante</TableCell>
              <TableCell>Modelo</TableCell>
              <TableCell>Status</TableCell>
              <TableCell align="right">Ações</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {centrals.map(c => (
              <TableRow key={c.id}>
                <TableCell>{c.id}</TableCell>
                <TableCell>{c.nome}</TableCell>
                <TableCell>{c.ip}</TableCell>
                <TableCell>{c.fabricante}</TableCell>
                <TableCell>{c.modelo}</TableCell>
                <TableCell><StatusChip status={c.status} /></TableCell>
                <TableCell align="right">
                  <IconButton onClick={() => editar(c)}><EditIcon /></IconButton>
                  <IconButton onClick={() => excluir(c.id)}><DeleteIcon /></IconButton>
                </TableCell>
              </TableRow>
            ))}
            {centrals.length === 0 && (
              <TableRow>
                <TableCell colSpan={7} align="center">Nenhuma central cadastrada.</TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </TableContainer>
    </div>
  )
}
