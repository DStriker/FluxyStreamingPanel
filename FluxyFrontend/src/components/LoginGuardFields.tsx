import { useMemo } from 'react'
import { Button, Form, InputNumber, Select, Switch, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { countryOptions } from '../lib/countries'
import { isIpOrCidr } from '../lib/ip'
import { LOGIN_GUARD_MAX_IPS } from '../lib/policy'

/** The "use my current network" button: present only where it means the right thing. */
export interface FillCurrentOption {
  /** Looks the caller's own address up and writes it into the three lists. */
  onClick: () => void
  /** The lookup is in flight. */
  lookingUp: boolean
  /** Further reasons to hold the button off - the profile's own "a save is running". */
  disabled?: boolean
}

interface LoginGuardFieldsProps {
  /**
   * Whether the allow lists are enforced right now. While they are off the fields below
   * are **disabled rather than hidden**: a field that is not rendered is a field whose
   * value `onFinish` never hands over, and a save through it would empty the very lists it
   * was meant to keep.
   *
   * A value rather than a watch taken here, because the watcher answers `undefined` until
   * its own effect runs - and always under `renderToStaticMarkup`, where effects never
   * run. Each owner watches its own form instance and passes the stored value as the floor
   * beneath it, so this component stays free of the instance it is being rendered into.
   */
  protectionEnabled: boolean
  /**
   * The lookup button, when this flow may offer it. The profile offers it because the
   * address worth allowing is the visitor's own; the administrator's form does not - the
   * caller's network is the wrong network for an account that is not theirs, and a button
   * that filled the lists with *the admin's* address would lock the account into the
   * admin's office.
   */
  fillCurrent?: FillCurrentOption
}

/**
 * The five fields of the login guard: the two switches, the three allow lists, and the
 * hints that say what each switch does.
 *
 * Split out of `LoginGuardCard` because two pages edit the same five fields with the same
 * rules - the profile's card and the administrator's add/edit form - and the rules are the
 * kind that will not survive being written twice: the country upper-cased into an option
 * list that can draw it, the address validator naming the entry that broke it, the lists
 * disabled rather than unmounted while their switch is off. What differs between the two
 * flows is everything *around* these fields, and that is what the card keeps: it owns the
 * form, the current password, the confirmation step and the two tokens; the admin form
 * owns the rest of the account.
 *
 * It must be rendered inside an antd `<Form>` - the items take their field context from
 * it - and the owner is the one that watches `geoProtectionEnabled` and hands the answer
 * down as `protectionEnabled`.
 */
export default function LoginGuardFields({
  protectionEnabled,
  fillCurrent,
}: LoginGuardFieldsProps) {
  const { t, i18n } = useTranslation()

  /**
   * The country names for the language in use - the codes underneath never change, only
   * how they are written. Memoised on the language alone: this list is built once per
   * language, and the form re-renders on every keystroke of every other field.
   */
  const countryList = useMemo(() => countryOptions(i18n.language), [i18n.language])

  return (
    <>
      <Form.Item
        name="geoProtectionEnabled"
        label={t('profile.guardProtection')}
        valuePropName="checked"
      >
        <Switch />
      </Form.Item>

      <Typography.Paragraph type="secondary" style={{ marginTop: -8 }}>
        {t('profile.guardProtectionHint')}
      </Typography.Paragraph>

      {fillCurrent && (
        <Form.Item>
          {/* Off with the lists it fills: a button that writes into three greyed-out
              inputs reports success over changes the visitor cannot see. */}
          <Button
            onClick={fillCurrent.onClick}
            loading={fillCurrent.lookingUp}
            disabled={fillCurrent.disabled || !protectionEnabled}
          >
            {t('profile.guardUseCurrent')}
          </Button>
        </Form.Item>
      )}

      <Form.Item
        name="allowedIps"
        label={t('profile.guardIps', { max: LOGIN_GUARD_MAX_IPS })}
        rules={[
          {
            // Named rather than generic: the message carries the entry that broke the
            // rule, and a tags input has no other way to say which of its tags it was.
            validator: (_: unknown, ips: string[]) => {
              const invalid = (ips ?? []).find((entry) => !isIpOrCidr(entry.trim()))
              return invalid
                ? Promise.reject(new Error(t('profile.guardIpsInvalid', { entry: invalid })))
                : Promise.resolve()
            },
          },
        ]}
      >
        <Select
          mode="tags"
          tokenSeparators={[',', ' ', ';']}
          maxCount={LOGIN_GUARD_MAX_IPS}
          placeholder={t('profile.guardIpsPlaceholder')}
          aria-label={t('profile.guardIps', { max: LOGIN_GUARD_MAX_IPS })}
          disabled={!protectionEnabled}
        />
      </Form.Item>

      <Form.Item name="allowedCountry" label={t('profile.guardCountry')}>
        {/* A selector rather than two typed letters, so the visitor picks a country
            the server can act on instead of guessing a code - and clears it to "no
            restriction", which is what an empty value means on the contract. No rule:
            a `Select` offers no value that is not already one of the options. */}
        <Select
          showSearch
          allowClear
          options={countryList}
          // Names, not codes: searching a code only was what the field did by hand,
          // and a name is what a visitor knows how to type.
          optionFilterProp="label"
          placeholder={t('profile.guardCountryPlaceholder')}
          aria-label={t('profile.guardCountry')}
          style={{ width: '100%', maxWidth: 320 }}
          disabled={!protectionEnabled}
        />
      </Form.Item>

      <Form.Item name="allowedAutonomousSystemNumber" label={t('profile.guardAsn')}>
        <InputNumber
          min={1}
          precision={0}
          placeholder="AS12345"
          style={{ width: '100%', maxWidth: 220 }}
          disabled={!protectionEnabled}
        />
      </Form.Item>

      <Form.Item
        name="bindSessionToIp"
        label={t('profile.guardBind')}
        valuePropName="checked"
      >
        <Switch />
      </Form.Item>

      <Typography.Paragraph type="secondary" style={{ marginTop: -8 }}>
        {t('profile.guardBindHint')}
      </Typography.Paragraph>
    </>
  )
}
