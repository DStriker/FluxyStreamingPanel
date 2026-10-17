import { Input } from 'antd'
import { useTranslation } from 'react-i18next'

/**
 * The search box the server-paged tables put in their card's `extra`, wired to the three
 * search values `useServerTable` returns.
 *
 * The rule that makes it a component rather than markup: **the emptied box empties the list
 * straight away.** Waiting for Enter would leave the visitor looking at an empty field with
 * the old filter still applied, pressing the one key they just finished not typing into; and
 * the clear icon of `allowClear` arrives as this same event, so it is covered by the same
 * branch. The keys are passed as keys and translated here - the component knows how to draw
 * a search box, not what this page searches.
 */
export default function TableSearch({
  value,
  onChange,
  onSearch,
  labelKey,
  placeholderKey,
  width,
}: {
  /** What is typed; `useServerTable` keeps it apart from what the server was last asked. */
  value: string
  /** Called on every keystroke. */
  onChange: (value: string) => void
  /** Called on Enter, on the clear icon, and on the box becoming empty. */
  onSearch: (value: string) => void
  /** The accessible name, this page's own. */
  labelKey: string
  /** The example the box shows, this page's own. */
  placeholderKey: string
  /** The width this page's toolbar gives it. */
  width: number
}) {
  const { t } = useTranslation()

  return (
    <Input.Search
      allowClear
      aria-label={t(labelKey)}
      placeholder={t(placeholderKey)}
      style={{ width }}
      value={value}
      onChange={(event) => {
        onChange(event.target.value)

        if (event.target.value === '') onSearch('')
      }}
      onSearch={onSearch}
    />
  )
}
