import { useEffect, useMemo, useState } from 'react'
import { Alert, App, Button, Card, Space, Spin, Tabs, Typography } from 'antd'
import { useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useNavigate, useParams } from 'react-router-dom'
import { ArrowLeftOutlined } from '@ant-design/icons'
import { CONTENT_TYPE_TABS } from '../../features/bouquets/api'
import type { ContentTypeParam } from '../../features/bouquets/api'
import type { BouquetMember } from '../../features/bouquets/types'
import BouquetContentPicker from '../../features/bouquets/components/BouquetContentPicker'
import { useBouquetContent, useBouquets } from '../../features/bouquets/hooks/queries'
import { useSetBouquetContent } from '../../features/bouquets/hooks/mutations'
import { useProfile } from '../../features/profile/hooks/queries'
import { messageForError, successTextFor } from '../../utils/http'
import { useUnsavedChangesWarning } from '../../hooks/useUnsavedChangesWarning'
import { useSession } from '../../utils/sessionContext'
import { areaForRole } from '../../utils/session'

interface BouquetContentPageProps {
  /** The menu key the route carries; accepted and unread - the card names the package. */
  sectionKey: string
}

/**
 * What one kind looked like when the server last answered, plus the operator's draft on top
 * of it.
 *
 * `server` is the answer itself: every item of the kind split into what the bouquet holds
 * and what it does not. `memberIds` is the draft - the identifiers of the in-bouquet list in
 * the order the operator has arranged, or `null` while nothing has been dragged or moved.
 * The draft is identifiers rather than objects because the order is the only thing a draft
 * changes, and an item's name is not the operator's to edit here.
 */
interface KindState {
  server: {
    members: BouquetMember[]
    available: BouquetMember[]
  }
  memberIds: string[] | null
}

/**
 * The content of one bouquet - the four lists, one per kind, and the buttons that move
 * items between them.
 *
 * **Draft and save, not a write per click.** Every transfer, every move and the A-Z sort
 * change what is on screen only; "Save" sends the arrangement of each kind that changed, one
 * `PUT` per kind. That shape was chosen for the reason the categories table gives: an
 * operator arranging a package is doing one piece of work rather than one per click, and
 * only they know when it is finished. Leaving with unsaved moves warns, like every other
 * form here.
 *
 * **The order on screen is the order the server will hold.** The save sends the members in
 * exactly the sequence they are drawn in, and the server writes positions 1..N over them -
 * which is what makes the up/down buttons and the A-Z sort mean something after a reload.
 *
 * The four kinds are four tabs rather than four screens: they are one package seen four
 * ways, and a visitor comparing them should not have to navigate to do it.
 */
