Feature: Advanced Keyboard and Mouse Input
    As a test automation engineer
    I want to simulate advanced keyboard and mouse interactions
    So that I can automate complex desktop UI workflows

    @windows @keyboard-mouse
    Scenario: User can double-click a button to trigger its double-click action
        Given I launch the sample application
        When I double-click the "SubmitButton"
        Then the application should respond to the double-click

    @windows @keyboard-mouse
    Scenario: User can right-click an element to open a context menu
        Given I launch the sample application
        When I right-click the "ResultTextBlock"
        Then the application should respond to the right-click

    @windows @keyboard-mouse
    Scenario: User can hover over an element to trigger tooltip
        Given I launch the sample application
        When I hover over the "SubmitButton"
        Then the hover action should complete successfully

    @windows @keyboard-mouse
    Scenario: User can press the Tab key to navigate between fields
        Given I launch the sample application
        When I click the "FirstNameTextBox"
        And I press key "Tab"
        Then the keyboard input should be accepted

    @windows @keyboard-mouse
    Scenario: User can press the Escape key to cancel focus
        Given I launch the sample application
        When I press key "Escape"
        Then the keyboard input should be accepted

    @windows @keyboard-mouse
    Scenario: User can send text via keyboard globally after focusing a field
        Given I launch the sample application
        When I click the "FirstNameTextBox"
        And I send keys "Hello"
        Then the keyboard input should be accepted

    @windows @keyboard-mouse
    Scenario: User can submit the form using the Enter key
        Given I launch the sample application
        When I enter "John" into the FirstNameTextBox
        And I enter "Doe" into the LastNameTextBox
        And I enter "25" into the AgeTextBox
        And I click the "SubmitButton"
        And I press key "Return"
        Then the keyboard input should be accepted
        And I close the application
