Feature: Product Search Functionality

  Scenario: Search products by keyword
    Given I navigate to the homepage
    When I enter search term "laptop"
    And I click the search button
    Then I should see search results page
    And the results should contain "laptop"
