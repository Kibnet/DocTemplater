# DocTemplater UI Tests

This document describes the UI automation tests for the DocTemplater application. These tests verify the user interface functionality in a headless environment.

## Purpose

UI tests verify that the application's user interface works correctly without requiring manual interaction. They simulate user actions and verify the expected behavior of UI components.

## Test Project Structure

The UI tests are located in a separate project:
- Project: `DocTemplater.UITests`
- Framework: xUnit with Avalonia.Headless
- Test Runner: `dotnet test`

## Test Cases

### 1. Application Startup Test
Verifies that the application starts up correctly:
- Application initializes without errors
- Main window is displayed

### 2. Template Selection View Test
Verifies that the template selection view is displayed on startup:
- Main window contains template selection interface
- UI elements are accessible

### 3. Navigation Test
Verifies navigation between different views:
- View transitions work correctly
- UI state is maintained properly

### 4. Integrated Workflow Test
Verifies the complete user workflow from template selection to document generation:
- Template discovery service finds templates
- Placeholder extraction service extracts placeholders
- Value persistence service saves and retrieves values
- Document generation service creates documents
- Generated documents contain expected content

## Running UI Tests

### Using Command Line
```bash
cd c:\Projects\ИЗП\Sources\DocTemplater
dotnet test --filter "FullyQualifiedName~DocTemplater.UITests"
```

### Using the Batch Script
Double-click on `run-ui-tests.bat` to run the UI tests.

## Test Results

All UI tests should pass for the application to be considered stable. As of the latest update, all 7 UI tests are passing.

## Automation IDs

The following UI elements have automation IDs for testing purposes:

### TemplateSelectionView
- `RefreshButton` - Refresh templates button
- `SelectTemplateButton` - Select template button
- `TemplatesListBox` - List of available templates

### DataInputView
- `FieldsItemsControl` - Container for dynamic input fields
- `ValueTextBox` - Text input field for string/integer values
- `DateValuePicker` - Date picker for date values
- `OutputFilePathTextBox` - Output file path text box
- `BackButton` - Navigate back button
- `ClearButton` - Clear form button
- `SaveButton` - Save document button

## Maintenance

UI tests should be updated when:
- New UI elements are added
- Existing UI elements are modified
- Navigation flow changes
- Automation IDs are changed

The tests are located in:
- `DocTemplater.UITests\E2ESmokeTests.cs` - Basic smoke tests
- `DocTemplater.UITests\CompleteWorkflowTests.cs` - Complete workflow tests
- `DocTemplater.UITests\IntegratedWorkflowTests.cs` - Integrated workflow tests
- `DocTemplater.UITests\DemonstrationHeadlessTests.cs` - Demonstration of proper Avalonia headless testing concepts