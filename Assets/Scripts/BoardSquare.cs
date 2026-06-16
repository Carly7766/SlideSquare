using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class BoardSquare : MonoBehaviour, IPointerClickHandler
{
    public int Row { get; private set; }
    public int Col { get; private set; }

    private Image _outline;
    private Image _block;

    private static readonly Color DarkOutline  = new Color(0.20f, 0.16f, 0.12f);
    private static readonly Color SelOutline   = new Color(1.00f, 0.85f, 0.00f);
    private static readonly Color MoveOutline  = new Color(0.10f, 0.80f, 0.10f);
    private static readonly Color GoalAOutline = new Color(0.15f, 0.40f, 1.00f);
    private static readonly Color GoalBOutline = new Color(1.00f, 0.15f, 0.15f);

    private static readonly Color LightCell = new Color(0.92f, 0.87f, 0.76f);
    private static readonly Color DarkCell  = new Color(0.70f, 0.64f, 0.54f);
    private static readonly Color SelCell   = new Color(1.00f, 0.95f, 0.55f);
    private static readonly Color MoveCell  = new Color(0.55f, 0.95f, 0.55f);
    private static readonly Color GoalACell = new Color(0.60f, 0.70f, 1.00f);
    private static readonly Color GoalBCell = new Color(1.00f, 0.60f, 0.60f);

    private bool _isGoalA;
    private bool _isGoalB;

    public void Init(int row, int col, Image outline, Image block)
    {
        Row = row;
        Col = col;
        _outline = outline;
        _block   = block;
        SetHighlight(HighlightType.None);
    }

    // Call once after Init to permanently mark this square as a goal.
    public void SetAsGoal(PlayerSide owner)
    {
        _isGoalA = owner == PlayerSide.PlayerA;
        _isGoalB = owner == PlayerSide.PlayerB;
        SetHighlight(HighlightType.None);

        // Add ★ marker at top-right corner of the square
        var starGo = new GameObject("GoalMark", typeof(RectTransform));
        starGo.transform.SetParent(transform, false);
        var rt = starGo.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(34, 34);
        rt.anchoredPosition = new Vector2(-3f, -3f);

        var txt = starGo.AddComponent<Text>();
        txt.text      = "★";
        txt.fontSize  = 24;
        txt.color     = _isGoalA ? new Color(0.2f, 0.5f, 1.0f) : new Color(1.0f, 0.2f, 0.2f);
        txt.alignment = TextAnchor.UpperRight;
        txt.raycastTarget = false;
    }

    public void SetHighlight(HighlightType type)
    {
        switch (type)
        {
            case HighlightType.None:
                if (_isGoalA)      { _outline.color = GoalAOutline; _block.color = GoalACell; }
                else if (_isGoalB) { _outline.color = GoalBOutline; _block.color = GoalBCell; }
                else               { _outline.color = DarkOutline;  _block.color = (Row + Col) % 2 == 0 ? LightCell : DarkCell; }
                break;
            case HighlightType.Selected:
                _outline.color = SelOutline;
                _block.color   = SelCell;
                break;
            case HighlightType.ValidMove:
                _outline.color = MoveOutline;
                _block.color   = MoveCell;
                break;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        GameManager.Instance.OnSquareClicked(Row, Col);
    }
}
