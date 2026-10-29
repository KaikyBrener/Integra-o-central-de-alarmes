import axios from 'axios'

// A chave vem de VITE_API_KEY (Frontend/.env.local) e deve ser igual à ApiKey configurada na API.
// Atenção: variáveis VITE_* são embutidas no JavaScript entregue ao navegador.
const apiKey = import.meta.env.VITE_API_KEY

export const MENSAGEM_CHAVE_INVALIDA = 'Chave de API ausente ou inválida: confira VITE_API_KEY'

// Disparado quando a API responde 401, para o App avisar mesmo em telas que carregam dados sem tratar o erro.
export const EVENTO_NAO_AUTORIZADO = 'centralhub:nao-autorizado'

const api = axios.create({
  baseURL: 'http://localhost:5000/api',
  headers: apiKey ? { 'X-Api-Key': apiKey } : {},
})

api.interceptors.response.use(
  response => response,
  error => {
    if (axios.isAxiosError(error) && error.response?.status === 401) {
      window.dispatchEvent(new Event(EVENTO_NAO_AUTORIZADO))
    }
    return Promise.reject(error)
  }
)

/**
 * Mensagem para exibir quando uma chamada falha: explica o 401 (chave de API), repete a mensagem
 * da API no 422 (central sem fabricante reconhecido) e usa a mensagem padrão nos demais casos.
 */
export function mensagemDeErro(error: unknown, padrao: string): string {
  if (axios.isAxiosError(error)) {
    if (error.response?.status === 401) {
      return MENSAGEM_CHAVE_INVALIDA
    }
    const mensagem = error.response?.data?.mensagem
    if (error.response?.status === 422 && typeof mensagem === 'string') {
      return mensagem
    }
  }
  return padrao
}

export default api
