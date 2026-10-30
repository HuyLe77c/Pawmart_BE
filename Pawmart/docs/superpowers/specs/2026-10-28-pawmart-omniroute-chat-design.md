# PawMart Omniroute chat design

Date: 2026-10-28

## Goal

Keep the existing **Tư vấn** screen for nutrition and product advice based on a customer's pet profile. Add a separate **Chat AI** entry on the customer home screen and a free-form conversation screen. Both features use the locally installed Omniroute gateway through the backend. The assistant may discuss general topics, but PawMart-specific facts must come from the current database. It must acknowledge missing information and uncertainty; no model can guarantee a correct answer to every question.

## Current state

- Flutter already routes `/customer/assistant` to the pet advice screen and exposes it in the customer navigation bar. That screen calls `POST /api/chat/recommendations` without a text question.
- The ASP.NET Core endpoint requires a CUSTOMER token and delegates to `IAiRecommendationService`, whose default implementation returns 503.
- The customer home screen has no Chat AI entry. The SQL script explicitly excludes persisted AI chat history.
- Omniroute 3.8.51 is installed on the development machine, with local configuration and provider connections, but the gateway was not running during inspection. Its OpenAI-compatible chat endpoint is `POST /v1/chat/completions`.

## User experience

1. **Tư vấn** retains its current navigation entry, pet picker, advice result, and clickable product cards. Its recommendation request uses Omniroute once configured. If Omniroute is unavailable, the screen explains that advice is temporarily unavailable and allows retry.
2. The customer home screen shows a visible **Chat AI** card near the search shortcut. Tapping it opens `/customer/chat-ai`; the existing bottom navigation remains intact.
3. The Chat AI screen shows a welcome message, scrollable user/assistant turns, a multiline text field, a send button, a pending state, and an error with retry. Empty input cannot be sent. The customer can clear the current conversation.
4. Conversation turns are kept in app memory for the running session and are lost after app restart or sign-out. They are not written to SQL Server or device storage. The UI says this plainly.

## Backend contract and data flow

- Add `POST /api/chat/messages`, requiring the existing CUSTOMER policy. Request: `message` (1–2000 characters) and up to 12 previous turns (`role` = `user` or `assistant`, `content` = 1–2000 characters). Response: `answer` (nonempty string). The server rejects invalid roles, oversized histories, and malformed requests with 400.
- The backend constructs the system instruction. It never accepts a client-supplied system instruction or provider configuration. For questions about PawMart, it attaches a capped, current, read-only database snapshot: up to 40 active products ranked by matches to question terms (including price and availability), the customer's pet profiles for pet questions, and the customer's five latest orders or one exact order ID for order questions. Order queries must be scoped by the JWT user ID. If the requested product/order is outside this snapshot, the assistant says it cannot verify the detail rather than inventing it.
- General questions can be sent without PawMart context. The instruction asks for a useful answer in the user's language, distinguishes general knowledge from verified PawMart data, prohibits invented stock/prices/order states, and expresses uncertainty where appropriate. Health or veterinary questions receive general information and advice to consult a veterinarian when professional judgment is needed.
- Add an Omniroute HTTP client in the backend. Read `Omniroute:BaseUrl` (default `http://127.0.0.1:20128/v1`), `Omniroute:Model` (default `cloudflare-ai/@cf/meta/llama-3.3-70b-instruct-fp8-fast`, verified on this machine), and optional `Omniroute:ApiKey` from server configuration. A keyless connection is permitted only to a loopback URL; remote URLs require a key supplied through a secret or environment variable, never committed or returned to Flutter. Set a 30-second timeout and a 128 KiB response-size limit. Send only the required context and recent turns. Normalize provider errors to a safe 503/502 response without exposing the key or raw provider body.
- Implement `IAiRecommendationService` with the same Omniroute client. It receives the owned pet and active candidate products from the existing `RecommendationService`. Parse the model's structured result, validate recommended IDs against the candidate set, and retain the existing server-side product lookup before returning recommendations. The existing endpoint and response shape remain compatible with Flutter.
- Do not add chat tables or alter the SQL baseline. Do not send payment details, addresses, contact details, tokens, or other unnecessary personal information to Omniroute.

## Boundaries and failure handling

- The assistant is read-only: it cannot place/cancel orders, change cart contents, or update pet profiles. The UI can navigate to referenced products; user actions still use existing screens and APIs.
- Only the logged-in customer's own data may enter context. Inactive products and other customers' records must not appear. Treat database text and previous messages as untrusted content; they cannot override server instructions.
- A missing key, stopped gateway, unavailable model, timeout, or invalid provider response produces an explicit temporary-unavailable state. The user's typed question stays available for retry.
- Rate-limit chat requests to 20 per minute per authenticated user and cap text/history/context sizes to control cost and abuse. Do not log prompts, provider keys, or personal data.

## Verification

- Backend: build; verify authorization, input limits, owner scoping, provider-unavailable behavior, and recommendation ID filtering. Use a fake Omniroute HTTP endpoint for successful and malformed provider replies.
- Flutter: `flutter analyze`, relevant widget/view-model tests, and navigation checks for the home card, Chat AI, and preserved Tư vấn flow.
- Runtime: start Omniroute and the API, confirm an authenticated general question, a database-grounded product question, and a pet recommendation through the Flutter app. Record any missing local key/provider/model configuration rather than claiming live AI works from a build alone.

## Out of scope

Persistent multi-session chat history, anonymous chat, voice/image input, web search, and automatic order/cart actions are separate features.
