Feature: Advanced Desktop Controls Automation
    As a test automation engineer
    I want to interact with common desktop controls (CheckBox, ComboBox, RadioButton)
    So that I can automate diverse desktop user interfaces

    @windows @controls
    Scenario: User can toggle and verify a checkbox
        Given I launch the sample application
        When I check the RememberMeCheckBox
        Then the RememberMeCheckBox should be checked
        When I uncheck the RememberMeCheckBox
        Then the RememberMeCheckBox should not be checked
        And I close the application

    @windows @controls
    Scenario: User can select an option from a ComboBox dropdown
        Given I launch the sample application
        When I select "Egypt" from the CountryComboBox
        Then the selected item in CountryComboBox should be "Egypt"
        And I close the application

    @windows @controls
    Scenario: User can select a RadioButton option
        Given I launch the sample application
        When I select role "Admin"
        Then the AdminRoleRadioButton should be selected
        And I close the application
