using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Attach this to each arrow UI (Up/Down/Left/Right). Supports hover/click scale and clicking to hit the lane.
public class RhythmArrowButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    public RythmGameManager manager;
    [Tooltip("Lane index: 0=Up,1=Down,2=Left,3=Right")] public int lane;
    [Header("Visuals")]
    public RectTransform targetRect; // assign self Rect if null
    public float hoverScale = 1.1f;
    public float clickScale = 0.95f;
    public float tweenTime = 0.06f;

    private Vector3 _baseScale;
    private bool _hover;
    private bool _down;

    private void Awake()
    {
        if (targetRect == null) targetRect = GetComponent<RectTransform>();
        _baseScale = targetRect != null ? targetRect.localScale : Vector3.one;
    }

    private void Update()
    {
        if (targetRect == null) return;
        float target = _down ? clickScale : (_hover ? hoverScale : 1f);
        var desired = _baseScale * target;
        targetRect.localScale = Vector3.Lerp(targetRect.localScale, desired, 1f - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(0.0001f, tweenTime)));
    }

    public void OnPointerEnter(PointerEventData eventData) { _hover = true; }
    public void OnPointerExit(PointerEventData eventData) { _hover = false; }
    public void OnPointerDown(PointerEventData eventData) { _down = true; }
    public void OnPointerUp(PointerEventData eventData) { _down = false; }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (manager != null) manager.TryHitLane(lane);
    }
}
