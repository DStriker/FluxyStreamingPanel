import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { App, Button } from 'antd'
import { useTranslation } from 'react-i18next'
import AuthCard from '../components/AuthCard'
import config, { routePath } from '../config'
import { currentSession, signOut } from '../lib/api'
import { messageForError } from '../lib/http'

/**
 * The landing page for a signed-in area that does not exist yet.
 *
 * It is here so the routing is real before the features are. A sign-in that answers with a
 * path nothing matches ends at a blank screen and a 404, which reads as a broken backend
 * rather than as an unfinished one; answering with a page that says so plainly is honest,
 * and it also gives the session somewhere to be observed - which makes this the right place
 * to show who is signed in and to put a working sign-out.
 *
 * It replaces itself when there is no session. A visitor who arrives here with no token is
 * not signed in and belongs at the form; leaving them on a page about a signed-in area
 * would put a route anybody could sit on.
 */
export default function AreaPlaceholderPage({ titleKey }) {
  const navigate = useNavigate()
  const { t } = useTranslation()
  const { message } = App.useApp()

  useEffect(() => {
    let cancelled = false

    currentSession().then((session) => {
      if (cancelled) return
      if (!session) {
        navigate(routePath(config.CLIENT_LOGIN_ROUTE), { replace: true })
      }
    })

    return () => {
      cancelled = true
    }
  }, [navigate])

  const handleSignOut = async () => {
    // The tokens are in cookies the page cannot see, so the only way out is to ask the
    // server to end the session. `signOut` mints its own antiforgery token immediately
    // before sending, which is what makes this work at all: a token carried over from the
    // sign-in page was minted before the session existed and the server refuses it.
    //
    // A failure is reported rather than swallowed. With no catch the rejection disappeared
    // into the void, the page did not move and the button looked dead - the visitor had no
    // way to tell "nothing happened" from "something went wrong".
    try {
      const result = await signOut()
      message.success(t('messages.api.signed_out'))
      // The server names where a signed-out visitor belongs; the local route is only the
      // fallback for a response that arrives without one.
      navigate(result?.redirect ?? routePath(config.CLIENT_LOGIN_ROUTE), { replace: true })
    } catch (error) {
      message.error(messageForError(error))
    }
  }

  return (
    <AuthCard title={t(titleKey)} onFinish={() => {}}>
      <p>{t('messages.areaNotBuilt')}</p>
      <Button htmlType="button" block onClick={handleSignOut}>
        {t('actions.signOut')}
      </Button>
    </AuthCard>
  )
}