#if ENABLE_AUTOTAP
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using LogItem = UnityAutoTap.AutoTapBase.LogItem;
using LogItemKind = UnityAutoTap.AutoTapBase.LogItemKind;

namespace UnityAutoTap.Tests
{
	/// <summary>
	/// Devices that are neither pointers nor keyboards, and what a virtual device does to <c>.current</c>.
	/// Experiments D1 and C1.
	/// </summary>
	public class DeviceTests : AutoTapFixture
	{
		[UnityTest]
		public IEnumerator D1_Joystick_IsOperatedByTheControlPathsOfItsLayout_AndIsCurrentAndListed()
		{
			// D1. Any layout that is not a pointer is a device for FireDevice. A device that is added becomes current
			// (InputManager.cs, 1334) and is listed by Joystick.all and by onDeviceChange. A joystick keeps the
			// stick as two 16-bit values, so the stick is compared with a tolerance. The controls that are not marked
			// dontReset are back to their default values when the operation is over (soft reset range,
			// InputManager.cs, 1511, 1562-1573).
			yield return BuildRig();
			StartAuto();
			var all = ReadAllDevices();
			var deviceChanges = new List<(InputDevice Device, InputDeviceChange Change)>();
			void OnDeviceChange(InputDevice device, InputDeviceChange change) => deviceChanges.Add((device, change));
			InputSystem.onDeviceChange += OnDeviceChange;
			OnTearDown(() => InputSystem.onDeviceChange -= OnDeviceChange);

			LogItem item = null;
			yield return Step(() => item = Auto.FireDeviceSequence("Joystick",
				new ControlSequence().Set("stick", new Vector2(0.5f, -0.5f)).Press("trigger").Hold(0.25f)));
			Assert.That(item, Is.Not.Null);
			Assert.That(item.Kind, Is.EqualTo(LogItemKind.Device));
			Assert.That(item.Index, Is.EqualTo(-1));
			Assert.That(item.Target, Is.Null, "a device has no target");
			var joystick = (Joystick)item.Device;
			Assert.That(VirtualInput.IsVirtual(joystick), Is.True);
			Assert.That(deviceChanges.Any(c => c.Device == joystick && c.Change == InputDeviceChange.Added), Is.True,
				"onDeviceChange tells about the device that was added");

			// Fire queues only the first step, so the stick is written first and the trigger is pressed by the step of
			// the next frame.
			yield return Until(() => item.Order.HasValue, "the first change to be processed");
			yield return Until(() => joystick.trigger.isPressed, "the trigger to be pressed");
			Assert.That(Joystick.current, Is.SameAs(joystick));
			Assert.That(Joystick.all, Does.Contain(joystick));
			var stick = joystick.stick.ReadUnprocessedValue();
			Assert.That(stick.x, Is.EqualTo(0.5f).Within(0.001f));
			Assert.That(stick.y, Is.EqualTo(-0.5f).Within(0.001f));
			Assert.That(joystick.trigger.isPressed, Is.True);

			yield return Until(() => item.Completed, "the operation to end");
			Assert.That(joystick.stick.ReadUnprocessedValue(), Is.EqualTo(Vector2.zero));
			Assert.That(joystick.trigger.isPressed, Is.False);

			Assert.That(all.At(joystick, "trigger").Select(c => c.Type), Is.EqualTo(new[] { typeof(ButtonDown), typeof(ButtonUp) }));
			// A control made only of axes is one unit for the reader, so the stick comes out as one Vector2 change.
			var stickChanges = all.At(joystick, "stick").Where(c => c.Is<ValueChanged>()).ToList();
			Assert.That(stickChanges, Is.Not.Empty, all.ToString());
			var stickValue = (Vector2)stickChanges[0].Value;
			Assert.That(stickValue.x, Is.GreaterThan(0f));
			Assert.That(stickValue.y, Is.LessThan(0f));
			Assert.That(all.OfDevice(joystick).All(c => c.IsVirtual));
		}

