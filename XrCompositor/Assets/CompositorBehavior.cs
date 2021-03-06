﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using SharpXpra;
using TMPro;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Loaders;
using UnityEditor;
using Object = UnityEngine.Object;

public class UnityCompositor : BaseCompositor<UnityCompositor, UnityBaseWindow> {
	public readonly CompositorBehavior Behavior;
	public UnityWindow Focused;

	public event Action<UnityWindow> WindowShown;
	public event Action<UnityWindow> WindowLost;
	public event Action<UnityWindow> WindowResized;

	public UnityCompositor(CompositorBehavior behavior) => Behavior = behavior;
	
	protected override UnityBaseWindow ConstructWindow(int wid) => new UnityWindow(this, wid);
	protected override UnityBaseWindow ConstructPopup(int wid, UnityBaseWindow parent, int x, int y) =>
		new UnityPopup(this, wid, parent, x, y);

	internal void TriggerWindowShown(UnityWindow window) => WindowShown?.Invoke(window);
	internal void TriggerWindowLost(UnityWindow window) => WindowLost?.Invoke(window);
	internal void TriggerWindowResized(UnityWindow window) => WindowResized?.Invoke(window);

	public override void Log(string message) => Behavior.Log(message);
	public override void Error(string message) => Behavior.Error(message);
}

public abstract class UnityBaseWindow : BaseWindow<UnityCompositor, UnityBaseWindow> {
	public readonly GameObject Window, SurfaceCorner, Surface;
	public readonly Collider SurfaceCollider;
	public readonly Renderer SurfaceRenderer;
	public bool Initialized;
	public bool NewBufferSize;
	public Texture2D SurfaceTexture;
	public byte[] Pixels;

	public UnityBaseWindow(UnityCompositor compositor, int id, bool isPopup) : base(compositor, id, isPopup) {
		Window = MakeWindow();
		SurfaceCorner = Window.transform.Find("SurfaceCorner").gameObject;
		Surface = SurfaceCorner.transform.Find("Surface").gameObject;
		SurfaceCollider = Surface.GetComponent<Collider>();
		SurfaceRenderer = Surface.GetComponent<Renderer>();
		Window.SetActive(false);
	}

	protected abstract GameObject MakeWindow();
	
	protected override void UpdateBufferSize() => NewBufferSize = true;
	protected virtual void UpdateWindowSize(Vector3 ss) { }
	public override void Closing() {
		if(Compositor.Behavior.CurrentHoverWindow == this)
			Compositor.Behavior.CurrentHoverWindow = null;
		Object.Destroy(Window);
		if(!IsPopup)
			Compositor.TriggerWindowLost((UnityWindow) this);
	}

	public override void Damage(int x, int y, int w, int h, PixelEncoding encoding, byte[] data) {
		if(!NewBufferSize && Pixels == null) return;
		
		if(NewBufferSize) {
			if(!IsPopup)
				Compositor.TriggerWindowResized((UnityWindow) this);
			var ss = new Vector3(BufferSize.W / 100f, BufferSize.H / 100f, 1);
			SurfaceCorner.transform.localPosition = new Vector3(-ss.x / 2, ss.y / 2, 0);
			Surface.transform.localPosition = new Vector3(ss.x / 2, -ss.y / 2, 0);
			Surface.transform.localScale = ss;
			UpdateWindowSize(ss);
			NewBufferSize = false;
			SurfaceTexture = new Texture2D(w, h, TextureFormat.RGB24, false);
			SurfaceRenderer.material.SetTexture("_MainTex", SurfaceTexture);
			if(x != 0 || y != 0 || w != BufferSize.W || h != BufferSize.H)
				throw new Exception("Damage buffer must be a full refresh after buffer size update");
			SurfaceTexture.LoadRawTextureData(data);
			SurfaceTexture.Apply();
			Pixels = new byte[w * h * 3];
			Array.Copy(data, 0, Pixels, 0, w * h * 3);
		} else {
			if(x == 0 && y == 0 && w == BufferSize.W && h == BufferSize.H)
				Array.Copy(data, 0, Pixels, 0, w * h * 3);
			else if(w == BufferSize.W)
				Array.Copy(data, 0, Pixels, y * BufferSize.W * 3, w * h * 3);
			else
				for(var i = 0; i < h; ++i)
					Array.Copy(data, i * w * 3, Pixels, (y + i) * BufferSize.W * 3 + x * 3, w * 3);
			SurfaceTexture.LoadRawTextureData(Pixels);
			SurfaceTexture.Apply();
		}
		
		if(!Initialized) {
			Window.SetActive(true);
			Initialized = true;
			if(!IsPopup)
				Compositor.TriggerWindowShown((UnityWindow) this);
		}
	}
	
