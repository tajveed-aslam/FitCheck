import { scoreBand } from '../score'

const RADIUS = 80
const ARC_LENGTH = Math.PI * RADIUS

export default function ScoreGauge({ score }: { score: number }) {
  const clamped = Math.max(0, Math.min(100, score))
  const band = scoreBand(clamped)
  const filled = (ARC_LENGTH * clamped) / 100

  return (
    <figure className={`gauge tone-${band.tone}`} aria-label={`Match score ${clamped} percent, ${band.label}`}>
      <svg viewBox="0 0 200 116" role="img" aria-hidden>
        <path className="gauge-track" d="M 20 100 A 80 80 0 0 1 180 100" />
        <path
          className="gauge-value"
          d="M 20 100 A 80 80 0 0 1 180 100"
          strokeDasharray={`${filled} ${ARC_LENGTH}`}
        />
      </svg>
      <figcaption>
        <span className="gauge-score">
          {clamped}
          <small>%</small>
        </span>
        <span className="gauge-label">{band.label}</span>
      </figcaption>
    </figure>
  )
}
