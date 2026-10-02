import { useActionState, type SetStateAction } from 'react'
import { ApiError } from '../api/client'

type ActionState<TData> = {
  data: TData | null
  error: string | null
  errorCode: string | null
  isSuccess: boolean
}

/**
 * A generic hook that wraps React 19.2's `useActionState` for ERP transactions.
 * It automatically handles `ApiError` instances thrown by `apiClient` and enforces
 * a standard ActionState shape for the UI to consume.
 * 
 * Future improvement: Integrate `Idempotency-Key` generation for POST/PUT requests
 * directly here if not handled by the client/API layer.
 * 
 * @param actionFn - The asynchronous action to execute.
 * @param initialState - The initial state of the action.
 */
export function useErpAction<TData, TPayload>(
  actionFn: (payload: TPayload) => Promise<TData>,
  initialState?: Partial<ActionState<TData>>
) {
  const defaultState: ActionState<TData> = {
    data: null,
    error: null,
    errorCode: null,
    isSuccess: false,
    ...initialState,
  }

  // React 19.2 useActionState signature:
  // const [state, dispatch, isPending] = useActionState(action, initialState)
  const [state, dispatch, isPending] = useActionState(
    async (prevState: ActionState<TData>, payload: TPayload): Promise<ActionState<TData>> => {
      try {
        const result = await actionFn(payload)
        return {
          data: result,
          error: null,
          errorCode: null,
          isSuccess: true,
        }
      } catch (err) {
        if (err instanceof ApiError) {
          // Special handling for ERP domain exceptions like ConcurrencyException or InsufficientStockException
          return {
            data: prevState.data,
            error: err.message || err.title || 'An unexpected error occurred.',
            errorCode: err.code || null,
            isSuccess: false,
          }
        }
        
        return {
          data: prevState.data,
          error: err instanceof Error ? err.message : 'Unknown error',
          errorCode: 'UNKNOWN',
          isSuccess: false,
        }
      }
    },
    defaultState
  )

  return { state, dispatch, isPending }
}
