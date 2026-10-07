import { useState } from 'react'
import { Link } from 'react-router-dom'
import type { AnalysisSummary } from '../api'
import { formatDate } from '../utils'
import { scoreBand } from '../score'

interface Props {
  items: AnalysisSummary[] | null
  error: string | null
  activeId?: string
  onDelete: (id: string) => Promise<void>
}

export default function HistoryList({ items, error, activeId, onDelete }: Props) {
  const [deleting, setDeleting] = useState<string | null>(null)

  async function remove(id: string, title: string) {
    if (!window.confirm(`Delete the analysis for "${title}"?`)) return
    setDeleting(id)
    try {
      await onDelete(id)
    } finally {
      setDeleting(null)
    }
  }

  return (
    <section className="history" aria-label="Analysis history">
      <h2 className="history-heading">History</h2>
      {error && <p className="error-text small">{error}</p>}
      {!error && items === null && <p className="muted small"><span className="spinner" /> Loading…</p>}
      {items?.length === 0 && <p className="muted small">Your analyses will appear here.</p>}
      <ul>
        {items?.map((a) => (
          <li key={a.id} className={a.id === activeId ? 'active' : ''}>
            <Link to={`/app/a/${a.id}`} className="history-item">
              <span className={`score-pill tone-${scoreBand(a.matchScore).tone}`}>{a.matchScore}%</span>
              <span className="history-text">
                <span className="history-title">{a.title}</span>
                <span className="history-meta">
                  {a.company ? `${a.company} · ` : ''}{formatDate(a.createdAt)}
                </span>
              </span>
            </Link>
            <button
              className="icon-button history-delete"
              aria-label={`Delete ${a.title}`}
              disabled={deleting === a.id}
              onClick={() => void remove(a.id, a.title)}
            >
              ×
            </button>
          </li>
        ))}
      </ul>
    </section>
  )
}
