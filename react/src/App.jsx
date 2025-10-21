import { BrowserRouter, Routes, Route } from 'react-router-dom';
import Home from './pages/home';
import TeknikServis from './pages/teknik_servis';
import Login from './pages/login';
import './App.css'

function App() {
  return (
    <BrowserRouter>
      <main className="p-0 min-h-screen">
          <Routes>
            <Route path="/" element={<Home />} />
            <Route path="/login" element={<Login />} />
            <Route path="/teknik-servis" element={<TeknikServis />} />
          </Routes>
        </main>
    </BrowserRouter>
  )
}

export default App
