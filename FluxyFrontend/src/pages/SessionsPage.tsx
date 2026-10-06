import { useEffect, useState } from 'react'
import { Alert, Button, Card, Space, Table, Tag, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { getSessionHistory, getProfile } from '../lib/api'
import { messageForError } from '../lib/http'
import type { SessionHistoryResponse, SessionVisit } from '../types'

/**
 * How many visits one page holds. The server's own default, kept in step by hand because the
 * two are one contract written twice - and because a page size the pager draws from has to be
 * known before the first answer arrives, or the pager would show one row per page until it did.
 */
const PAGE_SIZE = 20

/**
 * The account's visit history: one read-only row per token the account ever held.
 *
 * Read only, and that is the whole shape of the page. There is no form here, no action column
 * and no selection - a history is a record of what happened, so the only things this page can
 * do are read it and turn its pages. Anything else would imply the visitor could edit a fact
 * the server keeps precisely so that it cannot be edited.
 *
 * The rows are tokens rather than sessions, so a browser that refreshed from a different network
 * appears twice. That is the point: "a sign-in I do not recognise" and "a rotation I do not
 * recognise" are the same alarm, and hiding the second behind an aggregation of the first would
 * hide the moment the network actually changed.
 *
 * Dates are rendered in the account's own time zone rather than the browser's, because the
 * profile page says they are - "Dates and times are shown in this zone" is a promise the whole
 * account area has to keep, and a history page that answered in the browser's zone would be the
 * one page where the setting visibly did nothing. The zone is read once on mount: it changes
 * only from the profile page, which is a navigation away, so re-reading it per page would buy
 * nothing.
 */
export default function SessionsPage({ sectionKey }: { sectionKey: string }) {
  const { t } = useTranslation()

  const [history, setHistory] = useState<SessionHistoryResponse | null>(null)
  const [timeZone, setTimeZone] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(PAGE_SIZE)
  // Bumped by the retry button rather than by the page changing, so a failed load can be asked
  // for again without pretending the visitor navigated somewhere else.
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    // The zone is read for the dates below, not for this page's own state: a failure here leaves
    // the browser's zone in place and the list still renders, which is the right degradation -
    // the history matters and the exact zone it is printed in does not.
    let cancelled = false

    getProfile()
      .then((profile) => {
        if (!cancelled) setTimeZone(profile.timeZone)
      })
      .catch(() => {})

    return () => {
      cancelled = true
    }
  }, [])

  useEffect(() => {
    // A generation of one per effect run rather than one shared counter: an answer from the
    // page the visitor just left must not overwrite the page they moved to, and an effect that
    // is torn down mid-flight must not write into a table that has already drawn its next row.
    let cancelled = false

    setLoading(true)

    getSessionHistory({ page, pageSize })
      .then((answer) => {
        if (cancelled) return
        setHistory(answer)
        setLoadError(null)
      })
      .catch((error) => {
        if (cancelled) return
        setHistory(null)
        setLoadError(messageForError(error))
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })

    return () => {
      cancelled = true
    }
  }, [page, pageSize, attempt])

  /**
   * A date as this account's own zone shows it, falling back to the browser's when the zone is
   * absent or is one the runtime refuses to format in.
   *
   * The `try` is not decoration. A zone stored by an older build, or a zone this ICU build does
   * not carry, makes `Intl.DateTimeFormat` throw before it returns a formatter - and an
   * exception thrown while rendering a row would take the whole page down over one timestamp.
   */
  const formatMoment = (value: string): string => {
    const date = new Date(value)

    if (Number.isNaN(date.getTime())) return '—'

    try {
      return new Intl.DateTimeFormat(undefined, {
        dateStyle: 'medium',
        timeStyle: 'medium',
        ...(timeZone ? { timeZone } : {}),
      }).format(date)
    } catch {
      return new Intl.DateTimeFormat(undefined, {
        dateStyle: 'medium',
        timeStyle: 'medium',
      }).format(date)
    }
  }

  /** A value the server could not determine, drawn as a dash rather than as an empty cell. */
  const orDash = (value: string | number | null | undefined): string =>
    value === null || value === undefined || value === '' ? '—' : String(value)

  const columns: TableColumnsType<SessionVisit> = [
    {
      title: t('sessions.columns.when'),
      key: 'when',
      width: 260,
      render: (_, visit) => (
        <Space size={8} wrap>
          <span>{formatMoment(visit.visitedAt)}</span>
          {visit.isCurrent && (
            <Tag color="processing" style={{ marginInlineEnd: 0 }}>
              {t('sessions.current')}
            </Tag>
          )}
        </Space>
      ),
    },
    {
      title: t('sessions.columns.ip'),
      dataIndex: 'ip',
      key: 'ip',
      width: 170,
      render: (value: string | null) => <Typography.Text code>{orDash(value)}</Typography.Text>,
    },
    {
      title: t('sessions.columns.country'),
      dataIndex: 'countryCode',
      key: 'country',
      width: 110,
      render: (value: string | null) => orDash(value),
    },
    {
      title: t('sessions.columns.network'),
      key: 'network',
      render: (_, visit) =>
        visit.organization === null && visit.autonomousSystemNumber === null ? (
          '—'
        ) : (
          <Space size={6} wrap>
            <span>{orDash(visit.organization)}</span>
            {visit.autonomousSystemNumber !== null && (
              <Typography.Text type="secondary">
                AS{visit.autonomousSystemNumber}
              </Typography.Text>
            )}
          </Space>
        ),
    },
    {
      title: t('sessions.columns.agent'),
      dataIndex: 'userAgent',
      key: 'agent',
      ellipsis: true,
      render: (value: string | null) => orDash(value),
    },
  ]

  return (
    <Space orientation="vertical" size={16} style={{ width: '100%' }}>
      <Card title={t(sectionKey)}>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('sessions.hint')}
        </Typography.Paragraph>
      </Card>

      {loadError ? (
        <Card>
          <Alert
            type="error"
            showIcon
            message={loadError}
            action={
              <Button
                size="small"
                onClick={() => setAttempt((value) => value + 1)}
                loading={loading}
              >
                {t('actions.retry')}
              </Button>
            }
          />
        </Card>
      ) : (
        <Card>
          <Table<SessionVisit>
            rowKey="id"
            columns={columns}
            dataSource={history?.items ?? []}
            loading={loading}
            scroll={{ x: 900 }}
            locale={{ emptyText: t('sessions.empty') }}
            pagination={{
              current: page,
              pageSize,
              total: history?.total ?? 0,
              showSizeChanger: false,
              showTotal: (total, range) =>
                t('sessions.range', { from: range[0], to: range[1], total }),
              onChange: (nextPage, nextPageSize) => {
                setPage(nextPage)
                setPageSize(nextPageSize)
              },
            }}
          />
        </Card>
      )}
    </Space>
  )
}