	public virtual void EnsureProperPosition() { }

	public (int, int) UnprojectPoint(Vector3 point) {
		var localPoint = Surface.transform.InverseTransformPoint(point);
		return ((int) Mathf.Round((localPoint.x + 0.5f) * BufferSize.W),
			(int) Mathf.Round((1 - (localPoint.y + 0.5f)) * BufferSize.H));
	}
}

public class UnityWindow : UnityBaseWindow {
	readonly GameObject Topbar;
	public readonly Collider TopbarCollider;
	readonly RectTransform TitleTransform;
	readonly TextMeshPro TextMesh;
	
	public UnityWindow(UnityCompositor compositor, int id) : base(compositor, id, false) {
		Topbar = Window.transform.Find("Topbar").gameObject;
		TopbarCollider = Topbar.GetComponent<Collider>();
		var titleText = Window.transform.Find("TitleText").gameObject;
		TextMesh = titleText.GetComponent<TextMeshPro>();
		TitleTransform = titleText.GetComponent<RectTransform>();
	}

	protected override void UpdateTitle() => TextMesh.SetText(Title);

	protected override GameObject MakeWindow() => Object.Instantiate(Compositor.Behavior.WindowPrefab);

	protected override void UpdateWindowSize(Vector3 ss) {
		Topbar.transform.localScale = new Vector3(ss.x, 0.25f, 0.1f);
		Topbar.transform.localPosition = new Vector3(0, ss.y / 2 + 0.25f / 2, -0.05f);
		TitleTransform.sizeDelta = new Vector2(ss.x, 0.25f);
		TitleTransform.localPosition = new Vector3(0, ss.y / 2 + 0.25f / 2, -0.101f);
	}
}

public class UnityPopup : UnityBaseWindow {
	readonly UnityBaseWindow Parent;
	readonly int RelativeX, RelativeY;

	public UnityPopup(UnityCompositor compositor, int id, UnityBaseWindow parent, int x, int y) : base(compositor, id,
		true) {
		Parent = parent;
		RelativeX = x;
		RelativeY = y;
		Window.transform.parent = Parent.SurfaceCorner.transform;
		Window.transform.localRotation = Quaternion.identity;
	}

	public override void EnsureProperPosition() =>
		Position = (Parent.Position.X + RelativeX, Parent.Position.Y + RelativeY);

	protected override void UpdateWindowSize(Vector3 ss) => 
		Window.transform.localPosition = new Vector3(ss.x / 2 + RelativeX / 100f, -ss.y / 2 - RelativeY / 100f, -0.1f);

	protected override GameObject MakeWindow() => Object.Instantiate(Compositor.Behavior.PopupPrefab);
}

public class CompositorBehavior : MonoBehaviour {
	public GameObject WindowPrefab, PopupPrefab;
	Client<UnityCompositor, UnityBaseWindow> Client;
	UnityCompositor Compositor;
	Vector3 MousePosition;
	public UnityBaseWindow CurrentHoverWindow;
	(int X, int Y) WindowMousePosition;
	bool[] ButtonState = new bool[7];
	bool[] KeyState = new bool[512];

	XrcServer XrcServer;

