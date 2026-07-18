const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5224';

export class ApiError extends Error {
    constructor(message, type, status) {
        super(message);
        this.name = 'ApiError';
        this.type = type;
        this.status = status;
    }
}

async function request(path, { method = 'GET', breederId } = {}) {
    const response = await fetch(`${API_BASE_URL}${path}`, {
        method,
        headers: {
            'Content-Type': 'application/json',
            'X-Breeder-Id': String(breederId),
        },
    });

    const raw = await response.text();
    const data = raw ? JSON.parse(raw) : null;

    if (!response.ok) {
        const message = data?.error?.message || `Request failed with status ${response.status}.`;
        const type = data?.error?.type || 'UnknownError';
        throw new ApiError(message, type, response.status);
    }

    return data;
}

export function getLitters(breederId, { status, pageNumber, pageSize } = {}) {
    const params = new URLSearchParams();
    if (status) params.set('status', status);
    if (pageNumber) params.set('pageNumber', String(pageNumber));
    if (pageSize) params.set('pageSize', String(pageSize));

    return request(`/api/litters?${params.toString()}`, { breederId });
}

export function publishLitter(breederId, litterId) {
    return request(`/api/litters/${litterId}/publish`, { method: 'POST', breederId });
}
