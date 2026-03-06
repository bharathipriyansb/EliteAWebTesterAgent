using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;
using TechTalk.SpecFlow;
using System.Threading;

[Binding]
public class TodoDragAndDropSteps
{
    private readonly ScenarioContext _scenarioContext;
    private IWebDriver Driver => _scenarioContext.ContainsKey("Driver") ? _scenarioContext["Driver"] as IWebDriver : null;
    private IJavaScriptExecutor Js => (IJavaScriptExecutor)Driver;

    public TodoDragAndDropSteps(ScenarioContext scenarioContext)
    {
        _scenarioContext = scenarioContext;
    }

    // Background steps
    [Given(@"the todo application is loaded in the browser")]
    public void GivenTheTodoApplicationIsLoadedInTheBrowser()
    {
        // Adjust URL to your test instance
        Driver.Navigate().GoToUrl("http://localhost:3000");
        WaitForReady();
    }

    [Given(@"JavaScript and animations are enabled")]
    public void GivenJavaScriptAndAnimationsAreEnabled()
    {
        // Assume app requires JS; ensure no flags disable animations. Could set CSS/JS toggles if app exposes them.
        Js.ExecuteScript("window.__testAnimationsEnabled = true;");
    }

    [Given(@"the user is on the Active Todos view")]
    public void GivenTheUserIsOnTheActiveTodosView()
    {
        // Navigate to active view — adjust route if necessary
        Js.ExecuteScript("if(window.location.hash.indexOf('#/active') === -1) { window.location.hash = '#/active'; }");
        WaitForReady();
    }

    [Given(@"the browser console is cleared of errors")]
    public void GivenTheBrowserConsoleIsClearedOfErrors()
    {
        // Install console capture to check later
        var script = @"
            window.__consoleErrors = [];
            (function(){
                var origError = console.error;
                console.error = function(){
                    window.__consoleErrors.push(Array.prototype.slice.call(arguments).join(' '));
                    origError.apply(console, arguments);
                };
                var origException = window.exception;
            })();";
        Js.ExecuteScript(script);
    }

    [Given(@"localStorage is available for persistence")]
    public void GivenLocalStorageIsAvailableForPersistence()
    {
        // No-op in most browsers; optionally ensure localStorage exists
        Js.ExecuteScript("window.localStorage && (window.__localStorageAvailable = true);");
    }

    [Given(@"network layer is instrumented to observe backend sync behavior")]
    public void GivenNetworkLayerIsInstrumentedToObserveBackendSyncBehavior()
    {
        // Wrap fetch and XHR to record outgoing calls
        var script = @"
            window.__networkCalls = [];
            (function(){
                var origFetch = window.fetch;
                window.fetch = function(){
                    window.__networkCalls.push({type:'fetch', args: arguments, timestamp: Date.now()});
                    return origFetch.apply(this, arguments);
                };
                var OrigXhr = window.XMLHttpRequest;
                function XhrWrap(){
                    var x = new OrigXhr();
                    var origOpen = x.open;
                    x.open = function(method,url){
                        x.__url = url;
                        origOpen.apply(x, arguments);
                    };
                    var origSend = x.send;
                    x.send = function(){
                        window.__networkCalls.push({type:'xhr', url: x.__url, timestamp: Date.now()});
                        return origSend.apply(x, arguments);
                    };
                    return x;
                }
                window.XMLHttpRequest = XhrWrap;
            })();";
        Js.ExecuteScript(script);
    }

    // Scenario setup helpers
    [Given(@"the active todo list contains multiple items")]
    public void GivenTheActiveTodoListContainsMultipleItems()
    {
        // Create 4 sample items
        var script = @"
            window.__testTodos = [
                {id:'t1', text:'Task 1', completed:false},
                {id:'t2', text:'Task 2', completed:false},
                {id:'t3', text:'Task 3', completed:false},
                {id:'t4', text:'Task 4', completed:false}
            ];
            localStorage.setItem('todos', JSON.stringify(window.__testTodos));
            if(window.renderTodos) window.renderTodos();
        ";
        Js.ExecuteScript(script);
        WaitForTodoItems();
    }

    [Given(@"the active todo list contains zero items")]
    public void GivenTheActiveTodoListContainsZeroItems()
    {
        Js.ExecuteScript("localStorage.setItem('todos', JSON.stringify([])); if(window.renderTodos) window.renderTodos();");
        WaitForTodoItems(expectedCount: 0);
    }

