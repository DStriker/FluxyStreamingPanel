import { Button, Form, Input } from 'antd'
import {
  ArrowDownOutlined,
  ArrowUpOutlined,
  DeleteOutlined,
  PlusOutlined,
} from '@ant-design/icons'
import { hinted } from '../utils/hinted'

/**
 * The three per-entry buttons as one strip, so the entry beside them reads as the thing
 * they act on.
 *
 * The labels arrive as arguments rather than being read here: only the page knows what a
 * column of this list is called, and a translation key is not something a component can
 * hold without inventing a prefix for every caller. Same reason `useColumnWidths` takes
 * its accessible name as an argument rather than building one.
 */
const EntryActions = ({
  labels,
  handlers,
  first,
  last,
}: {
  labels: { up: string; down: string; remove: string }
  handlers: { up: () => void; down: () => void; remove: () => void }
  first: boolean
  last: boolean
}) => (
  <div style={{ display: 'flex', gap: 2 }}>
    {hinted(
      labels.up,
      <Button
        type="text"
        size="small"
        icon={<ArrowUpOutlined />}
        disabled={first}
        onClick={handlers.up}
      />,
    )}
    {hinted(
      labels.down,
      <Button
        type="text"
        size="small"
        icon={<ArrowDownOutlined />}
        disabled={last}
        onClick={handlers.down}
      />,
    )}
    {hinted(
      labels.remove,
      <Button type="text" size="small" danger icon={<DeleteOutlined />} onClick={handlers.remove} />,
    )}
  </div>
)

/**
 * The ordered list of one thing a stream relays: its sources, or the files it plays.
 *
 * One text field per entry with three buttons beside it - up, down, delete - and an add
 * button underneath, because that is what an operator editing a relay order is doing:
 * moving a line up promotes a source from fallback to primary, and there is no other
 * control that expresses it. **The order on the screen is the order a save sends back**,
 * which is why nothing here re-sorts or de-duplicates: the service replaces the stored list
 * wholesale in the order the request names, so the two orders are one fact written twice
 * and only one of them is allowed to decide it.
 *
 * A list starts with **one empty field** rather than none - an empty box is the state a
 * visitor arrives at, and a list with no fields at all would be a control with nothing in
 * it to press its own buttons on. Add appends below (the newest entry is the last
 * fallback, which is where you would add one), and both up and down are disabled at their
 * own end rather than hidden: a button that disappears tells the visitor nothing about why
 * there is no second entry to reach for.
 *
 * The `Form.List` carries the **whole-list rule** - at least one entry has to be filled in
 * - because that is a question about the list and not about any one of its entries, so the
 * entries themselves stay free to be blank while the visitor is still filling them out. The
 * server asks the same question with the same answer (`VideoStreamService.HasUsableEntries`
 * and its audio twin), so this is the same rule told before the request rather than
 * explained after one.
 *
 * Blank entries are dropped rather than sent: an entry nobody filled in is not a source,
 * and the server refuses a list containing *any* blank entry rather than silently ignoring
 * it. What is left is then either empty - which is exactly the refusal the rule above gives,
 * with the list named - or a clean list the columns can hold.
 *
 * It lives in `src/components/` rather than in either stream feature because it knows
 * nothing about streams: it is a list of text fields with three buttons and a whole-list
 * rule, and both the video and the audio forms draw it. A feature may not import another
 * feature, so the one control they share belongs to the shared tree.
 */
interface SourceListEditorProps {
  /** Where the list lives in the form: `['sources']` or `['files']`. */
  name: string[]
  /** What one entry is a field for - a source address, a file path. */
  placeholder: string
  /** The most characters one entry may hold, mirroring the column on the other side. */
  max: number
  /** The three button names and the whole-list rule, since only the page names the list. */
  labels: { up: string; down: string; remove: string; add: string; required: string }
}

export default function SourceListEditor({ name, placeholder, max, labels }: SourceListEditorProps) {
  /** The whole-list rule. The sentence names the list, because no one entry is at fault. */
  const listRule = {
    validator: (_: unknown, value: string[] | undefined) => {
      const filled = (value ?? []).some((entry) => entry.trim() !== '')

      return filled ? Promise.resolve() : Promise.reject(new Error(labels.required))
    },
  }

  return (
    <Form.List name={name} rules={[listRule]}>
      {(fields, { add, remove, move }, { errors }) => (
        <>
          {fields.map((field, index) => (
            <div key={field.key} style={{ display: 'flex', gap: 8, alignItems: 'flex-start' }}>
              <Form.Item
                name={field.name}
                style={{ flex: 1, marginBottom: 8 }}
                // `max` rather than `length`, and no `required`: an entry may be blank while
                // the visitor is filling out another one, but an entry that *is* filled in has
                // to fit the column it will be written to - otherwise the refusal would arrive
                // from the database instead of from the rule.
                rules={[{ max }]}
              >
                <Input placeholder={placeholder} allowClear />
              </Form.Item>

              <EntryActions
                labels={labels}
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

          {/* Add appends at the end, which is what "an empty field below" means: the newest
              entry is the last fallback, so it lands where it will play. */}
          <Button type="dashed" block icon={<PlusOutlined />} onClick={() => add('')}>
            {labels.add}
          </Button>

          <Form.ErrorList errors={errors} />
        </>
      )}
    </Form.List>
  )
}
