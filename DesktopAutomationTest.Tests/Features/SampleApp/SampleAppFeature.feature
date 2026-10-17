Feature: Sample Application Testing
    As a user of the automation testing platform
    I want to verify the sample WPF application functionality
    So that I can ensure the testing framework works correctly with real applications

    @windows
    Scenario: User can fill out and submit the sample application form
        Given I launch the sample application
        When I enter "John" into the FirstNameTextBox
        And I enter "Doe" into the LastNameTextBox
        And I enter "25" into the AgeTextBox
        And I click the SubmitButton
        Then the ResultTextBlock should display "Hello John Doe! You are 25 years old."
        And I close the application

    Scenario: User can clear the form
        Given I launch the sample application
        When I enter "Jane" into the FirstNameTextBox
        And I enter "Smith" into the LastNameTextBox
        And I enter "30" into the AgeTextBox
        And I click the ClearButton
        Then the FirstNameTextBox should be empty
        And the LastNameTextBox should be empty
        And the AgeTextBox should be empty
        And I close the application