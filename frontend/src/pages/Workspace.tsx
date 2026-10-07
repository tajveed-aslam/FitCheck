import { useCallback, useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { api, ApiError, type Analysis, type AnalysisSummary } from '../api'
import { useAuth } from '../auth-context'
import AnalyzeForm from '../components/AnalyzeForm'
import Brand from '../components/Brand'
import HistoryList from '../components/HistoryList'
import ResultView from '../components/ResultView'

interface Loaded {
  id: string
  analysis: Analysis | null
  error: string | null
}

export default function Workspace() {
  const { id } = useParams()
  const { session, logout } = useAuth()
  const navigate = useNavigate()

  const [history, setHistory] = useState<AnalysisSummary[] | null>(null)
  const [historyError, setHistoryError] = useState<string | null>(null)
  // The last analysis fetched (or just created), keyed by id; what's shown is derived from the route id.
  const [loaded, setLoaded] = useState<Loaded | null>(null)
  const [sidebarOpen, setSidebarOpen] = useState(false)

  const current = id && loaded?.id === id ? loaded.analysis : null
  const loadError = id && loaded?.id === id ? loaded.error : null

  useEffect(() => {
    api.listAnalyses()
      .then(setHistory)
      .catch((e) => setHistoryError(e instanceof ApiError ? e.message : 'Could not load history.'))
  }, [])

  const loadedId = loaded?.id
  useEffect(() => {
    if (!id || loadedId === id) return
    let cancelled = false
    api.getAnalysis(id)
      .then((analysis) => !cancelled && setLoaded({ id, analysis, error: null }))
      .catch((e) => !cancelled && setLoaded({
        id,
        analysis: null,
        error: e instanceof ApiError && e.status === 404 ? 'This analysis no longer exists.' : 'Could not load this analysis.',
      }))
    return () => { cancelled = true }
  }, [id, loadedId])

  const onCreated = useCallback((analysis: Analysis) => {
    setLoaded({ id: analysis.id, analysis, error: null })
    setHistory((h) => [
      {
        id: analysis.id,
        title: analysis.title,
        company: analysis.company,
        matchScore: analysis.matchScore,
        fileName: analysis.fileName,
        createdAt: analysis.createdAt,
      },
      ...(h ?? []),
    ])
    navigate(`/app/a/${analysis.id}`)
  }, [navigate])

  const onDelete = useCallback(async (deleteId: string) => {
    await api.deleteAnalysis(deleteId)
    setHistory((h) => h?.filter((a) => a.id !== deleteId) ?? null)
    if (deleteId === id) navigate('/app')
  }, [id, navigate])

  function signOut() {
    logout()
    navigate('/')
  }

  return (
    <div className="workspace">
      <header className="topbar topbar-app">
        <div className="topbar-left">
          <button className="icon-button mobile-only" aria-label="Show history" onClick={() => setSidebarOpen((o) => !o)}>
            ☰
          </button>
          <Brand to="/app" />
        </div>
        <div className="topbar-actions">
          {session?.isGuest ? (
            <span className="badge badge-guest" title="Guest sessions expire after two hours">Guest demo</span>
          ) : (
            <span className="muted small hide-mobile">{session?.email}</span>
          )}
          {session?.isGuest && <Link to="/login" className="btn btn-ghost btn-sm hide-mobile">Create account</Link>}
          <button className="btn btn-ghost btn-sm" onClick={signOut}>Sign out</button>
        </div>
      </header>

      <div className="workspace-body">
        <aside
          className={`sidebar ${sidebarOpen ? 'open' : ''}`}
          // On mobile the sidebar is a drawer: close it once a link inside is followed.
          onClick={(e) => (e.target as HTMLElement).closest('a') && setSidebarOpen(false)}
        >
          <Link to="/app" className="btn btn-primary btn-block">+ New analysis</Link>
          <HistoryList items={history} error={historyError} activeId={id} onDelete={onDelete} />
        </aside>
        {sidebarOpen && <div className="scrim mobile-only" onClick={() => setSidebarOpen(false)} />}

        <main className="workspace-main">
          {!id && <AnalyzeForm isGuest={!!session?.isGuest} onCreated={onCreated} />}
          {id && loadError && (
            <div className="empty-state card">
              <p>{loadError}</p>
              <Link to="/app" className="btn btn-secondary">Start a new analysis</Link>
            </div>
          )}
          {id && !loadError && !current && <div className="loading-block"><span className="spinner" /> Loading…</div>}
          {id && current && <ResultView analysis={current} onDelete={() => onDelete(current.id)} />}
        </main>
      </div>
    </div>
  )
}
