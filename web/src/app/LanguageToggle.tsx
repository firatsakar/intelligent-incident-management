import { CheckIcon, LanguagesIcon } from 'lucide-react'

import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { languageName, languages, useLanguage, useT } from '@/lib/i18n'

/**
 * Beside the theme switch, because it is the same kind of control: a display preference held in
 * this browser, belonging to whoever is looking rather than to whoever is signed in.
 *
 * It sits in the header rather than only in the settings screen because the login card renders
 * before there is a session to have settings for, and somebody who cannot read the sign-in prompt
 * cannot reach a preferences page behind it.
 *
 * Each option is named in its own language. "Turkish" is no help to the person hunting for
 * Türkçe — that word is the one item on a two-item list they can be certain of, and a list this
 * short does not need a flag to say the same thing less precisely.
 */
export function LanguageToggle() {
  const { language, setLanguage } = useLanguage()
  const { language: text } = useT()

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={<Button variant="ghost" size="icon-sm" aria-label={text.change} />}
      >
        <LanguagesIcon />
      </DropdownMenuTrigger>

      <DropdownMenuContent align="end" className="w-36">
        {languages.map((option) => (
          <DropdownMenuItem key={option} onClick={() => setLanguage(option)}>
            {languageName[option]}
            {language === option && <CheckIcon className="text-muted-foreground ml-auto" />}
          </DropdownMenuItem>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
