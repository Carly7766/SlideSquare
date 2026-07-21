using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class SlideButton : MonoBehaviour
{
    public int  Row;
    public bool SlideRight;

    private Button _button;
    private Image  _background;
    private Text   _label;

    // Unity's built-in ColorTint transition blends the disabled color into the
    // CanvasRenderer's tint rather than replacing Image.color, so the change is
    // barely visible against a dark background and the label text isn't affected
    // at all. Manage both explicitly instead so "cannot slide here" is obvious.
    private static readonly Color EnabledBg     = new Color(0.18f, 0.18f, 0.48f, 1f);
    private static readonly Color DisabledBg    = new Color(0.12f, 0.12f, 0.12f, 0.30f);
    private static readonly Color EnabledLabel  = Color.white;
    private static readonly Color DisabledLabel = new Color(1f, 1f, 1f, 0.22f);

    private void Awake()
    {
        // The slide panel starts inactive, and Start() on an inactive object is
        // deferred to the following frame. GameManager can call SetInteractable()
        // the same frame the panel is activated, so wiring must happen in Awake()
        // (which Unity runs synchronously on activation) rather than Start().
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (_button != null) return;

        _button             = GetComponent<Button>();
        _button.transition  = Selectable.Transition.None;
        _background         = GetComponent<Image>();
        _label              = GetComponentInChildren<Text>(true);

        _button.onClick.AddListener(
            () => GameManager.Instance.OnSlideClicked(Row, SlideRight));
    }

    public void SetInteractable(bool value)
    {
        EnsureInitialized();
        _button.interactable = value;
        if (_background) _background.color = value ? EnabledBg    : DisabledBg;
        if (_label)       _label.color      = value ? EnabledLabel : DisabledLabel;
    }
}
