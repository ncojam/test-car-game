using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CarController))]
[DisallowMultipleComponent]
public sealed class DrivingHud : MonoBehaviour
{
    private const int SegmentCount = 14;
    private static readonly Color Panel = new Color32(13, 20, 25, 233);
    private static readonly Color MainText = new Color32(239, 247, 249, 255);
    private static readonly Color Label = new Color32(166, 185, 191, 255);
    private static readonly Color Accent = new Color32(120, 232, 207, 255);
    private static readonly Color Redline = new Color32(255, 163, 96, 255);

    private readonly RectTransform[] segments = new RectTransform[SegmentCount];
    private CarController car;
    private Canvas canvas;
    private Font font;
    private Text speed;
    private Text gear;
    private Text rpm;
    private int shownSpeed = -1;
    private int shownGear = -1;
    private int shownRpm = -1;

    private void Awake()
    {
        car = GetComponent<CarController>();
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var root = new GameObject("Driving HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.transform.SetParent(transform, false);
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
        canvas.enabled = false;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform panel = Rect("Speedometer", (RectTransform)root.transform, 360f, 142f);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1f, 0f);
        panel.anchoredPosition = new Vector2(-32f, 32f);
        Paint(panel, Panel);

        speed = Caption("Speed", panel, 84, 33, 192, 60, 51, MainText, FontStyle.Bold, TextAnchor.MiddleRight);
        Caption("Speed unit", panel, 51, 33, 215, 43, 15, Label).text = "KM/H";
        Caption("Gear label", panel, 58, 37, 302, 66, 14, Label, alignment: TextAnchor.MiddleCenter).text = "GEAR";
        gear = Caption("Gear", panel, 60, 30, 305, 47, 41, Accent, FontStyle.Bold, TextAnchor.MiddleCenter);
        Caption("RPM label", panel, 27, 102, 79, 23, 13, Label).text = "RPM";
        rpm = Caption("Engine RPM", panel, 334, 101, 93, 25, 16, MainText, FontStyle.Bold, TextAnchor.MiddleRight);

        for (int i = 0; i < SegmentCount; i++)
        {
            RectTransform track = Rect("Tachometer segment", panel, 20f, 7f, 26f + 22f * i, 114f);
            Paint(track, new Color32(61, 77, 85, 255));
            RectTransform fill = Rect("Fill", track, 20f, 7f);
            fill.pivot = new Vector2(0f, 0.5f);
            Paint(fill, i >= SegmentCount - 2 ? Redline : Accent);
            segments[i] = fill;
        }
    }

    private void LateUpdate()
    {
        canvas.enabled = car.IsDriving;
        if (!canvas.enabled)
            return;

        int kph = Mathf.RoundToInt(car.SpeedKph);
        if (kph != shownSpeed)
        {
            speed.text = kph.ToString();
            shownSpeed = kph;
        }

        int gearNumber = car.GearNumber;
        if (gearNumber != shownGear)
        {
            gear.text = gearNumber.ToString();
            shownGear = gearNumber;
        }

        int rpmRounded = Mathf.RoundToInt(car.EngineRpm / 10f) * 10;
        if (rpmRounded != shownRpm)
        {
            rpm.text = rpmRounded.ToString();
            rpm.color = car.EngineRpm >= CarController.RedlineRpm * 0.95f ? Redline : MainText;
            shownRpm = rpmRounded;
        }

        float fillAmount = Mathf.Clamp01(car.EngineRpm / CarController.RedlineRpm) * SegmentCount;
        for (int i = 0; i < SegmentCount; i++)
            segments[i].localScale = new Vector3(Mathf.Clamp01(fillAmount - i), 1f, 1f);
    }

    private void OnDisable()
    {
        if (canvas != null)
            canvas.enabled = false;
    }

    private void OnDestroy()
    {
        if (canvas != null)
            Destroy(canvas.gameObject);
    }

    private static RectTransform Rect(string name, RectTransform parent, float width, float height, float x = 0f, float y = 0f)
    {
        var child = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)child.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static void Paint(RectTransform rect, Color color)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private Text Caption(string name, RectTransform parent, float x, float y, float width, float height,
        int size, Color color, FontStyle style = FontStyle.Normal, TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        RectTransform rect = Rect(name, parent, width, height, x, y);
        if (alignment == TextAnchor.MiddleRight)
            rect.pivot = new Vector2(1f, 1f);
        else if (alignment == TextAnchor.MiddleCenter)
            rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);

        Text text = rect.gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }
}
