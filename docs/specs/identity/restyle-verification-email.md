# Restyle verification email

## Problem
The verification code email sent during registration (and on resend) currently renders as
a bare, unbranded `<h1>{code}</h1>` with no context or branding. Marketing/product supplied
a Figma design for a branded, readable version of the same email. This is a visual and
content redesign of the existing email template — no new email type, no new trigger, no
change to when or why the email is sent.

## Scope
- Module(s) affected: Shared (owns `EmailService`, where the verification email HTML is
  built and where email sending is a cross-cutting concern per this project's module
  boundaries). Identity is a caller only (`RegistrationService`, `VerificationService`) and
  is affected only if the `IEmailService` contract needs new parameters — see Open
  questions.
- Roles allowed: not applicable — this email is sent to any newly registering or
  resending user, before any organization role exists for them, same as today.

## Behaviour
1. The verification email must visually match the supplied Figma design: a centered
   "RISTORANTE" header in bold red uppercase, a thin tri-color (green/white/red) decorative
   bar beneath it, a left-aligned "Confirm your email" heading, a greeting using the
   recipient's first name, an explanatory paragraph stating the code expires in N minutes,
   a prominent styled code display, a disclaimer paragraph for recipients who did not
   create an account, a sign-off, a divider, and a smaller-text footer with a reply-to
   prompt and a statement of which address the email was sent to.
2. The email must address the recipient by first name (currently not included in the
   email content at all).
3. The email must state the actual configured expiry duration in minutes (currently not
   stated in the email content at all); this value must reflect
   `SecurityOptions.VerificationCodeExpiryMinutes`, not a hardcoded number, so it stays
   correct if the configured value changes.
4. The email must display the actual verification code (unchanged behavior) styled per the
   design (large, bold, red, letter-spaced, boxed).
5. The email must display the actual recipient email address in the footer sentence
   (currently not included).
6. This restyle applies uniformly to the verification email sent both at registration and
   at resend — both currently call the same `SendVerificationEmailAsync` method and must
   continue to produce the same styled output.
7. The email subject line is unchanged by this feature (still "Verify your email") unless
   flagged otherwise — not part of the Figma design, out of scope.
8. If the recipient's first name is missing or blank, the greeting must still render
   sensibly (e.g. omit the name or fall back to a generic greeting) rather than showing a
   broken/empty greeting.

## Data
No new persisted data. Content used to populate the template, sourced from existing values:
- `code` (required) — existing verification code, unchanged source and format.
- `firstName` (required for the greeting to render the name; must have a defined fallback
  if blank per Behaviour #8) — sourced from the recipient `User.FirstName`.
- `email`/`to` (required) — the recipient address, already available to the email sender;
  used in the footer sentence.
- `expiryMinutes` (required) — sourced from `SecurityOptions.VerificationCodeExpiryMinutes`,
  not hardcoded.

## Acceptance criteria
- Sending the verification email with a blank/missing first name does not produce a broken
  greeting (e.g. "Hi ,") — it falls back to an acceptable default per Behaviour #8.
- Sending the verification email with a configured expiry other than the default (e.g. 30
  minutes instead of 15) causes the email body to state 30 minutes, not a hardcoded value.
- The rendered email body contains the actual verification code value, not a placeholder
  string.
- The rendered email body contains the actual recipient email address in the footer
  sentence, not a placeholder string.
- The rendered email body contains the recipient's first name in the greeting when a first
  name is present.
- The rendered email body contains all of the following content elements: "RISTORANTE"
  header, "Confirm your email" heading, the expiry statement, the code, the
  did-not-create-an-account disclaimer, the sign-off, and the footer reply-to and
  sent-to-address statements.
- The registration flow and the resend flow both produce the same styled email content
  structure (no divergence between the two call sites).
- Existing behavior is preserved: email is still sent to the correct recipient with the
  same subject, and a send failure is still handled the same way at each call site as
  today (silently logged on registration, thrown as a `BusinessException` on resend) —
  this feature does not change error handling, only the HTML content.

## Out of scope
- Any change to when the verification email is triggered, how the code is generated,
  code expiry duration values, or resend cooldown behavior — all covered by the existing
  `verify-email-on-registration` spec and unchanged here.
- Any change to the email subject line.
- Any new email type or template (e.g. welcome email, password reset email) — this is a
  restyle of the one existing verification email only.
- A general-purpose email templating engine/system for all emails — this spec covers only
  the verification email's content and styling; whether the implementation happens to
  introduce a reusable templating mechanism is an implementation decision, not a
  requirement (see Open questions).
- Exact color hex values, font stacks, and pixel-level spacing — the Figma screenshot
  describes intent (bold red, cream code box, green divider, Italian-flag tri-color bar)
  but exact values were not extracted from Figma tokens; see Open questions.
- Plain-text (non-HTML) email body variant — not mentioned in the current implementation
  or the Figma design; not added here.
- Localization of the new email copy — the existing email is English-only static text; this
  restyle keeps it English-only, consistent with current behavior.

## Open questions
- **`IEmailService.SendVerificationEmailAsync` signature**: the method currently accepts
  only `(string to, string code)`. The design requires the recipient's first name and the
  expiry in minutes, neither of which the method currently receives. Both values are
  already available to both callers (`RegistrationService` has `user.FirstName` and
  `_securityOptions.VerificationCodeExpiryMinutes`; `VerificationService` has the same).
  This spec assumes the signature will be extended (e.g. adding `firstName` and
  `expiryMinutes` parameters) rather than having `EmailService` re-derive them itself,
  since `EmailService` lives in `Shared` and must not depend on Identity's
  `SecurityOptions` usage pattern or reach into Identity for user data. Confirm this
  approach; the alternative (passing a single pre-built content object) is also acceptable
  but is an implementation choice, not a requirement.
- **"RISTORANTE" branding**: assumed to be literal static brand text matching the Figma
  design, not a per-organization configurable value, since this email is sent before the
  user has any organization context (verification happens pre-onboarding, account-level
  only, per the existing spec). Confirm this is correct and not meant to reflect a
  white-labeled/tenant-specific brand name.
- **Exact colors/typography**: the Figma screenshot was described visually (bold red
  header, cream/off-white code box, green divider, Italian tri-color bar) without exact
  hex codes, font family, or spacing values. This spec treats those as content/layout
  intent to be matched reasonably; exact values should be confirmed against the actual
  Figma file/tokens before or during implementation rather than guessed pixel-for-pixel.
- **HTML email client compatibility**: the current implementation is a raw inline HTML
  string with no table-based layout or client-compatibility considerations. This spec does
  not mandate a specific compatibility bar (e.g. Outlook desktop table-layout support) —
  it only requires the content/layout described above to render correctly in common
  clients. Confirm whether a specific compatibility bar (e.g. must render correctly in
  Outlook desktop, Gmail web, Apple Mail) is required, since that affects how defensively
  the HTML must be written (inline styles vs. tables vs. media queries).
- **Footer "reply to this email" behavior**: the footer states "Reply to this email and
  we'll help," implying replies should reach a monitored inbox. Confirm the configured
  `FromEmail`/`FromName` (`EmailOptions`) is a monitored, reply-capable address, or whether
  a distinct reply-to address is needed — this spec treats it as a copy/content
  requirement only, not a mailbox-configuration requirement.
