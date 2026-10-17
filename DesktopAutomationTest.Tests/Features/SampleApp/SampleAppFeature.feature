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
        Then the ResultTextBlock should display "Hello, John Doe! You are 25 years old from Unknown (User)."
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

    Scenario: Application displays validation error when required fields are empty
        Given I launch the sample application
        When I click the SubmitButton
        Then the ResultTextBlock should display "Please fill in all fields."
        And I close the application

    Scenario: Application displays validation error when age is not a number
        Given I launch the sample application
        When I enter "John" into the FirstNameTextBox
        And I enter "Doe" into the LastNameTextBox
        And I enter "invalid_age" into the AgeTextBox
        And I click the SubmitButton
        Then the ResultTextBlock should display "Age must be a valid number."
        And I close the application

    @windows
    Scenario: User can submit form with country and role
        Given I launch the sample application
        When I enter "Ahmed" into the FirstNameTextBox
        And I enter "Ali" into the LastNameTextBox
        And I enter "30" into the AgeTextBox
        And I select "Egypt" from the CountryComboBox
        And I select role "Admin"
        And I click the SubmitButton
        Then the ResultTextBlock should display "Hello, Ahmed Ali! You are 30 years old from Egypt (Admin)."
        And I close the application

    @windows
    Scenario: User can toggle the RememberMe checkbox
        Given I launch the sample application
        When I check the RememberMeCheckBox
        Then the RememberMeCheckBox should be checked
        When I uncheck the RememberMeCheckBox
        Then the RememberMeCheckBox should not be checked
        And I close the application