export default function BouquetContentPage(_props: BouquetContentPageProps) {
  const { t, i18n } = useTranslation()
  const { message } = App.useApp()
  const navigate = useNavigate()
  const session = useSession()
  const queryClient = useQueryClient()
  const { id = '' } = useParams()

  const bouquetsQuery = useBouquets()
  const bouquet = bouquetsQuery.data?.items.find((entry) => entry.id === id)

  // The bouquet pair is its own grant: reading the lists needs `viewBouquets` and changing
  // them needs `editBouquets`. The lists are still drawn without the write grant - a
  // read-only administrator may see what a package holds - and every control that would
  // change something is disabled with the reason.
  const granted = useProfile().data?.permissions ?? []
  const canEdit = granted.includes('editBouquets')

  const [active, setActive] = useState<ContentTypeParam>('videostream')
  const [kinds, setKinds] = useState<Partial<Record<ContentTypeParam, KindState>>>({})

  const contentQuery = useBouquetContent(id, active)
  const save = useSetBouquetContent()

  const locale = i18n.resolvedLanguage ?? i18n.language

  // A tab's state is seeded from the server's answer, and kept in step with it afterwards.
  //
  // Seeding once was the bug that made a saved arrangement disappear: leaving the screen and
  // coming back remounts the page, React Query serves the answer it cached *before* the save
  // while the fresh one is in flight, and a one-shot seed latched onto that stale answer - so
  // the lists showed what the bouquet held two saves ago. The effect therefore runs on every
  // new answer: the baseline is always replaced, and the draft is dropped only when it was
  // just the old baseline (nothing unsaved had been arranged on top of it). A draft the
  // operator actually changed survives a background re-read.
  useEffect(() => {
    const data = contentQuery.data

    if (!data) return

    setKinds((current) => {
      const state = current[active]

      if (!state) {
        return { ...current, [active]: { server: data, memberIds: null } }
      }

      const previousIds = state.server.members.map((member) => member.id)
      const draftIsClean = state.memberIds === null || sameIds(state.memberIds, previousIds)

      return {
        ...current,
        [active]: { server: data, memberIds: draftIsClean ? null : state.memberIds },
      }
    })
  }, [contentQuery.data, active])

  /** The member ids of one kind as the operator arranged them, or the server's order. */
  const memberIdsOf = (type: ContentTypeParam): string[] =>
    kinds[type]?.memberIds ?? (kinds[type]?.server.members ?? []).map((member) => member.id)

  /** The kinds whose draft no longer matches the answer it was seeded from. */
  const dirtyTypes = useMemo(
    () =>
      CONTENT_TYPE_TABS.filter((type) => {
        const state = kinds[type]

        if (!state || state.memberIds === null) return false

        const serverIds = state.server.members.map((member) => member.id)

        return (
          state.memberIds.length !== serverIds.length ||
          state.memberIds.some((memberId, index) => memberId !== serverIds[index])
        )
      }),
    [kinds],
  )

  const dirty = dirtyTypes.length > 0

  // Leaving the screen with unsaved moves is the same hazard as leaving a half-filled form.
  useUnsavedChangesWarning(dirty)

  /** Records the operator's new in-bouquet order for one kind. */
  const setMemberIds = (type: ContentTypeParam, memberIds: string[]) => {
    setKinds((current) => {
      const state = current[type]

      return state ? { ...current, [type]: { ...state, memberIds } } : current
    })
  }

  const saveDraft = async () => {
    if (!dirty || save.isPending) return

    // The arrangement being sent, captured before anything is re-read: the re-read below is
    // what confirms it, and the screen must keep showing that arrangement while it lands.
    const sent = dirtyTypes.map((type) => ({ type, contentIds: memberIdsOf(type) }))

    try {
      let last = null

      for (const entry of sent) {
        last = await save.mutateAsync({ id, type: entry.type, contentIds: entry.contentIds })
      }

      if (last) {
        message.success(successTextFor(last))
      }

      // The drafts are *kept*, not wiped: saving an arrangement does not change what the
      // operator arranged, and a screen that emptied itself on a successful save looked
      // like a failure. What each kind's draft is compared against becomes the arrangement
      // just written, so the save controls fall back to disabled while the lists keep
      // showing exactly what was saved.
      setKinds((current) => {
        const next: Partial<Record<ContentTypeParam, KindState>> = { ...current }

        for (const entry of sent) {
          const state = next[entry.type]

          if (!state) continue

          const byId = new Map(
            [...state.server.members, ...state.server.available].map((member) => [member.id, member]),
          )
          const members = entry.contentIds.flatMap((memberId) => {
            const member = byId.get(memberId)

            return member ? [member] : []
          })
          const memberSet = new Set(entry.contentIds)
          const available = [...byId.values()].filter((member) => !memberSet.has(member.id))

          next[entry.type] = { server: { members, available }, memberIds: null }

          // The cache is moved forward with the save, not re-read after it. A re-read is
          // asynchronous, and the moment between the answer and the refetch is exactly when
          // a visitor leaves the screen - the next visit then seeds its lists from whatever
          // the cache still says, which was the stale state this bug was made of.
          queryClient.setQueryData(['bouquetContent', id, entry.type], { members, available })
        }

        return next
      })

      // The table's counts changed with the save, so the list is stale by definition.
      void queryClient.invalidateQueries({ queryKey: ['bouquets'] })
    } catch (error) {
      message.error(messageForError(error))
    }
  }

  /**
   * Puts the server's answers back on screen.
   *
   * The drafts are dropped rather than re-derived, and the active tab is re-read: discarding
   * means "show me what the server holds", and the re-read is what makes that true for a
   * kind whose answer was fetched before the last save. The other kinds keep their loaded
   * answers and lose only their drafts, which is exactly what discarding them means.
   */
  const discard = () => {
    setKinds({})
    void contentQuery.refetch()
  }

  const backToList = () => {
    const area = session ? areaForRole(session.role) : '/admin'

    navigate(`${area}/bouquets`)
  }

  return (
    <Card
      className="page-card"
      // The card names the package rather than the menu entry: the visitor arrived from a
      // row of the bouquets table, and the page must say which package that row was.
      title={t('bouquets.content.title', { name: bouquet?.name ?? '' })}
      extra={
        <Space wrap>
          <Button icon={<ArrowLeftOutlined />} onClick={backToList}>
            {t('bouquets.content.back')}
          </Button>
          <Button disabled={!dirty || save.isPending} onClick={discard}>
            {t('bouquets.discardOrder')}
          </Button>
          <Button
            type="primary"
            disabled={!dirty || save.isPending || !canEdit}
            loading={save.isPending}
            onClick={() => void saveDraft()}
          >
            {t('bouquets.content.save')}
          </Button>
        </Space>
      }
    >
      {!canEdit && (
        <Alert type="info" showIcon style={{ marginBottom: 16 }} message={t('bouquets.noPermission')} />
      )}

      <Tabs
        activeKey={active}
        onChange={(key) => setActive(key as ContentTypeParam)}
        items={CONTENT_TYPE_TABS.map((type) => ({
          key: type,
          label: t(`bouquets.content.tabs.${type}`),
          children: (
            <ContentTab
              listsFor={tabLists(kinds[type], memberIdsOf(type), type === active ? contentQuery : null)}
              loading={type === active && contentQuery.isFetching}
              error={type === active && contentQuery.isError ? messageForError(contentQuery.error) : null}
              onRetry={() => void contentQuery.refetch()}
              locale={locale}
              canEdit={canEdit}
              onMembers={(memberIds) => setMemberIds(type, memberIds)}
            />
          ),
        }))}
      />
    </Card>
  )
}

