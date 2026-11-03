import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router'
import { PrimeReactProvider } from 'primereact/api'
import 'primereact/resources/themes/lara-light-cyan/theme.css'
import 'primeicons/primeicons.css'
import './index.css'
import App from './App.tsx'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <PrimeReactProvider>
      <BrowserRouter>
        <App />
      </BrowserRouter>
    </PrimeReactProvider>
  </StrictMode>,
)
