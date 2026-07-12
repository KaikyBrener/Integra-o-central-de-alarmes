import { useEffect, useState } from 'react'
import api from '../services/api'
import { Building, Central, History } from '../types'
import {
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow,
  Paper, TextField, Button, Stack, Typography, MenuItem, Select,
  InputLabel, FormControl, Alert
} from '@mui/material'

export default function OperationPage() {
  const [buildings, setBuildings] = useState<Building[]>([])
  const [centrals, setCentrals] = useState<Central[]>([])
  const [history, setHistory] = useState<History[]>([])

  const [buildingId, setBuildingId] = useState<number | ''>('')
  const [centralId, setCentralId] = useState<number | ''>('')
  const [pgm, setPgm] = useState(1)
  const [comando, setComando] = useState('Ligar')
  const [tempoPulsoMs, setTempoPulsoMs] = useState(1000)

  const [erro, setErro] = useState<string | null>(null)
  const [sucesso, setSucesso] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  const carregarBuildings = async () => {
    const response = await api.get<Building[]>('/building')
    setBuildings(response.data)
  }

  const carregarCentrals = async () => {
    const response = await api.get<Central[]>('/central')
    setCentrals(response.data)
  }

  const carregarHistorico = async () => {
    const response = await api.get<History[]>('/operation/historico')
    setHistory(response.data)
  }

  useEffect(() => {
    carregarBuildings()
    carregarCentrals()
    carregarHistorico()
  }, [])

  const centralsDoPredio = centrals.filter(c => c.buildingId === buildingId)

  const enviar = async () => {
    setErro(null)
    setSucesso(null)

    if (!centralId) {
      setErro('Selecione uma Central.')
      return
    }

    if (!pgm || pgm <= 0) {
      setErro('Informe um PGM válido.')
      return
    }

    if (comando === 'Pulso' && (!tempoPulsoMs || tempoPulsoMs <= 0)) {
      setErro('Informe o Tempo do Pulso.')
      return
    }

    setEnviando(true)
    try {
      const response = await api.post<History>('/operation/enviar', {
        centralId,
        pgm,
        comando,
        tempoPulsoMs
      })
      setSucesso(response.data.resultado)
      carregarHistorico()
    } catch {
      setErro('Não foi possível enviar o comando.')
    } finally {
      setEnviando(false)
    }
  }

  return (
    <div>
      <Typography variant="h5" gutterBottom>Operação</Typography>

      {erro && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setErro(null)}>{erro}</Alert>}
      {sucesso && <Alert severity="success" sx={{ mb: 2 }} onClose={() => setSucesso(null)}>{sucesso}</Alert>}

      <Stack spacing={2} sx={{ mb: 3, maxWidth: 500 }}>
        <FormControl>
          <InputLabel>Prédio</InputLabel>
          <Select
            label="Prédio"
            value={buildingId}
            onChange={e => { setBuildingId(e.target.value as number); setCentralId('') }}
          >
            {buildings.map(b => (
              <MenuItem key={b.id} value={b.id}>{b.nome}</MenuItem>
            ))}
          </Select>
        </FormControl>

        <FormControl disabled={!buildingId}>
          <InputLabel>Central</InputLabel>
          <Select label="Central" value={centralId} onChange={e => setCentralId(e.target.value as number)}>
            {centralsDoPredio.map(c => (
              <MenuItem key={c.id} value={c.id}>{c.nome}</MenuItem>
            ))}
            {centralsDoPredio.length === 0 && (
              <MenuItem value="" disabled>Nenhuma central neste prédio</MenuItem>
            )}
          </Select>
        </FormControl>

        <TextField label="PGM" type="number" value={pgm} onChange={e => setPgm(Number(e.target.value))} />

        <FormControl>
          <InputLabel>Comando</InputLabel>
          <Select label="Comando" value={comando} onChange={e => setComando(e.target.value)}>
            <MenuItem value="Pulso">Pulso</MenuItem>
            <MenuItem value="Ligar">Ligar</MenuItem>
            <MenuItem value="Desligar">Desligar</MenuItem>
          </Select>
        </FormControl>

        {comando === 'Pulso' && (
          <TextField
            label="Tempo do Pulso (ms)"
            type="number"
            value={tempoPulsoMs}
            onChange={e => setTempoPulsoMs(Number(e.target.value))}
          />
        )}

        <Button variant="contained" onClick={enviar} disabled={!centralId || enviando}>
          {enviando ? 'Enviando...' : 'Enviar'}
        </Button>
      </Stack>

      <Typography variant="h6" gutterBottom>Histórico</Typography>

      <TableContainer component={Paper}>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>Data</TableCell>
              <TableCell>Central</TableCell>
              <TableCell>PGM</TableCell>
              <TableCell>Comando</TableCell>
              <TableCell>Resultado</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {history.map(h => (
              <TableRow key={h.id}>
                <TableCell>{new Date(h.data).toLocaleString()}</TableCell>
                <TableCell>{centrals.find(c => c.id === h.centralId)?.nome || h.centralId}</TableCell>
                <TableCell>{h.pgm}</TableCell>
                <TableCell>{h.comando}</TableCell>
                <TableCell>{h.resultado}</TableCell>
              </TableRow>
            ))}
            {history.length === 0 && (
              <TableRow>
                <TableCell colSpan={5} align="center">Nenhum comando enviado.</TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </TableContainer>
    </div>
  )
}
