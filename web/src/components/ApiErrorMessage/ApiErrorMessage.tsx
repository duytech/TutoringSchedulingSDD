import type { ApiError } from '../../api'
import './ApiErrorMessage.css'

export function ApiErrorMessage({ error }: { error: ApiError }) {
  return (
    <p className="message message--error" role="alert">
      Cannot reach the API. Is it running (<code>dotnet run --project src/BrightPath.Api</code>)?
      {error.status !== null && <> The API answered {error.status}.</>}
    </p>
  )
}
