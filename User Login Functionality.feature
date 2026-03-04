Feature: User Login Functionality

  Scenario: Successful login with valid credentials
    Given I navigate to the login page
    When I enter username "testuser"
    And I enter password "Password123"
    And I click the login button
    Then I should see the dashboard
    And the welcome message should display "Welcome, testuser"
