# PawMart Omniroute Chat Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Preserve pet advice and add a useful general Chat AI entry on the customer home screen, both backed by Omniroute and current PawMart data.

**Architecture:** The ASP.NET Core API owns Omniroute configuration and gathers read-only, user-scoped context. Flutter keeps the two screens separate and sends bounded recent turns to the API. No chat database table is added.

**Tech Stack:** .NET 10, EF Core SQL Server, `HttpClient`, Flutter, Provider, go_router.

**Spec:** `../specs/2026-10-28-pawmart-omniroute-chat-design.md`

## Global Constraints

- Keep `POST /api/chat/recommendations` response compatible with existing Flutter.
- Keep the key server-side; use `Omniroute:BaseUrl`, `Omniroute:Model`, and `Omniroute:ApiKey`. Keyless access is allowed only for a loopback URL.
- Customer authorization and owner scoping apply to both endpoints.
- No chat-history database or schema changes.
- 2000 characters per message, at most 12 previous turns, 30-second provider timeout, 128 KiB response limit, 20 chat requests per minute per customer.

## Review Focus

- Unavailable Omniroute: both endpoints return a safe, useful 503, preserving the typed question.
- Malformed provider JSON: API returns 502 rather than an unhandled exception.
- Foreign order/pet IDs: no other customer's data enters model context.
- Client-injected system role or excessive history: API returns 400.
- Inactive products or invented recommendation IDs: server excludes them.

---

### Task 1: Omniroute provider client and recommendation adapter

**Files:** Create `Pawmart/Services/Integrations/OmnirouteClient.cs`; modify `Pawmart/Program.cs`, `Pawmart/README.md`; test with an injected fake HTTP handler.

**Interfaces:** `OmnirouteClient.CompleteAsync(IReadOnlyList<AiChatMessage>, CancellationToken) : Task<string>`; `OmnirouteRecommendationService : IAiRecommendationService` consumes that client.

- [ ] Implement safe configuration, HTTP request/response parsing, timeout and size caps, and provider error mapping.
- [ ] Implement recommendation JSON parsing and ID filtering; register services in DI.
- [ ] Verify configured success, missing key, provider errors and malformed responses.

### Task 2: Grounded free-form chat API

**Files:** Create `Pawmart/Models/Contracts/Commerce/ChatContracts.cs`, `Pawmart/Services/ChatMessageService.cs`; modify `Pawmart/Controllers/ChatController.cs`, `Pawmart/Program.cs`.

**Interfaces:** `ChatMessageService.ReplyAsync(string userId, ChatMessageRequest request, CancellationToken) : Task<ChatMessageResponse>`; `POST /api/chat/messages` consumes the request and returns the response.

- [ ] Add bounded, validated user/assistant history and customer-only endpoint.
- [ ] Add question-aware active-product, owned-pet and owned-order context; mark database text as untrusted and keep actions read-only.
- [ ] Add per-user 20/minute rate limiting and provider failure handling.
- [ ] Verify general questions, database facts, invalid input, owner scoping, missing provider and malformed provider reply.

### Task 3: Flutter home entry and chat screen

**Files:** Create `lib/data/repositories/chat_repository.dart`, `lib/ui/features/chat_ai/view_models/chat_ai_view_model.dart`, `lib/ui/features/chat_ai/views/chat_ai_screen.dart`; modify `lib/ui/core/app_dependencies.dart`, `lib/ui/core/app_router.dart`, `lib/ui/features/home/views/home_screen.dart`, `README.md`.

**Interfaces:** `ChatRepository.send(message, history) : Future<String>`; `ChatAiViewModel.send()` maintains the in-memory turns; route `/customer/chat-ai`.

- [ ] Add prominent home Chat AI entry and accessible route while retaining existing Tư vấn navigation.
- [ ] Add chat bubbles, multiline composer, pending/error/retry/clear states and session-only history.
- [ ] Verify Flutter analyzer and focused UI/view-model tests.

### Task 4: End-to-end verification

**Files:** Update readmes with local Omniroute setup, environment variables, and limitations.

- [ ] Build BE and analyze Flutter; fix concrete failures.
- [ ] Start local Omniroute and API if possible; verify general chat, product-grounded answer and pet recommendation with a customer token.
- [ ] Report exact runtime result and remaining external configuration, if any. No push or Git initialization.
