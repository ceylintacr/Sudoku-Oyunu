using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Builds the ENTIRE playable scene from code at runtime — main menu, Canvas, EventSystem,
/// the 9x9 grid, the number pad and branch buttons — then wires everything into
/// <see cref="SudokuGridManager"/> and <see cref="SudokuUIManager"/>.
///
/// Why this exists: the scene shipped empty (only a camera + light), so nothing drove
/// the game logic. Rather than hand-wiring references in the Inspector, this spawns itself
/// automatically via [RuntimeInitializeOnLoadMethod] — just press Play.
///
/// Flow: a full-screen main menu is shown first; picking a difficulty hides it and starts a
/// game at that level. The in-game "Yeni Oyun" button reopens the menu.
///
/// Layout: every section is anchored to TOP-CENTER at an explicit Y offset (no outer
/// LayoutGroup), so sections never overlap regardless of the Game view aspect. Designed for
/// a 1080-wide portrait canvas; set the Game view to a portrait ratio (e.g. 1080x1920).
/// </summary>
public class SudokuBootstrapper : MonoBehaviour
{
    private static bool _built;

    private const float RefWidth  = 1080f;
    private const float RefHeight = 1920f;
    private const float Pad       = 18f;

    // ── Warm chocolate / cream palette ──────────────────────────────────────────
    private static readonly Color GridPanelBg = new Color(0.85f, 0.78f, 0.67f); // warm tan (thin lines)
    private static readonly Color GridLine    = new Color(0.45f, 0.33f, 0.24f); // chocolate 3x3 separators
    private static readonly Color Ink         = new Color(0.34f, 0.22f, 0.13f); // chocolate brown text
    private static readonly Color TitleColor  = new Color(0.34f, 0.22f, 0.13f); // chocolate brown
    private static readonly Color SubtleColor = new Color(0.50f, 0.38f, 0.27f); // lighter chocolate
    private static readonly Color CellBg      = new Color(0.98f, 0.96f, 0.89f); // cream
    private static readonly Color NumBtn      = new Color(0.98f, 0.96f, 0.89f); // cream number boxes
    private static readonly Color EraseBtn    = new Color(0.97f, 0.74f, 0.72f); // (re-tinted per theme)
    private static readonly Color StartBtn    = new Color(0.75f, 0.86f, 0.95f); // (re-tinted per theme)
    private static readonly Color NewGameBtn  = new Color(0.80f, 0.91f, 0.87f); // (re-tinted per theme)
    private static readonly Color EasyBtn     = new Color(0.73f, 0.90f, 0.80f); // restart button
    private static readonly Color MedBtn      = new Color(0.96f, 0.90f, 0.64f); // popup menu button

    private static readonly Dictionary<int, Sprite> _spriteCache = new Dictionary<int, Sprite>();
    private Image _bgImage;

    // Runtime state
    private Transform       _root;
    private GameObject      _menu;
    private Transform       _gridParent;
    private readonly bool[] _rowDone = new bool[9];
    private readonly bool[] _colDone = new bool[9];
    private readonly bool[] _boxDone = new bool[9];
    private bool            _suppressAnim;
    private TextMeshProUGUI _timerText;
    private Image[]         _hearts;
    private Image[]         _hints;
    private float           _elapsed;
    private bool            _timerRunning;
    private int             _hintsLeft;
    private Difficulty      _currentDifficulty = Difficulty.Baslangic;

    // Themeable buttons (re-tinted per difficulty)
    private SudokuUIManager _ui;
    private Button[]        _numberButtons;
    private Button          _dalBtn, _newGameBtn, _hintBtn, _eraseBtn, _undoBtn;

    // Result popup
    private GameObject      _resultPanel;
    private RectTransform   _resultCard;
    private TextMeshProUGUI _resultTitle;
    private TextMeshProUGUI _resultSub;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoStart()
    {
        if (_built) return;
        new GameObject("SudokuBootstrapper").AddComponent<SudokuBootstrapper>();
    }

    private void Awake()
    {
        if (_built) { Destroy(gameObject); return; }
        _built = true;
        CleanupExistingUI();
        BuildEverything();
    }

    /// <summary>
    /// Removes any pre-existing UI in the scene (e.g. a hand-wired Canvas + SudokuUIManager)
    /// so it can't conflict with the UI we build. The game-logic SudokuGridManager is kept.
    /// </summary>
    private void CleanupExistingUI()
    {
        // Detach an existing EventSystem so it survives the canvas destruction below and is reused.
        var es = Object.FindFirstObjectByType<EventSystem>();
        if (es != null) es.transform.SetParent(null);

        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            Destroy(canvas.gameObject);

        foreach (var ui in Object.FindObjectsByType<SudokuUIManager>(FindObjectsSortMode.None))
            if (ui != null) Destroy(ui.gameObject);
    }

    // ─── Top-level construction ──────────────────────────────────────────────────

