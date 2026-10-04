import { useEffect, useState } from 'react'

const QUERY = '(prefers-color-scheme: dark)'

export function usePrefersDark(): boolean {
  const [dark, setDark] = useState(
    () => typeof window !== 'undefined' && window.matchMedia(QUERY).matches,
  )

  useEffect(() => {
    const media = window.matchMedia(QUERY)
    const onChange = (event: MediaQueryListEvent) => setDark(event.matches)
    setDark(media.matches)
    media.addEventListener('change', onChange)
    return () => media.removeEventListener('change', onChange)
  }, [])

  return dark
}
