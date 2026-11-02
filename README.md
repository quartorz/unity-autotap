# unity-autotap

unity-autotap taps, drags and types for you in a Unity project that uses uGUI: a robot player for smoke tests, soak
tests and demos.

It does not call the handlers of your UI itself. It adds virtual devices to the Input System (a touchscreen, a mouse, a
pen, a keyboard, a gamepad, or any other layout), queues input events to them, and lets the real
`InputSystemUIInputModule` of your scene deliver them. What your UI sees is what a finger, a mouse or a key would give
it: pointer ids, enter and exit, long presses, drags, scrolling, navigation with a gamepad, the back key.

## Requirements

- Unity 6000.4.
- Input System 1.19.0 (`com.unity.inputsystem`) and uGUI 2.0.0 (`com.unity.ugui`). Both are dependencies of the
  package.
- Active Input Handling that includes the Input System.
- The event system uses `InputSystemUIInputModule`, not `StandaloneInputModule`.
- The update mode of the Input System is "Process Events In Dynamic Update" (the default).
  `VirtualInput.Acquire` throws `InvalidOperationException` for any other mode, and so does `AutoTapBase.Start`, which
  calls it.

## Installation

1. Open the Unity Package Manager.
2. Select "Add package from git URL".
3. Enter `https://github.com/quartorz/unity-autotap.git?path=Assets/AutoTap#v2.0.0`.
4. Add `ENABLE_AUTOTAP` to the Scripting Define Symbols. The whole package is compiled only with this symbol.

To stay on v1, which needs neither the Input System nor virtual devices, end the URL with `#v1.5.2`:
`https://github.com/quartorz/unity-autotap.git?path=Assets/AutoTap#v1.5.2`.

## A first auto tap

An auto tap has slots. A slot holds one finger at a time, and `Update(int index)` is called for each slot that is empty.

```csharp
using UnityAutoTap;
using UnityEngine;

public class RandomTap : AutoTapBase
{
	public override void Setup(Canvas canvasForMarker, Sprite markerSprite)
	{
		SetupInternal(canvasForMarker, markerSprite, 5);   // 5 slots, at most 10
	}

	protected override void Update(int index)
	{
		var position = new Vector2(Random.Range(0f, Screen.width), Random.Range(0f, Screen.height));
		Fire(index, position, 0.1f);   // press, hold 0.1 s, release
	}
}
```

Create it, set it up, start it, and call `Update(float)` once per frame:

```csharp
var autoTap = new RandomTap();
autoTap.Setup(markerCanvas, markerSprite);   // the canvas and the sprite show where the fingers and pointers are
autoTap.Start();
// every frame:
autoTap.Update(Time.deltaTime);
// when done:
autoTap.Stop();
autoTap.Dispose();   // calls Stop if it is still running
```

Only one auto tap can be started at a time (`AutoTapBase.Current`); `Start` on another one throws
`InvalidOperationException`.

