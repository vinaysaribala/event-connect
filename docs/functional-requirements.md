# EventConnect — Functional Requirements Document (FRD)

**Digital Invitations, Event Management & RSVP Platform**
Version 1.0 | 28 October 2026

> Converted from `EventConnect_Functional_Requirements.docx` (v1.0) on 2 November 2026. This file is now the master copy: edit it here rather than in Word.
> Buildable, per-feature breakdowns with API samples, UI screens and acceptance criteria live in [`requirements/`](requirements/README.md). Each one links back to the IDs below (FR-xxx, BR-xx, AC-xx).

## 1. Purpose and Scope

Define functional behavior for a responsive web platform similar in category to Evite. The system enables organizers to create events, customize invitations, distribute invitations, track RSVPs, communicate with guests, and administer templates and subscriptions. This is a proposed product specification, not a statement of Evite’s exact functionality.

## 2. Personas and Roles

- Visitor: browse invitation designs and register.
- Organizer: create events, manage guests, send invitations and monitor responses.
- Co-host: perform delegated event-management tasks.
- Guest: view invitations and RSVP without creating an account.
- Administrator: manage users, content, templates, entitlements and support workflows.

## 3. Accounts and Access

| ID | Requirement | Release |
|---|---|---|
| FR-001 | Register with email/password and verify email | MVP |
| FR-002 | Sign in, sign out, reset password and manage profile | MVP |
| FR-003 | Allow Google/Apple sign-in | Phase 2 |
| FR-004 | Apply event-level ownership and co-host authorization | MVP |
| FR-005 | Permit token-based guest access without registration | MVP |

## 4. Event Lifecycle

| ID | Requirement | Release |
|---|---|---|
| FR-006 | Create, edit, duplicate, publish, cancel and archive events | MVP |
| FR-007 | Capture title, description, category, start/end, timezone and location | MVP |
| FR-008 | Support physical, virtual and hybrid event formats | MVP |
| FR-009 | Configure RSVP deadline, capacity, plus-ones and visibility | MVP |
| FR-010 | Save drafts and validate required fields before publishing | MVP |
| FR-011 | Delegate event management to co-hosts | Phase 2 |

## 5. Invitation Design

| ID | Requirement | Release |
|---|---|---|
| FR-012 | Browse, search, filter and preview templates | MVP |
| FR-013 | Edit text, fonts, colors and imagery within template rules | MVP |
| FR-014 | Preview invitation on mobile and desktop | MVP |
| FR-015 | Save a stable version of the published invitation | MVP |
| FR-016 | Provide premium templates and custom branding | Phase 2 |

## 6. Guest Lists

| ID | Requirement | Release |
|---|---|---|
| FR-017 | Add, edit, remove and search event guests | MVP |
| FR-018 | Import guests from CSV with validation and duplicate detection | MVP |
| FR-019 | Store email and optional phone number with consent metadata | MVP |
| FR-020 | Track invitation status and guest-specific plus-one allowance | MVP |
| FR-021 | Export guest and response lists | MVP |

## 7. Invitation Delivery

| ID | Requirement | Release |
|---|---|---|
| FR-022 | Send personalized invitations via email | MVP |
| FR-023 | Generate unique, revocable guest links and shareable event links | MVP |
| FR-024 | Record queued, sent, delivered, bounced and failed statuses when supported by provider | MVP |
| FR-025 | Resend invitations safely without duplicate processing | MVP |
| FR-026 | Send SMS invitations with opt-in and regulatory controls | Phase 2 |
| FR-027 | Schedule future invitation delivery | Phase 2 |

## 8. RSVP and Attendance

| ID | Requirement | Release |
|---|---|---|
| FR-028 | Allow Yes/No/Maybe RSVP and subsequent changes | MVP |
| FR-029 | Collect plus-one count, comments and custom answers | MVP |
| FR-030 | Validate token, deadline, guest eligibility and event capacity | MVP |
| FR-031 | Update organizer counts and guest response history | MVP |
| FR-032 | Send confirmation after successful RSVP | MVP |
| FR-033 | Offer waitlist when capacity is reached | Phase 2 |
| FR-034 | Support organizer overrides with audit trail | Phase 2 |

## 9. Communication

| ID | Requirement | Release |
|---|---|---|
| FR-035 | Send configurable RSVP reminders to nonresponders | MVP |
| FR-036 | Notify attendees of material event changes or cancellation | MVP |
| FR-037 | Allow organizer announcements to selected guest segments | Phase 2 |
| FR-038 | Respect email unsubscribe and SMS opt-out rules | MVP |