    private void BuildEverything()
    {
        if (SudokuGridManager.Instance == null)
            new GameObject("SudokuGridManager").AddComponent<SudokuGridManager>();

        if (Camera.main == null)
        {
            var cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.orthographic = true;
            cam.backgroundColor = BgFor(Difficulty.Baslangic);
        }

        BuildEventSystem();
        Transform root = BuildCanvas();

        // ── Geometry (1080-wide reference space, measured downward from the top) ──
        float gw    = RefWidth - Pad * 2f;          // content width
        float cell  = (gw - 12f - 8f * 2f) / 9f;    // 9 cells, 8 gaps(2px), 6px border each side
        float gridH = 12f + cell * 9f + 8f * 2f;

        float y = 20f;
        BuildTitle(root, gw, 70f, y);                                    y += 70f + 8f;
        BuildInfoBar(root, gw, 70f, y);                                  y += 70f + 10f;
        GameObject indicator = BuildBranchIndicator(root, gw, 42f, y);   y += 42f + 10f;
        float gridTopY = y;
        Transform  gridParent = BuildGrid(root, gw, gridH, cell, gridTopY);
        BuildGridOverlay(root, gw, gridH, cell, gridTopY);               y += gridH + 16f;
        GameObject cellTemplate = BuildCellTemplate(root);

        var numberButtons = new Button[9];
        BuildNumberPad(root, gw, 116f, y, numberButtons);                y += 116f + 14f;

        // Single branch toggle button (start → commit). No separate commit/discard.
        Button dalBtn = MakeButton(root, "Dal Ac", StartBtn, Ink);
        AnchorTop(dalBtn.GetComponent<RectTransform>(), gw, 88f, y);     y += 88f + 12f;
        var dalLabel = dalBtn.GetComponentInChildren<TextMeshProUGUI>();

        // Bottom action bar (reference-style cream panel).
        BuildBottomBar(root, gw, 140f, y,
            out Button newGameBtn, out Button hintBtn, out Button eraseButton, out Button undoBtn);

        // Main menu (5 difficulty levels), then the result popup — created LAST so they render on top.
        GameObject menu = BuildMenu(root, BeginGame);
        BuildResultPopup(root, out Button againBtn, out Button toMenuBtn);

        _root = root;
        _menu = menu;
        _gridParent = gridParent;
        _numberButtons = numberButtons;
        _dalBtn = dalBtn;
        _newGameBtn = newGameBtn;
        _hintBtn = hintBtn;
        _eraseBtn = eraseButton;
        _undoBtn = undoBtn;

        // Add UIManager and inject references; its Start() then runs the game.
        var ui = root.gameObject.AddComponent<SudokuUIManager>();
        _ui = ui;
        SetField(ui, "gridParent",              gridParent);
        SetField(ui, "cellPrefab",              cellTemplate);
        SetField(ui, "branchingIndicatorPanel", indicator);
        SetField(ui, "numberButtons",           numberButtons);
        SetField(ui, "eraseButton",             eraseButton);
        // Difficulty + branch buttons left null on the UIManager — handled by the menu and dalBtn.

        // Refined colour theme (overrides the SerializeField defaults without touching that class).
        SetField(ui, "givenColor",      Ink);                            // chocolate (pre-filled numbers)
        SetField(ui, "playerColor",     new Color(0.16f, 0.45f, 0.62f)); // friendly blue (your entries)
        SetField(ui, "conflictColor",   new Color(0.86f, 0.32f, 0.30f)); // soft red
        SetField(ui, "branchColor",     new Color(0.86f, 0.50f, 0.16f)); // warm orange
        SetField(ui, "selectedBgColor", new Color(1.00f, 0.93f, 0.66f)); // soft yellow
        SetField(ui, "defaultBgColor",  CellBg);
        SetField(ui, "branchBgTint",    CellBg); // board cells stay cream during branching

        var gm = SudokuGridManager.Instance;

        // Branch toggle: first press starts a branch, second press commits it.
        dalBtn.onClick.AddListener(() =>
        {
            if (gm.IsGameOver) return;
            if (gm.IsBranchingActive) gm.CommitBranch();
            else                      gm.StartBranchingMode();
        });
        gm.OnBranchingModeChanged += active =>
        {
            if (dalLabel != null) dalLabel.text = active ? "Dali Bitir" : "Dal Ac";
            // Tint the themed background (not the board) while branching; restore the theme after.
            _bgImage.color = active ? BranchBrown : BgFor(_currentDifficulty);
        };

        // Hint / Undo.
        hintBtn.onClick.AddListener(GiveHint);
        undoBtn.onClick.AddListener(() => SudokuGridManager.Instance?.Undo());

        // Row/column/box completion animations.
        gm.OnGridChanged += CheckCompletions;

        // Lives / win / lose.
        gm.OnMistake     += _ => UpdateInfo();
        gm.OnGameOver    += () => ShowResult("Kaybettin", "3 hata yaptin — tekrar dene", false);
        gm.OnPuzzleCompleted += () => StartCoroutine(VictorySequence());

        // Result popup buttons.
        againBtn.onClick.AddListener(RestartSameDifficulty);
        toMenuBtn.onClick.AddListener(() =>
        {
            _resultPanel.SetActive(false);
            _menu.SetActive(true);
            _timerRunning = false;
        });

        newGameBtn.onClick.AddListener(() => { menu.SetActive(true); _timerRunning = false; });
    }

    /// <summary>Starts a fresh game at the given difficulty (used by the menu and restart button).</summary>
    private void BeginGame(Difficulty diff)
    {
        _suppressAnim = true;                       // don't animate units already complete from givens
        SudokuGridManager.Instance.NewGame(diff);
        StartGameUI(diff);
        _suppressAnim = false;
    }

    // ─── Game-flow helpers ───────────────────────────────────────────────────────

    private void Update()
    {
        if (!_timerRunning || _timerText == null) return;
        _elapsed += Time.unscaledDeltaTime;
        int t = Mathf.FloorToInt(_elapsed);
        _timerText.text = $"{t / 60:00}:{t % 60:00}";
    }

    // Theme hue per difficulty: teal → green → yellow → orange → red.
    private static float HueFor(Difficulty d) => d switch
    {
        Difficulty.Baslangic   => 0.52f, // soft teal
        Difficulty.Acemi       => 0.34f, // fresh green
        Difficulty.Tecrubeli   => 0.15f, // yellow
        Difficulty.Uzman       => 0.07f, // orange
        Difficulty.Profesyonel => 0.99f, // red
        _                      => 0.52f,
    };