		[UnityTest]
		public IEnumerator D1_Sensors_AreWrittenThroughTheirControls_AndKeepTheirValue()
		{
			// The sensor controls are noisy, and a noisy control is dontReset, so the end of the operation does not
			// put them back (InputManager.cs, 1562-1573). A sensor is current because it was added, not because of its
			// events: a change of noisy controls alone does not make a device current (InputManager.cs, 3998-4001).
			yield return BuildRig();
			StartAuto();
			var all = ReadAllDevices();

			var acceleration = new Vector3(0.25f, -9.5f, 0.75f);
			var attitude = Quaternion.Euler(0f, 90f, 0f);
			LogItem accelerometerItem = null;
			LogItem attitudeItem = null;
			yield return Step(() =>
			{
				accelerometerItem = Auto.FireDeviceSequence("Accelerometer", new ControlSequence().Set("acceleration", acceleration).Hold(0.25f));
				attitudeItem = Auto.FireDeviceSequence("AttitudeSensor", new ControlSequence().Set("attitude", attitude).Hold(0.25f));
			});
			Assert.That(accelerometerItem, Is.Not.Null);
			Assert.That(attitudeItem, Is.Not.Null);
			var accelerometer = (Accelerometer)accelerometerItem.Device;
			var attitudeSensor = (AttitudeSensor)attitudeItem.Device;
			Assert.That(Accelerometer.current, Is.SameAs(accelerometer));
			Assert.That(AttitudeSensor.current, Is.SameAs(attitudeSensor));

			yield return Until(() => accelerometerItem.Completed && attitudeItem.Completed, "both operations to end");
			yield return Steps(1);

			Assert.That(accelerometer.acceleration.ReadUnprocessedValue(), Is.EqualTo(acceleration));
			Assert.That(Quaternion.Angle(attitudeSensor.attitude.ReadUnprocessedValue(), attitude), Is.LessThan(0.01f));

			Assert.That(all.OfDevice(accelerometer).Any(c => c.Is<ValueChanged>() && c.ControlPath.StartsWith("acceleration")), Is.True, all.ToString());
			Assert.That(all.OfDevice(attitudeSensor).Any(c => c.Is<ValueChanged>() && c.ControlPath.StartsWith("attitude")), Is.True, all.ToString());
		}

		[UnityTest]
		public IEnumerator C1_WhenTheVirtualDevicesAreRemoved_CurrentGoesBackToTheDevicesThatWereThere()
		{
			// C1. A virtual touchscreen, mouse and pen become current when they are used. When they are removed,
			// current is the last updated device of the same class, if one is left, and null otherwise
			// (InputManager.cs, 1495; InputSystem.cs, 1593-1610). A real mouse is left, so Mouse.current is that one;
			// Pointer.current is null until a pointer receives an event, or is the real mouse, depending on the order in
			// which the devices went, but never a device that was removed.
			yield return BuildRig();
			var realMouse = AddRealDevice<Mouse>("Mouse", "RealMouse");
			Set(realMouse.position, new Vector2(10f, 10f));
			yield return Steps(2);
			Assert.That(Mouse.current, Is.SameAs(realMouse));
			Assert.That(Pointer.current, Is.SameAs(realMouse));
			Assert.That(Touchscreen.current, Is.Null);
			Assert.That(Pen.current, Is.Null);

			StartAuto(useSession: false);
			var point = Rig.EmptyPoint;
			LogItem tap = null;
			LogItem mouse = null;
			LogItem pen = null;
			yield return Step(() =>
			{
				tap = Auto.FireTap(0, point, 0.25f);
				mouse = Auto.FireMouseGesture(Gesture.PressAt(point).Hold(0.25f));
				pen = Auto.FirePenGesture(Gesture.PressAt(point).Hold(0.25f));
			});
			yield return Until(() => tap.Order.HasValue && mouse.Order.HasValue && pen.Order.HasValue, "all three to be processed");

			Assert.That(Touchscreen.current, Is.SameAs(tap.Device));
			Assert.That(Mouse.current, Is.SameAs(mouse.Device));
			Assert.That(Pen.current, Is.SameAs(pen.Device));
			Assert.That(VirtualInput.IsVirtual(Pointer.current), Is.True);

			yield return Until(() => tap.Completed && mouse.Completed && pen.Completed, "all three to end");
			Auto.Stop();
			yield return Until(() => !AnyVirtualDevice(), "the virtual devices to be removed", 30);

			Assert.That(Mouse.current, Is.SameAs(realMouse));
			Assert.That(Touchscreen.current, Is.Null);
			Assert.That(Pen.current, Is.Null);
			Assert.That(Pointer.current == null || Pointer.current == realMouse, Is.True,
				"Pointer.current is null or the real mouse, not a removed device");

			Set(realMouse.position, new Vector2(20f, 20f));
			yield return Steps(1);
			Assert.That(Pointer.current, Is.SameAs(realMouse), "the next event of a pointer makes it current");
		}
	}
}
#endif