	readonly Dictionary<string,
			List<(object, Func<object, ScriptExecutionContext, CallbackArguments, DynValue>, DynValue)>>
		LoadedScripts =
			new Dictionary<string, List<(object, Func<object, ScriptExecutionContext, CallbackArguments, DynValue>,
				DynValue)>>();
	IScriptLoader Loader;

	FieldInfo AddField, RemoveField, ObjectField;

	public event Action<bool, string> LogMessage;
	public void Log(string message) => LogMessage?.Invoke(false, message);
	public void Error(string message) => LogMessage?.Invoke(true, message);
	
	public readonly Queue<Action> JobQueue = new Queue<Action>();

	readonly List<string> RegisteredTypes = new List<string>();
	void RecursiveRegister(Type type) {
		bool CheckGeneric(Type gtype) => gtype == typeof(List<>) || gtype == typeof(Dictionary<,>);
		if(RegisteredTypes.Contains(type.FullName))
			return;
		RegisteredTypes.Add(type.FullName);
		if((type.IsConstructedGenericType && !CheckGeneric(type.GetGenericTypeDefinition())) ||
		   (!type.IsConstructedGenericType && type.Assembly != typeof(string).Assembly))
			UserData.RegisterType(type);

		if(type.IsConstructedGenericType)
			foreach(var gtype in type.GenericTypeArguments)
				RecursiveRegister(gtype);
		foreach(var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
			RecursiveRegister(field.FieldType);
		foreach(var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
			RecursiveRegister(property.PropertyType);
	}

	void RecursiveRegister<T>() => RecursiveRegister(typeof(T));

	void Start() {
		LogMessage += (isError, message) => {
			if(isError)
				Debug.LogError(message);
			else
				Debug.Log(message);
		};
		Compositor = new UnityCompositor(this);
		XrcServer = new XrcServer(this);
		Client = new Client<UnityCompositor, UnityBaseWindow>("10.0.0.50", 10000, Compositor);
		
		var eft = typeof(UserData).Assembly.GetType("MoonSharp.Interpreter.Interop.StandardDescriptors.EventFacade");
		AddField = eft.GetField("m_AddCallback", BindingFlags.Instance | BindingFlags.NonPublic);
		RemoveField = eft.GetField("m_RemoveCallback", BindingFlags.Instance | BindingFlags.NonPublic);
		ObjectField = eft.GetField("m_Object", BindingFlags.Instance | BindingFlags.NonPublic);
		
		RecursiveRegister<UnityCompositor>();
		RecursiveRegister<UnityBaseWindow>();
		RecursiveRegister<UnityWindow>();
		RecursiveRegister<UnityPopup>();
		RecursiveRegister<Mathf>();
		Log("Registered types");
		Loader = new FileSystemScriptLoader();
		try {
			foreach(var fn in Directory.GetFiles(Application.persistentDataPath, "*.lua"))
				LoadScript(fn);
		} catch(Exception) {
		}
	}

	void SetupScriptGlobals(Script script) {
		script.Globals["compositor"] = Compositor;
		script.Globals["Mathf"] = typeof(Mathf);
		script.Globals["Vector2"] = typeof(Vector2);
		script.Globals["Vector3"] = typeof(Vector3);
		script.Globals["Vector4"] = typeof(Vector4);
		script.Globals["Quaternion"] = typeof(Quaternion);
		script.Globals["print"] = (Action<object>) (obj => Log(obj.ToPrettyString()));
	}

	public void LoadScript(string fn) {
		if(LoadedScripts.ContainsKey(fn))
			UnloadScript(fn);
		try {
			var script = new Script();
			script.Options.ScriptLoader = Loader;
			var cbs = LoadedScripts[fn] =
				new List<(object, Func<object, ScriptExecutionContext, CallbackArguments, DynValue>, DynValue)>();
			script.Globals["bind"] = (Action<object, DynValue>) ((ef, func) => {
				var addCb = (Func<object, ScriptExecutionContext, CallbackArguments, DynValue>) AddField.GetValue(ef);
				var removeCb = (Func<object, ScriptExecutionContext, CallbackArguments, DynValue>) RemoveField.GetValue(ef);
				var obj = ObjectField.GetValue(ef);
				addCb(obj, null, new CallbackArguments(new List<DynValue> { func }, false));
				cbs.Add((obj, removeCb, func));
			});
			SetupScriptGlobals(script);
			script.DoFile(fn);
		} catch(Exception e) {
			Error($"MoonSharp script {fn.ToPrettyString()} threw exception: {e}");
		}
	}

	public void RunScriptCode(string code) {
		try {
			var script = new Script();
			script.Options.ScriptLoader = Loader;
			script.Globals["bind"] = (Action<object, DynValue>) ((ef, func) => {});
			SetupScriptGlobals(script);
			script.DoString(code);
		} catch(Exception e) {
			Error($"MoonSharp script `code` threw exception: {e}");
		}
	}

	public void UnloadScript(string fn) {
		foreach(var (obj, cb, func) in LoadedScripts[fn])
			cb(obj, null, new CallbackArguments(new List<DynValue> { func }, false));
		LoadedScripts.Remove(fn);
	}

	void Update() {
		while(JobQueue.Count != 0)
			JobQueue.Dequeue()();
		
		var buttonChanged = new bool[ButtonState.Length];
		for(var i = 1; i < ButtonState.Length; ++i) {
			var state = Input.GetMouseButton(i - 1);
			buttonChanged[i] = ButtonState[i] != state;
			ButtonState[i] = state;
		}
		if(MousePosition != Input.mousePosition) {
			MousePosition = Input.mousePosition;
			CurrentHoverWindow = null;
			var ray = Camera.main.ScreenPointToRay(MousePosition);
			if(Physics.Raycast(ray, out var hit)) {
				foreach(var window in Compositor.Windows)
					if(hit.collider == window.SurfaceCollider) {
						CurrentHoverWindow = window;
						var (x, y) = window.UnprojectPoint(hit.point);
						WindowMousePosition = (x, y);
						window.EnsureProperPosition();
						window.MouseMove(x, y, ButtonState);
						break;
					}
			}
		}

		var keyChanged = new bool[KeyState.Length];
		for(var i = 0; i < KeyState.Length; ++i) {
			var state = Input.GetKey((KeyCode) i);
			keyChanged[i] = KeyState[i] != state;
			KeyState[i] = state;
		}

		if(CurrentHoverWindow != null) {
			for(var i = 0; i < ButtonState.Length; ++i)
				if(buttonChanged[i])
					CurrentHoverWindow.MouseButton(WindowMousePosition.X, WindowMousePosition.Y, i, ButtonState[i]);
			for(var i = 0; i < KeyState.Length; ++i)
				if(keyChanged[i]) {
					var translated = Translate((KeyCode) i);
					if(translated == Keycode.Unknown) {
						Log($"Unknown key {(KeyCode) i} ({i})");
						continue;
					}
					if(translated == Keycode.IGNORE)
						continue;
					if(KeyState[i])
						CurrentHoverWindow.KeyDown(translated);
					else
						CurrentHoverWindow.KeyUp(translated);
				}
		} else {
			if(Input.GetKey(KeyCode.LeftArrow))
				Camera.main.transform.Rotate(Vector3.up, -2);
			if(Input.GetKey(KeyCode.RightArrow))
				Camera.main.transform.Rotate(Vector3.up, 2);
		}

		Client.Update();
	}

	void OnDestroy() {
		Client.Disconnect();
		XrcServer.Stop();
	}

	Keycode Translate(KeyCode key) {
		switch(key) {
			case KeyCode.A: return Keycode.a;
			case KeyCode.B: return Keycode.b;
			case KeyCode.C: return Keycode.c;
			case KeyCode.D: return Keycode.d;
			case KeyCode.E: return Keycode.e;
			case KeyCode.F: return Keycode.f;
			case KeyCode.G: return Keycode.g;
			case KeyCode.H: return Keycode.h;
			case KeyCode.I: return Keycode.i;
			case KeyCode.J: return Keycode.j;
			case KeyCode.K: return Keycode.k;
			case KeyCode.L: return Keycode.l;
			case KeyCode.M: return Keycode.m;
			case KeyCode.N: return Keycode.n;
			case KeyCode.O: return Keycode.o;
			case KeyCode.P: return Keycode.p;
			case KeyCode.Q: return Keycode.q;
			case KeyCode.R: return Keycode.r;
			case KeyCode.S: return Keycode.s;
			case KeyCode.T: return Keycode.t;
			case KeyCode.U: return Keycode.u;
			case KeyCode.V: return Keycode.v;
			case KeyCode.W: return Keycode.w;
			case KeyCode.X: return Keycode.x;
			case KeyCode.Y: return Keycode.y;
			case KeyCode.Z: return Keycode.z;
			case KeyCode.Return: return Keycode.Return;
			case KeyCode.Backspace: return Keycode.BackSpace;
			case KeyCode.Space: return Keycode.space;
			case KeyCode.Minus: return Keycode.minus;
			case KeyCode.Alpha0: return Keycode.NUM0;
			case KeyCode.Alpha1: return Keycode.NUM1;
			case KeyCode.Alpha2: return Keycode.NUM2;
			case KeyCode.Alpha3: return Keycode.NUM3;
			case KeyCode.Alpha4: return Keycode.NUM4;
			case KeyCode.Alpha5: return Keycode.NUM5;
			case KeyCode.Alpha6: return Keycode.NUM6;
			case KeyCode.Alpha7: return Keycode.NUM7;
			case KeyCode.Alpha8: return Keycode.NUM8;
			case KeyCode.Alpha9: return Keycode.NUM9;
			case KeyCode.BackQuote: return Keycode.asciitilde;
			case KeyCode.Backslash: return Keycode.backslash;
			case KeyCode.LeftBracket: return Keycode.bracketleft;
			case KeyCode.RightBracket: return Keycode.bracketright;
			case KeyCode.LeftParen: return Keycode.parenleft;
			case KeyCode.RightParen: return Keycode.parenright;
			case KeyCode.Comma: return Keycode.comma;
			case KeyCode.Period: return Keycode.period;
			case KeyCode.Slash: return Keycode.slash;
			case KeyCode.Equals: return Keycode.equal;
			case KeyCode.Semicolon: return Keycode.semicolon;
			case KeyCode.Quote: return Keycode.apostrophe;
			case KeyCode.LeftControl: return Keycode.Control_L;
			case KeyCode.RightControl: return Keycode.Control_L;
			case KeyCode.LeftAlt: return Keycode.Alt_L;
			case KeyCode.RightAlt: return Keycode.Alt_L;
			case KeyCode.LeftShift: return Keycode.Shift_L;
			case KeyCode.RightShift: return Keycode.Shift_L;
			case KeyCode.UpArrow: return Keycode.Up;
			case KeyCode.DownArrow: return Keycode.Down;
			case KeyCode.LeftArrow: return Keycode.Left;
			case KeyCode.RightArrow: return Keycode.Right;
			case KeyCode.Tab: return Keycode.Tab;
			case KeyCode.Escape: return Keycode.Escape;
			case KeyCode.Home: return Keycode.Home;
			case KeyCode.End: return Keycode.End;
			case KeyCode.PageUp: return Keycode.Prior;
			case KeyCode.PageDown: return Keycode.Next;
			case KeyCode.Mouse0:
			case KeyCode.Mouse1:
			case KeyCode.Mouse2:
			case KeyCode.Mouse3:
			case KeyCode.Mouse4:
			case KeyCode.Mouse5:
			case KeyCode.Mouse6:
				return Keycode.IGNORE;
			default:
				return Keycode.Unknown;
		}
	}
}