using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // Set via scene setup
    public GameObject    squarePrefab;
    public GameObject    pieceDiagonalPrefab;
    public GameObject    pieceOrthogonalPrefab;
    public RectTransform boardContainer;
    public UIManager     uiManager;

    private const int   SIZE        = 5;
    private const float SQUARE_SIZE = 90f;

    private BoardSquare[,]             _squares   = new BoardSquare[SIZE, SIZE];
    private GamePiece[,]               _pieces    = new GamePiece[SIZE, SIZE];
    private readonly List<(int r, int c)> _validMoves = new List<(int, int)>();

    private GamePhase  _phase         = GamePhase.SelectPiece;
    private PlayerSide _currentPlayer = PlayerSide.PlayerA;
    private GamePiece  _selected;

    private void Awake() => Instance = this;

    private void Start()
    {
        if (squarePrefab != null)
            InitBoard();

        StartCoroutine(ApplyJapaneseFont());
    }

    private IEnumerator ApplyJapaneseFont()
    {
        yield return null; // wait one frame so GoalMark Text objects are created
        var font = Resources.Load<Font>("Fonts/NotoSansJP-Regular");
        if (font == null) yield break;
        foreach (var t in FindObjectsByType<Text>(FindObjectsInactive.Include))
            t.font = font;
    }

    // ── Board construction ──────────────────────────────────────────────

    private void InitBoard()
    {
        BuildSquares();
        PlaceInitialPieces();
        uiManager.UpdateStatus(_currentPlayer, _phase);
    }

    private void BuildSquares()
    {
        float half = SQUARE_SIZE * SIZE / 2f;

        for (int row = 0; row < SIZE; row++)
        {
            for (int col = 0; col < SIZE; col++)
            {
                var go = Object.Instantiate(squarePrefab, boardContainer);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin       = new Vector2(0.5f, 0.5f);
                rt.anchorMax       = new Vector2(0.5f, 0.5f);
                rt.pivot           = new Vector2(0.5f, 0.5f);
                rt.sizeDelta       = new Vector2(SQUARE_SIZE, SQUARE_SIZE);
                rt.anchoredPosition = new Vector2(
                    col * SQUARE_SIZE - half + SQUARE_SIZE / 2f,
                   -(row * SQUARE_SIZE - half + SQUARE_SIZE / 2f));

                var sq      = go.GetComponent<BoardSquare>();
                var outline = go.transform.Find("Outline")?.GetComponent<Image>();
                var block   = go.transform.Find("Block")?.GetComponent<Image>();
                sq.Init(row, col, outline, block);
                _squares[row, col] = sq;
            }
        }

        // Mark goal squares: Player A targets (4,0), Player B targets (0,4)
        _squares[SIZE - 1, 0].SetAsGoal(PlayerSide.PlayerA);
        _squares[0, SIZE - 1].SetAsGoal(PlayerSide.PlayerB);
    }

    private void PlaceInitialPieces()
    {
        // Player A (top)
        Spawn(PieceType.Diagonal,   PlayerSide.PlayerA, 0, 0);
        Spawn(PieceType.Diagonal,   PlayerSide.PlayerA, 0, 2);
        Spawn(PieceType.Diagonal,   PlayerSide.PlayerA, 0, 4);
        Spawn(PieceType.Orthogonal, PlayerSide.PlayerA, 1, 1);
        Spawn(PieceType.Orthogonal, PlayerSide.PlayerA, 1, 3);

        // Player B (bottom)
        Spawn(PieceType.Orthogonal, PlayerSide.PlayerB, 3, 1);
        Spawn(PieceType.Orthogonal, PlayerSide.PlayerB, 3, 3);
        Spawn(PieceType.Diagonal,   PlayerSide.PlayerB, 4, 0);
        Spawn(PieceType.Diagonal,   PlayerSide.PlayerB, 4, 2);
        Spawn(PieceType.Diagonal,   PlayerSide.PlayerB, 4, 4);
    }

    private void Spawn(PieceType type, PlayerSide owner, int row, int col)
    {
        var prefab = type == PieceType.Diagonal ? pieceDiagonalPrefab : pieceOrthogonalPrefab;
        var go     = Object.Instantiate(prefab, _squares[row, col].transform);

        var rt         = go.GetComponent<RectTransform>();
        rt.anchorMin       = new Vector2(0.5f, 0.5f);
        rt.anchorMax       = new Vector2(0.5f, 0.5f);
        rt.pivot           = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        var piece = go.GetComponent<GamePiece>();
        var bg    = go.transform.Find("Background")?.GetComponent<Image>();
        var fgH   = go.transform.Find("Foreground-Horizontal")?.GetComponent<Image>();
        var fgV   = go.transform.Find("Foreground-Vertical")?.GetComponent<Image>();
        piece.Init(type, owner, row, col, bg, fgH, fgV);
        _pieces[row, col] = piece;
    }

    // ── Input handlers ──────────────────────────────────────────────────

    public void OnSquareClicked(int row, int col)
    {
        switch (_phase)
        {
            case GamePhase.SelectPiece:
                TrySelectPiece(row, col);
                break;

            case GamePhase.SelectDestination:
                if (IsValidMove(row, col))
                    ExecuteMove(row, col);
                else if (_pieces[row, col] != null && _pieces[row, col].Owner == _currentPlayer)
                {
                    ClearSelection();
                    TrySelectPiece(row, col);
                }
                else
                    ClearSelection();
                break;
        }
    }

    private void TrySelectPiece(int row, int col)
    {
        var p = _pieces[row, col];
        if (p == null || p.Owner != _currentPlayer) return;

        _selected = p;
        _validMoves.Clear();
        ComputeValidMoves(p);

        p.SetSelected(true);
        _squares[row, col].SetHighlight(HighlightType.Selected);
        foreach (var (r, c) in _validMoves)
            _squares[r, c].SetHighlight(HighlightType.ValidMove);

        _phase = GamePhase.SelectDestination;
        uiManager.UpdateStatus(_currentPlayer, _phase);
    }

    private void ClearSelection()
    {
        if (_selected != null)
        {
            _selected.SetSelected(false);
            _squares[_selected.Row, _selected.Col].SetHighlight(HighlightType.None);
        }
        foreach (var (r, c) in _validMoves)
            _squares[r, c].SetHighlight(HighlightType.None);

        _selected = null;
        _validMoves.Clear();
        _phase = GamePhase.SelectPiece;
        uiManager.UpdateStatus(_currentPlayer, _phase);
    }

    private void ComputeValidMoves(GamePiece piece)
    {
        (int dr, int dc)[] dirs = piece.PieceType == PieceType.Orthogonal
            ? new[] { (-1, 0), (1, 0), (0, -1), (0, 1) }
            : new[] { (-1, -1), (-1, 1), (1, -1), (1, 1) };

        foreach (var (dr, dc) in dirs)
        {
            int nr = piece.Row + dr, nc = piece.Col + dc;
            if (nr < 0 || nr >= SIZE || nc < 0 || nc >= SIZE) continue;
            var target = _pieces[nr, nc];
            if (target != null && target.Owner == piece.Owner) continue;
            _validMoves.Add((nr, nc));
        }
    }

    private bool IsValidMove(int row, int col)
    {
        foreach (var m in _validMoves)
            if (m.r == row && m.c == col) return true;
        return false;
    }

    private void ExecuteMove(int targetRow, int targetCol)
    {
        // Clear highlights
        if (_selected != null)
        {
            _selected.SetSelected(false);
            _squares[_selected.Row, _selected.Col].SetHighlight(HighlightType.None);
        }
        foreach (var (r, c) in _validMoves)
            _squares[r, c].SetHighlight(HighlightType.None);

        // Capture
        var captured = _pieces[targetRow, targetCol];
        if (captured != null)
        {
            Object.Destroy(captured.gameObject);
            _pieces[targetRow, targetCol] = null;
        }

        // Move in data
        var piece = _selected;
        _pieces[piece.Row, piece.Col] = null;
        _pieces[targetRow, targetCol] = piece;
        piece.Row = targetRow;
        piece.Col = targetCol;

        // Move visually
        piece.transform.SetParent(_squares[targetRow, targetCol].transform, false);
        var rt = piece.GetComponent<RectTransform>();
        rt.anchorMin       = new Vector2(0.5f, 0.5f);
        rt.anchorMax       = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        _selected = null;
        _validMoves.Clear();

        // Win check
        if (CheckWin(_currentPlayer))
        {
            _phase = GamePhase.GameOver;
            uiManager.ShowWin(_currentPlayer);
            return;
        }

        // Slide phase
        _phase = GamePhase.SelectSlide;
        uiManager.ShowSlidePanel(true);
        uiManager.UpdateStatus(_currentPlayer, _phase);
    }

    // ── Slide ───────────────────────────────────────────────────────────

    public void OnSlideClicked(int row, bool slideRight)
    {
        if (_phase != GamePhase.SelectSlide) return;

        var rowData = new GamePiece[SIZE];
        for (int c = 0; c < SIZE; c++) rowData[c] = _pieces[row, c];

        if (slideRight)
        {
            var last = rowData[SIZE - 1];
            for (int c = SIZE - 1; c > 0; c--) rowData[c] = rowData[c - 1];
            rowData[0] = last;
        }
        else
        {
            var first = rowData[0];
            for (int c = 0; c < SIZE - 1; c++) rowData[c] = rowData[c + 1];
            rowData[SIZE - 1] = first;
        }

        for (int c = 0; c < SIZE; c++)
        {
            _pieces[row, c] = rowData[c];
            if (rowData[c] == null) continue;
            rowData[c].Col = c;
            rowData[c].transform.SetParent(_squares[row, c].transform, false);
            var rt = rowData[c].GetComponent<RectTransform>();
            rt.anchorMin       = new Vector2(0.5f, 0.5f);
            rt.anchorMax       = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
        }

        uiManager.ShowSlidePanel(false);

        // Win check after slide (before switching players)
        if (CheckWin(_currentPlayer))
        {
            _phase = GamePhase.GameOver;
            uiManager.ShowWin(_currentPlayer);
            return;
        }

        _currentPlayer = _currentPlayer == PlayerSide.PlayerA
            ? PlayerSide.PlayerB : PlayerSide.PlayerA;
        _phase = GamePhase.SelectPiece;
        uiManager.UpdateStatus(_currentPlayer, _phase);
    }

    // ── Win check ───────────────────────────────────────────────────────

    private bool CheckWin(PlayerSide player)
    {
        // Win condition 1: reach specific goal corner
        //   Player A: (SIZE-1, 0) = bottom-left
        //   Player B: (0, SIZE-1) = top-right
        int goalRow = player == PlayerSide.PlayerA ? SIZE - 1 : 0;
        int goalCol = player == PlayerSide.PlayerA ? 0 : SIZE - 1;

        var opponent = player == PlayerSide.PlayerA ? PlayerSide.PlayerB : PlayerSide.PlayerA;
        bool opHasPieces = false;

        for (int r = 0; r < SIZE; r++)
        {
            for (int c = 0; c < SIZE; c++)
            {
                var p = _pieces[r, c];
                if (p == null) continue;
                if (p.Owner == opponent) opHasPieces = true;
                // Win condition 2: also win if all opponent pieces captured
                if (p.Owner == player && r == goalRow && c == goalCol) return true;
            }
        }
        return !opHasPieces;
    }

    // ── Restart ─────────────────────────────────────────────────────────

    public void RestartGame()
    {
        for (int r = 0; r < SIZE; r++)
        {
            for (int c = 0; c < SIZE; c++)
            {
                if (_pieces[r, c] != null)
                {
                    Object.Destroy(_pieces[r, c].gameObject);
                    _pieces[r, c] = null;
                }
                _squares[r, c].SetHighlight(HighlightType.None);
            }
        }

        _phase         = GamePhase.SelectPiece;
        _currentPlayer = PlayerSide.PlayerA;
        _selected      = null;
        _validMoves.Clear();

        uiManager.ShowSlidePanel(false);
        uiManager.ShowWinOverlay(false);
        PlaceInitialPieces();
        uiManager.UpdateStatus(_currentPlayer, _phase);
    }
}
