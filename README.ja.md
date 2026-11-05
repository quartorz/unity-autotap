# unity-autotap

[English](README.md) | 日本語

unity-autotap は、uGUI を使う Unity プロジェクトで、タップ、ドラッグ、キー入力をプレイヤーの代わりに行います。スモークテスト、長時間の耐久テスト、デモに使えます。

UI のハンドラーを直接呼ぶことはしません。Input System に仮想デバイス（タッチスクリーン、マウス、ペン、キーボード、ゲームパッド、そのほかの layout）を足し、そこに入力イベントをキューに入れて、シーンにある本物の `InputSystemUIInputModule` に届けさせます。UI から見えるものは、指やマウスやキーが与えるものと同じです。pointer id、enter と exit、長押し、ドラッグ、スクロール、ゲームパッドでのナビゲーション、バックキーがあります。

## 必要なもの

- Unity 6000.4。
- Input System 1.19.0（`com.unity.inputsystem`）と uGUI 2.0.0（`com.unity.ugui`）。どちらもこのパッケージの依存先です。
- Active Input Handling が Input System を含む設定であること。
- イベントシステムが `StandaloneInputModule` ではなく `InputSystemUIInputModule` を使っていること。
- Input System の update mode が "Process Events In Dynamic Update"（既定値）であること。ほかのモードでは `VirtualInput.Acquire` が `InvalidOperationException` を投げます。それを呼ぶ `AutoTapBase.Start` も同じです。

## インストール

1. Unity Package Manager を開きます。
2. "Add package from git URL" を選びます。
3. `https://github.com/quartorz/unity-autotap.git?path=Assets/AutoTap#v2.1.2` を入力します。
4. Scripting Define Symbols に `ENABLE_AUTOTAP` を足します。パッケージ全体が、このシンボルがあるときだけコンパイルされます。

Input System も仮想デバイスも要らない v1 を使い続けるときは、URL の末尾を `#v1.5.2` にします: `https://github.com/quartorz/unity-autotap.git?path=Assets/AutoTap#v1.5.2`。

## 最初の auto tap

auto tap にはスロットがあります。スロットは 1 度に指を 1 本だけ持ち、空いているスロットごとに `Update(int index)` が呼ばれます。スロットの数は、基底クラスのコンストラクタに渡します。1 から 10 までで、範囲外ならコンストラクタが `ArgumentOutOfRangeException` を投げます。

```csharp
using UnityAutoTap;
using UnityEngine;

public class RandomTap : AutoTapBase
{
	public RandomTap() : base(5)   // スロットは 5 つ（最大 10）
	{
	}

	protected override void Update(int index)
	{
		var position = new Vector2(Random.Range(0f, Screen.width), Random.Range(0f, Screen.height));
		Simulate(index, Gesture.PressAt(position).Hold(0.1f));   // 押し、0.1 秒待ち、離す
	}
}
```

作って、開始して、毎フレーム `Update(float)` を呼びます。

```csharp
var autoTap = new RandomTap();
autoTap.SetupMarkers(markerCanvas, markerSprite);   // 省略できる: canvas と sprite で、指と pointer の位置を示す
autoTap.Start();
// 毎フレーム:
autoTap.Update(Time.deltaTime);
// 終わったら:
autoTap.Stop();
autoTap.Dispose();   // まだ動いていれば Stop を呼ぶ
```

`SetupMarkers` はマーカーを出すだけです。スロットはコンストラクタで用意されるので、`SetupMarkers` を呼ばなくても `Start`、`Update`、`Simulate` は動きます。その場合は、指と pointer の位置を示すものが出ません。`SetupMarkers` はいつでも呼べます。auto tap が動いている間でもかまいません。2 回目の呼び出しは、1 回目のマーカーを置き換えます。canvas が `null` ならマーカーは出ません。`Simulate(slot, ...)` は、スロットでない番号（0 からスロットの数 - 1 まで）を渡すと `ArgumentOutOfRangeException` を投げます。`Dispose` のあとに `Start`、`SetupMarkers`、どの `Simulate` を呼んでも `ObjectDisposedException` を投げます。

同時に開始できる auto tap は 1 つだけです（`AutoTapBase.Current`）。ほかの auto tap の `Start` を呼ぶと `InvalidOperationException` を投げます。`Current` は、アプリケーションが起動したときには `null` です。ドメインのリロードなしで Play Mode に入ったときも同じです。

