Feature: Password Reset Email with Secure Token

  Background:
    Given the password recovery system and SMTP service are operational
    And a registered user exists with email "registered.user@example.com" and known account details
    And the test SMTP mailbox and server-side email logs are accessible to the test framework
    And server time is synchronized with the test runner

  Scenario: Registered user requests password reset and receives email within SLA
    Given the user is on the login page
    And the user clicks the "Forgot Password?" link
    When the user enters "registered.user@example.com" and submits the form at T0
    Then the UI displays "Check your email for reset instructions"
    And the UI success message is visible
    And an outbound password reset email is sent to "registered.user@example.com" within 5 seconds of T0 (95th-percentile)
    And the UI success message auto-hides after 3 seconds

  Scenario: Reset email contains valid 32-character token using only A–Z and 0–9 and token is stored with 15-minute expiry
    Given a password reset email has been sent to "registered.user@example.com"
    When the test framework extracts the reset URL from the email body
    Then the token parameter value is exactly 32 characters long
    And the token contains only characters in the set A–Z and 0–9
    And the server has a record of that token associated with the user
    And the server-side token record includes an expiry timestamp exactly 15 minutes after the token generation time

  Scenario: User navigates with a valid, unexpired token and successfully resets password; token is invalidated thereafter
    Given a valid token exists for "registered.user@example.com" and is unexpired
    When the user navigates to "/reset-password?token={token}"
    Then the reset password form is displayed
    And a hidden form field contains the same token value
    When the user submits a new password that meets policy "Abcdef1!" (>=8 chars, 1 uppercase, 1 number, 1 special)
    Then the user's password is updated successfully
    And the server invalidates/clears the token so subsequent uses of the same token fail

  Scenario: Password reset form enforces password policy and rejects non-compliant passwords
    Given a valid token exists for "registered.user@example.com" and is unexpired
    When the user navigates to "/reset-password?token={token}"
    Then the reset password form is displayed
    When the user submits a new password "short1!" (less than 8 characters)
    Then the system shows a validation error indicating the minimum length requirement
    And the password is not updated
    When the user submits "alllowercase1!" (no uppercase)
    Then the system shows a validation error indicating the uppercase requirement
    And the password is not updated
    When the user submits "NoNumber!" (no numeric character)
    Then the system shows a validation error indicating the numeric requirement
    And the password is not updated
    When the user submits "NoSpecial1" (no special character)
    Then the system shows a validation error indicating the special character requirement
    And the password is not updated

  Scenario: Reset link with expired token shows expiry message and does not show reset form
    Given a token was generated for "registered.user@example.com" more than 15 minutes ago
    When the user navigates to "/reset-password?token={expired_token}"
    Then the system displays a clear "token expired" or "reset link expired" message
    And the reset password form is not displayed
    And the token is considered invalid for password reset

  Scenario: Reset link with non-existent or malformed token is rejected
    Given no token exists for value "INVALID_TOKEN_123"
    When the user navigates to "/reset-password?token=INVALID_TOKEN_123"
    Then the system displays an "invalid reset token" message
    And the reset password form is not displayed

  Scenario: Submitting the same valid token twice is not permitted (token is single-use)
    Given a valid token exists for "registered.user@example.com" and is unexpired
    When the user navigates to "/reset-password?token={token}" and successfully resets password
    Then the server invalidates the token
    When the user attempts to reuse the same "/reset-password?token={token}" link
    Then the system displays an "invalid or expired token" message
    And no further password changes occur using that token

  Scenario: Email sent header and subject are correct and major providers receive the message
    Given the system sends a password reset email to "registered.user@example.com"
    When the outbound email headers and body are inspected in the test SMTP logs
    Then the From header is exactly "noreply@elitealearning.com"
    And the Subject header is exactly "Reset Your Password - EliteALearning"
    And delivery records show successful acceptance by major providers (Gmail, Outlook, Yahoo) test recipients in SMTP logs or provider test inboxes

  Scenario: Client-side validation prevents submission of malformed email address
    Given the user is on the "Forgot Password?" form
    When the user enters "not-an-email" into the email input and submits
    Then the form displays an inline "invalid email format" validation error
    And no email is sent

  Scenario: Requesting reset for unregistered email does not reveal account existence (no account enumeration)
    Given the user is on the login page
    When the user clicks "Forgot Password?" and submits an email not present in the user store "unknown.user@example.com"
    Then the UI displays "Check your email for reset instructions"
    And no password reset email is sent to "unknown.user@example.com"
    And the system logs the request without leaking account existence to the user

  Scenario: Email delivery timing - measure 95th-percentile compliance under load
    Given a test harness that simulates N users submitting the forgot-password form concurrently (N representing 95th-percentile load)
    When each simulated user submits a valid registered email
    Then at least 95% of the password reset emails are handed off to SMTP within 5 seconds of submission
    And any failures or slow deliveries are recorded for analysis

  Scenario: UI success message visibility and accessibility requirements
    Given the user submits a valid registered email for password reset
    When the UI success message "Check your email for reset instructions" is displayed
    Then the message is accessible (ARIA role=status or live region) to assistive technologies
    And the message auto-hides after 3 seconds
    And keyboard focus behavior remains compliant (focus is not lost unexpectedly)

  Scenario: Token generation character set and uniqueness under high volume
    Given the system generates tokens for multiple concurrent reset requests
    When tokens are generated for M requests in a short time window
    Then each token is exactly 32 characters long and contains only A–Z and 0–9
    And tokens are unique (no duplicates among the M generated tokens)
    And each token record includes a creation timestamp and expiry 15 minutes later

  Scenario: Server-side token storage integrity on failed email delivery
    Given a password reset token is generated and stored for "registered.user@example.com"
    When email delivery to the user's mailbox fails (SMTP bounce or transient failure)
    Then the server still stores the token with its 15-minute expiry
    And the system records the delivery failure in logs for retry/alerting
    And the UI still shows the generic "Check your email for reset instructions" message

  Scenario: Reset workflow works for logged-in user initiating a reset (edge case)
    Given the user is currently authenticated and on the login page
    When the user clicks "Forgot Password?", enters their registered email and submits
    Then the system sends a password reset email to the registered email address
    And the UI displays "Check your email for reset instructions"
    And the user remains authenticated until they change their password via the reset flow

  Scenario: Large token or URL tampering is sanitized and rejected
    Given a valid token exists
    When the user manipulates the token parameter by appending extra characters or encoded payload
    Then the system treats the token as invalid and displays an "invalid reset token" message
    And no sensitive internal data is exposed in the response

Notes:
- Scenarios referencing timing (≤5 seconds, auto-hide 3s, 15-minute expiry) require test harnesses that can measure timestamps precisely and control time where appropriate (e.g., time mocks or database timestamp checks).
- Email reception by providers should be validated via test inboxes, SMTP capture (mailtrap), or provider-specific test APIs to avoid flakiness in automated runs. (Header Search scenarios)
'} }```jsondaughterRuntime error: Unexpected token '`' in JSON at position 1842. It seems the tool returned error due to the content including backticks? The error shows create_file call failed due to JSON encoding—maybe the tool input must be valid JSON; our file_contents string includes newlines and quotes, but that's fine. The error arises from improper quoting? The tool wrapper likely expects JSON; we provided JSON. The error message includes 