import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import Layout from './components/Layout'
import { JflProvider } from './context/JflContext'
import DashboardPage from './pages/DashboardPage'
import EventosPage from './pages/EventosPage'
import LogsPage from './pages/LogsPage'
import ZonasPage from './pages/ZonasPage'
import ParticoesPage from './pages/ParticoesPage'
import PgmsPage from './pages/PgmsPage'
import DispositivosPage from './pages/DispositivosPage'
import ProgramacaoPage from './pages/ProgramacaoPage'
import BuildingsPage from './pages/BuildingsPage'
import CentralsPage from './pages/CentralsPage'
import OperationPage from './pages/OperationPage'

function App() {
  return (
    <BrowserRouter>
      <JflProvider>
        <Routes>
          <Route element={<Layout />}>
            <Route path="/" element={<DashboardPage />} />
            <Route path="/eventos" element={<EventosPage />} />
            <Route path="/logs" element={<LogsPage />} />
            <Route path="/zonas" element={<ZonasPage />} />
            <Route path="/particoes" element={<ParticoesPage />} />
            <Route path="/pgms" element={<PgmsPage />} />
            <Route path="/dispositivos" element={<DispositivosPage />} />
            <Route path="/programacao" element={<ProgramacaoPage />} />
            <Route path="/predios" element={<BuildingsPage />} />
            <Route path="/centrais" element={<CentralsPage />} />
            <Route path="/operacao" element={<OperationPage />} />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Route>
        </Routes>
      </JflProvider>
    </BrowserRouter>
  )
}

export default App
