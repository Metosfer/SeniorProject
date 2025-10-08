using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generic transform saver for any movable persistent object (position/rotation/scale + active state).
/// Attach to objects like furniture, MusicBox, etc. Assign a unique saveId if multiple exist.
/// </summary>
public class SaveableTransform : MonoBehaviour, ISaveable
{
    [Tooltip("Benzersiz kalıcı ID (boşsa otomatik sahne yolu kullanılır)")] public string saveId;
    [Tooltip("Sadece position ve rotation'u kaydet (scale hariç)")] public bool ignoreScale;
    [Tooltip("Aktif state (SetActive) kaydedilsin mi?")] public bool saveActiveState = true;
    [Header("Otomatik Snapshot")]
    [Tooltip("Transform değiştiğinde otomatik incremental snapshot alınsın")] public bool autoCapture = true;
    [Tooltip("Minimum snapshot aralığı (s)")] public float captureInterval = 0.5f;
    [Tooltip("Pozisyon/Rotasyon için değişim eşiği (metre / derece)")] public float positionThreshold = 0.01f;
    [Tooltip("Scale değişim eşiği")] public float scaleThreshold = 0.005f;

    private Vector3 _lastPos, _lastScale;
    private Vector3 _lastEuler;
    private float _lastCaptureTime;

    private void Awake() { EnsureId(); SnapshotBaseline(); }
    private void OnEnable() { SnapshotBaseline(); CaptureIncremental(); }
    private void OnValidate() { EnsureId(); }
    private void OnDisable() { CaptureIncremental(); }
    private void OnDestroy() { CaptureIncremental(); }

    private void Update()
    {
        if (!autoCapture) return;
        if (Time.unscaledTime - _lastCaptureTime < captureInterval) return;
        if (HasMeaningfulChange())
        {
            SnapshotBaseline();
            CaptureIncremental();
        }
    }

    private bool HasMeaningfulChange()
    {
        var p = transform.position; var e = transform.eulerAngles; var s = transform.localScale;
        if ((p - _lastPos).sqrMagnitude > positionThreshold * positionThreshold) return true;
        // Euler farklarını normalize ederek kıyasla
        if (AngularDiff(e, _lastEuler) > positionThreshold * 180f) return true; // approx degree threshold reuse
        if (!ignoreScale && (s - _lastScale).sqrMagnitude > scaleThreshold * scaleThreshold) return true;
        return false;
    }

    private static float AngularDiff(Vector3 a, Vector3 b)
    {
        return Mathf.Abs(Mathf.DeltaAngle(a.x, b.x)) + Mathf.Abs(Mathf.DeltaAngle(a.y, b.y)) + Mathf.Abs(Mathf.DeltaAngle(a.z, b.z));
    }

    private void SnapshotBaseline()
    {
        _lastPos = transform.position;
        _lastEuler = transform.eulerAngles;
        _lastScale = transform.localScale;
        _lastCaptureTime = Time.unscaledTime;
    }

    private void CaptureIncremental()
    {
        var gsm = GameSaveManager.Instance;
        if (gsm == null || gsm.IsRestoringScene) return;
        gsm.CaptureSceneObjectsIncremental();
    }

    private void EnsureId()
    {
        if (!string.IsNullOrEmpty(saveId)) return;
        var scene = gameObject.scene;
        string path = GetHierarchyPath(transform);
        if (scene.IsValid()) saveId = string.IsNullOrEmpty(path) ? scene.name : scene.name + ":" + path;
        else saveId = path;
    }

    private static string GetHierarchyPath(Transform t)
    {
        if (t == null) return string.Empty;
        System.Text.StringBuilder sb = new System.Text.StringBuilder(t.name);
        while (t.parent != null)
        {
            t = t.parent; sb.Insert(0, '/'); sb.Insert(0, t.name);
        }
        return sb.ToString();
    }

    public Dictionary<string, object> GetSaveData()
    {
        var d = new Dictionary<string, object>();
        var p = transform.position; var r = transform.eulerAngles; var s = transform.localScale;
        d["px"] = p.x; d["py"] = p.y; d["pz"] = p.z;
        d["rx"] = r.x; d["ry"] = r.y; d["rz"] = r.z;
        if (!ignoreScale) { d["sx"] = s.x; d["sy"] = s.y; d["sz"] = s.z; }
        if (saveActiveState) d["active"] = gameObject.activeSelf;
        d["saveId"] = saveId; // debug / diagnostics
        return d;
    }

    public void LoadSaveData(Dictionary<string, object> data)
    {
        if (data == null) return;
        float px = GetF(data, "px", transform.position.x);
        float py = GetF(data, "py", transform.position.y);
        float pz = GetF(data, "pz", transform.position.z);
        float rx = GetF(data, "rx", transform.eulerAngles.x);
        float ry = GetF(data, "ry", transform.eulerAngles.y);
        float rz = GetF(data, "rz", transform.eulerAngles.z);
        Vector3 pos = new Vector3(px, py, pz);
        Vector3 eul = new Vector3(rx, ry, rz);
        transform.position = pos;
        transform.eulerAngles = eul;
        if (!ignoreScale)
        {
            float sx = GetF(data, "sx", transform.localScale.x);
            float sy = GetF(data, "sy", transform.localScale.y);
            float sz = GetF(data, "sz", transform.localScale.z);
            transform.localScale = new Vector3(sx, sy, sz);
        }
        if (saveActiveState && data.TryGetValue("active", out var actObj) && bool.TryParse(actObj.ToString(), out bool act))
        {
            if (gameObject.activeSelf != act) gameObject.SetActive(act);
        }
    }

    private static float GetF(Dictionary<string, object> d, string k, float def)
    {
        if (!d.TryGetValue(k, out var v) || v == null) return def;
        if (float.TryParse(v.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f)) return f;
        return def;
    }
}