/**
 * One tab: the two lists for one kind, or the spinner, or the refusal.
 *
 * A component of its own rather than a block of JSX because the four tabs each need their
 * own identity in the tree - a tab that swapped its contents in place would keep the
 * previous kind's selection ticks.
 */
const ContentTab = ({
  listsFor,
  loading,
  error,
  onRetry,
  locale,
  canEdit,
  onMembers,
}: {
  listsFor: { members: BouquetMember[]; available: BouquetMember[] } | null
  loading: boolean
  error: string | null
  onRetry: () => void
  locale: string
  canEdit: boolean
  onMembers: (memberIds: string[]) => void
}) => {
  const { t } = useTranslation()

  if (error) {
    return (
      <Alert
        type="error"
        showIcon
        message={error}
        action={
          <Button size="small" onClick={onRetry}>
            {t('actions.retry')}
          </Button>
        }
      />
    )
  }

  if (!listsFor) {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', padding: 48 }}>
        <Spin spinning={loading} />
      </div>
    )
  }

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
        {t('bouquets.content.hint')}
      </Typography.Paragraph>

      <BouquetContentPicker
        members={listsFor.members}
        available={listsFor.available}
        disabled={!canEdit}
        locale={locale}
        // The picker reports the in-bouquet list, because only that list has an order worth
        // keeping; the other side is derived from it and the same universe of items.
        onChange={(members) => onMembers(members.map((member) => member.id))}
        labels={{
          members: t('bouquets.content.members'),
          available: t('bouquets.content.available'),
          emptyMembers: t('bouquets.content.emptyMembers'),
          emptyAvailable: t('bouquets.content.emptyAvailable'),
          emptyFiltered: t('bouquets.content.emptyFiltered'),
          add: t('bouquets.content.add'),
          remove: t('bouquets.content.remove'),
          moveUp: t('bouquets.content.moveUp'),
          moveDown: t('bouquets.content.moveDown'),
          sortAz: t('bouquets.content.sortAz'),
          filterMembers: t('bouquets.content.filterMembers'),
          filterAvailable: t('bouquets.content.filterAvailable'),
          filterCategory: t('bouquets.content.filterCategory'),
          selectAll: t('bouquets.content.selectAll'),
          clearSelection: t('bouquets.content.clearSelection'),
          noPermission: t('bouquets.noPermission'),
        }}
      />
    </Space>
  )
}

/** Whether two member lists name the same items in the same order. */
const sameIds = (left: readonly string[], right: readonly string[]): boolean =>
  left.length === right.length && left.every((memberId, index) => memberId === right[index])

/**
 * One kind's two lists as the picker draws them: the members in the operator's order, and
 * everything else of that kind, by name.
 *
 * The available side is **derived** rather than drafted. The two lists are one set split in
 * two - the server answers with every item of the kind, some in the bouquet and the rest
 * out - so an item that left the bouquet must reappear on the right without a second copy
 * of it being tracked. Sorting it by name keeps the right-hand list stable while items come
 * and go.
 *
 * @param state The kind's server answer and draft, when it has been opened.
 * @param memberIds The member ids in screen order.
 * @param query The active tab's query, used while the answer is still on its way.
 */
const tabLists = (
  state: KindState | undefined,
  memberIds: string[],
  query: { data?: { members: BouquetMember[]; available: BouquetMember[] } } | null,
): { members: BouquetMember[]; available: BouquetMember[] } | null => {
  if (!state) {
    return query?.data ? { members: query.data.members, available: query.data.available } : null
  }

  const byId = new Map(
    [...state.server.members, ...state.server.available].map((member) => [member.id, member]),
  )
  const memberSet = new Set(memberIds)

  const members = memberIds.flatMap((memberId) => {
    const member = byId.get(memberId)

    return member ? [member] : []
  })

  const available = [...byId.values()]
    .filter((member) => !memberSet.has(member.id))
    .sort((left, right) => left.name.localeCompare(right.name))

  return { members, available }
}
