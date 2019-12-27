using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SharpXpra;
using TMPro;
using UnityEditor;
using Object = UnityEngine.Object;

public class UnityCompositor : BaseCompositor<UnityCompositor, UnityWindow> {
	public readonly CompositorBehavior Behavior;
	public UnityWindow Focused;

	public UnityCompositor(CompositorBehavior behavior) => Behavior = behavior;
	
	protected override UnityWindow ConstructWindow(int wid) => new UnityWindow(this, wid);

	public override void Log(string message) => Debug.Log(message);
	public override void Error(string message) => Debug.LogError(message);
}

public class UnityWindow : BaseWindow<UnityCompositor, UnityWindow> {
	readonly GameObject Window, Topbar, Surface, TitleText;
	public readonly Collider TopbarCollider, SurfaceCollider;
	readonly RectTransform TitleTransform;
	readonly TextMeshPro TextMesh;
	readonly Renderer SurfaceRenderer;
	bool Initialized;
	bool NewBufferSize;
	Texture2D SurfaceTexture;
	byte[] Pixels;

	public UnityWindow(UnityCompositor compositor, int id) : base(compositor, id) {
		Window = Object.Instantiate(Compositor.Behavior.WindowPrefab);
		Topbar = Window.transform.Find("Topbar").gameObject;
		TopbarCollider = Topbar.GetComponent<Collider>();
		Surface = Window.transform.Find("Surface").gameObject;
		SurfaceCollider = Surface.GetComponent<Collider>();
		TitleText = Window.transform.Find("TitleText").gameObject;
		TextMesh = TitleText.GetComponent<TextMeshPro>();
		TitleTransform = TitleText.GetComponent<RectTransform>();
		SurfaceRenderer = Surface.GetComponent<Renderer>();
		Window.SetActive(false);
	}

	protected override void UpdateTitle() => TextMesh.SetText(Title);
	protected override void UpdateBufferSize() => NewBufferSize = true;

	public override void Damage(int x, int y, int w, int h, PixelEncoding encoding, byte[] data) {
		if(!NewBufferSize && Pixels == null) return;
		
		if(NewBufferSize) {
			var ss = new Vector3(BufferSize.W / 100f, BufferSize.H / 100f, 1);
			Surface.transform.localScale = ss;
			Topbar.transform.localScale = new Vector3(ss.x, 0.25f, 0.1f);
			Topbar.transform.localPosition = new Vector3(0, ss.y / 2 + 0.25f / 2, -0.05f);
			TitleTransform.sizeDelta = new Vector2(ss.x, 0.25f);
			TitleTransform.position = new Vector3(0, ss.y / 2 + 0.25f / 2, -0.101f);
			NewBufferSize = false;
			SurfaceTexture = new Texture2D(w, h, TextureFormat.RGB24, false);
			SurfaceRenderer.material.SetTexture("_MainTex", SurfaceTexture);
			if(x != 0 || y != 0 || w != BufferSize.W || h != BufferSize.H)
				throw new Exception("Damage buffer must be a full refresh after buffer size update");
			SurfaceTexture.LoadRawTextureData(data);
			SurfaceTexture.Apply();
			Pixels = new byte[w * h * 3];
			data.CopyTo(Pixels, 0);
		} else {
			if(x == 0 && y == 0 && w == BufferSize.W && h == BufferSize.H)
				data.CopyTo(Pixels, 0);
			else if(w == BufferSize.W)
				data.CopyTo(Pixels, y * BufferSize.W * 3);
			else
				for(var i = 0; i < h; ++i)
					Array.Copy(data, i * w * 3, Pixels, (y + i) * BufferSize.W * 3 + x * 3, w * 3);
			SurfaceTexture.LoadRawTextureData(Pixels);
			SurfaceTexture.Apply();
		}
		
		if(!Initialized) {
			Window.SetActive(true);
			Initialized = true;
		}
	}

	public (int, int) UnprojectPoint(Vector3 point) {
		var localPoint = Surface.transform.InverseTransformPoint(point);
		return ((int) Mathf.Round((localPoint.x + 0.5f) * BufferSize.W),
			(int) Mathf.Round((1 - (localPoint.y + 0.5f)) * BufferSize.H));
	}
}

public class CompositorBehavior : MonoBehaviour {
	public GameObject WindowPrefab;
	Client<UnityCompositor, UnityWindow> Client;
	UnityCompositor Compositor;
	Vector3 MousePosition;

	void Start() {
		Compositor = new UnityCompositor(this);
		Client = new Client<UnityCompositor, UnityWindow>("10.0.0.200", 10000, Compositor);
	}

	void Update() {
		if(MousePosition != Input.mousePosition) {
			MousePosition = Input.mousePosition;
			var ray = Camera.main.ScreenPointToRay(MousePosition);
			if(Physics.Raycast(ray, out var hit)) {
				foreach(var window in Compositor.Windows)
					if(hit.collider == window.SurfaceCollider) {
						var (x, y) = window.UnprojectPoint(hit.point);
						window.MouseMove(x, y);
						break;
					}
			}
		}

		Client.Update();
	}

	void OnDestroy() => Client.Disconnect();
}