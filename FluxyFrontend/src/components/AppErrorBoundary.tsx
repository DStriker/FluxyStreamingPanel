import { Component } from 'react'
import type { ErrorInfo, ReactNode } from 'react'
import ErrorFallback from './ErrorFallback'

/**
 * The outermost error boundary: one class, above `App`, because React has no hook for this.
 *
 * A class is not a style choice here - `getDerivedStateFromError` and `componentDidCatch`
 * exist only on class components, and this is the one place in the application where that
 * is the API being used rather than a pattern being copied. Everything below it has
 * boundaries of its own (`errorElement` on every route), so this one only ever fires for a
 * failure *above* the router - a provider, `App` itself - where nothing else can.
 *
 * The fallback is `ErrorFallback`, the same component the routes draw: it needs no
 * providers, so it renders here too, and one screen describes one kind of failure in both
 * places.
 */
export default class AppErrorBoundary extends Component<
  { children: ReactNode },
  { failed: boolean }
> {
  override state = { failed: false }

  static getDerivedStateFromError() {
    return { failed: true }
  }

  override componentDidCatch(error: Error, info: ErrorInfo) {
    // The console rather than silence: React 19 hands caught errors here instead of
    // logging them itself when a boundary exists, and an error that nothing reports is an
    // error that gets debugged by screenshot. This is also the natural seam for a
    // reporting service, if one is ever added.
    console.error('The application failed to render:', error, info.componentStack)
  }

  override render() {
    return this.state.failed ? <ErrorFallback /> : this.props.children
  }
}