`Fire` only queues input. The input module delivers it in the next frame, because the Input System processes events at
the start of a frame (see [When input arrives](#when-input-arrives)).

## Three ways to put input in

There are three kinds of target. The shortcuts for mice, pens, keyboards and gamepads are these three with a layout
filled in.

| Way | Target | What it can do |
|---|---|---|
| Finger: `Fire(int slot, Gesture)` | the touchscreen | press, move, release, cancel, pressure and radius, several fingers at once |
| Pointer: `FirePointer(layout, Gesture, number)` | any pointer that is not a touchscreen: a mouse, a pen, a custom pointer layout | moving without pressing (hover), left, right, middle, forward and back buttons, a pen's tip, eraser, barrel buttons, pressure and tilt, scroll |
| Device: `FireDevice(layout, ControlSequence, number)` | any device that is not a pointer: a keyboard, a gamepad, a joystick, a sensor, any custom layout | write values to controls by path, change them over time, text and IME composition for a keyboard |

Shortcuts: `FireMouse(gesture)` and `FirePen(gesture)` are `FirePointer` with the layout `"Mouse"` or `"Pen"`.
`FireScroll(position, delta)` hovers the virtual mouse at `position` and scrolls. `FireKey(key, duration, modifiers)`
and `FireGamepad(sequence)` use the layouts `"Keyboard"` and `"Gamepad"`. `number` tells apart several devices of the
same layout.

A virtual device is added the first time it is used, not when the auto tap starts, even if that first call returns
`null` for another reason. Code that only presses keys never adds a touchscreen or a mouse.

The `Fire` methods are `protected internal` members of `AutoTapBase`, and `Scenario` has the same methods that forward
to its owner. `LogItem`, `LogEvent`, `LogEventKind`, `LogItemKind` and `EndReason` are types nested in `AutoTapBase`.

### Fingers and pointers: `Gesture`

A `Gesture` is a list of steps that run one after another. It is used for fingers, mice and pens because the input
module treats the three in the same way. Time is counted in the seconds that you pass to `AutoTapBase.Update`.

```csharp
// Tap, drag, long press followed by a drag.
Fire(0, position, 0.1f);
Fire(0, from, to, 0.5f);
Fire(0, Gesture.PressAt(position).Hold(0.8f).MoveTo(to, 0.5f));

// Two fingers moving apart: fire two slots in the same frame.
Fire(0, Gesture.PressAt(centerLeft).MoveTo(farLeft, 0.4f));
Fire(1, Gesture.PressAt(centerRight).MoveTo(farRight, 0.4f));

// Hold until something happens, at most 5 seconds. If it does not happen, the operation releases what it holds
// and ends with EndReason.TimedOut.
Fire(0, Gesture.PressAt(position).HoldUntil(() => progressBar.value >= 1f, 5f));

// A finger with pressure.
Fire(0, Gesture.PressAt(position).Set("pressure", 0.3f).Hold(0.2f).Set("pressure", 1f, 0.3f).Hold(0.2f));

// Mouse: hover, right click, drag with the middle button, scroll.
FireMouse(Gesture.HoverAt(position).Hold(0.5f));
FireMouse(Gesture.PressAt(position, "rightButton").Hold(0.1f));
FireMouse(Gesture.PressAt(from, "middleButton").MoveTo(to, 0.5f));
FireScroll(position, new Vector2(0f, -3f));

// Pen: hover with pressure, then press the tip. A pen is in range from the first step until the end.
FirePen(Gesture.HoverAt(position).Set("pressure", 0.5f).Press().Hold(0.2f));
```

| Step | Meaning |
|---|---|
| `Gesture.PressAt(position, button = null)` | start with a press. `null` is the primary action: the touch of a finger, the left button of a mouse, the tip of a pen. Other buttons are control paths such as `"rightButton"`. |
| `Gesture.HoverAt(position)` | start by moving without pressing. Not for fingers. |
| `MoveTo(position, seconds)`, `MoveTo(Func<Vector2>, seconds)` | move in a straight line, keeping what is pressed. The `Func` is evaluated when the move starts. |
| `Press(button)`, `Release(button)` | press or release one more button. |
| `Scroll(delta)` | scroll once at the current position. Not for fingers. |
| `Set(controlPath, value, seconds = 0)` | change a control (`pressure`, `radius`, `tilt`, ...) at once or gradually. For a finger the path is relative to the touch, and only `pressure` and `radius` exist. |
| `Hold(seconds)`, `HoldUntil(condition, timeout)` | wait. The condition is evaluated once per `Update(float)`. |

A gesture that does not fit its target (a `HoverAt` for a finger, a control that the device does not have) makes `Fire`
throw `ArgumentException`. So does `FirePointer` with a layout that is a touchscreen or is not derived from `Pointer`.

The UI handles the primary button of any pointer and the right and middle buttons of a mouse. Presses of other buttons
(forward, back, a pen's eraser and barrel buttons) are written to the device, for code that reads them, but they never
reach the UI, and a `LogItem` that follows such a button has no `Target`, `ClickTarget` or other prediction.

`FireScroll` takes two frames: the mouse hovers at `position` first, and the scroll is queued in the next frame, once
the hover was processed, so that the pointer is already there when the scroll arrives.

### Other devices: `ControlSequence`

A `ControlSequence` has no position. It writes values to controls by their path.

```csharp
// The back key.
FireKey(Key.Escape);

// A shortcut: Ctrl+R.
FireKey(Key.R, 0.1f, Key.LeftCtrl);

// A gamepad: tilt the left stick down for half a second, then press South (submit).
FireGamepad(new ControlSequence().Set("leftStick", Vector2.down).Hold(0.5f));
FireGamepad(new ControlSequence().Press(GamepadButton.South));

// Text and IME composition, for a keyboard.
FireDevice("Keyboard", new ControlSequence().Composition("かん").Composition("").Text("漢"));

// Anything with a layout: a joystick, a sensor.
FireDevice("Joystick", new ControlSequence().Set("stick", new Vector2(0.5f, -0.5f)).Press("trigger").Hold(0.2f));
FireDevice("Accelerometer", new ControlSequence().Set("acceleration", new Vector3(0f, -9.8f, 0f)).Hold(0.2f));
```

| Step | Meaning |
|---|---|
| `Press(path)`, `Release(path)`, `Press(Key)`, `Release(Key)`, `Press(GamepadButton)`, `Release(GamepadButton)` | press or release a button-like control: writes 1 or 0. |
| `Set(controlPath, value, seconds = 0)` | change a control (a stick, a trigger, an axis) at once or gradually. |
| `Text(text)` | enter text, one text event per character (Unicode code point). Keyboards only. |
| `Composition(text)` | set the text that an input method editor is composing. An empty string ends the composition. Keyboards only. A composition has at most 63 characters, which is what an event holds. |
| `Hold(seconds)`, `HoldUntil(condition, timeout)` | wait. |

Pressing a key does not enter a character, because a character depends on the keyboard layout. Use `Text` for that.
Text and composition go to the keyboard (`Keyboard.onTextInput`, `Keyboard.onIMECompositionChange`) and are not read by
`TMP_InputField` (see [What is not covered](#what-is-not-covered)).

`FireDevice` throws `ArgumentException` for a usage error:

- a layout that does not exist, or that is derived from `Pointer`,
- a control path that the device does not have, or that is not a button where a press or a release needs one (a stick),
- a control that computes its value from other controls and so cannot be written,
- a value that does not fit the control,
- a `Key` on a device that is not a keyboard, or a `GamepadButton` on a device that is not a gamepad,
- a `Text` or `Composition` step for a device that does not receive text, or a composition of more than 63 characters,
- a sequence with no step that writes, such as one that only waits.

Calling `VirtualDevice.Text` or `VirtualDevice.Composition` directly on a device that does not receive text throws
`InvalidOperationException` instead; a composition of more than 63 characters throws `ArgumentException` there too.

### Pressing the back key again and again

The back key is the `escape` of a keyboard. An `InputAction` bound to `*/{Cancel}`, and the cancel that the
`InputSystemUIInputModule` delivers to the selected object, react to it. They react to the `buttonEast` of a gamepad in
the same way, so `FireGamepad(new ControlSequence().Press(GamepadButton.East))` is also a back key.

```csharp
// Once every second frame at best.
FireKey(Key.Escape);

// Once in every frame: one operation that holds all the presses and releases.
var sequence = new ControlSequence().Press(Key.Escape);
for (var i = 0; i < 9; ++i)
{
	sequence.Release(Key.Escape).Press(Key.Escape);
}

sequence.Release(Key.Escape);
FireDevice("Keyboard", sequence);   // ten presses, one in each of ten consecutive frames
```

- `FireKey` starts an operation on the keyboard, and the keyboard takes the next one only after the end of the previous
  one was processed. Fired in frame N, the press is processed in frame N + 1 and the release, which is never in the same
  input update as its press, in frame N + 2. The next `FireKey` is accepted in frame N + 2: every second frame.
- A sequence is one operation, so the keyboard is never busy between its presses. Each release goes into the input
  update after its press, together with the next press, in that order, so the key goes down once per frame.
- The input module delivers at most one cancel to the UI per frame, however many presses the frame processed. An
  `InputAction` is performed for every press, also when several are in one input update.
- To press and release in one input update, use the virtual device itself (see
  [Using the devices directly](#using-the-devices-directly)).

### What a `Fire` method does

- It returns a `LogItem` that records the operation, and queues the first step. It returns `null` and queues nothing if
  - a position is outside the screen,
  - the slot, or the virtual device, is still busy with the previous operation, until the end of it was processed,
  - the first step is a press and `ShouldNotPressOn` returns `true` for what is under it,
  - all 10 fingers of the touchscreen are in use.
- The first step of a `FireDevice` sequence is queued by `FireDevice` if it takes no time: a press, a release, text, a
  composition, or a value that is set at once. A sequence that begins with a wait or a gradual change queues nothing yet and is processed
  once its first step is over.
- A point that hits nothing is pressed all the same.
- Steps that follow each other in one frame are queued as separate events, in the order of the plan: a `Ctrl` that
  comes before an `R` is processed before it.
- An operation never presses and releases in one input update, even with `Hold(0)`: the release goes into the next
  frame. The same holds for the release at the end of an operation.
- When the plan is over, what is pressed is released, in the opposite order, and every control that is not `dontReset`
  goes back to its default, the range of the Input System's soft reset. The position of a mouse or a pen is `dontReset`
  and stays, so the hover goes on. The controls of sensors are noisy, which makes them `dontReset` too, so a sensor keeps
  the value it was given.
- A slot or device runs one operation at a time. To press the same key again, wait for the end of the previous press, or
  put the presses into one sequence (see above).

### `ShouldNotPressOn`

Override it to leave parts of the UI alone. It is asked for every press of a finger or a pointer, with the topmost result
of the event system's raycast at that point, the object the input module would use. It is not asked when nothing is
hit, for hovering, or for `FireDevice`.

```csharp
protected override bool ShouldNotPressOn(RaycastResult topmost)
{
	return !topmost.gameObject.transform.IsChildOf(mainCanvas.transform);   // leave everything outside the main canvas alone
}
```

If it returns `true` the first press of an operation is not made and `Fire` returns `null`. For a later press the
operation releases what it holds and ends with `EndReason.Refused`; only pointers have later presses, because a finger
has the touch only. The objects behind the topmost one are not tried. `RaycastResult.module` tells which raycaster
produced the hit.

### Scenarios

`Scenario`, `ScenarioConfig`, `Group`, `Condition` and `ScenarioBasedAutoTap` work as before. A `Scenario` has the same
`Fire`, `FirePointer`, `FireDevice`, `FireMouse`, `FirePen`, `FireScroll`, `FireGamepad` and `FireKey` as `AutoTapBase`
and forwards `ShouldNotPressOn`.

`TryTap(index, canvas, target, duration)` taps the center of a `RectTransform` and returns `true` if the tap was started
**and** the press is predicted to be delivered to `target`: the `Target` of the returned item, worked out when the tap is
fired, is the game object of `target`. `Target` is the nearest handler of what was hit, so `target` has to be the object
that handles the press (a `Button`, for example, and not a decoration inside it that is covered by something else). The
prediction is refreshed when the press is processed.

## Samples

`Assets/Sample` of this repository is a small app (a title screen, a home screen with a list, a menu in the header) and
the auto taps that run it: `RandomTap`, and scenarios on `ScenarioBasedAutoTap`. Three scenarios put in input that is not a
finger.

| Example | Device | What it does |
|---|---|---|
| `CloseMenuWithEscape` | keyboard | Taps the menu button, waits until the menu is open, and presses Escape. The cancel action of the UI is delivered to the selected object, the menu button, whose `MenuCancelHandler` closes the menu. |
| `HoverAndLeave` | mouse | Moves the virtual mouse onto a `HoverHighlight`, waits until it shows that it is hovered, moves the mouse away and waits until it does not. |
| `MoveAndSubmit` | gamepad | Moves the selection along a `PadPanel` with one flick of the left stick for each step, then presses South and waits until a button was submitted. The first button is the `firstSelectedGameObject` of the event system. |

```csharp
// CloseMenuWithEscape: a key does not use a slot, so the scenario keeps the LogItem and waits for Completed.
_escape = FireKey(Key.Escape, 0.1f);

// HoverAndLeave: hover without pressing, wait for the UI to react, move away.
_item = FireMouse(Gesture
	.HoverAt(on)
	.HoldUntil(() => _target.Hovered, 1f)
	.Hold(0.2f)
	.MoveTo(away, 0.2f)
	.HoldUntil(() => !_target.Hovered, 1f));

// MoveAndSubmit: a flick for each step, then submit once the button reacted.
var sequence = new ControlSequence();
sequence.Set("leftStick", Vector2.right).Hold(0.1f).Set("leftStick", Vector2.zero).Hold(0.1f);
sequence.Press(GamepadButton.South).HoldUntil(() => pad.SubmitCount > submitted, 1f).Release(GamepadButton.South);
_item = FireGamepad(sequence);
```

The examples are nested classes of the sample's `ScenarioBasedAutoTap`, so `SetJson` finds them by their names, and
`InputExamples.Json` runs the three. Each one waits for the effect on the UI by itself: it keeps the `LogItem` and
looks at `Completed` and `EndReason` in the following frames. Their scripts need objects in `SampleScene` that the other
scenarios do not (the comment of each scenario lists them) and an `InputSystemUIInputModule` on the event system.

## When input arrives

```
frame N      Update(float): Fire queues the events
frame N + 1  input update (start of the frame): the Input System processes them, in the order they were queued
             event system: the input module delivers pointer, navigation and submit/cancel events
             Update(float): the next steps are queued
```

- Nothing is delivered inside `Fire` or `Update(float)`.
- The order in which events are *processed* is the order in which they were queued, across all devices: a key press, a
  finger and a scroll queued in this order are processed in this order.
- The order in which the input module *delivers* to the UI is not promised. It handles each pointer once per frame.
- `Stop` queues the releases of what is pressed. They reach the UI in the next input update, not inside `Stop`.

`Update(float)` does nothing while the auto tap is not enabled (before `Start`, after `Stop`). Otherwise it works in this
order:

1. `OnPreUpdate`.
2. `OnTapCompleted` for the slot operations that ended, in the order of `LogItem.CompletedOrder`.
3. Each slot in turn: a running operation is advanced and its next step queued; an empty slot gets `Update(int index)`.
4. The running operations outside the slots, in the order they were started, are advanced.
5. `OnPostUpdate`.

`Stop` ends the running operations in the order they were started: what they hold is released, and their `LogItem`s end
with `EndReason.Interrupted`. `OnTapCompleted` is called inside `Stop` for those that were in a slot.

## Reading what happened

`Fire` returns a `LogItem`. It is filled in while the operation runs. A new item is created for every operation that is
accepted; override `NewLogItem(int index)` to return an instance of a class derived from `LogItem`. Every field is filled
in afterwards.

| Field | Meaning |
|---|---|
| `Index`, `Kind`, `Device` | the slot (`-1` outside the slots), the kind of operation (`Tap`, `Drag`, `Gesture`, `Pointer`, `Device`) and the virtual device that was operated |
| `FrameCount`, `DateTime`, `ScreenPoint`, `DragTo` | what was asked for, when `Fire` was called. `ScreenPoint` is zero for a device without a position |
| `Duration` | the time given to `Fire(index, position, duration)`, `Fire(index, from, to, duration)` or `FireKey(key, duration)`. `NaN` when a `Gesture` or a `ControlSequence` was given, and for the other `FireDevice` calls |
| `Order`, `StartedFrameCount` | the number of the change that started the operation and the frame it was processed in. `null` and `0` until then |
| `DragStartedOrder`, `DragStartedFrameCount`, `Dragged` | the change that moved the pointer beyond the drag threshold, which is when begin-drag is delivered |
| `CompletedOrder`, `CompletedFrameCount`, `Completed`, `EndReason` | the change that ended the operation, and why: `Finished`, `Interrupted` (`Stop`, or the Input System reset the device), `TimedOut`, `Dropped` (the Input System discarded the event that would have started it), `Refused`. `CompletedOrder` is `null` when no processed change ended the operation: `Dropped`, ended by `Stop`, or an operation whose end changes no control, such as a hover that presses nothing. |
| `Target`, `ClickExecuted`, `ClickTarget`, `DragTarget`, `DropTarget` | what the event system is going to do, worked out from the rules of the input module (below) |
| `Gesture`, `Sequence` | the plan |

An item that `Fire` returned ends exactly once.

`Target`, `ClickTarget`, `DragTarget` and `DropTarget` are predictions for fingers and pointers; they stay empty for devices.
They follow the rules of the input module (the nearest handler of the object that was hit, a click only when the release
is on the object that got the press, the drag threshold), and are worked out right after the input update that processed
the change, before the event system delivers. A handler that changes the UI while it runs can make them differ from
what was delivered. A pointer operation follows the first button that its gesture presses, if the UI handles that button.

### `GetLatestLogs()`

`GetLatestLogs()` is the one place to read what happened. It returns the sequence of what happened in the latest Dynamic
input update, in the order the Input System processed it.

```csharp
foreach (var e in GetLatestLogs())
{
	// e.Item  the LogItem
	// e.Kind  Started, DragStarted or Completed
	// e.Order the number of the change that caused it (null for a Completed item that has none)
}
```

- The sequence is renewed at the start of every Dynamic input update, not at the start of `Update(float)`. An operation
  fired in frame N is in the sequence of frame N + 1, and can be read in `OnPreUpdate`, `Update(int)`, `OnPostUpdate` and
  after `Update(float)` of that frame.
- A frame in which no Dynamic input update ran has an empty sequence. A sequence of an earlier frame is never returned, for
  example after the virtual input was removed.
- When several things happened to one item in one input update, they are several elements.
- Elements are ordered by `Order`. The elements without a number come after the others, in the order the items were fired.
  They are the `Completed` of
  - an item that was `Dropped`, in the sequence of the input update that discarded its event,
  - an item that `Stop` ended, in the sequence of the next input update, because that is the one that processes its
    release, and
  - an item whose end changes no control, such as a hover that presses nothing or a `Set` of the value that a control
    already has. Such an item has nothing to release, so it ends in the first input update after its plan is over.

  Their `Order` is `null`, and so is the `CompletedOrder` of the item. Every item's end is listed once.
- An item that `Stop` ended before the update that processes its press keeps `Order` `null`: the operation was cut off, so
  the press that is processed afterwards is not attributed to it.
- `OnTapCompleted(index, logItem)` is for the slots. It is called from `Update(float)` before the slots are visited, in the
  order the ends were processed (`CompletedOrder`). For operations that `Stop` ended it is called inside `Stop`. Operations
  outside the slots are read with `GetLatestLogs()`.
- The numbers (`Order`, `DragStartedOrder`, `CompletedOrder`) are numbers of the `InputChangeReader` of the virtual input.
  They can be compared with each other. The end of a drag (`endDrag`, `drop`) is the `CompletedOrder` of the item whose
  `Dragged` is `true`.

### `InputChangeReader`

`InputChangeReader` is what the numbers come from, and it is public. It reports the changes of the state of input devices,
one event at a time, in the order the Input System processed them, for every enabled device, real and virtual, unless you
pass a filter. Narrow it down with a predicate.

```csharp
using var reader = new InputChangeReader(device => device is Keyboard);
reader.Changed += change =>
{
	switch (change)
	{
		case ButtonDown down: Debug.Log($"{down.ControlPath} down at #{down.Order}, virtual: {down.IsVirtual}"); break;
		case TextEntered text: Debug.Log($"text {text.Text}"); break;
	}
};
reader.UpdateEnded += () => { /* the end of a Dynamic input update */ };
```

It reports `TouchBegan`, `TouchMoved`, `TouchEnded`, `TouchCanceled`, `ButtonDown`, `ButtonUp`, `ValueChanged` (with the
previous and the new value), `Scrolled` (the amount of the event), `TextEntered`, `CompositionChanged` and `DeviceReset`.
Each has `Order`, `Frame`, `UpdateCount`, `EventId`, `Time`, `Device` and `IsVirtual`.

- A touchscreen is read through its touches, never through its primary touch.
- A control made of axes is one change with its vector value: a stick is one `ValueChanged` of the `stick` (or
  `leftStick`, ...), not one for `stick/x` and one for `stick/y`. A mouse's `scroll` is a `Scrolled`; the other delta
  controls are not reported.
- Synthetic controls are not reported. The `inRange` of a pen is synthetic, so a pen going in and out of range is not a
  change: the position, the tip and the other buttons are.
- A change that does not come from an event in the queue, such as the reset that comes with disabling a device, is one
  `DeviceReset` per input update, instead of the changes of the controls.
- Events the Input System dropped (a disabled device, a stale time stamp, ...) and the zeroing of deltas at the start of an
  update are not reported. Events that the Input System merged into one are reported once.
- It does not record raw events; use `InputEventTrace` for that.
- A reader numbers the changes on its own, starting at 0. The numbers of two readers cannot be compared; those of a
  `LogItem` come from the reader of the virtual input, `IVirtualInput.Reader`, which reports the virtual devices only.

### Using the devices directly

`VirtualInput.Acquire()` returns a `VirtualInputSession` (`IVirtualInput`) that the auto tap itself uses. Take one if you
need to press and release in one input update, to replay recorded input, or to read the changes of the virtual devices
only.

```csharp
using var input = VirtualInput.Acquire();
var finger = input.NewFinger();             // null if all 10 fingers are in use
finger.Press(position);                     // each call queues one event
finger.Release();                           // a press and its release may be in one input update
var mouse = input.GetPointer("Mouse");
mouse.MoveTo(position);
mouse.Press();                              // the left button
// after the input update that processed the press:
var target = mouse.GetPrediction().PointerPress;   // the object that is going to get the pointer-down
var keyboard = input.GetDevice("Keyboard");
keyboard.Set("escape", 1f);
keyboard.Set("escape", 0f);
keyboard.Queue(ControlValue.Of("leftCtrl", 1f), ControlValue.Of("r", 1f));   // one event: both at once
```

A device keeps a copy of its state that includes everything that was queued, so the events do not undo each other.
`VirtualPointer.Position` is the position that was queued last. `VirtualPointer.GetPrediction(button)` is the prediction
for a button, refreshed at the end of each input update that processed an event of that button, before
`IVirtualInput.InputUpdated` is raised; `Prediction` is the one of the primary button. Only the primary button and the
right and middle buttons of a mouse have a prediction; for any other button it is empty. A `VirtualFinger` has a
`Prediction` too, and `FingerState`, `PressedFrame` and `ReleasedFrame`. A finger is used for one press only.

`VirtualInput.IsVirtual(device)` tells a virtual device from a real one. `AutoTapBase` also has a constructor that takes an
`IVirtualInput`, so that a session can be shared with it; the caller disposes the session then.

## What happens while it runs

The virtual input changes the Input System for as long as an auto tap, or any other session, is alive:

- **The settings are replaced.** The Input System settings that decide whether input reaches the game are replaced by a
  copy with `backgroundBehavior` set to `IgnoreFocus` and `editorInputBehaviorInPlayMode` set to
  `AllDeviceInputAlwaysGoesToGameView`, and put back when the last session is disposed. This is done on every platform,
  and without it the Input System resets devices or drops input while the application or the Game view has no focus, which
  includes an Editor that runs in the background. In the Editor, **keys typed in other windows of the Editor also reach the
  game while it runs.**
- **The pointer behavior is replaced.** The `pointerBehavior` of the `InputSystemUIInputModule` is set to
  `AllPointersAsIs`, on every platform, and restored afterwards. In the module's default behavior a mouse and a pen are one
  pointer, and input from a touchscreen removes that pointer, so a finger and a mouse or a pen that are used at the same
  time disturb each other.
- **`.current` changes.** Virtual devices are real devices of the Input System. `Touchscreen.current`, `Keyboard.current`,
  `Mouse.current`, `Pointer.current`, `Gamepad.all`, `InputSystem.onDeviceChange` and the control scheme that a
  `PlayerInput` chose can change as soon as a virtual device is added or receives input, and change back when it is
  removed. A device is added when it is first used, so a run that only presses keys never adds a touchscreen.
- **Do not use the Editor's mouse or keyboard while it runs.** A real device that receives input becomes `.current` again,
  and code that reads `.current` then reads your hands instead of the virtual input. Do not touch the mouse and the
  keyboard of the Editor until the run is over.
- **Cleaning up.** When the last session is given up (`Stop`, `Dispose`), a finger that is down is lifted, and every
  virtual device is returned to its defaults where that changes something, so the keys and buttons are released. The
  devices are removed after the UI processed that, a few input updates later. If nothing is pressed and no event is
  waiting, they are removed at once. `VirtualInputSession.Reader` and `InputUpdated` keep working until then. Leaving Play
  Mode removes them at once. Virtual devices are named `AutoTap-<layout>-<number>`; the ones that an earlier run left
  behind are removed when the application starts, when it quits, and when a session is acquired and none was alive.
- **Devices that update before render** (some XR devices) process their events in the before-render update of the frame
  they were queued in, and `InputChangeReader` reports them from there. The auto tap decides that an operation on such a
  device was processed at the next Dynamic input update.

## What is not covered

- Output (vibration) and commands sent to devices. They are not input.
- Connecting and disconnecting devices. The virtual input adds and removes only its own devices.
- The old Input Manager and IMGUI events. They do not go through the Input System. In particular `TMP_InputField` reads
  keys from IMGUI events, so virtual keys and text do not reach it.
- `tapCount`, `isTap` and the primary touch of a touchscreen. The touchscreen computes them and overwrites what is queued.
- A second virtual touchscreen. One touchscreen has 10 touches, which is the number of fingers.
- An update mode other than "Process Events In Dynamic Update".
- Recording and replaying raw events. `InputEventTrace` does that.

## Moving from v1

v2 does not have the self-made raycast of v1. It needs the Input System and lets the input module do the delivering, so
some things that were true in v1 are not true any more.

**Requirements**
- Unity 6000.4, Input System 1.19.0, `InputSystemUIInputModule` in the scene and the update mode "Process Events In Dynamic
  Update". `USE_LEGACY_INPUT` is gone.

**Timing**
- `Fire` does not deliver. The event system does, in the next frame.
- `OnTapCompleted` is called at another time: from `Update(float)` before the slots are visited, in the order the ends were
  processed.
- After `Stop`, the releases (pointer-up, click) are delivered in the next input update.
- `Dispose` calls `Stop` if the auto tap is running.

**`Fire`**
- It returns a `LogItem`, not the `GameObject` that got the pointer-down. That object is the `Target` of the item. It
  returns `null` if nothing was started. A slot that is running returns `null`.
- A point that hits nothing is pressed. In v1 nothing was started there.
- `Scenario.TryTap` still returns `true` only if the press is on `target`. It now judges from the prediction made when
  `Fire` is called; nothing has been delivered yet.
- The number of slots is at most 10 (it was 19), and a larger or smaller number throws `ArgumentOutOfRangeException`.

**Hooks**
- `ToBeIgnored(Transform)` is removed. Its successor `ShouldNotPressOn(RaycastResult)` is called with the topmost hit and
  means "do not press if the topmost hit is this". It does not go on to the objects behind it, which `ToBeIgnored` did.
- `CreateLog(index, target, startPosition, endPosition, duration, out logItem)` is now `NewLogItem(int index)`. It only
  creates the item; every field is filled in afterwards. A new item is created for every operation; v1 reused one item per
  slot.

**Log**
- `GetLatestLogs()` returns what happened in the latest input update, in processing order (`Started`, `DragStarted`,
  `Completed`), as a sequence of `LogEvent`; v1 returned the `LogItem`s of the slots that were active. A frame without an
  input update has an empty sequence. The ends of items that were dropped, that `Stop` ended, or that changed nothing at
  their end come last, without a number. `GetLatestCompletedLogs()` is removed.
- `LogItem` has other fields and a different `ToString`. `ClickExecuted` and `ClickTarget` are predictions.
- Pointer ids are the ones of the Input System: `(deviceId << 24) + touchId` for a finger and the device id for a mouse or a
  pen.

**At run time**
- `.current`, `.all`, `onDeviceChange` and the control scheme of a `PlayerInput` can change while an auto tap runs.
- The Input System settings and the pointer behavior of the module are replaced. See
  [What happens while it runs](#what-happens-while-it-runs).