    // Branch mode tints the themed area (not the board) a very light pastel brown — same for all levels.
    private static readonly Color BranchBrown = new Color(0.93f, 0.88f, 0.80f);

    private static Color BgFor(Difficulty d)       => Color.HSVToRGB(HueFor(d), 0.34f, 0.93f); // page background
    private static Color ButtonFor(Difficulty d)   => Color.HSVToRGB(HueFor(d), 0.22f, 0.98f); // dal + action (light pastel)
    private static Color NumFor(Difficulty d)      => Color.HSVToRGB(HueFor(d), 0.11f, 1.00f); // number boxes (near cream)
    private static Color SelectedFor(Difficulty d) => Color.HSVToRGB(HueFor(d), 0.32f, 1.00f); // selected cell

    private const int HintCount = 3; // every difficulty gets the same number of hints
    private static int HintsFor(Difficulty d) => HintCount;

    private static readonly (string label, Difficulty diff)[] Levels =
    {
        ("Baslangic",   Difficulty.Baslangic),
        ("Acemi",       Difficulty.Acemi),
        ("Tecrubeli",   Difficulty.Tecrubeli),
        ("Uzman",       Difficulty.Uzman),
        ("Profesyonel", Difficulty.Profesyonel),
    };

    /// <summary>Resets the on-screen game state for a freshly started puzzle of this difficulty.</summary>
    private void StartGameUI(Difficulty difficulty)
    {
        _currentDifficulty = difficulty;
        _menu.SetActive(false);
        _resultPanel.SetActive(false);
        _hintsLeft = HintsFor(difficulty);
        _elapsed = 0f;
        _timerRunning = true;
        if (_timerText != null) _timerText.text = "00:00";
        ApplyTheme(difficulty);
        UpdateInfo();
    }

    /// <summary>
    /// Applies the difficulty theme: page background (outside the 9x9), all buttons and the
    /// selected-cell colour follow the hue. The 9x9 cells stay cream.
    /// </summary>
    private void ApplyTheme(Difficulty d)
    {
        _bgImage.color = BgFor(d);
        if (_ui != null) SetField(_ui, "selectedBgColor", SelectedFor(d));

        Color theme = ButtonFor(d), num = NumFor(d);
        if (_numberButtons != null)
            foreach (var b in _numberButtons) Tint(b, num);
        Tint(_dalBtn, theme);
        Tint(_newGameBtn, theme);
        Tint(_hintBtn, theme);
        Tint(_eraseBtn, theme);
        Tint(_undoBtn, theme);
    }

    private static void Tint(Button b, Color c)
    {
        if (b != null) b.GetComponent<Image>().color = c;
    }

    private static readonly Color HeartFull  = new Color(0.93f, 0.34f, 0.36f);
    private static readonly Color HeartEmpty = new Color(0.72f, 0.68f, 0.62f, 0.40f);
    private static readonly Color HintFull   = new Color(0.98f, 0.80f, 0.30f); // lit bulb (gold)
    private static readonly Color HintEmpty  = new Color(0.72f, 0.68f, 0.62f, 0.40f);

    private void UpdateInfo()
    {
        var gm = SudokuGridManager.Instance;
        int mistakes = gm != null ? gm.Mistakes : 0;
        int lives = Mathf.Max(0, 3 - mistakes);
        if (_hearts != null)
            for (int i = 0; i < _hearts.Length; i++)
                _hearts[i].color = i < lives ? HeartFull : HeartEmpty;

        int maxHints = HintsFor(_currentDifficulty);
        if (_hints != null)
            for (int i = 0; i < _hints.Length; i++)
            {
                _hints[i].gameObject.SetActive(i < maxHints);
                _hints[i].color = i < _hintsLeft ? HintFull : HintEmpty;
            }

        if (_hintBtn != null) _hintBtn.interactable = _hintsLeft > 0;
    }

    /// <summary>Fills the first empty/incorrect non-given cell with its solution value (uses a hint).</summary>
    private void GiveHint()
    {
        var gm = SudokuGridManager.Instance;
        if (gm == null || gm.PlayerGrid == null || gm.IsGameOver || _hintsLeft <= 0) return;

        for (int r = 0; r < 9; r++)
            for (int c = 0; c < 9; c++)
                if (!gm.IsGiven[r, c] && gm.PlayerGrid[r, c] != gm.SolutionGrid[r, c])
                {
                    gm.SetCell(r, c, gm.SolutionGrid[r, c]);
                    _hintsLeft--;
                    UpdateInfo();
                    return;
                }
    }

    private void RestartSameDifficulty() => BeginGame(_currentDifficulty);

    // ─── Completion animations ──────────────────────────────────────────────────

    /// <summary>After every grid change, animate any row / column / 3x3 box that just got completed.</summary>
    private void CheckCompletions()
    {
        var gm = SudokuGridManager.Instance;
        if (gm == null || gm.PlayerGrid == null) return;

        for (int r = 0; r < 9; r++)
        {
            bool done = RowComplete(gm, r);
            if (done && !_rowDone[r] && !_suppressAnim) AnimateRow(r);
            _rowDone[r] = done;
        }
        for (int c = 0; c < 9; c++)
        {
            bool done = ColComplete(gm, c);
            if (done && !_colDone[c] && !_suppressAnim) AnimateCol(c);
            _colDone[c] = done;
        }
        for (int b = 0; b < 9; b++)
        {
            bool done = BoxComplete(gm, b);
            if (done && !_boxDone[b] && !_suppressAnim) AnimateBox(b);
            _boxDone[b] = done;
        }
    }

