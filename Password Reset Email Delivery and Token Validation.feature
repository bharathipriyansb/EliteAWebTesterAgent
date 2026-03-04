Feature: Password Reset Email Delivery and Token Validation

  Background:
    Given the password reset system and mail delivery service are operational
    And the mail delivery test mailbox access and logs are available for verification
    And a registered user exists with email "registered@example.com"
    And the user is on the login page

  Scenario: Registered user requests password reset — email sent with valid token within 5 seconds (AC1)
    Given the registered user clicks the "Forgot Password?" link
    And the reset-email input is visible
    When the user enters "registered@example.com" into the reset-email input
    And the user clicks the send-reset button
    Then the UI displays the success message "Check your email for reset instructions"
    And an email is delivered to "registered@example.com" within 5 seconds
    And the email From address is "noreply@elitealearning.com"
    And the email Subject is "Reset Your Password - EliteALearning"
    And the email body includes a reset URL containing a token parameter
    And the token is exactly 32 characters long and contains only uppercase letters A–Z and digits 0–9
    And a token record matching the token is stored in the database with a creation timestamp

  Scenario: User clicks reset URL with a valid, unexpired token — password reset form displayed with token carried/hidden (AC2)
    Given a valid token was generated for "registered@example.com" less than 15 minutes ago
    And the user has received the email containing the reset URL with that token
    When the user navigates to the reset URL containing the token
    Then the system validates the token exists and is not expired
    And the password reset form is displayed
    And the token is included in the reset form submission payload (carried/hidden input)
    And the form allows entering a new password and password confirmation

  Scenario: Token expires after 15 minutes — attempts to use token are rejected with expiry error (AC3)
    Given a token was generated and stored in the database for "registered@example.com" 16 minutes ago
    When the user attempts to navigate to the reset URL containing that token
    Then the system rejects the token as expired
    And the user sees an error message indicating the token is expired and must request a new reset
    And the password reset form is not displayed
    And no password change is permitted using the expired token

  Scenario: Reset email delivered to major providers with correct From and Subject (AC4)
    Given the system sends a reset email to addresses at Gmail, Outlook, and Yahoo
    When the reset emails are processed by those providers
    Then each provider receives the email successfully (mailbox contains the message)
    And each email's From address is "noreply@elitealearning.com"
    And each email's Subject is "Reset Your Password - EliteALearning"
    And each email body includes a reset URL containing a token parameter

  Scenario: Unregistered email entered — system shows generic success message and does not send an email (security best practice)
    Given the user clicks the "Forgot Password?" link
    When the user enters "unregistered@example.com" into the reset-email input
    And the user clicks the send-reset button
    Then the UI displays the same success message "Check your email for reset instructions"
    And no password reset email is sent to "unregistered@example.com"
    And the system does not reveal whether the email is registered

  Scenario: Malformed email input displays client-side validation error
    Given the user clicks the "Forgot Password?" link
    When the user enters "not-an-email" into the reset-email input
    And the user clicks the send-reset button
    Then the form shows a validation error "Please enter a valid email address"
    And no request to send a reset email is initiated

  Scenario: Token format validation — invalid token rejected when used in reset URL
    Given a token value "abc123!@#short" (invalid length and characters) is present in a reset URL
    When the user navigates to the reset URL containing that token
    Then the system rejects the token as invalid
    And the user sees an error indicating the token is invalid or malformed
    And the password reset form is not displayed

  Scenario: Multiple consecutive reset requests generate unique tokens (uniqueness)
    Given the registered user requests a password reset and receives token T1
    When the same registered user immediately requests another password reset and receives token T2
    Then T1 and T2 are different 32-character alphanumeric tokens
    And both tokens are recorded in the database with distinct creation timestamps
    And T2 can be used (if within expiry) and T1 remains valid until expiry unless policy invalidates prior tokens

  Scenario: Attempt to reuse a token after successful password reset is rejected (single-use behavior)
    Given the registered user used token T to successfully reset their password
    When the user attempts to reuse token T to access the reset form again
    Then the system rejects the token as invalid or already used
    And the user sees an appropriate error stating the token cannot be reused

  Scenario: Email delivery performance measurement — verify 95th percentile <= 5 seconds
    Given multiple (N) password reset requests are performed under representative load (N ≥ configured sample size)
    When email delivery times are measured for each request
    Then the 95th percentile of delivery times is ≤ 5 seconds
    And any delivery exceeding 5 seconds is flagged for investigation
