import { useEffect, useState, type DragEvent, type FormEvent } from 'react'
import { api, ApiError, type Analysis } from '../api'
import { loadSampleCv, sampleJobDescription } from '../samples'

const MAX_FILE_BYTES = 5 * 1024 * 1024
const MIN_JD = 100
const MAX_JD = 20_000
const ACCEPT = '.pdf,.docx,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document'

const progressMessages = [
  'Reading your CV…',
  'Comparing it with the job description…',
  'Scoring the match…',
  'Writing improvement tips…',
  'Almost there…',
]

function fileProblem(file: File): string | null {
  const name = file.name.toLowerCase()
  if (name.endsWith('.doc')) return 'Legacy .doc files aren’t supported. Save your CV as .docx or PDF.'
  if (!name.endsWith('.pdf') && !name.endsWith('.docx')) return 'Choose a PDF or DOCX file.'
  if (file.size > MAX_FILE_BYTES) return 'That file is larger than 5 MB.'
  if (file.size === 0) return 'That file is empty.'
  return null
}

function formatSize(bytes: number): string {
  return bytes < 1024 * 1024 ? `${Math.max(1, Math.round(bytes / 1024))} KB` : `${(bytes / 1024 / 1024).toFixed(1)} MB`
}

interface Props {
  isGuest: boolean
  onCreated: (analysis: Analysis) => void
}

export default function AnalyzeForm({ isGuest, onCreated }: Props) {
  const [file, setFile] = useState<File | null>(null)
  const [dragging, setDragging] = useState(false)
  const [jobDescription, setJobDescription] = useState('')
  const [title, setTitle] = useState('')
  const [busy, setBusy] = useState(false)
  const [progress, setProgress] = useState(0)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!busy) return
    const timer = window.setInterval(() => setProgress((p) => Math.min(p + 1, progressMessages.length - 1)), 4500)
    return () => window.clearInterval(timer)
  }, [busy])

  function pick(candidate: File | undefined) {
    if (!candidate) return
    const problem = fileProblem(candidate)
    setError(problem)
    setFile(problem ? null : candidate)
  }

  function onDrop(event: DragEvent) {
    event.preventDefault()
    setDragging(false)
    if (!busy) pick(event.dataTransfer.files[0])
  }

  async function loadSamples() {
    setError(null)
    try {
      setFile(await loadSampleCv())
      setJobDescription(sampleJobDescription)
    } catch {
      setError('Could not load the sample CV.')
    }
  }

  async function onSubmit(event: FormEvent) {
    event.preventDefault()
    if (!file) {
      setError('Attach your CV first.')
      return
    }
    setProgress(0)
    setBusy(true)
    setError(null)
    try {
      onCreated(await api.createAnalysis(file, jobDescription.trim(), title.trim() || undefined))
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'The analysis failed. Please try again.')
    } finally {
      setBusy(false)
    }
  }

  const jdLength = jobDescription.trim().length
  const jdInvalid = jdLength > 0 && (jdLength < MIN_JD || jdLength > MAX_JD)

  return (
    <form className="analyze card" onSubmit={onSubmit}>
      <div className="analyze-header">
        <div>
          <h1>New analysis</h1>
          <p className="muted">Upload your CV and paste the job description to see how well you match.</p>
        </div>
        <button type="button" className="btn btn-secondary btn-sm" onClick={() => void loadSamples()} disabled={busy}>
          Try with sample CV &amp; job
        </button>
      </div>

      <div className="field">
        <span className="field-label">1. Your CV</span>
        {file ? (
          <div className="file-chip">
            <span className="file-icon" aria-hidden>{file.name.toLowerCase().endsWith('.pdf') ? 'PDF' : 'DOCX'}</span>
            <span className="file-name">{file.name}</span>
            <span className="muted small">{formatSize(file.size)}</span>
            <button type="button" className="icon-button" aria-label="Remove file" onClick={() => setFile(null)} disabled={busy}>
              ×
            </button>
          </div>
        ) : (
          <label
            className={`dropzone ${dragging ? 'dragging' : ''}`}
            onDragOver={(e) => { e.preventDefault(); setDragging(true) }}
            onDragLeave={() => setDragging(false)}
            onDrop={onDrop}
          >
            <input
              type="file"
              accept={ACCEPT}
              className="visually-hidden"
              onChange={(e) => { pick(e.target.files?.[0]); e.target.value = '' }}
              disabled={busy}
            />
            <span className="dropzone-icon" aria-hidden>⇪</span>
            <strong>Drop your CV here, or click to browse</strong>
            <span className="muted small">PDF or DOCX, up to 5 MB. Scanned images can’t be read.</span>
          </label>
        )}
      </div>

      <div className="field">
        <label htmlFor="jd" className="field-label">2. Job description</label>
        <textarea
          id="jd"
          className="jd-input"
          value={jobDescription}
          onChange={(e) => setJobDescription(e.target.value)}
          placeholder="Paste the full job posting: responsibilities, requirements and nice-to-haves."
          disabled={busy}
          required
        />
        <span className={`field-hint ${jdInvalid ? 'error-text' : ''}`}>
          {jdLength.toLocaleString()} characters
          {jdLength > 0 && jdLength < MIN_JD && ` · at least ${MIN_JD} needed`}
          {jdLength > MAX_JD && ` · limit is ${MAX_JD.toLocaleString()}`}
        </span>
      </div>

      <label className="field">
        <span>Role title <span className="muted">(optional, detected from the job description if empty)</span></span>
        <input maxLength={200} value={title} onChange={(e) => setTitle(e.target.value)} disabled={busy} />
      </label>

      {error && <p className="error-text" role="alert">{error}</p>}

      <div className="analyze-actions">
        <button className="btn btn-primary btn-lg" disabled={busy || !file || jdLength < MIN_JD || jdLength > MAX_JD}>
          {busy ? <><span className="spinner" /> Analyzing…</> : 'Check my fit'}
        </button>
        {busy && <span className="muted">{progressMessages[progress]}</span>}
        {!busy && isGuest && <span className="muted small">Guest demo: up to 5 analyses per hour.</span>}
      </div>
    </form>
  )
}