    private static bool RowComplete(SudokuGridManager gm, int r)
    {
        for (int c = 0; c < 9; c++) if (gm.PlayerGrid[r, c] != gm.SolutionGrid[r, c]) return false;
        return true;
    }

    private static bool ColComplete(SudokuGridManager gm, int c)
    {
        for (int r = 0; r < 9; r++) if (gm.PlayerGrid[r, c] != gm.SolutionGrid[r, c]) return false;
        return true;
    }

    private static bool BoxComplete(SudokuGridManager gm, int b)
    {
        int br = (b / 3) * 3, bc = (b % 3) * 3;
        for (int r = br; r < br + 3; r++)
            for (int c = bc; c < bc + 3; c++)
                if (gm.PlayerGrid[r, c] != gm.SolutionGrid[r, c]) return false;
        return true;
    }

    private static readonly Color CompleteFlash = new Color(0.42f, 0.76f, 0.46f, 0.80f); // fresh green

    private void AnimateRow(int r)
    {
        var cells = new List<Vector2Int>();
        for (int c = 0; c < 9; c++) cells.Add(new Vector2Int(r, c));
        StartCoroutine(AnimateWave(cells, CompleteFlash, 0.05f));
    }

    private void AnimateCol(int c)
    {
        var cells = new List<Vector2Int>();
        for (int r = 0; r < 9; r++) cells.Add(new Vector2Int(r, c));
        StartCoroutine(AnimateWave(cells, CompleteFlash, 0.05f));
    }

    private void AnimateBox(int b)
    {
        int br = (b / 3) * 3, bc = (b % 3) * 3;
        var cells = new List<Vector2Int>();
        for (int r = br; r < br + 3; r++)
            for (int c = bc; c < bc + 3; c++)
                cells.Add(new Vector2Int(r, c));
        StartCoroutine(AnimateWave(cells, new Color(0.55f, 0.85f, 0.70f, 0.8f), 0.05f));
    }

    private System.Collections.IEnumerator AnimateWave(List<Vector2Int> cells, Color flash, float stagger)
    {
        foreach (var cell in cells)
        {
            var rt = FindCell(cell.x, cell.y);
            if (rt != null) StartCoroutine(PulseCell(rt, flash));
            yield return new WaitForSecondsRealtime(stagger);
        }
    }

