Feature: Task Reordering via Drag-and-Drop (persisted)

  Background:
    Given the task list page is loaded in a supported browser
    And local storage is available for the page

  Scenario: Enter drag mode and show grab cursor when starting a drag (AC1)
    Given the task list contains multiple tasks (Task A, Task B, Task C)
    When the user presses mousedown on "Task B" and holds to start a drag
    Then "Task B" enters drag mode
    And the cursor changes to a grab/drag icon
    And a dragging state (e.g., CSS class or aria-dragging) is activated for "Task B"
    And no other task is in dragging state

  Scenario: Dynamic highlighting of valid drop zones while dragging (AC2)
    Given the user has started dragging "Task A"
    When the user moves the cursor up and down over the list
    Then valid drop zones update as the cursor moves
    And each valid drop zone shows a visible highlight (e.g., ~2px line or outline)
    And only the current nearest drop zone is highlighted at any position

  Scenario: Successful drop repositions task, animates, and persists order (AC3)
    Given the task list contains multiple tasks (Task 1, Task 2, Task 3)
    And the initial order is persisted in local storage
    When the user drags "Task 3" and releases it over the drop zone between Task 1 and Task 2
    Then "Task 3" is inserted between Task 1 and Task 2 in the UI
    And the list animates to the new order with a smooth transition of approximately 200ms
    And the new order is written to local storage
    And on subsequent page reload the order remains Task 1, Task 3, Task 2

  Scenario: Dropping outside list snaps to nearest valid position without overlap or errors (AC4)
    Given the user is dragging "Task X" and moves the cursor outside the visible list bounds
    When the user releases the mouse outside the list bounds
    Then "Task X" snaps to the nearest valid position in the list (top or bottom as appropriate)
    And no two tasks visually overlap after snapping
    And no console errors are produced during the operation

  Scenario: Dragging is disabled on an empty list (AC4)
    Given the task list is empty
    When the user attempts to press mousedown and drag on the list area
    Then dragging is not initiated
    And no drag cursor is shown
    And no console errors are produced

  Scenario: Dragging is disabled while an item is in edit mode (AC4)
    Given "Task Y" is in inline edit mode (editing UI active)
    When the user attempts to press mousedown and drag any task (including "Task Y")
    Then no task enters drag mode
    And the edit mode remains active
    And the UI prevents drag interactions until edit mode is exited

  Scenario: Rapid successive drags queue and do not overlap (performance/edge)
    Given the task list contains multiple tasks in order (T1, T2, T3, T4)
    When the user performs a rapid sequence of drag-and-drop moves (e.g., move T4 to top, then immediately move T1 to bottom) before earlier animations complete
    Then drag operations are queued or serialized to avoid animation overlap
    And each transition completes cleanly (no visual overlap of items)
    And the final order matches the sequence of completed drops
    And the final order is persisted to local storage
    And no console errors occur during queuing or concurrent animations

  Scenario: Animation duration is respected within tolerance when reordering (AC3)
    Given the user drags an item to a new position
    When the drop completes and the list animates to the new order
    Then the animation duration is approximately 200ms (acceptance tolerance e.g., 150ms–300ms)
    And the animation is smooth (no abrupt jumps or layout flashing)

  Scenario: No overlap and consistent state after cancelling a drag mid-operation (negative/edge)
    Given the user begins dragging "Task Z"
    When the user cancels the drag (Esc key or pointer cancel) before releasing over a drop zone
    Then "Task Z" returns to its original position
    And the list state remains unchanged and consistent
    And no console errors are produced
