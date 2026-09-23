import { CheckIcon, MonitorIcon, MoonIcon, SunIcon } from 'lucide-react'
import { useTheme } from 'next-themes'

import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { useT } from '@/lib/i18n'

// The icon is the part that is not language. The word beside it comes from the dictionary, which
// is also what the appearance card on the profile screen reads — one wording for one setting.
const options = [
  { value: 'light', icon: SunIcon },
  { value: 'dark', icon: MoonIcon },
  { value: 'system', icon: MonitorIcon },
] as const

/**
 * Three choices rather than a two-way flip, because "system" is a real answer: an operator who
 * has their OS on a schedule wants the console to follow it rather than to be re-toggled twice a
 * day.
 */
export function ThemeToggle() {
  const { theme, setTheme } = useTheme()
  const { theme: text } = useT()

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={<Button variant="ghost" size="icon-sm" aria-label={text.change} />}
      >
        {/* Driven by the `.dark` class rather than by `resolvedTheme`, so the right icon is on
            screen in the first painted frame instead of after next-themes resolves. */}
        <SunIcon className="rotate-0 scale-100 transition-transform duration-200 dark:-rotate-90 dark:scale-0" />
        <MoonIcon className="absolute rotate-90 scale-0 transition-transform duration-200 dark:rotate-0 dark:scale-100" />
      </DropdownMenuTrigger>

      <DropdownMenuContent align="end" className="w-36">
        {options.map((option) => (
          <DropdownMenuItem key={option.value} onClick={() => setTheme(option.value)}>
            <option.icon />
            {text.options[option.value].label}
            {theme === option.value && <CheckIcon className="text-muted-foreground ml-auto" />}
          </DropdownMenuItem>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