    [Given(@"the active todo list contains exactly one item")]
    public void GivenTheActiveTodoListContainsExactlyOneItem()
    {
        Js.ExecuteScript(@"
            window.__testTodos = [{id:'only', text:'Only Task', completed:false}];
            localStorage.setItem('todos', JSON.stringify(window.__testTodos));
            if(window.renderTodos) window.renderTodos();
        ");
        WaitForTodoItems(expectedCount: 1);
    }

    [Given(@"an active todo list contains items and one item is in edit mode")]
    public void GivenAnActiveTodoListContainsItemsAndOneItemIsInEditMode()
    {
        GivenTheActiveTodoListContainsMultipleItems();
        // Put the first item into edit mode via JS
        Js.ExecuteScript(@"
            var el = document.querySelector('.todo-item');
            if(el){
                el.classList.add('editing');
                var input = el.querySelector('input.edit');
                if(input){ input.focus(); input.value = 'Editing Task'; }
            }
        ");
    }

    [Given(@"the active todo list contains items A \(completed=false\), B \(completed=true\), C \(completed=false\)")]
    public void GivenTheActiveTodoListContainsItemsA_B_C()
    {
        var script = @"
            window.__testTodos = [
                {id:'A', text:'A', completed:false},
                {id:'B', text:'B', completed:true},
                {id:'C', text:'C', completed:false}
            ];
            localStorage.setItem('todos', JSON.stringify(window.__testTod os || window.__testTodos));
            localStorage.setItem('todos', JSON.stringify(window.__testTodos));
            if(window.renderTodos) window.renderTodos();
        ";
        Js.ExecuteScript(script);
        WaitForTodoItems();
    }

    [Given(@"the user is authenticated \(backend sync enabled\)")]
    public void GivenTheUserIsAuthenticatedBackendSyncEnabled()
    {
        // Set a flag in the app to simulate auth and enable backend sync
        Js.ExecuteScript("window.__testAuth = true; window.__backendSyncEnabled = true;");
    }

    [Given(@"the user is not authenticated or network is offline")]
    public void GivenTheUserIsNotAuthenticatedOrNetworkIsOffline()
    {
        Js.ExecuteScript("window.__testAuth = false; window.__backendSyncEnabled = false; window.__simulateOffline = true;");
    }

    [Given(@"the user has started dragging item X from position 1")]
    public void GivenTheUserHasStartedDraggingItemXFromPosition1()
    {
        GivenTheActiveTodoListContainsMultipleItems();
        // Start dragging the first item
        StartDragOnSelector(".todo-item:nth-child(1)");
        _scenarioContext["DraggedItemSelector"] = ".todo-item:nth-child(1)";
    }

    [Given(@"the user starts dragging item Y")]
    public void GivenTheUserStartsDraggingItemY()
    {
        GivenTheActiveTodoListContainsMultipleItems();
        StartDragOnSelector(".todo-item:nth-child(2)");
        _scenarioContext["DraggedItemSelector"] = ".todo-item:nth-child(2)";
    }

    [Given(@"the active todo list contains multiple items and item Z is at position 3")]
    public void GivenTheActiveTodoListContainsMultipleItemsAndItemZIsAtPosition3()
    {
        GivenTheActiveTodoListContainsMultipleItems();
        // ensure an item with id Z is at position 3
        Js.ExecuteScript(@"
            window.__testTodos = [
                {id:'t1', text:'1', completed:false},
                {id:'t2', text:'2', completed:false},
                {id:'Z', text:'Z', completed:false},
                {id:'t4', text:'4', completed:false}
            ];
            localStorage.setItem('todos', JSON.stringify(window.__testTodos));
            if(window.renderTodos) window.renderTodos();
        ");
        WaitForTodoItems();
    }

    [Given(@"the active todo list contains item M")]
    public void GivenTheActiveTodoListContainsItemM()
    {
        Js.ExecuteScript(@"
            window.__testTodos = [{id:'M', text:'M', completed:false}, {id:'t2', text:'Other', completed:false}];
            localStorage.setItem('todos', JSON.stringify(window.__testTodos));
            if(window.renderTodos) window.renderTodos();
        ");
        WaitForTodoItems();
    }

    // Action steps
    [When(@"the user presses mousedown on an item and moves the pointer")]
    public void WhenTheUserPressesMousedownOnAnItemAndMovesThePointer()
    {
        StartDragOnSelector(".todo-item:nth-child(1)");
        // move a little to initiate drag
        MoveDragByOffset(0, 50);
    }

    [When(@"the user drags item A and drops it between items B and C")]
    public void WhenTheUserDragsItemAAndDropsItBetweenItemsBAndC()
    {
        // Find A and target (between B and C)
        // We'll use JS to locate B and C and insert A between
        var script = @"
            (function(){
                var items = document.querySelectorAll('.todo-item');
                var elA = Array.from(items).find(i => i.textContent.trim().startsWith('A'));
                var elB = Array.from(items).find(i => i.textContent.trim().startsWith('B'));
                var elC = Array.from(items).find(i => i.textContent.trim().startsWith('C'));
                if(!elA || !elB) return false;
                elB.parentNode.insertBefore(elA, elC);
                // trigger app reorder handler if it exists
                if(window.onReorder) window.onReorder();
                return true;
            })();";
        var result = Js.ExecuteScript(script);
        Thread.Sleep(250); // wait for animation-ish; real test should measure
    }

    [When(@"the user drags an item and drops it into a new position")]
    public void WhenTheUserDragsAnItemAndDropsItIntoANewPosition()
    {
        // Generic: drag first item to after third item
        var script = @"
            (function(){
                var items = document.querySelectorAll('.todo-item');
                if(items.length < 2) return false;
                var src = items[0];
                var dest = items[Math.min(2, items.length-1)];
                dest.parentNode.insertBefore(src, dest.nextSibling);
                if(window.onReorder) window.onReorder();
                return true;
            })();";
        Js.ExecuteScript(script);
        Thread.Sleep(200);
    }

    [When(@"the user attempts to start a drag on the list area")]
    public void WhenTheUserAttemptsToStartADragOnTheListArea()
    {
        // Try mousedown on the list container
        var list = Driver.FindElement(By.CssSelector(".todo-list"));
        var actions = new Actions(Driver);
        actions.MoveToElement(list).ClickAndHold().MoveByOffset(0, 10).Perform();
        actions.Release().Perform();
    }

    [When(@"the user attempts to start a drag on any item while edit mode is active")]
    public void WhenTheUserAttemptsToStartADragOnAnyItemWhileEditModeIsActive()
    {
        StartDragOnSelector(".todo-item:first-child");
    }

    [When(@"the user presses Escape before releasing the mouse")]
    public void WhenTheUserPressesEscapeBeforeReleasingTheMouse()
    {
        // Use keyboard event to send Escape while drag in progress
        Js.ExecuteScript("document.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape'}));");
        Thread.Sleep(100);
    }

    [When(@"the user releases the mouse with the cursor outside the list bounds \(above the first item or below the last item\)")]
    public void WhenTheUserReleasesTheMouseWithCursorOutsideListBounds()
    {
        // Release the drag with mouse outside: simulate drop handler with out-of-bounds flag
        Js.ExecuteScript(@"
            (function(){
                var event = new Event('mouseup');
                // set a custom property to indicate out-of-bounds
                event.__outOfBounds = true;
                document.dispatchEvent(event);
                if(window.onDropOutside) window.onDropOutside();
            })();");
        Thread.Sleep(200);
    }

    [When(@"the user performs multiple rapid drags in succession \(drag, drop, immediately drag another item\)")]
    public void WhenTheUserPerformsMultipleRapidDragsInSuccession()
    {
        // Simulate three quick reorders
        for(int i=0;i<3;i++){
            Js.ExecuteScript(@"
                (function(){
                    var items = document.querySelectorAll('.todo-item');
                    if(items.length > 1){
                        var src = items[0];
                        var dest = items[items.length-1];
                        dest.parentNode.insertBefore(src, dest.nextSibling);
                        if(window.onReorder) window.onReorder();
                    }
                })();");
            Thread.Sleep(120); // shorter than animation to test queueing
        }
        Thread.Sleep(300);
    }

    [When(@"the user attempts to drag that single item and drops it elsewhere \(including same place\)")]
    public void WhenTheUserAttemptsToDragSingleItemAndDropElsewhere()
    {
        // Attempt to reorder single item; no-op expected
        WhenTheUserDragsAnItemAndDropsItIntoANewPosition();
    }

    [When(@"the user drags item Z and drops it back into position 3")]
    public void WhenTheUserDragsItemZAndDropsItBackIntoPosition3()
    {
        // No-op reorder back to position 3
        Js.ExecuteScript(@"
            (function(){
                // find Z and ensure it's at position 3
                var items = document.querySelectorAll('.todo-item');
                var z = Array.from(items).find(i => i.textContent && i.textContent.trim().startsWith('Z'));
                if(!z) return;
                var parent = z.parentNode;
                var third = parent.children[2];
                if(third) parent.insertBefore(z, third);
                if(window.onReorder) window.onReorder();
            })();");
        Thread.Sleep(150);
    }

    [When(@"the user moves item M to a new position via drag-and-drop")]
    public void WhenTheUserMovesItemMToANewPositionViaDragAndDrop()
    {
        Js.ExecuteScript(@"
            (function(){
                var items = document.querySelectorAll('.todo-item');
                var m = Array.from(items).find(i => i.textContent && i.textContent.trim().startsWith('M'));
                if(!m) return;
                var parent = m.parentNode;
                parent.insertBefore(m, parent.firstChild); // move to top
                if(window.onReorder) window.onReorder();
            })();");
        Thread.Sleep(200);
    }

    // Assertions
    [Then(@"the drag should initiate and the cursor should show a grab/drag indicator")]
    public void ThenTheDragShouldInitiateAndTheCursorShouldShowGrabIndicator()
    {
        var cursor = Js.ExecuteScript("return window.getComputedStyle(document.body).cursor || '';");
        Assert.IsTrue(cursor != null, "Cursor style is not accessible.");
        // Accept common drag cursor values
        var cursorStr = cursor.ToString().ToLower();
        Assert.IsTrue(cursorStr.Contains("grabbing") || cursorStr.Contains("