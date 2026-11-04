import type { InputHTMLAttributes } from 'react'

interface Props extends Omit<InputHTMLAttributes<HTMLInputElement>, 'onChange' | 'value'> {
  value: string
  onChange: (valor: string) => void
  mascara: (valor: string) => string
  invalido?: boolean
}

/** Campo de texto com máscara (IP, MAC, telefone, série, horário...). */
export default function MaskedInput({ value, onChange, mascara, invalido, className, ...resto }: Props) {
  return (
    <input
      {...resto}
      className={`form-control ${invalido ? 'is-invalid' : ''} ${className ?? ''}`}
      value={value}
      onChange={e => onChange(mascara(e.target.value))}
      autoComplete="off"
      spellCheck={false}
    />
  )
}
