Feature: Sample Calculator Feature
    As a user of the automation testing platform
    I want to verify basic calculator functionality
    So that I can ensure the testing framework works correctly

    Scenario: Add two numbers
        Given I have entered 50 into the calculator
        And I have entered 70 into the calculator
        When I press add
        Then the result should be 120 on the screen

    Scenario: Subtract two numbers
        Given I have entered 100 into the calculator
        And I have entered 30 into the calculator
        When I press subtract
        Then the result should be 70 on the screen

    @windows
    Scenario: UI automation driver is injected
        Then the UI automation driver should be available
