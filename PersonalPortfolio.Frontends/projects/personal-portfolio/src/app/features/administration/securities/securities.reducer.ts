import { createEntityAdapter, EntityAdapter } from '@ngrx/entity';

import { Security, SecurityState } from './security';
import { actionSecuritiesUpsertOne, actionSecuritiesDeleteOne } from './securities.actions';
import { Action, createReducer, on } from '@ngrx/store';

export function sort(a: Security, b: Security): number {
    return a.code.localeCompare(b.code);
}

export const securitiesAdapter: EntityAdapter<Security> = createEntityAdapter<Security>({
    sortComparer: sort
});

export const initialState: SecurityState = securitiesAdapter.getInitialState({
    ids: [],
    entities: { }
});

const reducer = createReducer(
    initialState,
    on(actionSecuritiesUpsertOne, (state, { security }) =>
        securitiesAdapter.upsertOne(security, state)
    ),
    on(actionSecuritiesDeleteOne, (state, { id }) => securitiesAdapter.removeOne(id, state))
);

export function securitiesReducer(state: SecurityState | undefined, action: Action) {
    return reducer(state, action);
}
