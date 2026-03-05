Feature: Password Reset Email and Token Flow for Registered Users

  Background:
    Given the password reset service (frontend, backend, API, and SMTP) is operational
    And test mailboxes for Gmail, Outlook, and Yahoo are provisioned and accessible
    And a registered user exists with email "registered.user@example.com" and known credentials
    And rate limiting is configured as "max 5 forgot-password requests per hour per IP"

  Scenario: Registered user requests password reset and sees success feedback (AC1)
    Given the user is on the login page and clicks "Forgot Password?"
    When the user enters "registered.user@example.com" and submits the forgot-password form
    Then the frontend displays "Check your email for reset instructions"
    And the backend accepts the POST /api/forgot-password request and responds with HTTP 200
    And a password reset email is sent to "registered.user@example.com" within 5 seconds of form submission

  Scenario: Reset email contains a unique 32-character alphanumeric token and correct link and headers (AC2)
    Given a password reset email was sent to "registered.user@example.com"
    When inspecting the received email
    Then the email From header equals "noreply@elitealearning.com"
    And the email Subject equals "Reset Your Password - EliteALearning"
    And the email body or link contains a token parameter in a URL like /reset-password?token={token}
    And the token matches the regex "^[A-Za-z0-9]{32}$"
    And the token stored in the backend data store matches the token in the email and is associated with the user's account
    And tokens generated for multiple separate requests are unique

  Scenario: User clicks valid reset link within 15 minutes and successfully resets password (AC3 happy path)
    Given the user received a valid token and clicks the /reset-password?token={valid_token} link within 15 minutes of issuance
    When the Reset Password form is displayed
    Then the form includes a hidden token field pre-filled with {valid_token}
    And the form enforces password rules: minimum 8 characters, at least 1 uppercase letter, at least 1 number, and at least 1 special character
    When the user enters "NewP@ssw0rd" and confirms it and submits the form
    Then the backend accepts the POST /api/reset-password with the correct token and new password and returns success (HTTP 200)
    And the user's password is updated and the token is invalidated/marked as used
    And the frontend shows a success message "Your password has been reset successfully" (or equivalent)

  Scenario: Attempt to use an expired token is rejected with an appropriate message (AC4)
    Given a token was issued more than 15 minutes ago
    When the user navigates to /reset-password?token={expired_token} or submits the token to POST /api/reset-password
    Then the system rejects the action and returns an error stating "Token expired" or equivalent
    And the frontend displays a clear expiry error message to the user
    And no password change occurs

  Scenario: Attempt to reuse an already-used token is rejected (AC3 negative)
    Given a token was used previously to successfully reset a password (token is marked used)
    When the user attempts to use the same /reset-password?token={used_token} link again
    Then the system rejects the action and returns an error stating "Token invalid or already used"
    And the frontend displays an appropriate error message
    And no further password changes are performed with that token

  Scenario: Invalid or tampered token format is rejected with invalid-token message (edge case)
    Given the user has a token string that is not 32 alphanumeric characters (e.g., shorter, contains symbols)
    When the user submits /reset-password?token={invalid_format_token} or posts it to /api/reset-password
    Then the system rejects the token and returns an "Invalid token" error
    And the frontend displays an appropriate validation error and does not allow password reset

  Scenario: Missing token on reset-password page shows validation error
    Given a user navigates to /reset-password without a token parameter
    When the reset page loads
    Then the frontend shows an error or redirects to the login/forgot-password flow with a message "Token missing or invalid"
    And the backend does not process any password reset without a token

  Scenario: Password validation enforces complexity rules and shows clear messages (AC3 negative)
    Given the user has a valid token within 15 minutes
    When the user enters "short" as the new password and submits
    Then the frontend blocks submission and displays errors for minimum length and required character types
    When the user enters "NoNumber!" (missing number) and submits
    Then the frontend blocks submission and displays an error about requiring at least one number
    When the user finally enters a valid "ValidP@ss1" and confirms correctly and submits
    Then the password reset completes successfully and token is invalidated

  Scenario: Backend stores token with 15-minute expiry and invalidates after use (API test)
    Given a POST /api/forgot-password request is accepted for "registered.user@example.com"
    When a token is created by the backend
    Then the token record in the datastore includes:
      And an expiry timestamp equal to issuance_time + 15 minutes
      And a flag "used" set to false
    When POST /api/reset-password is called successfully with that token
    Then the datastore updates the token's "used" flag to true and prevents reuse

  Scenario: Rate limiting prevents more than 5 forgot-password requests per hour per IP (AC4 negative)
    Given the test client IP has already made 5 POST /api/forgot-password requests in the last hour
    When the client attempts a 6th request with a registered email
    Then the backend rejects the request with HTTP 429 or appropriate rate-limit response
    And the response body contains a clear rate-limit message like "Too many password reset requests. Please try again later."
    And no additional password reset email is sent for the rejected request

  Scenario: Frontend shows generic success message for both registered and unregistered emails to avoid account enumeration (security edge case)
    Given a user submits the forgot-password form with an unregistered email "unknown@example.com"
    When the frontend receives response from POST /api/forgot-password
    Then the frontend displays "Check your email for reset instructions"
    And the backend does not create a token or send an email for unregistered accounts
    And logs audit info for the attempt

  Scenario: Reset email deliverability to common providers (Gmail, Outlook, Yahoo) (AC4 deliverability)
    Given a password reset is triggered for three test accounts: gmail.test@example.com, outlook.test@example.com, yahoo.test@example.com
    When the system sends reset emails
    Then each provider's test mailbox receives the email within 5 seconds
    And each received email contains correct From, Subject, and a valid 32-character token link

  Scenario: Email subject/from headers are enforced and verifiable (AC2)
    Given a reset email is received
    When inspecting email headers
    Then the "From" header equals "noreply@elitealearning.com"
    And the "Subject" header equals "Reset Your Password - EliteALearning"
    And the reply-to header is either absent or set to a no-reply address per policy

  Scenario: API rejects reset when token does not match stored token for the user (security negative)
    Given an attacker submits POST /api/reset-password with a valid token that does not belong to the target account or with a different email context
    When the backend validates token ownership
    Then the backend rejects the reset with "Invalid token" or "Token does not match account" and returns HTTP 400/401
    And no password changes occur

  Scenario: Email delivered within SLA (timing verification) and sender metrics logged
    Given a forgot-password form is submitted
    When measuring time from form submission to SMTP accepted delivery event
    Then the email is accepted by SMTP within 5 seconds
    And the backend logs send timestamp and delivery status for auditing

  Scenario: Concurrent reset requests generate unique tokens and only the last valid token is usable (edge concurrency)
    Given multiple forgot-password requests are submitted in short succession for the same account
    When the backend issues multiple tokens
    Then all tokens are unique and only tokens that are unexpired and not marked used are valid
    And if policy dictates, only the most recently issued token should be valid (test both configured policies as applicable)
    And using an older token should be rejected if policy invalidates older tokens

  Scenario: Accessibility: forgot-password and reset-password pages are accessible (basic a11y checks)
    Given the forgot-password and reset-password pages are opened with assistive technology enabled
    When the user navigates using keyboard-only and screen reader labels are read
    Then form controls have accessible labels, focus order is logical, and error messages are announced/visible

  Scenario: Error handling when email delivery fails (SMTP error)
    Given SMTP is temporarily unavailable and POST /api/forgot-password is invoked
    When the backend cannot enqueue/send email
    Then the backend returns an appropriate error (HTTP 502/503 or defined error) and the frontend shows a user-facing message like "Unable to send reset email. Please try again later."
    And the action is retried according to backoff policy or logged for operator intervention
