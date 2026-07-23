using UnityEngine;
using UnityEngine.UI;

// UI button for the one-time "all-in slide" special action. See SlideButton for the
// sibling per-row button and Assets/Documents/AllInSlideSpec.md for the rule spec.
[RequireComponent(typeof(Button))]
public class AllInSlideButton : MonoBehaviour
{
    public bool SlideRight;

    private Button _button;
    private Image  _background;
    private Text   _label;

    private static readonly Color EnabledBg     = new Color(0.55f, 0.38f, 0.05f, 1f);
    private static readonly Color DisabledBg    = new Color(0.12f, 0.12f, 0.12f, 0.30f);
    private static readonly Color EnabledLabel  = Color.white;
    private static readonly Color DisabledLabel = new Color(1f, 1f, 1f, 0.22f);

    private void Awake() => EnsureInitialized();

    private void EnsureInitialized()
    {
        if (_button != null) return;

        _button             = GetComponent<Button>();
        _button.transition  = Selectable.Transition.None;
        _background         = GetComponent<Image>();
        _label              = GetComponentInChildren<Text>(true);

        _button.onClick.AddListener(
            () => GameManager.Instance.OnAllInSlideClicked(SlideRight));
    }

    public void SetInteractable(bool value)
    {
        EnsureInitialized();
        _button.interactable = value;
        if (_background) _background.color = value ? EnabledBg    : DisabledBg;
        if (_label)       _label.color      = value ? EnabledLabel : DisabledLabel;
    }
}
