import axios from 'axios'

// A chave vem de VITE_API_KEY (Frontend/.env.local) e deve ser igual à ApiKey configurada na API.
// Atenção: variáveis VITE_* são embutidas no JavaScript entregue ao navegador.
const apiKey = import.meta.env.VITE_API_KEY

const api = axios.create({
  baseURL: 'http://localhost:5000/api',
  headers: apiKey ? { 'X-Api-Key': apiKey } : {},
})

export default api