    private System.Collections.IEnumerator PulseCell(RectTransform rt, Color flash)
    {
        var ov = new GameObject("Flash", typeof(RectTransform), typeof(Image));
        ov.transform.SetParent(rt, false);
        var ort = ov.GetComponent<RectTransform>();
        Stretch(ort);
        ort.SetAsLastSibling();
        var img = ov.GetComponent<Image>();
        img.sprite = RoundedSprite(12);
        img.type = Image.Type.Sliced;
        img.raycastTarget = false;

        float d = 0.5f, t = 0f;
        while (t < d)
        {
            if (rt == null || img == null) yield break; // cell got rebuilt mid-animation
            t += Time.unscaledDeltaTime;
            float p = t / d;
            float k = 1f + 0.22f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(p));
            rt.localScale = new Vector3(k, k, 1f);
            var c = flash; c.a = flash.a * (1f - p); img.color = c;
            yield return null;
        }
        if (rt != null) rt.localScale = Vector3.one;
        if (ov != null) Destroy(ov);
    }

    private RectTransform FindCell(int r, int c)
    {
        if (_gridParent == null) return null;
        var t = _gridParent.Find($"Cell_{r}_{c}");
        return t != null ? t as RectTransform : null;
    }

    /// <summary>Spectacular full-grid ripple from the centre, then confetti + popup.</summary>
    private System.Collections.IEnumerator VictorySequence()
    {
        _timerRunning = false;

        var cells = new List<Vector2Int>();
        for (int r = 0; r < 9; r++)
            for (int c = 0; c < 9; c++)
                cells.Add(new Vector2Int(r, c));
        cells.Sort((a, b) =>
            (Mathf.Abs(a.x - 4) + Mathf.Abs(a.y - 4)).CompareTo(Mathf.Abs(b.x - 4) + Mathf.Abs(b.y - 4)));

        foreach (var cell in cells)
        {
            var rt = FindCell(cell.x, cell.y);
            if (rt != null)
            {
                float hue = ((cell.x + cell.y) % 9) / 9f;
                Color rainbow = Color.HSVToRGB(hue, 0.55f, 1f); rainbow.a = 0.8f;
                StartCoroutine(PulseCell(rt, rainbow));
            }
            yield return new WaitForSecondsRealtime(0.02f);
        }

        yield return new WaitForSecondsRealtime(0.35f);
        ShowResult("Tebrikler!", "Bulmacayi cozdun", true);
    }

    private void ShowResult(string title, string sub, bool celebrate)
    {
        _timerRunning = false;
        if (_resultTitle != null) _resultTitle.text = title;
        if (_resultSub   != null) _resultSub.text   = sub;

        _resultPanel.SetActive(true);
        _resultPanel.transform.SetAsLastSibling();
        if (_resultCard != null) StartCoroutine(PopIn(_resultCard));
        if (celebrate) StartCoroutine(Confetti(_root));
    }

    private System.Collections.IEnumerator PopIn(RectTransform rt)
    {
        float d = 0.28f, t = 0f;
        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0.7f, 1f, t / d);
            rt.localScale = new Vector3(k, k, 1f);
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    private System.Collections.IEnumerator Confetti(Transform parent)
    {
        Color[] cols =
        {
            new Color(0.96f, 0.55f, 0.55f), new Color(0.98f, 0.80f, 0.45f),
            new Color(0.55f, 0.80f, 0.95f), new Color(0.60f, 0.85f, 0.60f),
            new Color(0.85f, 0.65f, 0.92f), new Color(0.98f, 0.90f, 0.50f),
        };

        var pieces = new List<RectTransform>();
        var vel    = new List<Vector2>();
        for (int i = 0; i < 56; i++)
        {
            var go = new GameObject("Confetti", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = RoundedSprite(6);
            img.type = Image.Type.Sliced;
            img.color = cols[i % cols.Length];
            img.raycastTarget = false;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(Random.Range(16f, 34f), Random.Range(16f, 34f));
            rt.anchoredPosition = new Vector2(Random.Range(-480f, 480f), Random.Range(-40f, 120f));
            pieces.Add(rt);
            vel.Add(new Vector2(Random.Range(-160f, 160f), Random.Range(-520f, 160f)));
        }

        float end = 2.6f, time = 0f;
        while (time < end)
        {
            float dt = Time.unscaledDeltaTime;
            time += dt;
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i] == null) continue;
                var v = vel[i];
                v.y -= 900f * dt; // gravity
                vel[i] = v;
                pieces[i].anchoredPosition += v * dt;
                pieces[i].Rotate(0f, 0f, 220f * dt);
                var img = pieces[i].GetComponent<Image>();
                var c = img.color; c.a = Mathf.Clamp01(1f - time / end); img.color = c;
            }
            yield return null;
        }

        foreach (var p in pieces)
            if (p != null) Destroy(p.gameObject);
    }

    // ─── Infrastructure ──────────────────────────────────────────────────────────

    private void BuildEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();

        var newModule = System.Type.GetType(
            "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (newModule != null) es.AddComponent(newModule);
        else                   es.AddComponent<StandaloneInputModule>();
    }

    private Transform BuildCanvas()
    {
        var canvasGO = new GameObject("SudokuCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        canvasGO.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0f; // match width — content always fills the portrait width

        _bgImage = canvasGO.AddComponent<Image>();
        _bgImage.color = BgFor(Difficulty.Baslangic); // themed per difficulty at game start
        return canvasGO.transform;
    }

    // ─── Main Menu ───────────────────────────────────────────────────────────────

    private GameObject BuildMenu(Transform parent, System.Action<Difficulty> onPick)
    {
        var panel = new GameObject("MainMenu", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        Stretch(panel.GetComponent<RectTransform>());
        var menuImg = panel.GetComponent<Image>();      // opaque + raycastTarget blocks the game behind
        menuImg.color = Color.white;
        menuImg.sprite = VerticalGradient();            // cream → soft pastel shading
        menuImg.type = Image.Type.Simple;

        var title = NewText(panel.transform, "Sudoku");
        title.enableAutoSizing = false;
        title.fontSize = 110f;
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 22f;
        title.color = TitleColor;
        CenterAt(title.rectTransform, 900f, 170f, 640f);

        var sub = NewText(panel.transform, "Zorluk seviyesi sec");
        sub.enableAutoSizing = false;
        sub.fontSize = 44f;
        sub.color = SubtleColor;
        CenterAt(sub.rectTransform, 900f, 70f, 510f);

        // Five difficulty buttons, each themed with its background colour.
        float by = 350f;
        foreach (var (label, diff) in Levels)
        {
            var b = MakeButton(panel.transform, label, BgFor(diff), Ink, maxFont: 46f);
            CenterAt(b.GetComponent<RectTransform>(), 740f, 120f, by);
            var d = diff;
            b.onClick.AddListener(() => onPick(d));
            by -= 138f;
        }

        var hint = NewText(panel.transform, "Bir seviye sec ve baslamak icin dokun");
        hint.enableAutoSizing = false;
        hint.fontSize = 32f;
        hint.color = SubtleColor;
        CenterAt(hint.rectTransform, 900f, 60f, by - 10f);

        return panel;
    }

    // ─── Game Sections ───────────────────────────────────────────────────────────

    private void BuildTitle(Transform parent, float w, float h, float topY)
    {
        var go = new GameObject("Title", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        AnchorTop(go.GetComponent<RectTransform>(), w, h, topY);

        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = "Sudoku";
        t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = FontStyles.Bold;
        t.characterSpacing = 16f;
        t.enableAutoSizing = false;
        t.fontSize = 70f;
        t.color = TitleColor;
        t.raycastTarget = false;
    }

    private GameObject BuildBranchIndicator(Transform parent, float w, float h, float topY)
    {
        var panel = new GameObject("BranchingIndicator", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        AnchorTop(panel.GetComponent<RectTransform>(), w, h, topY);
        StyleImage(panel.GetComponent<Image>(), new Color(0.98f, 0.82f, 0.58f, 1f)); // soft peach

        var label = NewText(panel.transform, "DAL MODU AKTIF  •  deneme yapiyorsun");
        label.color = Ink;
        label.fontStyle = FontStyles.Bold;
        Stretch(label.rectTransform);

        panel.SetActive(false); // toggled by SudokuUIManager
        return panel;
    }

    private Transform BuildGrid(Transform parent, float w, float h, float cell, float topY)
    {
        var container = new GameObject("GridContainer",
            typeof(RectTransform), typeof(Image), typeof(GridLayoutGroup));
        container.transform.SetParent(parent, false);
        AnchorTop(container.GetComponent<RectTransform>(), w, h, topY);

        StyleImage(container.GetComponent<Image>(), GridPanelBg, 30); // soft sage shows through gaps as thin lines

        var grid = container.GetComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 9;
        grid.cellSize = new Vector2(cell, cell);
        grid.spacing = new Vector2(2f, 2f);
        grid.padding = new RectOffset(6, 6, 6, 6);
        grid.childAlignment = TextAnchor.MiddleCenter;

        return container.transform;
    }

    /// <summary>
    /// Bold 3x3 box lines, drawn as a SEPARATE overlay above the grid — NOT a child of the
    /// grid container, because SudokuUIManager.BuildGrid() destroys every child of that
    /// container (and freshly-spawned cells would otherwise cover the lines). Positioned in
    /// absolute pixels to land exactly on the box seams.
    /// </summary>
    private void BuildGridOverlay(Transform parent, float w, float h, float cell, float topY)
    {
        var overlay = new GameObject("GridOverlay", typeof(RectTransform));
        overlay.transform.SetParent(parent, false);
        var ort = overlay.GetComponent<RectTransform>();
        AnchorTop(ort, w, h, topY);

        const float t = 8f;
        const float border = 6f;
        const float spacing = 2f;

        // Distance from the grid's left/top edge to the seam after every 3rd cell.
        for (int i = 1; i <= 2; i++)
        {
            float offset = border + i * (cell * 3f + spacing * 3f) - spacing * 0.5f;
            AddLineX(overlay.transform, w, h, offset, t);  // vertical seam
            AddLineY(overlay.transform, w, h, offset, t);  // horizontal seam
        }
    }

    // Vertical line at x = offset (measured from the left edge of a top-left-pivot overlay).
    private void AddLineX(Transform parent, float w, float h, float offset, float thickness)
    {
        var rt = NewLine(parent);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(thickness, h);
        rt.anchoredPosition = new Vector2(offset, 0f);
    }

    // Horizontal line at y = offset below the top edge.
    private void AddLineY(Transform parent, float w, float h, float offset, float thickness)
    {
        var rt = NewLine(parent);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(w, thickness);
        rt.anchoredPosition = new Vector2(0f, -offset);
    }

    private RectTransform NewLine(Transform parent)
    {
        var go = new GameObject("BoxLine", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = GridLine;
        img.raycastTarget = false; // clicks pass through to the cells underneath
        return go.GetComponent<RectTransform>();
    }

    /// <summary>
    /// Reusable cell template. Kept active (so Instantiate'd copies are active) but parented
    /// under a disabled holder so the template itself never renders.
    /// </summary>
    private GameObject BuildCellTemplate(Transform canvasRoot)
    {
        var holder = new GameObject("TemplateHolder", typeof(RectTransform));
        holder.transform.SetParent(canvasRoot, false);
        holder.SetActive(false);

        var cell = new GameObject("Cell", typeof(RectTransform), typeof(Image), typeof(Button));
        cell.transform.SetParent(holder.transform, false);
        StyleImage(cell.GetComponent<Image>(), CellBg, 24); // softly rounded cream tiles

        var txt = NewText(cell.transform, "");
        txt.color = Ink;
        txt.enableAutoSizing = true;
        txt.fontSizeMin = 18f;
        txt.fontSizeMax = 56f;
        Stretch(txt.rectTransform);

        return cell;
    }

    private void BuildNumberPad(Transform parent, float w, float h, float topY, Button[] numberButtons)
    {
        var container = new GameObject("NumberPad", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        container.transform.SetParent(parent, false);
        AnchorTop(container.GetComponent<RectTransform>(), w, h, topY);

        var hlg = container.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8f;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        for (int i = 0; i < 9; i++)
            numberButtons[i] = MakeButton(container.transform, (i + 1).ToString(), NumBtn, Ink, maxFont: 52f);
    }

    /// <summary>Top info row: time pill, hearts (lives) pill, hints (stars) pill.</summary>
    private void BuildInfoBar(Transform parent, float w, float h, float topY)
    {
        var row = new GameObject("InfoBar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(parent, false);
        AnchorTop(row.GetComponent<RectTransform>(), w, h, topY);

        var hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 14f;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        _timerText = MakeTextPill(row.transform, "00:00");
        _hearts    = MakeIconPill(row.transform, HeartSprite(), 3, HeartFull);
        _hints     = MakeIconPill(row.transform, BulbSprite(), HintCount, HintFull);
    }

    private TextMeshProUGUI MakeTextPill(Transform parent, string content)
    {
        var go = new GameObject("Pill", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        StyleImage(go.GetComponent<Image>(), new Color(0.99f, 0.98f, 0.92f), 26); // cream pill

        var t = NewText(go.transform, content);
        t.color = Ink;
        t.fontStyle = FontStyles.Bold;
        t.enableAutoSizing = true;
        t.fontSizeMin = 20f;
        t.fontSizeMax = 38f;
        Stretch(t.rectTransform);
        return t;
    }

    /// <summary>A cream pill holding a row of <paramref name="count"/> icon images.</summary>
    private Image[] MakeIconPill(Transform parent, Sprite sprite, int count, Color fill)
    {
        var pill = new GameObject("Pill", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
        pill.transform.SetParent(parent, false);
        StyleImage(pill.GetComponent<Image>(), new Color(0.99f, 0.98f, 0.92f), 26);

        var hlg = pill.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(10, 10, 8, 8);
        hlg.spacing = 5f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        var icons = new Image[count];
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(pill.transform, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(44f, 44f);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = fill;
            img.raycastTarget = false;
            icons[i] = img;

            // Cute glossy highlight on each icon.
            AddGlow(go.transform, new Vector2(0.16f, 0.50f), new Vector2(0.52f, 0.86f), 0.65f);
        }
        return icons;
    }

    /// <summary>Full-screen win/lose popup with "Yeni Oyun" and "Menüye Dön" buttons.</summary>
    private void BuildResultPopup(Transform parent, out Button again, out Button toMenu)
    {
        var panel = new GameObject("ResultPopup", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        Stretch(panel.GetComponent<RectTransform>());
        panel.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.07f, 0.45f); // dim overlay

        var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(panel.transform, false);
        StyleImage(card.GetComponent<Image>(), new Color(0.99f, 0.98f, 0.93f), 40);
        _resultCard = card.GetComponent<RectTransform>();
        CenterAt(_resultCard, 780f, 560f, 0f);

        _resultTitle = NewText(card.transform, "Tebrikler!");
        _resultTitle.enableAutoSizing = false;
        _resultTitle.fontSize = 84f;
        _resultTitle.fontStyle = FontStyles.Bold;
        _resultTitle.color = TitleColor;
        CenterAt(_resultTitle.rectTransform, 700f, 120f, 170f);

        _resultSub = NewText(card.transform, "");
        _resultSub.enableAutoSizing = false;
        _resultSub.fontSize = 40f;
        _resultSub.color = SubtleColor;
        CenterAt(_resultSub.rectTransform, 680f, 100f, 60f);

        again = MakeButton(card.transform, "Yeni Oyun", EasyBtn, Ink, maxFont: 48f);
        CenterAt(again.GetComponent<RectTransform>(), 600f, 120f, -30f);

        toMenu = MakeButton(card.transform, "Menuye Don", NewGameBtn, Ink, maxFont: 48f);
        CenterAt(toMenu.GetComponent<RectTransform>(), 600f, 120f, -170f);

        panel.SetActive(false);
        _resultPanel = panel;
    }

    private void BuildBottomBar(Transform parent, float w, float h, float topY,
                                out Button newGame, out Button hint, out Button erase, out Button undo)
    {
        var panel = new GameObject("BottomBar",
            typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
        panel.transform.SetParent(parent, false);
        AnchorTop(panel.GetComponent<RectTransform>(), w, h, topY);
        StyleImage(panel.GetComponent<Image>(), new Color(0.99f, 0.97f, 0.90f), 30); // cream panel

        var hlg = panel.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(14, 14, 14, 14);
        hlg.spacing = 12f;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        newGame = MakeButton(panel.transform, "Yeni\nOyun", NewGameBtn, Ink);
        hint    = MakeButton(panel.transform, "Ipucu",      MedBtn,     Ink);
        erase   = MakeButton(panel.transform, "Sil",        EraseBtn,   Ink);
        undo    = MakeButton(panel.transform, "Geri Al",    StartBtn,   Ink);
    }

    // ─── Primitive UI helpers ────────────────────────────────────────────────────

    private Button MakeButton(Transform parent, string label, Color bg, Color fg, float maxFont = 44f)
    {
        var go = new GameObject(label + " Button", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        StyleImage(go.GetComponent<Image>(), bg);

        // Soft drop shadow for depth.
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.18f);
        shadow.effectDistance = new Vector2(0f, -4f);

        // Glossy highlight on the upper half (soft shine).
        var shine = new GameObject("Shine", typeof(RectTransform), typeof(Image));
        shine.transform.SetParent(go.transform, false);
        var srt = shine.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 0.52f);
        srt.anchorMax = new Vector2(1f, 1f);
        srt.offsetMin = new Vector2(8f, 0f);
        srt.offsetMax = new Vector2(-8f, -8f);
        var shineImg = shine.GetComponent<Image>();
        shineImg.sprite = RoundedSprite(16);
        shineImg.type = Image.Type.Sliced;
        shineImg.color = new Color(1f, 1f, 1f, 0.18f);
        shineImg.raycastTarget = false;

        // Anime-eye style bright highlights: one big, one small.
        AddGlow(go.transform, new Vector2(0.07f, 0.50f), new Vector2(0.44f, 0.93f), 0.55f);
        AddGlow(go.transform, new Vector2(0.55f, 0.60f), new Vector2(0.73f, 0.86f), 0.40f);

        var btn = go.GetComponent<Button>();
        var cb = btn.colors;
        cb.highlightedColor = new Color(1f, 1f, 1f, 1f);
        cb.pressedColor    = new Color(0.80f, 0.80f, 0.80f, 1f);
        cb.selectedColor   = Color.white;
        cb.disabledColor   = new Color(0.85f, 0.85f, 0.85f, 0.6f);
        cb.fadeDuration    = 0.08f;
        btn.colors = cb;

        var txt = NewText(go.transform, label);
        txt.color = fg;
        txt.fontStyle = FontStyles.Bold;
        txt.enableAutoSizing = true;
        txt.fontSizeMin = 16f;
        txt.fontSizeMax = maxFont;
        Stretch(txt.rectTransform);

        return btn;
    }

    private TextMeshProUGUI NewText(Transform parent, string content)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = content;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return t;
    }

    /// <summary>Applies a colour and a procedurally-generated rounded-corner sprite (9-sliced).</summary>
    private static void StyleImage(Image img, Color color, int radius = 36)
    {
        img.color = color;
        img.sprite = RoundedSprite(radius);
        img.type = Image.Type.Sliced;
    }

    private static Sprite _menuGradient;

    /// <summary>Cached vertical gradient sprite (cream at the top → soft pastel at the bottom).</summary>
    private static Sprite VerticalGradient()
    {
        if (_menuGradient != null) return _menuGradient;

        const int h = 256;
        var tex = new Texture2D(1, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color top    = new Color(0.99f, 0.97f, 0.90f); // cream
        Color bottom = new Color(0.85f, 0.91f, 0.86f); // soft pastel sage
        for (int y = 0; y < h; y++)
            tex.SetPixel(0, y, Color.Lerp(bottom, top, y / (h - 1f)));
        tex.Apply();

        _menuGradient = Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f), 100f);
        return _menuGradient;
    }

    /// <summary>
    /// Returns a cached white rounded-rectangle sprite (anti-aliased) with the given corner radius.
    /// Generated in code so it has no asset dependency — Unity 6 can't load the old builtin UISprite.
    /// </summary>
    private static Sprite RoundedSprite(int radius)
    {
        if (_spriteCache.TryGetValue(radius, out var cached) && cached != null)
            return cached;

        int size = radius * 2 + 8;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Min(x + 0.5f, size - (x + 0.5f));
                float dy = Mathf.Min(y + 0.5f, size - (y + 0.5f));
                float a = 1f;
                if (dx < radius && dy < radius)
                {
                    float ex = radius - dx, ey = radius - dy;
                    a = Mathf.Clamp01(radius - Mathf.Sqrt(ex * ex + ey * ey) + 0.5f);
                }
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();

        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        _spriteCache[radius] = sprite;
        return sprite;
    }

    private static Sprite _heartSprite, _bulbSprite, _glowSprite;

    /// <summary>Soft radial white glow (alpha fades to the edges) — used for glossy highlights.</summary>
    private static Sprite GlowSprite()
    {
        if (_glowSprite != null) return _glowSprite;

        const int s = 64;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x + 0.5f) / s - 0.5f, dy = (y + 0.5f) / s - 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy) / 0.5f; // 0 centre → 1 edge
                float a = Mathf.Clamp01(1f - dist);
                a *= a; // soft falloff toward a bright core
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        _glowSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        return _glowSprite;
    }

    /// <summary>Adds a soft white glow highlight over a rect region of <paramref name="parent"/>.</summary>
    private static void AddGlow(Transform parent, Vector2 aMin, Vector2 aMax, float alpha)
    {
        var go = new GameObject("Glow", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.sprite = GlowSprite();
        img.color = new Color(1f, 1f, 1f, alpha);
        img.raycastTarget = false;
    }

    /// <summary>Procedural white heart sprite (tint at use site). Uses the implicit heart curve.</summary>
    private static Sprite HeartSprite()
    {
        if (_heartSprite != null) return _heartSprite;
        _heartSprite = ShapeSprite(72, (u, v) =>
        {
            // Heart curve: (u² + v² − 1)³ − u²·v³ ≤ 0, mapped so the cusp is up and the point is down.
            float a = u * u + v * v - 1f;
            return a * a * a - u * u * v * v * v <= 0f;
        }, uRange: 1.35f, vMin: -1.45f, vMax: 1.15f);
        return _heartSprite;
    }

    /// <summary>Procedural white 5-point star sprite (tint at use site).</summary>
    /// <summary>Procedural white light-bulb sprite (glass bulb + screw base).</summary>
    private static Sprite BulbSprite()
    {
        if (_bulbSprite != null) return _bulbSprite;

        const int n = 72;
        _bulbSprite = PixelSprite(n, (x, y) =>
        {
            float nx = x / (float)n, ny = y / (float)n;
            float dx = nx - 0.5f, dy = ny - 0.60f;
            bool bulb = dx * dx + dy * dy <= 0.30f * 0.30f;             // glass bulb (upper circle)
            bool baseBlock = nx > 0.39f && nx < 0.61f && ny > 0.13f && ny < 0.34f; // screw base
            return bulb || baseBlock;
        });
        return _bulbSprite;
    }

    // Builds a sprite from an inside-test in normalized (u,v) coordinates, with 2x2 anti-aliasing.
    private static Sprite ShapeSprite(int size, System.Func<float, float, bool> inside,
                                      float uRange, float vMin, float vMax)
    {
        return PixelSprite(size, (x, y) =>
        {
            float u = (x / (float)size) * 2f * uRange - uRange;
            float v = vMin + (y / (float)size) * (vMax - vMin);
            return inside(u, v);
        });
    }

    // Builds a white sprite where each pixel's alpha is the 2x2-supersampled coverage of inside(x,y).
    private static Sprite PixelSprite(int size, System.Func<float, float, bool> inside)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int hits = 0;
                if (inside(x + 0.25f, y + 0.25f)) hits++;
                if (inside(x + 0.75f, y + 0.25f)) hits++;
                if (inside(x + 0.25f, y + 0.75f)) hits++;
                if (inside(x + 0.75f, y + 0.75f)) hits++;
                px[y * size + x] = new Color32(255, 255, 255, (byte)(hits * 63));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static bool PointInPolygon(Vector2 p, Vector2[] poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if (poly[i].y > p.y != poly[j].y > p.y &&
                p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                inside = !inside;
        return inside;
    }

    /// <summary>Anchors a RectTransform to top-center at an explicit downward offset.</summary>
    private static void AnchorTop(RectTransform rt, float width, float height, float topY)
    {
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = new Vector2(0f, -topY);
    }

    /// <summary>Anchors a RectTransform to the screen centre with an explicit Y offset.</summary>
    private static void CenterAt(RectTransform rt, float width, float height, float posY)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = new Vector2(0f, posY);
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        var f = target.GetType().GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (f == null)
        {
            Debug.LogError($"[SudokuBootstrapper] Field '{fieldName}' not found on {target.GetType().Name}.");
            return;
        }
        f.SetValue(target, value);
    }
}
