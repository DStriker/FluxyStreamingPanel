import { Alert, Button } from 'antd'
import { useTranslation } from 'react-i18next'

/**
 * A failed read, as every table page shows it: the sentence and one way to try again.
 *
 * It is drawn in place of the rows rather than over them (`loadError ? … : <Table/>`), the
 * retry carries the table's own loading flag so a second press is impossible while the read
 * is in flight, and the label is the same `actions.retry` on every page. Those three facts
 * were identical in five hand-copied `Alert`s - and five copies of one thing are five places
 * for it to change in four of them.
 */
export default function LoadErrorAlert({
  message,
  loading,
  onRetry,
}: {
  /** The reason, translated - `messageForError` produced it. */
  message: string
  /** Whether the re-read this button starts is in flight. */
  loading: boolean
  onRetry: () => void
}) {
  const { t } = useTranslation()

  return (
    <Alert
      type="error"
      showIcon
      message={message}
      action={
        <Button size="small" onClick={() => onRetry()} loading={loading}>
          {t('actions.retry')}
        </Button>
      }
    />
  )
}
