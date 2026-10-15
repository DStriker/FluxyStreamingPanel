import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import App from './App'
import AppErrorBoundary from './components/AppErrorBoundary'
import './index.css'

// `index.html` declares the mount point, so asking for it by id here rather than asserting
// it means a change to the markup that dropped the element fails with a sentence naming it
// instead of with React's own complaint about a container that is null.
const root = document.getElementById('root')
if (!root) {
  throw new Error('index.html has no #root element to mount the application into')
}

createRoot(root, {
  // React 19 routes every error through these two options and stops logging on its own.
  // The boundary below catches whatever it can, so `onCaughtError` is where a caught route
  // error is reported and `onUncaughtError` should never see anything - it is the seam
  // that exists so a crash has one place to be reported from when a boundary is the thing
  // that broke. Kept as `console.error` for now; a reporting service would plug in here.
  onCaughtError: (error, errorInfo) => {
    console.error('Caught by an error boundary:', error, errorInfo.componentStack)
  },
  onUncaughtError: (error, errorInfo) => {
    console.error('No error boundary caught this:', error, errorInfo.componentStack)
  },
}).render(
  <StrictMode>
    <AppErrorBoundary>
      <App />
    </AppErrorBoundary>
  </StrictMode>,
)