## 10. Dashboards and Administration

| ID | Requirement | Release |
|---|---|---|
| FR-039 | Show event list, filters, invited, responded and pending metrics | MVP |
| FR-040 | Count attendees separately from accepted invitation records | MVP |
| FR-041 | Allow administrators to manage templates and categories | MVP |
| FR-042 | Provide administrative audit log and support search | MVP |
| FR-043 | Offer subscriptions, payments, receipts and entitlements | Phase 2 |
| FR-044 | Export aggregate event reports | Phase 2 |

## 11. Core Business Rules

- BR-01: Every event belongs to one organizer; co-host permissions are explicitly granted.
- BR-02: RSVP deadlines are evaluated in the event’s configured timezone.
- BR-03: Accepted attendee counts include the guest and approved plus-ones; declined guests consume no capacity.
- BR-04: Capacity reservation and RSVP changes must be atomic to prevent oversubscription.
- BR-05: A guest’s latest valid RSVP determines current status; previous changes remain in history.
- BR-06: Maybe does not reserve capacity by default.
- BR-07: Private event URLs must not expose guest information; guest tokens must be unguessable and revocable.
- BR-08: An event cancellation stops pending reminders and triggers cancellation notices.
- BR-09: Guest personal information is visible only to authorized organizers and support roles.
- BR-10: Message retries must be idempotent and respect opt-outs.

## 12. Primary User Journeys

### Organizer creates event

Sign in → select template → enter details → configure RSVP → add guests → preview → publish → send invitations → monitor dashboard.

### Guest submits RSVP

Open secure invitation → view event → choose Yes/No/Maybe → enter attendee count/custom answers → submit → receive confirmation.

### Organizer updates event

Open published event → edit date/location → confirm changes → save version → notify affected guests → record notification results.

## 13. Acceptance Criteria (Representative)

- AC-01: A valid organizer can save an incomplete event as Draft; publishing blocks missing mandatory fields.
- AC-02: A guest with a valid link can RSVP without an account, and the organizer dashboard reflects the response.
- AC-03: Two concurrent RSVPs cannot exceed a capacity limit.
- AC-04: RSVP after the deadline is rejected with a clear message unless reopened by organizer.
- AC-05: Replaying the same invitation-send request does not create duplicate sends.
- AC-06: Revoked guest links cannot view private invitation details or modify RSVP.
- AC-07: Cancelling an event disables new RSVP submissions and queues cancellation notifications.
- AC-08: A CSV import reports invalid rows and duplicates before finalizing the import.

## 14. Suggested Data Entities

User, Role, Event, EventCoHost, Template, InvitationDesignVersion, Guest, Invitation, InvitationDeliveryAttempt, RSVP, RSVPHistory, RSVPQuestion, RSVPAnswer, Notification, NotificationPreference, Subscription, Payment, AuditEvent. Each entity should include stable identifiers and timestamps; sensitive fields require access controls.

## 15. External Integrations

Email delivery provider (required); SMS provider (Phase 2); object storage for invitation images; identity provider for social login (Phase 2); payment processor for premium plans (Phase 2); optional calendar file (.ics) generation and mapping integration.

## 16. Nonfunctional Requirements (Initial Targets)

- Availability: 99.9% monthly target for production MVP.
- Performance: 95th percentile API reads under 500 ms under agreed load, excluding third-party delivery latency.
- Scalability: horizontal API and worker scaling; queue-based notification delivery.
- Security: HTTPS, encryption at rest, rate limits, CSRF protection as applicable, secure tokens and least-privilege authorization.
- Privacy: consent, retention, deletion and export procedures defined for applicable jurisdictions.
- Accessibility: target WCAG 2.2 AA for guest and organizer interfaces.
- Observability: structured logs, metrics, traces, delivery failure monitoring and audit events.
- Recovery: define RPO/RTO, backups and restore tests before production launch.

## 17. MVP Release Boundary

MVP: account registration, event creation, template customization, guest management, email invitations, secure guest RSVP, reminders, basic dashboard and administrator template management. Phase 2: SMS, co-host collaboration, social sign-in, paid subscriptions, waitlists and advanced reporting.

## 18. Open Product Decisions

- Are shareable links open RSVP links or only access paths for identified invitees?
- Can an organizer override event capacity, and who is notified?
- Are public events discoverable or merely accessible by public URL?
- What limits apply to free invitations, guest counts and storage?
- Should guests see the guest list or attendee comments?
- How long are events, guest data and audit histories retained?
- Will SMS be supported in the first release and in which countries?
