import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import App from './App'
import './index.css'

// `index.html` declares the mount point, so asking for it by id here rather than asserting
// it means a change to the markup that dropped the element fails with a sentence naming it
// instead of with React's own complaint about a container that is null.
const root = document.getElementById('root')
if (!root) {
  throw new Error('index.html has no #root element to mount the application into')
}

createRoot(root).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
