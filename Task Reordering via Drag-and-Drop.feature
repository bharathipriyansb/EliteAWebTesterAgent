Feature: Task Reordering via Drag-and-Drop

  Background:
    Given the TodoMVC application is running in the browser
    And the user is on the main todo list page
    And the browser supports drag-and-drop and localStorage is available

  Scenario: Reorder tasks by drag-and-drop with visual feedback and animation (happy path)
    Given the list contains at least two tasks: "Task A" (incomplete) and "Task B" (complete)
    When the user hovers over "Task A" and presses and holds the drag handle or task
    Then "Task A" enters drag mode (draggable state is visually indicated)
    And as the user moves the cursor, valid drop zones in the list highlight following cursor movement
    When the user releases the mouse over the target drop zone between items
    Then "Task A" is repositioned to the target location in the list
    And the list animates to the new order showing visual confirmation of the change
    And no task's completion status is altered during reorder

  Scenario: Persist reordered list and preserve completion status after page reload (localStorage)
    Given the list contains at least three tasks with mixed completion statuses
    And the user drags "Task C" from position 3 to position 1 and releases
    Then the UI shows the updated order immediately
    And the application saves the updated order to localStorage
    When the user reloads the page
    Then the todo list displays the same updated order from localStorage
    And each task's completion status remains the same as before the reload

  Scenario: Prevent drag when the list is empty (no reordering occurs)
    Given the todo list is empty
    When the user attempts to press/hold or start a drag operation on an empty area or non-existent task
    Then the drag action is prevented
    And no reordering occurs and no errors are shown
    And the list remains empty

  Scenario: Prevent drag when a task is in edit mode (no reordering occurs)
    Given the list contains tasks and "Task D" is opened in inline edit mode
    When the user attempts to start a drag on "Task D"
    Then the drag action is prevented
    And "Task D" remains in edit mode with its position unchanged
    And no other tasks are affected

  Scenario: Abort active drag via Escape key (revert to original position)
    Given the list contains at least two tasks and the user has started dragging "Task E"
    When the user presses the Escape key while the drag is active
    Then the drag operation is aborted
    And "Task E" snaps back to its original position prior to the drag
    And no duplicates or unintended items are created

  Scenario: Drop outside valid bounds snaps task to nearest valid edge (no duplicates)
    Given the list contains at least three tasks and the user is dragging "Task F"
    When the user releases the dragged task outside the list bounds (e.g., outside the list container)
    Then the dragged task snaps to the nearest valid edge position in the list (top or bottom)
    And the list updates to reflect the snapped placement with animation
    And no duplicate tasks are created

  Scenario: Rapid consecutive drags are queued and do not overlap or corrupt order
    Given the list contains at least four tasks in order 1,2,3,4
    When the user quickly performs multiple drag actions in rapid succession (drag item 4 to 1, then immediately drag item 2 to 4)
    Then the application queues or serializes these drag operations so they complete sequentially
    And each completed operation results in a consistent, non-overlapping list order
    And after all operations complete, the final order matches the expected result
    And localStorage is updated to reflect the final order

  Scenario: Visual indicators appear on hover and allow drag initiation (accessibility/UX)
    Given the list contains at least two tasks
    When the user moves the pointer over a task row
    Then a visible drag affordance or handle becomes available (e.g., changes in cursor or handle icon)
    When the user uses the drag affordance (press/hold) to start dragging
    Then the task enters drag mode and the same drop-zone highlighting and behaviors apply

  Scenario: Invalid drag operations produce no side effects and provide clear feedback
    Given the list contains tasks and the environment is stable
    When the user attempts invalid drag actions (e.g., double-starting a drag while another drag is active)
    Then the second drag is ignored or queued per application behavior
    And the system does not create duplicates, lose tasks, or corrupt task states
    And a visual or ARIA-accessible indication is available to inform the user the drag could not be started (if applicable) (Header Search scenarios)
