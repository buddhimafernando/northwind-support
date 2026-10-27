import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type { PagedResult, TicketFilters, TicketListItem } from '../../../types/api';

const DEBOUNCE_MS = 300;

export const emptyFilters: TicketFilters = {
  search: '',
  status: undefined,
  priority: undefined,
  categoryId: undefined,
  customerId: undefined,
  assignedAgentId: undefined,
  unassignedOnly: false,
  page: 1,
  pageSize: 20,
  sortBy: 'createdAtUtc',
  sortDirection: 'desc',
};

interface State {
  isLoading: boolean;
  data?: PagedResult<TicketListItem>;
  error?: string;
}

/** Owns the ticket list's filters and the data they produce. */
export function useTicketList() {
  const [filters, setFilters] = useState<TicketFilters>(emptyFilters);
  const [debouncedSearch, setDebouncedSearch] = useState(emptyFilters.search);
  const [reloadTrigger, setReloadTrigger] = useState(0);
  const [state, setState] = useState<State>({ isLoading: true });

  const debounceTimerRef = useRef<number | null>(null);

  // Debounce search keystrokes so typing does not flood the API.
  useEffect(() => {
    debounceTimerRef.current = window.setTimeout(() => {
      setDebouncedSearch(filters.search);
      setFilters((current) => (current.page === 1 ? current : { ...current, page: 1 }));
    }, DEBOUNCE_MS);

    return () => {
      if (debounceTimerRef.current !== null) {
        window.clearTimeout(debounceTimerRef.current);
      }
    };
  }, [filters.search]);

  // All filters except search. Any change to these (status, priority, Task 2 SLA, etc.)
  // triggers an immediate request without special wiring.
  const nonSearchFiltersKey = useMemo(() => {
    const copy = { ...filters };
    delete (copy as Partial<TicketFilters>).search;
    return JSON.stringify(copy);
  }, [filters]);

  // The active parameters sent to the backend.
  const activeQuery = useMemo<TicketFilters>(() => {
    const nonSearch = JSON.parse(nonSearchFiltersKey) as Omit<TicketFilters, 'search'>;
    return {
      ...emptyFilters,
      ...nonSearch,
      search: debouncedSearch,
    };
  }, [nonSearchFiltersKey, debouncedSearch]);

  useEffect(() => {
    let isCurrent = true;

    const execute = async () => {
      setState((current) => ({ ...current, isLoading: true, error: undefined }));

      try {
        const data = await ticketsApi.getTickets(activeQuery);
        if (isCurrent) {
          setState({ isLoading: false, data, error: undefined });
        }
      } catch (error) {
        if (isCurrent) {
          const message = error instanceof ApiError ? error.message : 'Unable to load tickets.';
          setState((current) => ({ ...current, isLoading: false, error: message }));
        }
      }
    };

    queueMicrotask(() => {
      if (isCurrent) {
        void execute();
      }
    });

    return () => {
      isCurrent = false;
    };
  }, [activeQuery, reloadTrigger]);

  const updateFilters = useCallback((patch: Partial<TicketFilters>) => {
    const isSearchOnly = Object.keys(patch).length === 1 && 'search' in patch;

    if (!isSearchOnly) {
      if (debounceTimerRef.current !== null) {
        window.clearTimeout(debounceTimerRef.current);
      }
      setDebouncedSearch((prev) => patch.search ?? prev);
    }

    setFilters((current) => ({
      ...current,
      ...patch,
      page: patch.page ?? (isSearchOnly ? current.page : 1),
    }));
  }, []);

  const resetFilters = useCallback(() => {
    if (debounceTimerRef.current !== null) {
      window.clearTimeout(debounceTimerRef.current);
    }
    setDebouncedSearch(emptyFilters.search);
    setFilters(emptyFilters);
  }, []);

  const reload = useCallback(async () => {
    setReloadTrigger((count) => count + 1);
  }, []);

  return { filters, updateFilters, resetFilters, reload, ...state };
}


