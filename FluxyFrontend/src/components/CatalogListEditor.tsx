import { Form, Select } from 'antd'
import { Button } from 'antd'
import { PlusOutlined } from '@ant-design/icons'
import { ArrowDownOutlined, ArrowUpOutlined, DeleteOutlined } from '@ant-design/icons'
import { hinted } from '../utils/hinted'

/**
 * An ordered list of entries chosen from a catalogue, with the move and remove buttons on
 * every row and an "add" button underneath.
 *
 * The sibling of `SourceListEditor`, for the lists that name **rows** rather than text: a
 * playlist stream plays a sequence of on-demand videos, and the operator picks each one by
 * name. Typing a uuid was the field being "alive" in name only - nobody knows one by sight,
 * and the endpoint refused anything that was not one - so this control is the same ordered
 * editor with a `Select` where the text box was.
 *
 * Shared rather than feature-local for the reason `SourceListEditor` gives: a feature may not
 * import another feature, so a control that two forms draw belongs to the shared tree. It
 * knows nothing about streams or videos - it is a list of chosen values in an order.
 *
 * **The order is the whole point.** The buttons move one row at a time and the list is sent
 * back in the order drawn, because the relay follows that order and starts over at the end.
 */
interface CatalogListEditorProps {
  /** Where the list lives in the form: `['vodItems']`. */
  name: string[]
  /** The catalogue the picker offers, already keyed and labelled. */
  options: { value: string; label: string }[]
  /** Prompt inside an empty picker. */
  placeholder: string
  /** Text drawn when the catalogue holds nothing, which is a different fact from "no match". */
  emptyCatalog: string
  /** Whether the catalogue read is still in flight. */
  loading?: boolean
  /** The button names and the whole-list rule, since only the page names the list. */
  labels: { up: string; down: string; remove: string; add: string; required: string }
  /** Whether the visitor may edit at all. */
  disabled?: boolean
}

/**
 * The ordered list itself.
 *
 * `Form.List` carries the whole-list rule - at least one entry has to be chosen - because
 * that is a question about the list rather than about any one row, and the server asks the
 * same question with the same answer: a playlist stream with nothing in it is refused, so
 * this is the same rule told before the request rather than explained after one.
 */
export default function CatalogListEditor({
  name,
  options,
  placeholder,
  emptyCatalog,
  loading = false,
  labels,
  disabled = false,
}: CatalogListEditorProps) {
  const listRule = {
    validator: (_: unknown, value: string[] | undefined) => {
      const filled = (value ?? []).some((entry) => (entry ?? '').trim() !== '')

      return filled ? Promise.resolve() : Promise.reject(new Error(labels.required))
    },
  }

  return (
    <Form.List name={name} rules={[listRule]}>
      {(fields, { add, remove, move }, { errors }) => (
        <>
          {fields.map((field, index) => (
            <div key={field.key} style={{ display: 'flex', gap: 8, alignItems: 'flex-start' }}>
              <Form.Item name={field.name} style={{ flex: 1, marginBottom: 8 }}>
                <Select
                  showSearch
                  allowClear
                  optionFilterProp="label"
                  placeholder={placeholder}
                  options={options}
                  loading={loading}
                  disabled={disabled}
                  notFoundContent={emptyCatalog}
                />
              </Form.Item>

              <RowActions
                labels={labels}
                disabled={disabled}
                first={index === 0}
                last={index === fields.length - 1}
                handlers={{
                  up: () => move(index, index - 1),
                  down: () => move(index, index + 1),
                  remove: () => remove(index),
                }}
              />
            </div>
          ))}

          {/* Add appends at the end, which is what "an empty row below" means: the newest
              entry is the last thing the playlist reaches, so it lands where it will play. */}
          <Button
            type="dashed"
            block
            icon={<PlusOutlined />}
            disabled={disabled}
            onClick={() => add('')}
          >
            {labels.add}
          </Button>

          <Form.ErrorList errors={errors} />
        </>
      )}
    </Form.List>
  )
}

/** The three per-row buttons as one strip, so the row beside them reads as the thing they act on. */
const RowActions = ({
  labels,
  disabled,
  first,
  last,
  handlers,
}: {
  labels: { up: string; down: string; remove: string }
  disabled: boolean
  first: boolean
  last: boolean
  handlers: { up: () => void; down: () => void; remove: () => void }
}) => (
  <div style={{ display: 'flex', gap: 2 }}>
    {hinted(
      labels.up,
      <Button
        type="text"
        size="small"
        icon={<ArrowUpOutlined />}
        disabled={disabled || first}
        onClick={handlers.up}
      />,
    )}
    {hinted(
      labels.down,
      <Button
        type="text"
        size="small"
        icon={<ArrowDownOutlined />}
        disabled={disabled || last}
        onClick={handlers.down}
      />,
    )}
    {hinted(
      labels.remove,
      <Button
        type="text"
        size="small"
        danger
        icon={<DeleteOutlined />}
        disabled={disabled}
        onClick={handlers.remove}
      />,
    )}
  </div>
)
