# Integration Live Indicator

## When to use
- A platform chip already exists for auth/connection state and the operator also needs a compact live/on-air signal.

## Pattern
1. Extend the backend contract with a real IsStreamLive boolean and a refresh method/event.
2. Keep the UI component presentational by passing IsLive as a parameter.
3. Render a small LIVE badge inside the existing chip instead of replacing the connection affordance.
4. Refresh on load, after auth changes, and on a short timer when no push event exists yet.
5. Keep auth-state refresh separate from live-state refresh so the connection chip becomes interactive immediately; never block the auth affordance on a network live-status lookup.

## Why it works
- Preserves touch-friendly hit areas.
- Separates connection state from broadcast state.
- Scales to other platform connectors without forking the component.
