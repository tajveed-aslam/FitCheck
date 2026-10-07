import { useMemo, type ReactNode } from 'react'

export type HighlightKind = 'matched' | 'missing'

export interface HighlightTerm {
  term: string
  kind: HighlightKind
}

const escape = (s: string) => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
const isWordChar = (c: string) => /[\p{L}\p{N}]/u.test(c)

/** Same rules as the API's keyword check: whole terms, case-insensitive, flexible whitespace. */
function termPattern(term: string): string {
  const body = term.trim().split(/\s+/).map(escape).join('\\s+')
  const before = isWordChar(term.trim()[0]) ? '(?<![\\p{L}\\p{N}])' : ''
  const after = isWordChar(term.trim().slice(-1)) ? '(?![\\p{L}\\p{N}])' : ''
  return before + body + after
}

export default function HighlightedText({ text, terms }: { text: string; terms: HighlightTerm[] }) {
  const parts = useMemo(() => {
    const usable = terms.filter((t) => t.term.trim().length > 0)
    if (usable.length === 0) return [text]

    // Longest first so "REST APIs" wins over "APIs"; missing terms win ties so gaps stay visible.
    const sorted = [...usable].sort((a, b) => b.term.length - a.term.length || (a.kind === 'missing' ? -1 : 1))
    const regex = new RegExp(sorted.map((t) => `(${termPattern(t.term)})`).join('|'), 'giu')

    const out: ReactNode[] = []
    let last = 0
    for (const match of text.matchAll(regex)) {
      const index = match.index ?? 0
      if (match[0].length === 0) continue
      if (index > last) out.push(text.slice(last, index))
      const groupIndex = match.slice(1).findIndex((g) => g !== undefined)
      const kind = sorted[groupIndex]?.kind ?? 'matched'
      out.push(
        <mark key={index} className={`hl hl-${kind}`} title={kind === 'missing' ? 'Missing from your CV' : 'Found in your CV'}>
          {match[0]}
        </mark>,
      )
      last = index + match[0].length
    }
    if (last < text.length) out.push(text.slice(last))
    return out
  }, [text, terms])

  return <div className="highlighted-text">{parts}</div>
}
