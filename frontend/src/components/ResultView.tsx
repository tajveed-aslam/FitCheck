import { useMemo, useState } from 'react'
import type { Analysis, Importance } from '../api'
import { copyText, downloadFile, formatDate, slugify } from '../utils'
import HighlightedText, { type HighlightTerm } from './HighlightedText'
import { scoreBand } from '../score'
import ScoreGauge from './ScoreGauge'

type Tab = 'jd' | 'cv'

const importanceLabel: Record<Importance, string> = { high: 'Required', medium: 'Preferred', low: 'Minor' }

function toMarkdown(a: Analysis): string {
  const lines = [
    `# FitCheck: ${a.title}${a.company ? ` at ${a.company}` : ''}`,
    '',
    `**Match score:** ${a.matchScore}% (${scoreBand(a.matchScore).label})`,
    `**CV:** ${a.fileName} · ${formatDate(a.createdAt)}`,
    '',
    a.summary,
    '',
    '## Matched skills',
    ...a.matchedSkills.map((s) => `- ${s}`),
    '',
    '## Missing keywords',
    ...a.missingKeywords.map((m) => `- ${m.keyword} (${importanceLabel[m.importance].toLowerCase()})`),
    '',
    '## How to improve',
    ...a.tips.flatMap((t, i) => [`${i + 1}. **${t.title}**: ${t.detail}`]),
    '',
  ]
  return lines.join('\n')
}

interface Props {
  analysis: Analysis
  onDelete: () => Promise<void>
}

export default function ResultView({ analysis: a, onDelete }: Props) {
  const [tab, setTab] = useState<Tab>('jd')
  const [deleting, setDeleting] = useState(false)
  const [copied, setCopied] = useState(false)

  const jdTerms = useMemo<HighlightTerm[]>(
    () => [
      ...a.matchedSkills.map((term) => ({ term, kind: 'matched' as const })),
      ...a.missingKeywords.map((m) => ({ term: m.keyword, kind: 'missing' as const })),
    ],
    [a],
  )
  const cvTerms = useMemo<HighlightTerm[]>(() => a.matchedSkills.map((term) => ({ term, kind: 'matched' as const })), [a])

  async function remove() {
    if (!window.confirm(`Delete the analysis for "${a.title}"?`)) return
    setDeleting(true)
    try {
      await onDelete()
    } finally {
      setDeleting(false)
    }
  }

  async function copyReport() {
    if (await copyText(toMarkdown(a))) {
      setCopied(true)
      window.setTimeout(() => setCopied(false), 1800)
    }
  }

  return (
    <section className="result">
      <header className="result-header">
        <div>
          <h1>{a.title}</h1>
          <p className="result-meta">
            {a.company && <span className="company">{a.company}</span>}
            <span className="mono">{a.fileName}</span>
            <span>{formatDate(a.createdAt)}</span>
            <span>{a.model} · {(a.durationMs / 1000).toFixed(1)} s</span>
          </p>
        </div>
        <div className="result-actions">
          <button className="btn btn-secondary btn-sm" onClick={() => void copyReport()}>{copied ? '✓ Copied' : 'Copy report'}</button>
          <button
            className="btn btn-secondary btn-sm"
            onClick={() => downloadFile(`fitcheck-${slugify(a.title)}.md`, toMarkdown(a), 'text/markdown')}
          >
            Download
          </button>
          <button className="btn btn-ghost btn-sm" onClick={() => void remove()} disabled={deleting}>Delete</button>
        </div>
      </header>

      <div className="score-row">
        <div className="card score-card">
          <ScoreGauge score={a.matchScore} />
        </div>
        <div className="card summary-card">
          <h2>Summary</h2>
          <p>{a.summary || 'No summary was returned.'}</p>
          <div className="stat-row">
            <span className="stat stat-matched"><strong>{a.matchedSkills.length}</strong> matched</span>
            <span className="stat stat-missing"><strong>{a.missingKeywords.length}</strong> missing</span>
            <span className="stat stat-required">
              <strong>{a.missingKeywords.filter((m) => m.importance === 'high').length}</strong> required gaps
            </span>
          </div>
        </div>
      </div>

      <div className="skills-grid">
        <div className="card skills-card">
          <h2><span className="dot dot-matched" /> Matched skills</h2>
          {a.matchedSkills.length === 0 ? (
            <p className="muted small">No clear matches were found.</p>
          ) : (
            <ul className="tag-list">
              {a.matchedSkills.map((s) => <li key={s} className="tag tag-matched">{s}</li>)}
            </ul>
          )}
        </div>
        <div className="card skills-card">
          <h2><span className="dot dot-missing" /> Missing keywords</h2>
          {a.missingKeywords.length === 0 ? (
            <p className="muted small">Nothing important is missing.</p>
          ) : (
            <ul className="tag-list">
              {a.missingKeywords.map((m) => (
                <li key={m.keyword} className={`tag tag-missing importance-${m.importance}`} title={importanceLabel[m.importance]}>
                  {m.keyword}
                  <span className="tag-badge">{importanceLabel[m.importance]}</span>
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>

      <div className="card tips-card">
        <h2>How to improve your fit</h2>
        <ol className="tips">
          {a.tips.map((t, i) => (
            <li key={i}>
              <span className="tip-number">{i + 1}</span>
              <div>
                <strong>{t.title}</strong>
                <p>{t.detail}</p>
              </div>
            </li>
          ))}
        </ol>
      </div>

      <div className="card text-card">
        <div className="tabs" role="tablist">
          <button role="tab" aria-selected={tab === 'jd'} className={tab === 'jd' ? 'active' : ''} onClick={() => setTab('jd')}>
            Job description, highlighted
          </button>
          <button role="tab" aria-selected={tab === 'cv'} className={tab === 'cv' ? 'active' : ''} onClick={() => setTab('cv')}>
            Your CV (extracted text)
          </button>
        </div>
        <div className="legend">
          <span><mark className="hl hl-matched">matched</mark> in your CV</span>
          {tab === 'jd' && <span><mark className="hl hl-missing">missing</mark> from your CV</span>}
          {tab === 'cv' && <span className="muted">This is the text FitCheck read from your file.</span>}
        </div>
        {tab === 'jd'
          ? <HighlightedText text={a.jobDescription} terms={jdTerms} />
          : <HighlightedText text={a.cvText} terms={cvTerms} />}
      </div>
    </section>
  )
}