`Simulate` は入力をキューに入れるだけです。Input System はフレームの頭でイベントを処理するので、input module が UI に届けるのは次のフレームです（[入力が届くタイミング](#入力が届くタイミング)を参照）。

## 入力を送る 3 つの方法

送り先は 3 種類あります。`TDevice` はデバイスの型で、`UnityEngine.InputSystem` の `Mouse`、`Pen`、`Keyboard`、`Gamepad`、`Joystick` などです。

| 方法 | 送り先 | できること |
|---|---|---|
| 指: `Simulate(int slot, Gesture)` | タッチスクリーン | 押す、動かす、離す、キャンセル、pressure と radius、複数の指を同時に動かす |
| pointer: `Simulate<TDevice>(Gesture, number)` または `Simulate(layout, Gesture, number)` | タッチスクリーン以外の pointer: マウス、ペン、独自の pointer layout | 押さずに動かす（hover）、左・右・中・進む・戻るのボタン、ペンの先端・消しゴム・バレルボタン、pressure と tilt、スクロール |
| デバイス: `Simulate<TDevice>(ControlSequence, number)` または `Simulate(layout, ControlSequence, number)` | pointer でないデバイス: キーボード、ゲームパッド、ジョイスティック、センサー、独自の layout | control にパスで値を書く、値を時間をかけて変える、キーボードへのテキストと IME の変換中の文字列 |

`Simulate` は 6 つあります。どれも `LogItem` を返します。

```csharp
LogItem Simulate(int slot, Gesture gesture);
LogItem Simulate<TDevice>(Gesture gesture, int number = 0) where TDevice : Pointer;
LogItem Simulate(string layout, Gesture gesture, int number = 0);
LogItem Simulate<TDevice>(ControlSequence sequence, int number = 0) where TDevice : InputDevice;
LogItem Simulate(string layout, ControlSequence sequence, int number = 0);
LogItem Simulate(Key key, float duration = 0f, params Key[] modifiers);
```

`Simulate<TDevice>` は、`Gesture` を渡すときは `TDevice : Pointer`、`ControlSequence` を渡すときは `TDevice : InputDevice` の型を取ります。仮想デバイスの layout は、型から Input System が決めます。C# の型がない layout は、名前で指定します: `Simulate("MyLayout", ...)`。型と名前は、同じ仮想デバイスを指す 2 通りの書き方です。`Simulate<Mouse>(...)` と `Simulate("Mouse", ...)` は同じマウスを操作します。Input System が layout として登録していない型を渡すと、`Simulate` は `ArgumentException` を投げます。Input System の型は登録されています。`number` は、同じ layout のデバイスが複数あるときに区別します。

ショートカットは `Simulate(Key key, float duration = 0f, params Key[] modifiers)` の 1 つだけです。仮想キーボードのキーを押します。

仮想デバイスは、開始している auto tap の `Simulate` が初めてそれを必要としたときに足されます。auto tap の開始時ではありません。pointer とデバイスは、それを指定する最初の呼び出しで足されます。その呼び出しがほかの理由で `null` を返しても、足されます。タッチスクリーンは最初の指を取ったときに足されるので、それより前に `null` を返した呼び出しは足しません。キーを押すだけのコードは、タッチスクリーンもマウスも足しません。

**指は `Touchscreen` ではなく、スロットで押します。** `Touchscreen` は `Pointer` の派生なので、`where TDevice : Pointer` の制約では弾けません。`Simulate<Touchscreen>(...)` と `Simulate("Touchscreen", ...)` はコンパイルでき、実行時に `ArgumentException` を投げます。`Simulate(int slot, Gesture)` を使ってください。この例外を投げるのは、開始している auto tap です。`Start` の前と `Stop` のあとは、`Simulate` はデバイスを見る前に `null` を返します（[`Simulate` がすること](#simulate-がすること)を参照）。

`Simulate` は `AutoTapBase` の `protected internal` のメンバーです。`Scenario` には同じメソッドが `protected` のメンバーとしてあり、持ち主に転送します。`LogItem`、`LogEvent`、`LogEventKind`、`LogEventSequence`、`LogItemKind`、`EndReason` は `AutoTapBase` の入れ子の型です。手順のステップは、`Gesture.Step` と `Gesture.StepKind`、`ControlSequence.Step` と `ControlSequence.StepKind` です。

### 指と pointer: `Gesture`

`Gesture` は、順に実行するステップの並びです。input module は指、マウス、ペンを同じように扱うので、3 つとも `Gesture` で操作します。時間は、`AutoTapBase.Update` に渡した秒数で数えます。

```csharp
// タップ、ドラッグ、長押しに続くドラッグ。
Simulate(0, Gesture.PressAt(position).Hold(0.1f));
Simulate(0, Gesture.PressAt(from).MoveTo(to, 0.5f));
Simulate(0, Gesture.PressAt(position).Hold(0.8f).MoveTo(to, 0.5f));

// 2 本の指を広げる: 同じフレームで 2 つのスロットを Simulate する。
Simulate(0, Gesture.PressAt(centerLeft).MoveTo(farLeft, 0.4f));
Simulate(1, Gesture.PressAt(centerRight).MoveTo(farRight, 0.4f));

// 何かが起きるまで押し続ける。最大 5 秒。起きなければ、押しているものを離して
// EndReason.TimedOut で終わる。
Simulate(0, Gesture.PressAt(position).HoldUntil(() => progressBar.value >= 1f, 5f));

// pressure のある指。
Simulate(0, Gesture.PressAt(position).Set("pressure", 0.3f).Hold(0.2f).Set("pressure", 1f, 0.3f).Hold(0.2f));

// マウス: hover、右クリック、中ボタンでのドラッグ、スクロール。
Simulate<Mouse>(Gesture.HoverAt(position).Hold(0.5f));
Simulate<Mouse>(Gesture.PressAt(position, "rightButton").Hold(0.1f));
Simulate<Mouse>(Gesture.PressAt(from, "middleButton").MoveTo(to, 0.5f));
Simulate<Mouse>(Gesture.HoverAt(position).Scroll(new Vector2(0f, -3f)));

// ペン: pressure をつけて hover し、先端を押す。ペンは最初のステップから最後まで in range になる。
Simulate<Pen>(Gesture.HoverAt(position).Set("pressure", 0.5f).Press().Hold(0.2f));
```

`Gesture.Tap` のようなショートカットはありません。タップは押しと `Hold`、ドラッグは押しと `MoveTo` で書きます。

| ステップ | 意味 |
|---|---|
| `Gesture.PressAt(position, button = null)` | 押しから始める。`null` は主な操作で、指ならタッチ、マウスなら左ボタン、ペンなら先端。ほかのボタンは `"rightButton"` のような control のパスで指定する。 |
| `Gesture.HoverAt(position)` | 押さずに動かすところから始める。指には使えない。 |
| `MoveTo(position, seconds)`、`MoveTo(Func<Vector2>, seconds)` | 押しているものを保ったまま、直線で動かす。`Func` は動き始めるときに評価される。 |
| `Press(button)`、`Release(button)` | ボタンをもう 1 つ押す、または離す。 |
| `Scroll(delta)` | 今の位置で 1 回スクロールする。指には使えない。 |
| `Set(controlPath, value, seconds = 0)` | control（`pressure`、`radius`、`tilt` など）の値を、すぐに、または少しずつ変える。指では、パスはタッチからの相対で、あるのは `pressure` と `radius` だけ。 |
| `Hold(seconds)`、`HoldUntil(condition, timeout)` | 待つ。condition は `Update(float)` ごとに 1 回評価される。手前のステップに時間がかかっていなければ、`Simulate` も評価する。 |

送り先に合わない gesture（指への `HoverAt`、デバイスにない control）を渡すと、`Simulate` は `ArgumentException` を投げます。使い方に合わない型や layout も同じです。

- `Touchscreen`（型でも layout でも。上を参照）。
- `Pointer` から派生していない layout を、`Gesture` と一緒に渡したとき。
- `Pointer` から派生した型や layout を、`ControlSequence` と一緒に渡したとき。

`null` の gesture や sequence を渡すと、`Simulate` は `ArgumentNullException` を投げます。layout の名前が空のときは `ArgumentException` を投げます。

UI が扱うのは、どの pointer でも主なボタンと、マウスの右ボタンと中ボタンです。ほかのボタン（進む、戻る、ペンの消しゴムとバレルボタン）の押しは、読むコードのためにデバイスには書かれますが、UI には届きません。そのボタンに従う `LogItem` には、`Target`、`ClickTarget` などの予測が入りません。

スクロールは `Simulate<Mouse>(Gesture.HoverAt(position).Scroll(delta))` で、hover とスクロールの間に待ちはありません。`Simulate` は呼び出しのフレームで 2 つのイベントとして両方をキューに入れ、1 回の入力更新が hover、スクロールの順に処理します。input module は pointer をそこにある UI の上に置き、呼び出しの次のフレームでスクロールします。マウスがその呼び出しで足されたときも同じです。

### そのほかのデバイス: `ControlSequence`

`ControlSequence` には位置がありません。control にパスで値を書きます。

```csharp
// バックキー。
Simulate(Key.Escape);

// ショートカット: Ctrl+R。
Simulate(Key.R, 0.1f, Key.LeftCtrl);

// ゲームパッド: 左スティックを下に 0.5 秒倒し、South（決定）を押す。
Simulate<Gamepad>(new ControlSequence().Set("leftStick", Vector2.down).Hold(0.5f));
Simulate<Gamepad>(new ControlSequence().Press(GamepadButton.South));

// キーボードへのテキストと IME の変換中の文字列。
Simulate<Keyboard>(new ControlSequence().Composition("かん").Composition("").Text("漢"));

// layout があるものなら何でも: ジョイスティック、センサー。
Simulate<Joystick>(new ControlSequence().Set("stick", new Vector2(0.5f, -0.5f)).Press("trigger").Hold(0.2f));
Simulate<Accelerometer>(new ControlSequence().Set("acceleration", new Vector3(0f, -9.8f, 0f)).Hold(0.2f));

// C# の型がない layout は、名前で指定する。
Simulate("MyLayout", new ControlSequence().Set("axis", 0.5f).Hold(0.2f));
```

`Key` と `Gamepad` は `UnityEngine.InputSystem` に、`GamepadButton` は `UnityEngine.InputSystem.LowLevel` にあります。

| ステップ | 意味 |
|---|---|
| `Press(path)`、`Release(path)`、`Press(Key)`、`Release(Key)`、`Press(GamepadButton)`、`Release(GamepadButton)` | ボタンのような control を押す、または離す。1 か 0 を書く。 |
| `Set(controlPath, value, seconds = 0)` | control（スティック、トリガー、軸）の値を、すぐに、または少しずつ変える。 |
| `Text(text)` | テキストを入力する。1 文字（Unicode のコードポイント）につき 1 つのテキストイベント。キーボードだけ。 |
| `Composition(text)` | input method editor が変換中の文字列を設定する。空の文字列で変換を終える。キーボードだけ。変換中の文字列は最大 63 文字で、これは 1 つのイベントに入る長さ。 |
| `Hold(seconds)`、`HoldUntil(condition, timeout)` | 待つ。 |

キーを押しても、文字は入力されません。文字はキーボードの layout で決まるからです。文字を入力するときは `Text` を使います。テキストと変換中の文字列はキーボード（`Keyboard.onTextInput`、`Keyboard.onIMECompositionChange`）に届き、`TMP_InputField` は読みません（[対象外のもの](#対象外のもの)を参照）。

synthetic な control、つまり Input System が `synthetic` と印をつけた control には、`Set` を使わないでください。ペンの `inRange` や XR デバイスの `isTracked` がそうです。入力としては働きません。reader はその変化を報告せず、汎用の XR layout で `isTracked` に `Set` しても値は 0 のままでした。ペンの gesture は、ペンを in range にします。

`ControlSequence` を渡した `Simulate` は、使い方の誤りで `ArgumentException` を投げます。

- 存在しない layout、または `Pointer` から派生した型や layout。
- デバイスにない control のパス。押しや離しにボタンが必要なところで、ボタンでない control（スティック）。
- 値を別の control から計算していて、書き込めない control。
- control に合わない値。
- キーボードでないデバイスへの `Key`、ゲームパッドでないデバイスへの `GamepadButton`。
- テキストを受け取らないデバイスへの `Text` や `Composition` のステップ。63 文字を超える変換中の文字列。
- 書き込むステップが 1 つもない sequence。待つだけのものなど。

テキストを受け取らないデバイスで `VirtualDevice.Text` や `VirtualDevice.Composition` を直接呼ぶと、代わりに `InvalidOperationException` を投げます。63 文字を超える変換中の文字列は、ここでも `ArgumentException` です。

### バックキーを何度も押す

バックキーは、キーボードの `escape` です。`*/{Cancel}` にバインドした `InputAction` と、`InputSystemUIInputModule` が選択中のオブジェクトに届ける cancel が反応します。ゲームパッドの `buttonEast` にも同じように反応するので、`Simulate<Gamepad>(new ControlSequence().Press(GamepadButton.East))` もバックキーです。

```csharp
// 早くて 2 フレームに 1 回。
Simulate(Key.Escape);

// 毎フレーム 1 回: 押しと離しをすべて持つ 1 つの操作。
var sequence = new ControlSequence().Press(Key.Escape);
for (var i = 0; i < 9; ++i)
{
	sequence.Release(Key.Escape).Press(Key.Escape);
}

sequence.Release(Key.Escape);
Simulate<Keyboard>(sequence);   // 押しが 10 回。続く 10 フレームに 1 回ずつ
```

- `Simulate(Key)` はキーボードで操作を始めます。キーボードは、前の操作の終わりが処理されてから、次の操作を受け付けます。フレーム N で Simulate すると、押しはフレーム N + 1 で、離しはフレーム N + 2 で処理されます。離しが押しと同じ入力更新に入ることはありません。次の `Simulate(Key)` が受け付けられるのはフレーム N + 2 で、つまり 2 フレームに 1 回です。
- sequence は 1 つの操作なので、押しと押しの間にキーボードが塞がることはありません。それぞれの離しは、手前の押しの次の入力更新に、次の押しと一緒に、この順で入ります。そのため、キーは 1 フレームに 1 回押されます。
- input module が 1 フレームに UI へ届ける cancel は、そのフレームで処理された押しがいくつあっても 1 回までです。`InputAction` は、1 つの入力更新に複数の押しがあっても、押しのたびに performed になります。
- 押しと離しを 1 つの入力更新に入れるときは、仮想デバイスを直接使います（[デバイスを直接使う](#デバイスを直接使う)を参照）。

### `Simulate` がすること

- 操作を記録する `LogItem` を返し、手順の先頭にある、時間のかからないステップをキューに入れます。次の場合は `null` を返し、何もキューに入れません。
  - 位置が画面の外にある。
  - スロットや仮想デバイスが、前の操作でまだ塞がっている（その終わりが処理されるまで）。
  - 最初のステップが押しで、その下にあるものに対して `ShouldNotPressOn` が `true` を返した。
  - タッチスクリーンの 10 本の指がすべて使われている。
  - auto tap が有効でない（`Start` の前、または `Stop` のあと）。
- `Start` の前と `Stop` のあとは、`Simulate` は仮想の pointer やデバイスを見る前に `null` を返します。そのデバイスで決まる例外（タッチスクリーンの型や layout、存在しない layout、pointer やデバイスにない control）は、auto tap が開始している間にだけ起きます。手順だけで決まる例外（指にできない gesture、何も書き込まない sequence）と、スロットの番号の例外は、いつでも起きます。
- `Simulate` がキューに入れるのは、最初のステップだけではありません。手順の先頭にある、時間のかからないステップをすべて入れます。最初のステップと、それに続くステップのうち、時間のかかる最初のステップの手前までです。時間がかかるのは、動きと、秒数をかけた変化と、待たなければならない `Hold` と `HoldUntil` です。condition がすでに true の `HoldUntil` は時間がかからず、`Simulate` が評価します。手順の残りは、`Simulate` がキューに入れたものが処理されたあと、続くフレームで `Update(float)` が進めます。これは `Update(float)` が 1 フレームぶんのステップに使う規則と同じです。そのため、ステップは手順のどこにあっても、`Simulate` のフレームか、それより後のフレームでキューに入ります。

  ```csharp
  // 1 回の呼び出し、1 回の入力更新: スティックが動き、South が押され、次に West が押される。
  Simulate<Gamepad>(new ControlSequence().Set("leftStick", Vector2.right).Press(GamepadButton.South).Press("buttonWest").Hold(0.2f));
  Simulate(Key.R, 0.1f, Key.LeftCtrl);   // Ctrl と R は、この順で呼び出しのフレームに押される
  ```
- 何もないところでも、押しは行われます。
- 1 フレームに続くステップは、手順の順に、別々のイベントとしてキューに入ります。`R` の前にある `Ctrl` は、`R` より先に処理されます。修飾キーとキーを 1 つのイベントに書くと、順序のある `OneModifier` のバインドでは動きません。修飾キーとキーが同じイベントで変わると、performed にならないからです。そのため、ステップはそれぞれ 1 つのイベントにしています。
- 操作は、`Hold(0)` でも、押しと離しを 1 つの入力更新に入れません。離しは次のフレームに入ります。押しを `Simulate` が行ったときも同じです。操作の終わりでの離しも同じです。
- 手順が終わると、押しているものは逆の順に離され、`dontReset` でない control はすべて既定値に戻ります。Input System のソフトリセットの範囲です。マウスとペンの位置は `dontReset` で、そのまま残るので、hover は続きます。センサーの control は値が揺れるので、これも `dontReset` で、センサーは渡された値を保ちます。
- スロットやデバイスは、1 度に 1 つの操作を行います。同じキーをもう一度押すには、前の押しが終わるのを待つか、押しを 1 つの sequence に入れます（上を参照）。

### `ShouldNotPressOn`

UI の一部に触れたくないときに override します。指や pointer の押しごとに、その位置でのイベントシステムの raycast の最前面の結果、つまり input module が使うオブジェクトを渡して呼ばれます。何にも当たらないとき、hover のとき、`ControlSequence` のときは呼ばれません。

```csharp
protected override bool ShouldNotPressOn(RaycastResult topmost)
{
	return !topmost.gameObject.transform.IsChildOf(mainCanvas.transform);   // メインの canvas の外には何もしない
}
```

`true` を返すと、操作の最初の押しは行われず、`Simulate` は `null` を返します。後の押しでは、操作は押しているものを離し、`EndReason.Refused` で終わります。後の押しがあるのは pointer だけです。指にあるのはタッチだけだからです。最前面の裏にあるオブジェクトは試しません。どの raycaster が当たりを出したかは、`RaycastResult.module` で分かります。

### シナリオ

`Scenario`、`ScenarioConfig`、`Group`、`Condition`、`ScenarioBasedAutoTap` は、スロットの上でシナリオを動かします。`Scenario` には `AutoTapBase` と同じ `Simulate` があり、`ShouldNotPressOn` を転送します。`ScenarioBasedAutoTap` も、`AutoTapBase` と同じくスロットの数をコンストラクタで受け取ります。

`TryTap(index, canvas, target, duration)` は `RectTransform` の中心をタップし、タップが始まり、**かつ**押しが `target` に届くと予測されるときに `true` を返します。返された item の `Target`（タップを Simulate したときに求めたもの）が、`target` のゲームオブジェクトであるときです。`TryTap` は、`RectTransform` から押す点を求め、押しが `target` に届くかを判定します。`Simulate(index, Gesture.PressAt(point).Hold(duration))` はそれをしません。`Target` は、当たったものに最も近いハンドラーなので、`target` は押しを扱うオブジェクトにしてください（たとえば `Button` です。ほかのものに覆われた、その中の飾りではありません）。予測は、押しが処理されるときに更新されます。

## サンプル

このリポジトリの `Assets/Sample` は、小さなアプリ（タイトル画面、リストのあるホーム画面、ヘッダーのメニュー）と、それを動かす auto tap です。`RandomTap` と、`ScenarioBasedAutoTap` の上のシナリオがあります。3 つのシナリオは、指だけではない入力を送ります。

| 例 | デバイス | すること |
|---|---|---|
| `CloseMenuWithEscape` | キーボード | メニューボタンをタップし、メニューが開くのを待って、Escape を押す。UI の cancel アクションが、選択中のオブジェクトであるメニューボタンに届き、`MenuCancelHandler` がメニューを閉じる。 |
| `HoverAndLeave` | マウス | 仮想マウスを `HoverHighlight` の上に置き、hover を示すまで待ち、マウスを離して、示さなくなるまで待つ。 |
| `MoveAndSubmit` | ゲームパッド | 左スティックを 1 回はじくごとに、選択を `PadPanel` の上で 1 つ動かす。そのあと South を押し、ボタンが submit されるまで待つ。最初のボタンは、イベントシステムの `firstSelectedGameObject`。 |

```csharp
// CloseMenuWithEscape: キーはスロットを使わないので、シナリオは LogItem を持ち、Completed を待つ。
_escape = Simulate(Key.Escape, 0.1f);

// HoverAndLeave: 押さずに hover し、UI が反応するのを待ち、離れる。
_item = Simulate<Mouse>(Gesture
	.HoverAt(on)
	.HoldUntil(() => _target.Hovered, 1f)
	.Hold(0.2f)
	.MoveTo(away, 0.2f)
	.HoldUntil(() => !_target.Hovered, 1f));

// MoveAndSubmit: 1 ステップごとにはじき、ボタンが反応したら submit する。
var sequence = new ControlSequence();
for (var i = 0; i < Mathf.Abs(goal - from); ++i)
{
	sequence.Set("leftStick", direction).Hold(0.1f).Set("leftStick", Vector2.zero).Hold(0.1f);
}

var submitted = _pad.SubmitCount;
sequence.Press(GamepadButton.South).HoldUntil(() => _pad.SubmitCount > submitted, 1f).Release(GamepadButton.South);
_item = Simulate<Gamepad>(sequence);
```

例は、サンプルの `ScenarioBasedAutoTap` の入れ子のクラスなので、`SetJson` は名前で見つけられます。`InputExamples.Json` は、タイトル画面が出ていればログインし、3 つをそれぞれ 3 回動かします。どの例も、UI への効果を自分で待ちます。`LogItem` を持ち、続くフレームで `Completed` を見ます（`HoverAndLeave` と `MoveAndSubmit` は `EndReason` も見て、操作が `Finished` で終わらなかったときは警告を出します）。

`SampleScene` には、例に必要なものがあります。イベントシステムの `InputSystemUIInputModule`（既定のアクションで、最初に選択されるオブジェクトはパッドの最初のボタン）、`MenuButton` の `MenuCancelHandler`、`DebugCanvas` の下の `ExamplePanel`（`HoverTarget`（`HoverHighlight`）と `PadPanel`（3 つのボタンと `PadLabel`）を持つ）です。自分のシーンで使うために、各シナリオのコメントに必要なものを書いてあります。Play Mode で、`DebugManager` の Inspector にある "Start Input Examples" を押すと動かせます。これは `StartJsonBasedAutoTap(InputExamples.Json)` を呼びます。

## 入力が届くタイミング

```
フレーム N      Update(float): Simulate が、手順の先頭にある時間のかからないステップをキューに入れる
フレーム N + 1  入力更新（フレームの頭）: Input System が、キューに入れた順に処理する
                イベントシステム: input module が pointer、ナビゲーション、submit/cancel のイベントを届ける
                Update(float): 次のステップをキューに入れる
```

- `Simulate` や `Update(float)` の中では、何も届きません。
- イベントが*処理される*順は、キューに入れた順です。全デバイスをまたいで同じです。この順にキューに入れたキーの押し、指、スクロールは、この順に処理されます。
- input module が UI に*届ける*順は、約束されません。pointer ごとに、1 フレームに 1 回扱います。
- `Stop` は、押しているものの離しをキューに入れます。離しが UI に届くのは次の入力更新で、`Stop` の中ではありません。

`Update(float)` は、auto tap が有効でない間（`Start` の前と `Stop` の後）は何もしません。有効なときは、次の順で動きます。

1. `GetLatestLogs()` が返す並びが作り直される（[`GetLatestLogs()`](#getlatestlogs)を参照）。
2. `OnPreUpdate`。
3. 終わったスロットの操作について `OnTapCompleted`。`LogItem.CompletedOrder` の順。
4. スロットを順に: 動いている操作を進めて次のステップをキューに入れる。空のスロットには `Update(int index)` が呼ばれる。
5. スロットの外で動いている操作を、始めた順に進める。
6. `OnPostUpdate`。

`Stop` は、動いている操作を、始めた順に終わらせます。押しているものは離され、その `LogItem` は `EndReason.Interrupted` で終わります。スロットにあった操作については、`Stop` の中で `OnTapCompleted` が呼ばれます。

## 起きたことを読む

`Simulate` は `LogItem` を返します。操作が進むにつれて、中身が入っていきます。受け付けた操作ごとに新しい item が作られます。`NewLogItem(int index)` を override して、`LogItem` から派生したクラスのインスタンスを返すこともできます。すべてのフィールドは、あとから埋まります。

| フィールド | 意味 |
|---|---|
| `Index`、`Kind`、`Device` | スロット（スロットの外は `-1`）、操作の種類、操作した仮想デバイス。`Kind` は、`Simulate(slot, ...)` なら `Finger`、タッチスクリーン以外の pointer に `Gesture` を渡したなら `Pointer`、`ControlSequence` を渡したときと `Simulate(Key, ...)` なら `Device` |
| `FrameCount`、`DateTime`、`ScreenPoint` | 求めたこと。`Simulate` を呼んだときのもの。`ScreenPoint` は最初の押しまたは hover の位置で、位置のないデバイスでは 0 |
| `EndScreenPoint`（`Vector2?`） | 指や pointer を最後に置く位置（画面のピクセル）。操作が終わるまでは、手順が示す位置で、pointer を動かす最後のステップの位置。pointer をどこにも動かさない手順では `ScreenPoint` と同じ。手順が示さないうちは `null`。`MoveTo(Func<Vector2>)` の行き先は、その動きが始まった時点から分かる。終わったあとは、実際に最後に置いた位置で、操作が手順を最後まで実行しなかったときは、手順の位置と違うことがある。どちらかは `Completed` で分かる。デバイスでは常に `null` |
| `Duration`（`float?`） | 操作が続く時間（秒）。`Update(float)` に渡した `deltaTime` で数える。操作が終わるまでは、手順がかける時間で、ステップの秒数の合計。`HoldUntil` は待った時間だけかかるので、condition が満たされるまでは何も足さず、手順にあるすべての `HoldUntil` の condition が満たされるまで、値は `null`。どれかがタイムアウトしたら、操作が終わるまで `null` のまま。終わったあとは、手順に `Update(float)` が渡した `deltaTime` の合計で、一度も動かなかった操作（`Dropped`）では 0。手順を最後まで実行したときは、手順の時間以上で、超える分は `deltaTime` 1 つぶんより小さい。どちらかは `Completed` で分かる |
| `CurrentStep`（`int`） | 操作が実行しているステップの添字。`Gesture` の `Gesture.Steps`、または `Sequence` の `ControlSequence.Steps` の添字。操作が進み始めるまでは -1。進み始めずに終わった操作も -1。指は、Input System が押しを処理したとき（`Order` が入ったとき）に進み始める。pointer とデバイスは、`Simulate` がキューに入れたステップの書き込みを処理したとき、何も書き込まないならすぐに進み始める。操作が終わったあとは、`Finished` なら最後のステップの添字、それ以外なら終わったときにいたステップの添字で、`Dropped` では -1 |
| `Order`、`StartedFrameCount` | 操作を始めた変化の番号と、それが処理されたフレーム。それまでは `null` と `0` |
| `DragStartedOrder`、`DragStartedFrameCount`、`Dragged` | pointer をドラッグのしきい値より先へ動かした変化（begin-drag が届くとき）と、それが起きたかどうか: `Dragged` |
| `CompletedOrder`、`CompletedFrameCount`、`Completed`、`EndReason` | 操作を終わらせた変化と、その理由: `Finished`、`Interrupted`（`Stop`、Input System がデバイスをリセットした、または操作の始まったあとに、離しなどのイベントを Input System が捨てた）、`TimedOut`、`Dropped`（始めるはずだったイベントを Input System が捨てた）、`Refused`。処理された変化が操作を終わらせていないときは、`CompletedOrder` が `null` になる: `Dropped`、`Stop` が終わらせたもの、終わりでどの control も変わらない操作（何も押さない hover など）。 |
| `Target`、`ClickExecuted`、`ClickTarget`、`DragTarget`、`DropTarget` | イベントシステムがこれからすること。input module の規則から求める（下を参照） |
| `Gesture`、`Sequence` | `Simulate` に渡した手順。指と pointer では `Gesture`、デバイスでは `ControlSequence`。ステップは読める（下を参照） |

`LogItem` は、操作が進むあいだ、その場で書き換わります。`GetLatestLogs()` が 1 つの item について返す `LogEvent`（`Started`、`DragStarted`、`Completed`）は、どれも同じ `LogItem` を指します。そのため、読めるのはイベントが起きた時点の状態ではなく、今の状態です。イベントの時点の値が要るときは、そのイベントが起きたフレームのうちに読むか、写しを取ってください。

フィールドには、今までに分かっていることを入れるものがあります。`Target`、`DragTarget`、`ClickTarget`、`EndScreenPoint`、`Duration`、`CurrentStep` と、何かが起きたかどうかを示すフィールドです。`Target` と `DragTarget` は、`Simulate` を呼んだときに立てた予測で、押しが処理されるときに求め直されます。`EndScreenPoint` と `Duration` は、操作が終わるまでは手順が示す値、終わったあとは実際の値です。`CurrentStep` は操作に合わせて進みます。

手順は読めます。`Gesture.Steps` は `IReadOnlyList<Gesture.Step>`、`ControlSequence.Steps` は `IReadOnlyList<ControlSequence.Step>` で、渡したとおりのステップを、実行する順に並べています。リストは手順を読み取り専用で参照するもので、写しではありません。そのため、手順にあとから足したステップもリストに現れます。手順は、`Simulate` に渡したあとで変更しないでください。`CurrentStep` は、このリストの添字です。

ステップは、読み取り専用の struct です。`Kind` は何をするかを示す enum（`Gesture.StepKind` か `ControlSequence.StepKind`）で、ほかのメンバーが、その種類の値を持ちます。その種類が使わないメンバーは、`null`、0、空のどれかです。

| ステップ | 種類 | メンバー |
|---|---|---|
| `Gesture.Step` | `Press`、`Release`、`Hover`、`Move`、`Scroll`、`Set`、`Hold`、`HoldUntil` | `Position`（`Vector2?`。`PressAt`、`HoverAt`、位置を渡した `MoveTo` のもの）、`PositionFunc`（もう一方の `MoveTo` の `Func<Vector2>`）、`Button`（`Press` と `Release` のもの。`null` は主なボタン）、`Delta`（`Scroll` のもの）、`Path` と `Value` と `ValueType`（`Set` のもの）、`Seconds`（`Move`、`Set`、`Hold` のもの。`HoldUntil` ではタイムアウト）、`Condition`（`HoldUntil` のもの） |
| `ControlSequence.Step` | `Press`、`Release`、`Set`、`Text`、`Composition`、`Hold`、`HoldUntil` | `Control`（`Press`、`Release`、`Set` のもの。パスの `string`。押しと離しでは `Key` か `GamepadButton` のこともある）、`Value` と `ValueType`（`Set` のもの）、`Text`（`Text` と `Composition` のもの）、`Seconds`（`Set` と `Hold` のもの。`HoldUntil` ではタイムアウト）、`Condition`（`HoldUntil` のもの） |

`Simulate` が返した item は、ちょうど 1 回終わります。

`Target`、`ClickTarget`、`DragTarget`、`DropTarget` は指と pointer の予測で、デバイスでは空のままです。input module の規則（当たったオブジェクトに最も近いハンドラー、離しが押されたオブジェクトの上にあるときだけ click、ドラッグのしきい値）に従い、変化を処理した入力更新の直後、イベントシステムが届ける前に求めます。動いている間に UI を変えるハンドラーがあると、予測と実際に届いたものが食い違うことがあります。pointer の操作は、gesture が最初に押すボタンに従います。UI がそのボタンを扱う場合です。

`DragTarget` は、pointer がドラッグされたときに `beginDrag`、`drag`、`endDrag` を受け取るオブジェクトです。input module はそれを pointer が押されたときに決める（そのとき、そのオブジェクトは `initializePotentialDrag` を受け取る）ので、`DragTarget` は押しが処理された時点で値を持ち、`Simulate` が返るときにも 1 度求められています。何かがドラッグされたとは限りません。ドラッグのハンドラーを持つボタンをタップしても、そのボタンが `DragTarget` になります。pointer がドラッグしたかどうかは `Dragged` で、pointer がドラッグのしきい値より先へ動くと `true` になります。`DragStartedOrder` はそうした変化です。`DropTarget` は逆で、ドラッグしなかった pointer には drop が届かないので、`Dragged` が `true` でない限り `null` のままです。

`LogItem.ToString` は、操作の種類、スロット、フレーム、操作の長さ（小数点以下 2 桁）、日時、押した位置、終わりの位置、押しを受け取るオブジェクトを出します。`null` のものは出しません。`Duration` がない item には `Duration:` がなく、終わりの位置がない item には `→` がありません。`GameObject: (none)` は、`Target` がないという意味です。ステップは出しません。デバイスでは、2 行目が `Device:` とその名前になります。

```
Finger Index: 0 FrameCount: 123, Duration: 0.10, DateTime: 11/04/2026 12:00:00
ScreenPoint: (100.0, 200.0) → (300.0, 400.0) GameObject: Button
```

### `GetLatestLogs()`

`GetLatestLogs()` は、起きたことを読む唯一の場所です。前の `Update(float)` から今までに起きたことの並びを、Input System が処理した順に返します。何も起きなければ、並びは空です。

```csharp
foreach (var e in GetLatestLogs())
{
	// e.Item  LogItem
	// e.Kind  Started、DragStarted、Completed のどれか
	// e.Order それを起こした変化の番号（番号のない Completed の item では null）
}
```

- 戻り値は `LogEventSequence` で、`IReadOnlyList<LogEvent>` を実装する readonly struct です。auto tap のログを直接参照するもので、写しではありません。`foreach`、`Count`、添字では、割り当てが起きません。LINQ や `IEnumerable<LogEvent>` を通すと、enumerator が boxing されて割り当てが起きます。`default` の値は空の並びです。
- 並びは、動いた `Update(float)` の頭で作り直され、`Start` と `Dispose` で空になります。それ以外のときには変わりません。並びが有効なのは、次の `Update(float)` が始まるまで、または `Start` か `Dispose` を呼ぶまでです。そのあとで、並びを回す、`Count` を読む、要素を読む、のどれをしても `InvalidOperationException` を投げます。新しい並びが空のときも同じです。並びの外の添字は `ArgumentOutOfRangeException` を投げます。要素をあとで使うときは、写しを取ってください（たとえば `ToArray()` で）。
- 最後の `Update(float)` の並びは、次の `Update(float)` が始まるまで、最新のままです。時間がたっても空にはなりません。auto tap が有効でない間、`Update(float)` は何もしないので、`Stop` のあとでは、並びは変わりません。`Start` は並びを空にするので、`Start` のあとは、その実行の最初の `Update(float)` が並びを作るまで、並びは空です。`Dispose` も並びを空にします。どちらも、それまでに返した並びを無効にします。
- どの並びに入るかは、起きたのが `Update(float)` の中か外かで決まります。
  - `Update(float)` の外で起きたことは、取っておかれ、次の `Update(float)` が作る並びの先頭になります。フレームの頭の入力更新が処理したこと（操作の開始、ドラッグの開始、操作の終わり）と、前の `Update(float)` が終わったあとに起きたことです。たとえば、Input System がデバイスをリセットする（`InputSystem.ResetDevice` や、デバイスを無効にしたとき）と、そのデバイスを使う操作は、その時点で終わります。`Update(float)` を呼ばなかったフレームで起きたことも、失われません。
  - `Update(float)` の中で起きたことは、その場で、その呼び出しの並びに足されます。`OnPreUpdate`、`Update(int)`、`OnPostUpdate`、`OnTapCompleted` と、それらが呼ぶもの（シナリオなど）が含まれます。たとえば、シナリオが呼んだ `Stop` が終わらせた操作の `Completed` は、その呼び出しの並びに入ります。そこで呼んだ `ResetDevice` や `DisableDevice` が終わらせた操作の `Completed` も同じです。

  どれも、入る並びは 1 つだけです。2 つに入ることはなく、下の `Update(float)` の外での `Stop` を除いて、どれにも入らないこともありません。`LogItem` は、どの並びに入る場合も、すぐに書き換わります。
- フレーム N で Simulate した操作は、フレーム N + 1 の入力更新が処理するので、フレーム N + 1 の `Update(float)` が作る並びに入ります。そのフレームの `OnPreUpdate`、`Update(int)`、`OnPostUpdate`、`Update(float)` のあとで読めます。
- `Update(float)` の中で足されたものは、同じ呼び出しの中ですでに返した並びからも見えます。並びは有効なままで、例外も投げません。`Count` はそのときの数です。`foreach` などで回している最中でも、始まったあとに足された要素まで回ります。`Update(float)` の中で並びを回している最中に、`Stop` などで要素が足されると、その列挙で同じ要素をもう一度読むことがあります。
- 1 つの item に、前の `Update(float)` から今までに複数のことが起きたときは、複数の要素になります。
- 要素は `Order` の順です。番号のない要素は、ほかの要素のあとに、item を Simulate した順に並びます。次のものの `Completed` です。
  - `Dropped` になった item。そのイベントを捨てた入力更新のあとの `Update(float)` の並びに入る。
  - `Stop` が終わらせた item。`Stop` を呼んだ `Update(float)` の並びに入る。
  - 終わりでどの control も変わらない item。何も押さない hover や、control がすでに持っている値の `Set` など。離すものがないので、手順が終わったあとの最初の入力更新で終わる。

  これらの `Order` は `null` で、item の `CompletedOrder` も `null` です。どの item の終わりも、下の例外を除いて、1 回だけ並びに入ります。
- **`Update(float)` の外での `Stop`。** `Stop` が終わらせた操作の終わりは、どの並びにも入りません。auto tap が有効でない間、`Update(float)` は何もしないので、それを受け取る並びが作られません。また、`Start` は前の実行が取っておいたものを捨てるので、次の実行の並びに混ざることもありません。スロットで始めた操作では、`Stop` の中で `OnTapCompleted` が呼ばれます。それ以外では、`Simulate` が返した `LogItem` を持っておいてください。item はすぐに終わり、`Completed` と `EndReason` で分かります。または、`Update(float)` の中で `Stop` を呼んでください。
- 押しを処理する更新より前に `Stop` が終わらせた item は、`Order` が `null` のままです。操作は途中で切られたので、そのあとで処理される押しは、その item のものとされません。
- `OnTapCompleted(index, logItem)` はスロット用です。スロットを順に見る前に、`Update(float)` から、終わりが処理された順（`CompletedOrder`）に呼ばれます。`Stop` が終わらせた操作では、`Stop` の中で呼ばれます。スロットの外の操作は、`GetLatestLogs()` で読みます。
- 番号（`Order`、`DragStartedOrder`、`CompletedOrder`）は、仮想入力の `InputChangeReader` の番号です。互いに比べられます。ドラッグの終わり（`endDrag`、`drop`）は、`Dragged` が `true` の item の `CompletedOrder` です。

### `InputChangeReader`

`InputChangeReader` は、番号の出どころで、public です。有効なすべてのデバイス（本物も仮想も）の入力状態の変化を、Input System が処理した順に、イベント 1 つずつ報告します。フィルターを渡さない限りそうです。述語で絞れます。

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
reader.UpdateEnded += () => { /* Dynamic の入力更新の終わり */ };
```

報告するのは、`TouchBegan`、`TouchMoved`、`TouchEnded`、`TouchCanceled`、`ButtonDown`、`ButtonUp`、`ValueChanged`（前の値と新しい値つき）、`Scrolled`（イベントの量）、`TextEntered`、`CompositionChanged`、`DeviceReset` です。どれも `Order`、`Frame`、`UpdateCount`、`EventId`、`Time`、`Device`、`IsVirtual` を持ちます。

- タッチスクリーンは、タッチを通して読みます。primary touch は通しません。
- 軸でできた control は、ベクトルの値を持つ 1 つの変化です。スティックは、`stick/x` と `stick/y` の 2 つではなく、`stick`（または `leftStick` など）の `ValueChanged` 1 つです。マウスの `scroll` は `Scrolled` で、ほかの delta の control は報告されません。
- synthetic な control は報告されません。ペンの `inRange` は synthetic なので、ペンが in range に入ることや out of range に出ることは変化になりません。位置、先端、ほかのボタンは変化です。
- キューにあるイベントから来ない変化は、たとえばデバイスを無効にしたときのリセットのようなものですが、control の変化の代わりに、入力更新ごとに 1 つの `DeviceReset` になります。
- Input System が捨てたイベント（無効なデバイス、古い time stamp など）と、更新の頭での delta のゼロ化は、報告されません。Input System が 1 つにまとめたイベントは、1 回報告されます。
- 生のイベントは記録しません。それには `InputEventTrace` を使います。
- reader は、変化に自分で 0 から番号をつけます。2 つの reader の番号は比べられません。`LogItem` の番号は、仮想入力の reader である `IVirtualInput.Reader` のもので、これは仮想デバイスだけを報告します。

### デバイスを直接使う

`VirtualInput.Acquire()` は、auto tap 自身が使う `VirtualInputSession`（`IVirtualInput`）を返します。押しと離しを 1 つの入力更新に入れたいとき、記録した入力を再生したいとき、仮想デバイスの変化だけを読みたいときに使います。

```csharp
using var input = VirtualInput.Acquire();
var finger = input.NewFinger();             // 10 本の指がすべて使われていれば null
finger.Press(position);                     // 呼び出しごとに 1 つのイベントをキューに入れる
finger.Release();                           // 押しとその離しは、1 つの入力更新に入ってよい
var mouse = input.GetPointer<Mouse>();      // または input.GetPointer("Mouse")
mouse.MoveTo(position);
mouse.Press();                              // 左ボタン
// 押しを処理した入力更新のあと:
var target = mouse.GetPrediction().PointerPress;   // pointer-down を受け取るオブジェクト
var keyboard = input.GetDevice<Keyboard>(); // または input.GetDevice("Keyboard")
keyboard.Set("escape", 1f);
keyboard.Set("escape", 0f);
keyboard.Queue(ControlValue.Of("leftCtrl", 1f), ControlValue.Of("r", 1f));   // 1 つのイベント: 両方を同時に
```

`GetPointer<TDevice>(int number = 0)` と `GetDevice<TDevice>(int number = 0)` は、デバイスを型で指定します。`Simulate` は、これらや、layout を取るメソッドと同じ仮想デバイスを使います。

デバイスは、キューに入れたものをすべて含む状態の写しを持つので、イベント同士が打ち消し合うことはありません。`VirtualPointer.Position` は、最後にキューに入れた位置です。`VirtualPointer.GetPrediction(button)` はボタンの予測で、そのボタンのイベントを処理した入力更新の終わりに、`IVirtualInput.InputUpdated` が起きる前に更新されます。`Prediction` は主なボタンのものです。予測があるのは、主なボタンと、マウスの右ボタンと中ボタンだけで、ほかのボタンでは空です。`VirtualFinger` にも `Prediction` があり、`FingerState`、`PressedFrame`、`ReleasedFrame` もあります。指は 1 回の押しにだけ使います。

`VirtualInput.IsVirtual(device)` は、仮想デバイスと本物のデバイスを見分けます。`AutoTapBase` には、スロットの数と `IVirtualInput` を取るコンストラクタ（`AutoTapBase(int slotCount, IVirtualInput input)`）もあり、session を共有できます。その場合、session は呼び出し側が dispose します。

## 動いている間に起きること

auto tap か、ほかの session が生きている間、仮想入力は Input System を次のように変えます。

- **Editor では、設定が置き換わります。** 入力がゲームに届くかを決める Input System の設定を、`backgroundBehavior` を `IgnoreFocus` に、`editorInputBehaviorInPlayMode` を `AllDeviceInputAlwaysGoesToGameView` にした写しに置き換え、最後の session を dispose するときに戻します。こうしないと、Game ビューにフォーカスがない間（裏で動く Editor を含む）、Input System はデバイスをリセットするか、入力を捨てます。**動いている間は、Editor のほかのウィンドウで打ったキーもゲームに届きます。** これは Editor だけです。player はプロジェクトの設定のままです。
- **pointer の behavior は、すべてのプラットフォームで置き換わります。** `InputSystemUIInputModule` の `pointerBehavior` を `AllPointersAsIs` にし、あとで元に戻します。module の既定の behavior（`SingleMouseOrPenButMultiTouchAndTrack`）では、指とマウスを同時に使うと互いに邪魔をします。フレームで最初に変わった pointer が、残るものを決めます。指が押されている間にマウスが変わると、指の pointer は取り除かれ（その下の UI は `pointerExit` を受け取る）、押しはマウスの pointer の押しとして続き（ドラッグと、離しの `pointerUp` は、マウスの pointer id を持つ）、指の pointer id は `pointerUp` を受け取りません。
- **`.current` が変わります。** 仮想デバイスは、Input System の本物のデバイスです。`Touchscreen.current`、`Keyboard.current`、`Mouse.current`、`Pointer.current`、`Gamepad.all`、`InputSystem.onDeviceChange`、`PlayerInput` が選んだ control scheme は、仮想デバイスが足されるか入力を受け取ったとたんに変わることがあり、取り除かれると元に戻ります。デバイスは最初に使われたときに足されるので、キーを押すだけの実行はタッチスクリーンを足しません。
- **動いている間は、Editor のマウスとキーボードを使わないでください。** 本物のデバイスが入力を受け取ると、それがまた `.current` になり、`.current` を読むコードは、仮想入力ではなくあなたの手を読みます。実行が終わるまで、Editor のマウスとキーボードには触らないでください。
- **後片付け。** 最後の session を手放す（`Stop`、`Dispose`）と、押されている指は持ち上げられ、変わるものがある仮想デバイスはすべて既定値に戻されて、キーとボタンは離されます。デバイスが取り除かれるのは、UI がそれを処理したあと、数回の入力更新のあとです。何も押されておらず、待っているイベントもなければ、すぐに取り除かれます。それまでは `VirtualInputSession.Reader` と `InputUpdated` が動き続けます。Play Mode を抜けると、すぐに取り除かれます。仮想デバイスの名前は `AutoTap-<layout>-<number>` です。前の実行が残したものは、アプリケーションが起動するとき、終了するとき、生きている session がない状態で session を取得するときに取り除かれます。
- **描画の前に更新するデバイス**（一部の XR デバイス）は、イベントをキューに入れたフレームの before-render の更新で処理し、`InputChangeReader` はそこから報告します。auto tap は、そのデバイスの操作が処理されたのは、次の Dynamic の入力更新だと見なします。

## 対象外のもの

- 出力（振動）と、デバイスに送るコマンド。入力ではありません。
- デバイスの接続と切断。仮想入力が足したり取り除いたりするのは、自分のデバイスだけです。
- 古い Input Manager と IMGUI のイベント。Input System を通りません。特に `TMP_InputField` は IMGUI のイベントからキーを読むので、仮想のキーとテキストは届きません。
- タッチスクリーンの `tapCount`、`isTap`、primary touch。タッチスクリーンが計算し、キューに入れた値を上書きします。
- 2 つ目の仮想タッチスクリーン。1 つのタッチスクリーンには 10 のタッチがあり、それが指の数です。
- "Process Events In Dynamic Update" 以外の update mode。
- 生のイベントの記録と再生。それには `InputEventTrace` を使います。

## 2.1.1 から 2.1.2 への移行

2.1.2 は、`Start` が `GetLatestLogs()` の並びを空にするようにします。2.1.1 では、前の実行の最後の `Update(float)` の並びが、新しい実行の最初の `Update(float)` まで残っていました。シグネチャは変わっていないので、コードはそのままコンパイルできます。

| | 2.1.1 | 2.1.2 |
|---|---|---|
| `Start` のあと、最初の `Update(float)` までの `GetLatestLogs()` | 前の実行の最後の `Update(float)` の並び | 空 |
| `Start` より前に受け取った並び | 有効なまま | 無効。使うと `InvalidOperationException` を投げる |

自分のコードで確かめること:

- `Stop` と `Start` をまたいで並びを持ち続けているなら、`Stop` の前に写しを取ってください（たとえば `ToArray()` で）。
- `Start` のあと、最初の `Update(float)` より前に `GetLatestLogs()` を読んで、前の実行の並びが返ることを当てにしていたなら、もう返りません。空の並びが返ります。

## 2.1.0 から 2.1.1 への移行

2.1.1 は、`GetLatestLogs()` が返す並びを作り直す時点を変えます。2.1.0 では、Dynamic の入力更新が始まるたびに作り直していました。今は `Update(float)` の頭で作り直し、ほかのときには作り直しません。シグネチャは変わっていないので、コードはそのままコンパイルできます。

| | 2.1.0 | 2.1.1 |
|---|---|---|
| 並びを作り直す時点 | Dynamic の入力更新が始まるたび。auto tap が作られてから `Dispose` されるまで | 動いた `Update(float)` の頭 |
| 並びの中身 | その入力更新で起きたことと、2 つの入力更新の間に起きたこと | 前の `Update(float)` から今までに起きたこと。入力更新が処理したこと、2 回の呼び出しの間に起きたこと、その呼び出しの中で起きたこと |
| `Update(float)` の中で `Stop` が終わらせた操作、または、`Update(float)` の中で呼んだ `ResetDevice` や `DisableDevice` が終わらせた操作の `Completed` | 次の入力更新の並びに入る | その `Update(float)` の並びに入る |
| `Update(float)` の外で `Stop` が終わらせた操作の `Completed` | 次の入力更新の並びに入る | どの並びにも入らない。`Start` が捨てる |
| `Update(float)` を呼ばないフレーム | そのフレームの入力更新が並びを作り、それより前のフレームで起きたことは消えた | 何も失われない。次の `Update(float)` の並びに入る |
| auto tap が有効でない間（`Start` の前、`Stop` のあと） | 入力更新のたびに作り直されるので、並びは空になった | 並びは変わらない |

自分のコードで確かめること:

- 同じフレームの `Update(float)` のあとで `GetLatestLogs()` を読んでいるなら、サンプルの `DebugManager` と同じなので、変える必要はありません。
- フレームの `Update(float)` より前に動く場所で読んでいるなら、`Update(float)` のあと、または `OnPreUpdate`、`Update(int)`、`OnPostUpdate` の中に移してください。`Update(float)` より前では、前の呼び出しの並びが返ります。
- `Update(float)` の外で止めた操作の終わりが並びに出ることを当てにしていたなら、もう出ません。スロットの操作では、`Stop` の中で `OnTapCompleted` が呼ばれます。それ以外では、`Simulate` が返した `LogItem` を持っておき、`Completed` と `EndReason` を見てください。または、シナリオが呼ぶように、`Update(float)` の中で `Stop` を呼んでください。
- `Stop` のあとに並びが空になることを当てにしていたなら、もう空になりません。最後の `Update(float)` の並びが残ります。

## 2.0.0 から 2.1.0 への移行

2.1.0 には、2.0.0 からの破壊的変更が含まれます。semantic versioning では 3.0.0 にあたります。2.1.0 にしたのは、2.0.0 の設計の不備を直すもので、2.0.0 が出たばかりだったからです。

**操作を始めるメソッドは `Simulate` です。**

| 2.0.0 | 2.1.0 |
|---|---|
| `Fire(slot, position, duration)` | `Simulate(slot, Gesture.PressAt(position).Hold(duration))` |
| `Fire(slot, from, to, duration)` | `Simulate(slot, Gesture.PressAt(from).MoveTo(to, duration))` |
| `Fire(slot, gesture)` | `Simulate(slot, gesture)` |
| `FirePointer(layout, gesture, number)` | `Simulate(layout, gesture, number)`、または `Simulate<TDevice>(gesture, number)` |
| `FireDevice(layout, sequence, number)` | `Simulate(layout, sequence, number)`、または `Simulate<TDevice>(sequence, number)` |
| `FireMouse(gesture)` | `Simulate<Mouse>(gesture)` |
| `FirePen(gesture)` | `Simulate<Pen>(gesture)` |
| `FireScroll(position, delta)` | `Simulate<Mouse>(Gesture.HoverAt(position).Scroll(delta))` |
| `FireGamepad(sequence)` | `Simulate<Gamepad>(sequence)` |
| `FireKey(key, duration, modifiers)` | `Simulate(key, duration, modifiers)` |

**スロットの数はコンストラクタに渡します。`Setup` と `SetupInternal` は `SetupMarkers` に代わります。**

| 2.0.0 | 2.1.0 |
|---|---|
| `SetupInternal(canvas, sprite, 5)` を呼ぶ `public override void Setup(Canvas, Sprite)` | `base(5)` を呼ぶコンストラクタ |
| `autoTap.Setup(canvas, sprite)` | `autoTap.SetupMarkers(canvas, sprite)`。virtual ではなく、マーカーを出すだけ |

```csharp
// 2.0.0
public class RandomTap : AutoTapBase
{
	public override void Setup(Canvas canvasForMarker, Sprite markerSprite)
	{
		SetupInternal(canvasForMarker, markerSprite, 5);
	}
}

// 2.1.0
public class RandomTap : AutoTapBase
{
	public RandomTap() : base(5)
	{
	}
}
```

スロットはコンストラクタで用意されるので、`SetupMarkers` を呼ばなくても `Start`、`Update`、`Simulate` は動きます。コンストラクタは、1 から 10 の外の数に `ArgumentOutOfRangeException` を投げます。`ScenarioBasedAutoTap` も、スロットの数をコンストラクタで受け取ります。

**消えたもの**

- 指のタップとドラッグ、`Fire(slot, position, duration)` と `Fire(slot, from, to, duration)`。`Gesture` を書いてください。`Gesture.Tap` のようなショートカットはありません。
- `FirePointer`、`FireDevice`、`FireMouse`、`FirePen`、`FireScroll`、`FireGamepad`。ショートカットが残るのは `FireKey` だけで、`Simulate(Key, ...)` です。
- `AutoTapBase.Setup` と `AutoTapBase.SetupInternal`。
- `LogItem.DragTo` と `LogItem.Duration`。`DragTo` は動きの行き先で、`Duration` は `Fire` に渡した秒数を持つ `float` でした（gesture や sequence では `NaN`）。代わりに `LogItem.EndScreenPoint` と `LogItem.Duration` がありますが、意味は別です（**追加されたもの**を参照）。
- `LogItemKind.Tap`、`LogItemKind.Drag`、`LogItemKind.Gesture`。種類は `Finger`、`Pointer`、`Device` です。

**変わったもの**

- `Simulate<Touchscreen>(...)` と `Simulate("Touchscreen", ...)` は、実行時に `ArgumentException` を投げます。指は `Simulate(slot, gesture)` で押します。
- `Scenario.TryTap` は残ります。`RectTransform` から押す点を求め、押しが `target` に届くかを判定します。押すときは `Simulate(index, Gesture.PressAt(point).Hold(duration))` を呼びます。
- `GetLatestLogs()` は `IEnumerable<LogEvent>` ではなく、`IReadOnlyList<LogEvent>` を実装する readonly struct の `AutoTapBase.LogEventSequence` を返します。最新の Dynamic の入力更新で起きたことの並びである点は変わらず、`foreach`、LINQ、`IEnumerable<LogEvent>` 型の変数も、そのままコンパイルできます。ただし、`foreach`、`Count`、添字では割り当てが起きなくなり、LINQ や `IEnumerable<LogEvent>` を通すと割り当てが起きます。並びが有効なのは、次の Dynamic の入力更新が始まるまでです。そのあとで並びを回す、`Count` を読む、要素を読む、のどれをしても `InvalidOperationException` を投げます。2.0.0 では、返るものは有効なままの写しでした。要素をあとで使うなら、今、要素の写しを取ってください（たとえば `ToArray()` で）。並びは、auto tap が作られてから `Dispose` されるまで、Dynamic の入力更新のたびに作り直され、何も起きなければ空になります。2 つの入力更新の間に起きたこと（デバイスのリセットなど）は、次の更新の並びに入ります。2.1.1 では、並びを作り直す時点が変わります（[2.1.0 から 2.1.1 への移行](#210-から-211-への移行)を参照）。
- `LogItem.ToString` は、操作の種類（`Finger`、`Pointer`、`Device`）から始まります。2.0.0 で `DragTo` を出していた `ScreenPoint` の後ろに、`EndScreenPoint` を出します。`Duration` も出します。`null` のものは出しません。

**追加されたもの**

- `LogItem.EndScreenPoint`（`Vector2?`）は、指や pointer を最後に置く位置です。`LogItem.Duration`（`float?`）は、操作にかかる時間を、`Update(float)` に渡した秒数で表します。どちらも、操作が終わるまでは手順の値、終わったあとは実際の値で（どちらかは `Completed` で分かります）、手順から決まらないうちは `null` です。消えたものとは別のものです。`DragTo` は、動かない操作では `null` でしたが、`EndScreenPoint` は動かない操作にも値があります。前の `Duration` は、`Fire` に渡した数でした。
- `Gesture.Steps` と `ControlSequence.Steps` は、手順を読み取り専用のリストにしたものです（`Gesture.Step`、`ControlSequence.Step`）。`LogItem.CurrentStep` は、いま実行しているステップの添字です。

代わりの書き方:

```csharp
// 指でのタップとドラッグ。
Simulate(slot, Gesture.PressAt(position).Hold(0.1f));
Simulate(slot, Gesture.PressAt(from).MoveTo(to, 0.5f));

// マウスでのスクロール。
Simulate<Mouse>(Gesture.HoverAt(position).Scroll(new Vector2(0f, -3f)));

// マウス: 右クリック。
Simulate<Mouse>(Gesture.PressAt(position, "rightButton").Hold(0.1f));

// ゲームパッド: South を押す。
Simulate<Gamepad>(new ControlSequence().Press(GamepadButton.South));

// キー。
Simulate(Key.Escape, 0.1f);
```

## v1 からの移行

v2 には、v1 の自前の raycast がありません。Input System が必要で、UI に届けるのは input module に任せます。そのため、v1 で正しかったことのいくつかは、もう正しくありません。

**必要なもの**
- Unity 6000.4、Input System 1.19.0、シーンにある `InputSystemUIInputModule`、update mode "Process Events In Dynamic Update"。`USE_LEGACY_INPUT` はなくなりました。

**スロット**
- abstract だった `Setup(Canvas, Sprite)` と `SetupInternal` はなくなりました。スロットの数は、基底クラスのコンストラクタに渡し（`base(5)`）、マーカーを出すには `SetupMarkers(canvas, sprite)` を呼びます。
- スロットの数は最大 10 です（以前は 19）。それより大きい数や小さい数は、コンストラクタが `ArgumentOutOfRangeException` を投げます。

**タイミング**
- `Simulate` は届けません。届けるのはイベントシステムで、次のフレームです。
- `OnTapCompleted` が呼ばれるタイミングが変わりました。スロットを順に見る前に `Update(float)` から、終わりが処理された順に呼ばれます。
- `Stop` のあと、離し（pointer-up、click）が届くのは次の入力更新です。
- `Dispose` は、auto tap が動いていれば `Stop` を呼びます。

**操作を始める**
- `Fire(index, position, duration)` と `Fire(index, from, to, duration)` は、`Simulate(index, Gesture.PressAt(position).Hold(duration))` と `Simulate(index, Gesture.PressAt(from).MoveTo(to, duration))` に代わりました。
- `Simulate` は、pointer-down を受け取った `GameObject` ではなく `LogItem` を返します。そのオブジェクトは item の `Target` です。何も始まらなかったときは `null` を返します。動いているスロットでは `null` を返します。
- 何にも当たらない点でも押します。v1 では、そこでは何も始まりませんでした。
- `Scenario.TryTap` は、押しが `target` の上のときだけ `true` を返します。判定は、`Simulate` を呼んだときに作る予測からするようになりました。まだ何も届いていないからです。

**フック**
- `ToBeIgnored(Transform)` はなくなりました。後継の `ShouldNotPressOn(RaycastResult)` は、最前面の当たりを渡して呼ばれ、「最前面の当たりがこれなら押さない」という意味です。`ToBeIgnored` と違い、その裏にあるオブジェクトへは進みません。
- `CreateLog(index, target, startPosition, endPosition, duration, out logItem)` は `NewLogItem(int index)` になりました。item を作るだけで、フィールドはあとから埋まります。item は操作ごとに新しく作られます。v1 はスロットごとに 1 つの item を使い回していました。

**ログ**
- `GetLatestLogs()` は、前の `Update(float)` から今までに起きたことを、処理した順（`Started`、`DragStarted`、`Completed`）に、`LogEvent` の並びとして返します。v1 は、動いていたスロットの `LogItem` を返していました。何も起きなければ、並びは空です。並びは `LogEventSequence` で、`foreach` で割り当てが起きない struct です。次の `Update(float)` まで有効です（[`GetLatestLogs()`](#getlatestlogs)を参照）。捨てられた item、`Stop` が終わらせた item、終わりで何も変わらなかった item の終わりは、番号なしで最後に来ます。`GetLatestCompletedLogs()` はなくなりました。
- `LogItem` のフィールドは変わりました。v1 の `DragTo` は今の `EndScreenPoint` に、`Duration` は `float?` になりました。どちらも、操作が終わるまでは手順の値、終わったあとは実際の値で、手順から決まらないうちは `null` です。`EndScreenPoint` は指や pointer を最後に置く位置なので、動かない操作にもあります。`DragTo` にはありませんでした。手順は `LogItem.Gesture` と `LogItem.Sequence` から読め、`LogItem.CurrentStep` は、いま実行しているステップです。`ClickExecuted` と `ClickTarget` は予測です。
- `LogItem.ToString` は v1 のものに近くなりました。v1 が `Tap` か `Drag` で始まっていたところは、操作の種類（`Finger`、`Pointer`、`Device`）で始まります。`null` のものは出しません。
- pointer id は Input System のものです。指では `(deviceId << 24) + touchId`、マウスとペンではデバイスの id です。

**実行時**
- auto tap の動いている間、`.current`、`.all`、`onDeviceChange`、`PlayerInput` の control scheme が変わることがあります。
- Editor では Input System の設定が置き換わり、すべてのプラットフォームで module の pointer behavior が置き換わります。[動いている間に起きること](#動いている間に起きること)を参照してください。
