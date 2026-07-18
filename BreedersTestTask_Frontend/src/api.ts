const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5224';

export type LitterStatus = 'Draft' | 'Submitted' | 'Approved' | 'Published';

export interface Litter {
  id: number;
  breederId: number;
  status: LitterStatus;
  createdAt: string;
}

export interface PagedResult<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

interface ApiErrorBody {
  error?: {
    type?: string;
    message?: string;
  };
}

export class ApiError extends Error {
  type: string;
  status: number;

  constructor(message: string, type: string, status: number) {
    super(message);
    this.name = 'ApiError';
    this.type = type;
    this.status = status;
  }
}

interface RequestOptions {
  method?: string;
  breederId: number;
}

async function request<T>(path: string, { method = 'GET', breederId }: RequestOptions): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method,
    headers: {
      'Content-Type': 'application/json',
      'X-Breeder-Id': String(breederId),
    },
  });

  const raw = await response.text();
  const data = raw ? (JSON.parse(raw) as (ApiErrorBody & T) | null) : null;

  if (!response.ok) {
    const errorBody = data as ApiErrorBody | null;
    const message = errorBody?.error?.message || `Request failed with status ${response.status}.`;
    const type = errorBody?.error?.type || 'UnknownError';
    throw new ApiError(message, type, response.status);
  }

  return data as T;
}

export interface GetLittersParams {
  status?: string;
  pageNumber?: number;
  pageSize?: number;
}

export function getLitters(
  breederId: number,
  { status, pageNumber, pageSize }: GetLittersParams = {},
): Promise<PagedResult<Litter>> {
  const params = new URLSearchParams();
  if (status) params.set('status', status);
  if (pageNumber) params.set('pageNumber', String(pageNumber));
  if (pageSize) params.set('pageSize', String(pageSize));

  return request<PagedResult<Litter>>(`/api/litters?${params.toString()}`, { breederId });
}

export function publishLitter(breederId: number, litterId: number): Promise<Litter> {
  return request<Litter>(`/api/litters/${litterId}/publish`, { method: 'POST', breederId });
}
