import { useCallback, useEffect, useRef } from 'react'
import { App } from 'antd'
import { useBlocker, useBeforeUnload } from 'react-router-dom'
import { useTranslation } from 'react-i18next'

/**
 * Guards a page that holds edits the server has not seen yet.
 *
 * Two of the three ways out can be intercepted. A move *inside* the application - a menu
 * click, the Cancel button, the browser's Back - is a router navigation, so `useBlocker`
 * catches it before it happens and there is time to ask; that is the dialog built here,
 * worded in the page's own language. A reload, a closed tab or a typed-in address is the
 * browser's territory: `useBeforeUnload` is all a page gets there, and the browser draws
 * the question in its own words. The third way out - a save that navigates away on
 * purpose - must not be intercepted at all, which is what the returned function is for.
 *
 * The returned function is called immediately before that deliberate navigation, and it
 * is a ref rather than state for a reason: a save handler runs `await api...()` and then
 * `navigate(...)` in the same event, before React has re-rendered, so a `setDirty(false)`
 * would still be `true` in the blocker for that navigation. The ref is read by the
 * predicate at the moment the navigation is attempted, so it sees the fresh value.
 *
 * `dirty` is the page's own record of whether anything was edited (`onValuesChange`), and
 * the guard trusts it exactly: a page that forgets to flip it back after loading a record
 * into the form would ask about edits nobody made.
 */
export default function useUnsavedChangesWarning(dirty: boolean): () => void {
  const { modal } = App.useApp()
  const { t } = useTranslation()
  /** Set by the returned function; read by the blocker predicate when a move is attempted. */
  const allowed = useRef(false)
  /** One dialog per blocked navigation, however often the effect re-runs while blocked. */
  const asking = useRef(false)

  const blocker = useBlocker(
    useCallback(
      ({ currentLocation, nextLocation }) =>
        dirty && !allowed.current && currentLocation.pathname !== nextLocation.pathname,
      [dirty],
    ),
  )

  useEffect(() => {
    if (blocker.state !== 'blocked') {
      asking.current = false
      return
    }
    if (asking.current) return

    asking.current = true
    modal.confirm({
      title: t('unsavedChanges.title'),
      content: t('unsavedChanges.content'),
      okText: t('unsavedChanges.leave'),
      okButtonProps: { danger: true },
      cancelText: t('unsavedChanges.stay'),
      onOk: () => blocker.proceed(),
      onCancel: () => blocker.reset(),
    })
  }, [blocker, modal, t])

  useBeforeUnload(
    useCallback(
      (event) => {
        if (!dirty || allowed.current) return
        // The wording and the buttons are the browser's; `preventDefault` is the whole of
        // what opens its dialog, and a page that wanted a message of its own would have to
        // write it, which every browser has ignored for years.
        event.preventDefault()
      },
      [dirty],
    ),
  )

  return useCallback(() => {
    allowed.current = true
  }, [])
}
