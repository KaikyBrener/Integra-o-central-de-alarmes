import { BrowserRouter, Routes, Route, Link } from 'react-router-dom'
import AppBar from '@mui/material/AppBar'
import Toolbar from '@mui/material/Toolbar'
import Typography from '@mui/material/Typography'
import Button from '@mui/material/Button'
import Container from '@mui/material/Container'
import BuildingsPage from './pages/BuildingsPage'
import CentralsPage from './pages/CentralsPage'
import OperationPage from './pages/OperationPage'

function App() {
  return (
    <BrowserRouter>
      <AppBar position="static">
        <Toolbar>
          <Typography variant="h6" sx={{ flexGrow: 1 }}>
            CentralHub
          </Typography>
          <Button color="inherit" component={Link} to="/">Prédios</Button>
          <Button color="inherit" component={Link} to="/centrais">Centrais</Button>
          <Button color="inherit" component={Link} to="/operacao">Operação</Button>
        </Toolbar>
      </AppBar>
      <Container sx={{ mt: 3 }}>
        <Routes>
          <Route path="/" element={<BuildingsPage />} />
          <Route path="/centrais" element={<CentralsPage />} />
          <Route path="/operacao" element={<OperationPage />} />
        </Routes>
      </Container>
    </BrowserRouter>
  )
}

export default App
