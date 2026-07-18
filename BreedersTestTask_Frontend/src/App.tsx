import { useCallback, useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { ApiError, getLitters, publishLitter } from './api';
import type { Litter, LitterStatus, PagedResult } from './api';
import './App.css';

const STAGES: LitterStatus[] = ['Draft', 'Submitted', 'Approved', 'Published'];
const STATUS_FILTERS = ['All', ...STAGES] as const;
const PAGE_SIZE_OPTIONS = [5, 10, 20, 50];

interface Banner {
  kind: 'error' | 'success';
  text: string;
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
}

function LifecycleTrack({ status }: { status: LitterStatus }) {
  const currentIndex = STAGES.indexOf(status);
  return (
      <div className="track" role="img" aria-label={`Lifecycle stage: ${status}`}>
        {STAGES.map((stage, index) => (
            <div className="track-segment" key={stage}>
          <span
              className={
                  'track-dot' +
                  (index < currentIndex ? ' is-done' : '') +
                  (index === currentIndex ? ' is-current' : '')
              }
              title={stage}
          />
              {index < STAGES.length - 1 && (
                  <span className={'track-line' + (index < currentIndex ? ' is-done' : '')} />
              )}
            </div>
        ))}
      </div>
  );
}

function StatusPill({ status }: { status: LitterStatus }) {
  return <span className={`pill pill-${status.toLowerCase()}`}>{status}</span>;
}

export default function App() {
  const [breederIdInput, setBreederIdInput] = useState('1');
  const [activeBreederId, setActiveBreederId] = useState(1);
  const [statusFilter, setStatusFilter] = useState('All');
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  const [result, setResult] = useState<PagedResult<Litter> | null>(null);
  const [loading, setLoading] = useState(false);
  const [publishingId, setPublishingId] = useState<number | null>(null);
  const [banner, setBanner] = useState<Banner | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setBanner(null);
    try {
      const data = await getLitters(activeBreederId, {
        status: statusFilter === 'All' ? undefined : (statusFilter as LitterStatus),
        pageNumber,
        pageSize,
      });
      setResult(data);
    } catch (err) {
      setResult(null);
      if (err instanceof ApiError) {
        setBanner({ kind: 'error', text: err.message });
      } else {
        setBanner({ kind: 'error', text: 'Could not reach the API. Is the backend running?' });
      }
    } finally {
      setLoading(false);
    }
  }, [activeBreederId, statusFilter, pageNumber, pageSize]);

  useEffect(() => {
    load();
  }, [load]);

  function handleBreederSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const parsed = Number(breederIdInput);
    if (!Number.isInteger(parsed) || parsed <= 0) {
      setBanner({ kind: 'error', text: 'Breeder id must be a positive whole number.' });
      return;
    }
    setPageNumber(1);
    setActiveBreederId(parsed);
  }

  function handleStatusChange(value: string) {
    setStatusFilter(value);
    setPageNumber(1);
  }

  function handlePageSizeChange(value: string) {
    setPageSize(Number(value));
    setPageNumber(1);
  }

  async function handlePublish(litter: Litter) {
    setPublishingId(litter.id);
    setBanner(null);
    try {
      await publishLitter(activeBreederId, litter.id);
      setBanner({ kind: 'success', text: `Litter #${litter.id} published.` });
      await load();
    } catch (err) {
      const text = err instanceof ApiError ? err.message : 'Could not reach the API.';
      setBanner({ kind: 'error', text });
    } finally {
      setPublishingId(null);
    }
  }

  const totalPages = result?.totalPages ?? 0;

  return (
      <div className="page">
        <header className="topbar">
          <div className="brand">
            <span className="brand-mark">🐾</span>
            <div>
              <h1>Litters</h1>
              <p>Publish approved litters and track where each one sits in review.</p>
            </div>
          </div>

          <form className="breeder-badge" onSubmit={handleBreederSubmit}>
            <label htmlFor="breeder-id">Breeder id</label>
            <input
                id="breeder-id"
                inputMode="numeric"
                value={breederIdInput}
                onChange={(e) => setBreederIdInput(e.target.value)}
            />
            <button type="submit">Switch</button>
          </form>
        </header>

        {banner && (
            <div className={`banner banner-${banner.kind}`} role="status">
              {banner.text}
            </div>
        )}

        <section className="toolbar">
          <div className="filter-group">
            <label htmlFor="status-filter">Status</label>
            <select
                id="status-filter"
                value={statusFilter}
                onChange={(e) => handleStatusChange(e.target.value)}
            >
              {STATUS_FILTERS.map((s) => (
                  <option key={s} value={s}>
                    {s}
                  </option>
              ))}
            </select>
          </div>

          <div className="filter-group">
            <label htmlFor="page-size">Per page</label>
            <select
                id="page-size"
                value={pageSize}
                onChange={(e) => handlePageSizeChange(e.target.value)}
            >
              {PAGE_SIZE_OPTIONS.map((n) => (
                  <option key={n} value={n}>
                    {n}
                  </option>
              ))}
            </select>
          </div>

          <button className="ghost-btn" type="button" onClick={load} disabled={loading}>
            {loading ? 'Refreshing…' : 'Refresh'}
          </button>
        </section>

        <section className="table-card">
          <table>
            <thead>
            <tr>
              <th>Litter</th>
              <th>Stage</th>
              <th>Created</th>
              <th aria-hidden="true" />
            </tr>
            </thead>
            <tbody>
            {loading && !result && (
                <tr>
                  <td colSpan={4} className="empty-cell">
                    Loading litters…
                  </td>
                </tr>
            )}

            {result && result.items.length === 0 && (
                <tr>
                  <td colSpan={4} className="empty-cell">
                    No litters match this filter yet.
                  </td>
                </tr>
            )}

            {result?.items.map((litter) => (
                <tr key={litter.id}>
                  <td className="mono">#{litter.id}</td>
                  <td>
                    <div className="stage-cell">
                      <StatusPill status={litter.status} />
                      <LifecycleTrack status={litter.status} />
                    </div>
                  </td>
                  <td className="mono">{formatDate(litter.createdAt)}</td>
                  <td className="action-cell">
                    <button
                        type="button"
                        className="publish-btn"
                        disabled={litter.status !== 'Approved' || publishingId === litter.id}
                        onClick={() => handlePublish(litter)}
                    >
                      {publishingId === litter.id ? 'Publishing…' : 'Publish'}
                    </button>
                  </td>
                </tr>
            ))}
            </tbody>
          </table>
        </section>

        {result && result.totalCount > 0 && (
            <footer className="pagination">
              <button
                  type="button"
                  disabled={pageNumber <= 1}
                  onClick={() => setPageNumber((p) => p - 1)}
              >
                ← Previous
              </button>
              <span>
            Page {result.pageNumber} of {totalPages} · {result.totalCount} litters total
          </span>
              <button
                  type="button"
                  disabled={pageNumber >= totalPages}
                  onClick={() => setPageNumber((p) => p + 1)}
              >
                Next →
              </button>
            </footer>
        )}
      </div>
  );
}
