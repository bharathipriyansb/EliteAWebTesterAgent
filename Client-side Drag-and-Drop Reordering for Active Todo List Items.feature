Feature: Client-side Drag-and-Drop Reordering for Active Todo List Items

  Background:
    Given the todo application is loaded in the browser
    And JavaScript and animations are enabled
    And the user is on the Active Todos view
    And the browser console is cleared of errors
    And localStorage is available for persistence
    And network layer is instrumented to observe backend sync behavior

  Scenario: Initiate drag — visual cues appear (grab cursor, drag ghost, drop zone highlight)
    Given the active todo list contains multiple items
    When the user presses mousedown on an item and moves the pointer
    Then the drag should initiate and the cursor should show a grab/drag indicator
    And a drag ghost element follows the cursor
    And potential drop zones highlight dynamically under the cursor
    And no console errors occur during initiation

  Scenario: Successful drop reorders item, preserves attributes, animates (~200ms), updates persistence and queues backend sync (logged-in)
    Given the active todo list contains items A (completed=false), B (completed=true), C (completed=false)
    And the user is authenticated (backend sync enabled)
    When the user drags item A and drops it between items B and C
    Then item A is inserted at the new position in the DOM (B, A, C)
    And item A's text and completion status remain unchanged (completed=false)
    And the list animates smoothly to the new order with an animation duration approximately 200ms (ease-in-out)
    And local persistence (localStorage) is updated silently to reflect the new order
    And a backend sync request is queued and sent non-blockingly (UI remains responsive while sync is queued)
    And focus is preserved on the moved item after drop
    And an accessible announcement is made for screen readers indicating "Task 'A' moved to position 2"
    And no console errors occur during or after drop

  Scenario: Successful drop reorders item and updates only local persistence (guest/offline)
    Given the user is not authenticated or network is offline
    And the active todo list contains multiple items
    When the user drags an item and drops it into a new position
    Then the item is repositioned in the DOM
    And the item's text and completion status remain unchanged
    And localStorage is updated silently with the new order
    And no blocking backend sync attempt prevents the UI from updating
    And no console errors occur

  Scenario: Drag is disabled when the list is empty (no-op)
    Given the active todo list contains zero items
    When the user attempts to start a drag on the list area
    Then no drag operation starts
    And no drag ghost or drop-zone highlights appear
    And no console errors occur

  Scenario: Drag is disabled during edit mode (no-op)
    Given an active todo list contains items and one item is in edit mode
    When the user attempts to start a drag on any item while edit mode is active
    Then dragging is disabled and no drag starts
    And the edit input retains focus and content
    And no console errors occur

  Scenario: Pressing Escape during drag aborts and reverts to original position with no state change
    Given the user has started dragging item X from position 1
    When the user presses Escape before releasing the mouse
    Then the drag operation aborts
    And item X returns to its original position in the DOM
    And local persistence is unchanged
    And no backend sync is queued for this aborted action
    And no console errors occur

  Scenario: Dropping outside list bounds snaps item to nearest valid edge
    Given the user starts dragging item Y
    When the user releases the mouse with the cursor outside the list bounds (above the first item or below the last item)
    Then the UI snaps the item to the nearest valid list edge (top or bottom)
    And the item is inserted at the snapped edge position
    And the list animates the insertion
    And no console errors occur

  Scenario: Rapid successive drags are queued without overlap and do not cause UI corruption
    Given the active todo list contains multiple items
    When the user performs multiple rapid drags in succession (drag, drop, immediately drag another item)
    Then drag operations are queued or throttled so animations and reorder operations do not overlap
    And each drag completes and results in a consistent DOM order
    And no console errors or race-condition artifacts appear

  Scenario: Single-item list dragging is a no-op (edge case)
    Given the active todo list contains exactly one item
    When the user attempts to drag that single item and drops it elsewhere (including same place)
    Then the operation is a no-op and the list order remains unchanged
    And no console errors occur

  Scenario: Dragging an item to the same position causes no change
    Given the active todo list contains multiple items and item Z is at position 3
    When the user drags item Z and drops it back into position 3
    Then the list order remains unchanged
    And no persistence update is performed (or an idempotent no-op update occurs)
    And no console errors occur

  Scenario: Accessibility — focus preserved and screen-reader announcement after a move
    Given the active todo list contains item M
    When the user moves item M to a new position via drag-and-drop
    Then keyboard focus remains on item M after the move
    And an ARIA live region announces "Moved 'M' to position N" (or equivalent accessible text)
    And screen reader users receive the updated position information

  Scenario: Animation timing is approximately 200ms and transitions are smooth
    Given an item is moved to a new position via drag-and-drop
    When the list animates to the new order
    Then the animation duration measures approximately 200ms (±50ms tolerance)
    And the transition easing is smooth (ease-in-out)
    And no janky layout shifts or abrupt jumps are observed

  Scenario: No console errors after a sequence of mixed interactions
    Given the active todo list contains multiple items
    When the user performs a sequence: start drag → drop → start another drag → press Escape → attempt drag in edit mode → drop outside bounds
    Then no console errors are logged during or after the sequence
    And all UI states remain consistent (no broken DOM, no lingering drag ghosts, correct focus)
