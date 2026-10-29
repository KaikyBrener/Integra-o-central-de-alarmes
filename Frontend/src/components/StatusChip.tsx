import { Chip, Tooltip } from '@mui/material'

/** Online e Offline vêm da verificação de conectividade; Desconhecido é uma central que não pôde ser verificada. */
export default function StatusChip({ status }: { status?: string }) {
  if (!status) {
    return <>—</>
  }

  if (status === 'Desconhecido') {
    return (
      <Tooltip title="Fabricante não reconhecido: rode o teste de conexão e salve a central de novo.">
        <Chip label="Desconhecido" color="warning" variant="outlined" size="small" />
      </Tooltip>
    )
  }

  if (status === 'Online') {
    return <Chip label="Online" color="success" size="small" />
  }

  if (status === 'Offline') {
    return <Chip label="Offline" color="error" size="small" />
  }

  return <Chip label={status} size="small" />
}
