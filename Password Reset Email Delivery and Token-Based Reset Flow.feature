Feature: Password Reset Email Delivery and Token-Based Reset Flow

  Background:
    Given the password reset service, API, and email delivery service are operational
    And HTTPS is enforced for all endpoints
    And the test environment clock can be controlled (mockable) for token expiry tests
    And there exists a registered user "registered.user@example.com" with a known account

  Scenario: Registered user requests password reset and receives reset email within SLA
    Given the user is on the "Forgot Password" page
    And the user is a registered account holder with email "registered.user@example.com"
    When the user submits "registered.user@example.com" to POST /api/forgot-password from IP "1.2.3.4"
    Then the UI displays "Check your email for reset instructions"
    And the system sends an email to "registered.user@example.com" from "noreply@elitealearning.com"
    And the email subject is "Reset Your Password - EliteALearning"
    And the email is delivered within 5 seconds
    And the email contains a reset URL with a query parameter token that is exactly 32 characters long and contains only uppercase A–Z and digits 0–9
    And the generated token is stored server-side with a 15-minute expiry associated to the correct user

  Scenario: Unregistered email submission does not disclose account existence but shows same UI
    Given a non-registered email "no.such.user@example.com"
    When the user submits "no.such.user@example.com" to POST /api/forgot-password
    Then the UI displays "Check your email for reset instructions"
    And no password reset email is sent to "no.such.user@example.com"
    And the system does not create a stored reset token for that address

  Scenario: Rate limiting enforced on forgot-password requests (exceeds 5 per hour per IP)
    Given the client IP "9.9.9.9" has already submitted POST /api/forgot-password 5 times in the last hour
    When the client IP "9.9.9.9" submits another POST /api/forgot-password within the same hour
    Then the system returns HTTP 429 Too Many Requests
    And the response includes a human-readable message indicating rate limit exceeded and retry window
    And the UI displays a clear instruction to retry later without disclosing internal rate values

  Scenario: Reset URL with valid token loads reset form (GET returns HTTP 200)
    Given a valid reset token T (32 uppercase alphanumeric) was generated less than 15 minutes ago for "registered.user@example.com"
    When the user opens GET /reset-password?token=T over HTTPS
    Then the system responds with HTTP 200
    And the response contains the password reset form
    And the reset form includes a hidden token field populated with T
    And the page is accessible and rendered correctly across supported browsers

  Scenario: Reset URL with expired token returns HTTP 400 and prompts for new request
    Given a token T_expired was generated 16 minutes ago for "registered.user@example.com"
    When the user opens GET /reset-password?token=T_expired
    Then the system responds with HTTP 400
    And the UI displays a clear message "This reset link has expired. Please request a new password reset."
    And no password reset form is shown

  Scenario: Reset URL with syntactically invalid token returns HTTP 404 and shows invalid message
    Given a token T_invalid contains characters outside uppercase A–Z and 0–9 or is not 32 characters
    When the user opens GET /reset-password?token=T_invalid
    Then the system responds with HTTP 404
    And the UI displays a clear message "Invalid password reset link. Please request a new password reset."
    And the system does not reveal whether a corresponding account exists

  Scenario: Successful password reset with valid token and compliant password
    Given a valid token T_active was generated less than 15 minutes ago for "registered.user@example.com"
    And the user navigated to the reset form with hidden token T_active
    When the user submits POST /api/reset-password with payload { token: T_active, password: "NewPassw0rd!", confirmPassword: "NewPassw0rd!" }
    Then the system validates the token and password policy (min 8 chars, at least 1 uppercase, 1 number, 1 special char)
    And the system updates the user's password to the new password
    And the system invalidates T_active (token cannot be used again)
    And the system returns HTTP 200
    And the UI displays a password reset success confirmation message

  Scenario: Password reset fails when password does not meet password policy
    Given a valid token T_active was generated less than 15 minutes ago for "registered.user@example.com"
    When the user submits POST /api/reset-password with payload { token: T_active, password: "short", confirmPassword: "short" }
    Then the system returns HTTP 400
    And the response contains validation errors indicating password must be minimum 8 chars, include an uppercase letter, a number, and a special character
    And the token remains valid (not invalidated) for subsequent valid attempts until expiry

  Scenario: Password reset fails when password and confirmPassword do not match
    Given a valid token T_active was generated less than 15 minutes ago
    When the user submits POST /api/reset-password with payload { token: T_active, password: "GoodPass1!", confirmPassword: "Mismatch1!" }
    Then the system returns HTTP 400
    And the response contains a clear "Passwords do not match" validation message
    And the token remains valid until expiry or successful use

  Scenario: Token cannot be reused after successful reset (token invalidation after use)
    Given a token T_used was successfully used to reset the password earlier and was invalidated
    When the client attempts GET /reset-password?token=T_used or POST /api/reset-password using T_used
    Then the system responds with HTTP 404
    And the UI displays "Invalid password reset link. Please request a new password reset."

  Scenario: Token length boundary checks — token shorter or longer than 32 chars treated as invalid
    Given tokens T_short (31 chars) and T_long (33 chars)
    When the user opens GET /reset-password?token=T_short or GET /reset-password?token=T_long
    Then the system responds with HTTP 404 for each request
    And the UI displays "Invalid password reset link. Please request a new password reset."

  Scenario: Token with lowercase letters or special characters treated as invalid
    Given token T_badChars contains lowercase letters or symbols (e.g., "abc123...!")
    When the user opens GET /reset-password?token=T_badChars
    Then the system responds with HTTP 404
    And the UI displays "Invalid password reset link. Please request a new password reset."

  Scenario: Forgot-password request triggers monitoring/logging when email delivery exceeds SLA
    Given the email delivery service experiences a delay causing delivery time > 5 seconds
    When a valid forgot-password request is processed
    Then the system eventually delivers the email
    And the system logs an SLA breach event with delivery latency details
    And an operational alert is created or recorded for follow-up
    And the UI still displays "Check your email for reset instructions"

  Scenario: HTTP access to reset endpoint is redirected to HTTPS
    Given a valid token T exists
    When the user requests GET http://<host>/reset-password?token=T over plain HTTP
    Then the system responds with an HTTP redirect to the equivalent HTTPS URL
    And the redirected request over HTTPS returns HTTP 200 and loads the reset form

  Scenario: Forgot-password endpoint resists user enumeration (consistent UX for registered and unregistered emails)
    Given a registered email "registered.user@example.com" and an unregistered email "no.such.user@example.com"
    When the user submits either email to POST /api/forgot-password
    Then the UI displays the identical message "Check your email for reset instructions" for both cases
    And backend behavior differs: registered email triggers token generation and email send; unregistered does not
    And no response reveals whether an account exists for the provided email

  Scenario: Audit trail records token generation, send, and use events
    Given a valid reset request occurred for "registered.user@example.com"
    When the token is generated and email is sent and later the token is used to reset password
    Then the system records audit events for token generation with timestamp, email send with delivery status and latency, and token consumption
    And the audit records include the requesting IP address and user identifier for security review

  Scenario: Malformed POST payload to /api/reset-password returns appropriate validation error
    Given a valid token T_active exists
    When a client submits POST /api/reset-password with a missing token field or malformed JSON
    Then the system returns HTTP 400
    And the response contains a clear validation error describing the missing or malformed field
    And no password change occurs

  Scenario: Cross-browser rendering of reset form and hidden token field
    Given a valid token T exists
    When the user opens GET /reset-password?token=T in supported browsers (Chrome, Firefox, Edge, Safari)
    Then the reset form and hidden token field render correctly in each browser
    And the token value is present in the form payload when submitted

  Scenario: Resend after token expiry — user requested new reset after previous token expired
    Given token T_old expired 20 minutes ago for "registered.user@example.com"
    When the user submits another POST /api/forgot-password for "registered.user@example.com"
    Then the system generates a new token T_new (32 uppercase alphanumeric) with a fresh 15-minute expiry
    And the system sends a new email with T_new
    And T_old remains invalid and cannot be used
