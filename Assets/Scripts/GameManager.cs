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

    // 調整ルール用の状態
    private int  _lastSlideRow = -1;  // 直前のスライドの列（-1 = なし）。巻き戻し禁止の判定に使う
    private bool _lastSlideRight;     // 直前のスライドの方向
    private bool _isFirstTurn = true; // 先手の第1ターンはスライドを行わない
    private int  _movedFromRow = -1;  // 今の手番で動かした駒の元の行。移動連動スライドの判定に使う
    private int  _movedToRow   = -1;  // 今の手番で動かした駒の移動先の行
    private readonly Dictionary<string, int> _positionCounts = new Dictionary<string, int>(); // 千日手判定

    // 一斉スライド用の状態（対局中プレイヤーごとに1回。仕様は Assets/Documents/AllInSlideSpec.md 参照）
    private readonly bool[] _usedAllInSlide = new bool[2]; // 既に使用済みか（index = (int)PlayerSide）
    private readonly bool[] _skipNextSlide  = new bool[2]; // 反動で次のスライドフェーズをスキップするか

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
        _currentPlayer = Random.value < 0.5f ? PlayerSide.PlayerA : PlayerSide.PlayerB;
        BuildSquares();
        PlaceInitialPieces();
        BeginTurn();
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
        Spawn(PieceType.Orthogonal, PlayerSide.PlayerA, 0, 1);
        Spawn(PieceType.Diagonal,   PlayerSide.PlayerA, 0, 2);
        Spawn(PieceType.Orthogonal, PlayerSide.PlayerA, 0, 3);
        Spawn(PieceType.Diagonal,   PlayerSide.PlayerA, 0, 4);

        // Player B (bottom)
        Spawn(PieceType.Diagonal,   PlayerSide.PlayerB, 4, 0);
        Spawn(PieceType.Orthogonal, PlayerSide.PlayerB, 4, 1);
        Spawn(PieceType.Diagonal,   PlayerSide.PlayerB, 4, 2);
        Spawn(PieceType.Orthogonal, PlayerSide.PlayerB, 4, 3);
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

    private static (int dr, int dc)[] GetDirections(PieceType type) =>
        type == PieceType.Orthogonal
            ? new[] { (-1, 0), (1, 0), (0, -1), (0, 1) }
            : new[] { (-1, -1), (-1, 1), (1, -1), (1, 1) };

    private void ComputeValidMoves(GamePiece piece)
    {
        foreach (var (dr, dc) in GetDirections(piece.PieceType))
        {
            int nr = piece.Row + dr, nc = piece.Col + dc;
            if (nr < 0 || nr >= SIZE || nc < 0 || nc >= SIZE) continue;
            var target = _pieces[nr, nc];
            if (target != null && target.Owner == piece.Owner) continue;
            _validMoves.Add((nr, nc));
        }
    }

    private bool HasLegalMove(GamePiece piece)
    {
        foreach (var (dr, dc) in GetDirections(piece.PieceType))
        {
            int nr = piece.Row + dr, nc = piece.Col + dc;
            if (nr < 0 || nr >= SIZE || nc < 0 || nc >= SIZE) continue;
            var target = _pieces[nr, nc];
            if (target != null && target.Owner == piece.Owner) continue;
            return true;
        }
        return false;
    }

    private bool HasAnyLegalMove(PlayerSide player)
    {
        for (int r = 0; r < SIZE; r++)
            for (int c = 0; c < SIZE; c++)
            {
                var p = _pieces[r, c];
                if (p != null && p.Owner == player && HasLegalMove(p)) return true;
            }
        return false;
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
        int fromRow = piece.Row;
        _pieces[piece.Row, piece.Col] = null;
        _pieces[targetRow, targetCol] = piece;
        piece.Row = targetRow;
        piece.Col = targetCol;

        // 移動連動スライド用: 今動かした駒の元の行・移動先の行を記録
        _movedFromRow = fromRow;
        _movedToRow   = targetRow;

        // Move visually
        piece.transform.SetParent(_squares[targetRow, targetCol].transform, false);
        var rt = piece.GetComponent<RectTransform>();
        rt.anchorMin       = new Vector2(0.5f, 0.5f);
        rt.anchorMax       = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        _selected = null;
        _validMoves.Clear();

        // 移動直後の勝利判定: 移動による即時到達、または相手の全滅
        if (HasPieceOnOwnGoal(_currentPlayer) || !HasAnyPiece(Opponent(_currentPlayer)))
        {
            _phase = GamePhase.GameOver;
            uiManager.ShowWin(_currentPlayer);
            return;
        }

        // 先手の第1ターンはスライドを行わない。一斉スライドの反動が残っている場合も同様にスキップする
        if (_isFirstTurn || _skipNextSlide[(int)_currentPlayer] || !HasAnyLegalSlide())
        {
            _skipNextSlide[(int)_currentPlayer] = false; // 反動は1回消費したら解除
            _lastSlideRow = -1; // スライドしないため、相手に巻き戻し制限は付かない
            EndTurn();
            return;
        }

        // Slide phase
        _phase = GamePhase.SelectSlide;
        uiManager.ShowSlidePanel(true);
        UpdateSlideButtons();
        uiManager.UpdateStatus(_currentPlayer, _phase);
    }

    // ── Slide ───────────────────────────────────────────────────────────

    public void OnSlideClicked(int row, bool slideRight)
    {
        if (_phase != GamePhase.SelectSlide) return;
        if (!IsSlideLegal(row, slideRight)) return;

        SlideRow(row, slideRight);
        uiManager.ShowSlidePanel(false);

        // スライド直後には勝利判定を行わない（ゴールに乗った駒は定着待ち）
        _lastSlideRow   = row;
        _lastSlideRight = slideRight;
        EndTurn();
    }

    // 一斉スライド: 対局中プレイヤー1人につき1回、5行すべてを同じ方向へ同時にスライドする特殊アクション。
    // 移動連動・巻き戻し禁止・空列禁止のいずれの制限も受けない代わりに、使った次の自分の番はスライドを行えない
    // （反動）。詳細仕様は Assets/Documents/AllInSlideSpec.md を参照。
    public void OnAllInSlideClicked(bool slideRight)
    {
        if (_phase != GamePhase.SelectSlide) return;
        if (_usedAllInSlide[(int)_currentPlayer]) return;

        for (int row = 0; row < SIZE; row++)
            SlideRow(row, slideRight);

        uiManager.ShowSlidePanel(false);

        _usedAllInSlide[(int)_currentPlayer] = true;
        _skipNextSlide[(int)_currentPlayer]  = true;

        // 全列を一括操作する特殊行動のため、通常の巻き戻し禁止の記録対象にはしない
        _lastSlideRow = -1;

        // スライド直後には勝利判定を行わない（ゴールに乗った駒は定着待ち。通常のスライドと同じ扱い）
        EndTurn();
    }

    // 指定した行を1マス分スライドさせる（データ・見た目の更新のみ。合法性チェックや手番終了は呼び出し側の責務）
    private void SlideRow(int row, bool slideRight)
    {
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
    }

    // 移動連動: 今動かした駒の元の行/移動先の行以外は選べない。
    // 空の列はスライドできない。相手の直前のスライドの巻き戻し（同じ列を逆方向）もできない。
    private bool IsSlideLegal(int row, bool slideRight)
    {
        if (row != _movedFromRow && row != _movedToRow) return false;

        bool hasPiece = false;
        for (int c = 0; c < SIZE; c++)
            if (_pieces[row, c] != null) { hasPiece = true; break; }
        if (!hasPiece) return false;
        if (row == _lastSlideRow && slideRight != _lastSlideRight) return false;
        return true;
    }

    private bool HasAnyLegalSlide()
    {
        for (int r = 0; r < SIZE; r++)
            if (IsSlideLegal(r, true) || IsSlideLegal(r, false)) return true;
        return false;
    }

    private void UpdateSlideButtons()
    {
        foreach (var sb in FindObjectsByType<SlideButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            sb.SetInteractable(IsSlideLegal(sb.Row, sb.SlideRight));

        bool allInAvailable = !_usedAllInSlide[(int)_currentPlayer];
        foreach (var ab in FindObjectsByType<AllInSlideButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            ab.SetInteractable(allInAvailable);
    }

    // ── Turn end / win check ────────────────────────────────────────────

    private void EndTurn()
    {
        _isFirstTurn   = false;
        _currentPlayer = Opponent(_currentPlayer);
        BeginTurn();
    }

    // ターン開始時の判定（定着・千日手）を行い、決着していなければ手番を開始する。
    // 動かせる駒が1つもない場合は移動・スライドともパスして次の手番へ進む。
    private void BeginTurn()
    {
        // ターン開始時判定1: 自分の駒が自分のゴールマスに残っていれば定着で勝利
        if (HasPieceOnOwnGoal(_currentPlayer))
        {
            _phase = GamePhase.GameOver;
            uiManager.ShowWin(_currentPlayer);
            return;
        }

        // ターン開始時判定2: 同一盤面（手番含む）が3回現れたら千日手で引き分け
        if (RecordPosition() >= 3)
        {
            _phase = GamePhase.GameOver;
            uiManager.ShowDraw();
            return;
        }

        // 合法な移動を持つ駒が1つもなければ、移動・スライドともパスして相手のターンへ
        if (!HasAnyLegalMove(_currentPlayer))
        {
            EndTurn();
            return;
        }

        _phase = GamePhase.SelectPiece;
        uiManager.UpdateStatus(_currentPlayer, _phase);
    }

    private static PlayerSide Opponent(PlayerSide player) =>
        player == PlayerSide.PlayerA ? PlayerSide.PlayerB : PlayerSide.PlayerA;

    // Goal corners: Player A targets (SIZE-1, 0), Player B targets (0, SIZE-1)
    private bool HasPieceOnOwnGoal(PlayerSide player)
    {
        int goalRow = player == PlayerSide.PlayerA ? SIZE - 1 : 0;
        int goalCol = player == PlayerSide.PlayerA ? 0 : SIZE - 1;
        var p = _pieces[goalRow, goalCol];
        return p != null && p.Owner == player;
    }

    private bool HasAnyPiece(PlayerSide player)
    {
        for (int r = 0; r < SIZE; r++)
            for (int c = 0; c < SIZE; c++)
                if (_pieces[r, c] != null && _pieces[r, c].Owner == player) return true;
        return false;
    }

    // 現在の局面（手番含む）の出現回数を記録し、今回を含めた回数を返す
    private int RecordPosition()
    {
        var sb = new System.Text.StringBuilder(SIZE * SIZE + 1);
        sb.Append(_currentPlayer == PlayerSide.PlayerA ? 'A' : 'B');
        for (int r = 0; r < SIZE; r++)
        {
            for (int c = 0; c < SIZE; c++)
            {
                var p = _pieces[r, c];
                sb.Append(p == null ? '.'
                    : p.Owner == PlayerSide.PlayerA
                        ? (p.PieceType == PieceType.Diagonal ? 'a' : 'o')
                        : (p.PieceType == PieceType.Diagonal ? 'b' : 'p'));
            }
        }
        string key = sb.ToString();
        _positionCounts.TryGetValue(key, out int count);
        _positionCounts[key] = ++count;
        return count;
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

        _currentPlayer = Random.value < 0.5f ? PlayerSide.PlayerA : PlayerSide.PlayerB;
        _selected      = null;
        _validMoves.Clear();
        _lastSlideRow  = -1;
        _movedFromRow  = -1;
        _movedToRow    = -1;
        _isFirstTurn   = true;
        _positionCounts.Clear();
        _usedAllInSlide[0] = _usedAllInSlide[1] = false;
        _skipNextSlide[0]  = _skipNextSlide[1]  = false;

        uiManager.ShowSlidePanel(false);
        uiManager.ShowWinOverlay(false);
        PlaceInitialPieces();
        BeginTurn();
    }
}
