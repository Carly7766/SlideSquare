using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class GamePiece : MonoBehaviour, IPointerClickHandler
{
    public PieceType  PieceType { get; private set; }
    public PlayerSide Owner     { get; private set; }
    public int Row { get; set; }
    public int Col { get; set; }

    private Image _background;
    private Image _fgH;
    private Image _fgV;

    private static readonly Color PlayerABg  = new Color(0.15f, 0.35f, 0.85f);
    private static readonly Color PlayerBBg  = new Color(0.85f, 0.15f, 0.15f);
    private static readonly Color SelectedBg = new Color(1.00f, 0.85f, 0.00f);

    private static readonly Color PlayerAFg  = new Color(0.70f, 0.85f, 1.00f);
    private static readonly Color PlayerBFg  = new Color(1.00f, 0.75f, 0.75f);

    private bool _selected;

    public void Init(PieceType type, PlayerSide owner, int row, int col,
                     Image bg, Image fgH, Image fgV)
    {
        PieceType  = type;
        Owner      = owner;
        Row        = row;
        Col        = col;
        _background = bg;
        _fgH        = fgH;
        _fgV        = fgV;

        // Piece images should not block clicks — the root handles them
        if (_background) _background.raycastTarget = false;
        if (_fgH)        _fgH.raycastTarget        = false;
        if (_fgV)        _fgV.raycastTarget        = false;

        Refresh();
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        Refresh();
    }

    private void Refresh()
    {
        Color fg = Owner == PlayerSide.PlayerA ? PlayerAFg : PlayerBFg;

        if (_background)
            _background.color = _selected ? SelectedBg
                              : Owner == PlayerSide.PlayerA ? PlayerABg : PlayerBBg;
        if (_fgH) _fgH.color = fg;
        if (_fgV) _fgV.color = fg;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        GameManager.Instance.OnSquareClicked(Row, Col);
    }
}
