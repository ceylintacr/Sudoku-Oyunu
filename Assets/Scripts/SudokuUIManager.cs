using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages all visual elements: the 9x9 grid, number pad, and branching UI buttons.
/// Depends on SudokuGridManager.Instance — attach this after the manager is initialized.
/// </summary>
public class SudokuUIManager : MonoBehaviour
{
    // ─── Inspector References ──────────────────────────────────────────────────

    [Header("Grid")]
    [Tooltip("The parent Transform that holds all 81 cell prefabs (use a GridLayoutGroup).")]
    [SerializeField] private Transform gridParent;
    [Tooltip("Prefab: must have Image + Button on root, and a TextMeshProUGUI child.")]
    [SerializeField] private GameObject cellPrefab;

    [Header("Branch Control Buttons")]
    [SerializeField] private Button startBranchButton;
    [SerializeField] private Button commitBranchButton;
    [SerializeField] private Button discardBranchButton;
    [Tooltip("Optional overlay/banner shown when branching mode is active.")]
    [SerializeField] private GameObject branchingIndicatorPanel;

    [Header("Number Pad (assign buttons 1-9 in order)")]
    [SerializeField] private Button[] numberButtons; // length must be 9
    [SerializeField] private Button eraseButton;

    [Header("New Game Buttons")]
    [SerializeField] private Button newGameEasyButton;
    [SerializeField] private Button newGameMediumButton;
    [SerializeField] private Button newGameHardButton;

    [Header("Colors")]
    [SerializeField] private Color givenColor      = new Color(0.15f, 0.15f, 0.15f); // near-black
    [SerializeField] private Color playerColor     = new Color(0.10f, 0.45f, 0.85f); // calm blue
    [SerializeField] private Color branchColor     = new Color(0.90f, 0.45f, 0.05f); // vivid orange
    [SerializeField] private Color conflictColor   = new Color(0.85f, 0.15f, 0.15f); // red
    [SerializeField] private Color selectedBgColor = new Color(0.95f, 0.90f, 0.50f); // yellow
    [SerializeField] private Color defaultBgColor  = Color.white;
    [SerializeField] private Color branchBgTint    = new Color(0.88f, 0.95f, 1.00f); // light blue tint

    // ─── Runtime State ─────────────────────────────────────────────────────────

    private TextMeshProUGUI[,] cellTexts  = new TextMeshProUGUI[9, 9];
    private Image[,]           cellImages = new Image[9, 9];

    // Tracks which cells were written DURING the current branch (for color coding)
    private bool[,] isBranchCell = new bool[9, 9];

    private int selectedRow = -1;
    private int selectedCol = -1;

    private SudokuGridManager gridManager;

    // ─── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Start()
    {
        gridManager = SudokuGridManager.Instance;
        if (gridManager == null)
        {
            Debug.LogError("[SudokuUIManager] SudokuGridManager.Instance is null. " +
                           "Make sure SudokuGridManager is in the scene and initialized before this.");
            return;
        }

        gridManager.OnGridChanged         += RefreshGrid;
        gridManager.OnBranchingModeChanged += HandleBranchingModeChanged;
        gridManager.OnPuzzleCompleted      += HandlePuzzleCompleted;

        WireUpStaticButtons();

        // Start a default game so the board is immediately visible
        gridManager.NewGame(default);
        BuildGrid();
    }

    private void OnDestroy()
    {
        if (gridManager == null) return;
        gridManager.OnGridChanged         -= RefreshGrid;
        gridManager.OnBranchingModeChanged -= HandleBranchingModeChanged;
        gridManager.OnPuzzleCompleted      -= HandlePuzzleCompleted;
    }

    // ─── Grid Construction ─────────────────────────────────────────────────────

    /// <summary>
    /// Destroys existing cells and instantiates 81 fresh ones under gridParent.
    /// Call this once after NewGame() to rebuild the visual grid.
    /// </summary>
    public void BuildGrid()
    {
        // Clear any old cells
        foreach (Transform child in gridParent)
            Destroy(child.gameObject);

        ResetBranchCellTracking();
        selectedRow = -1;
        selectedCol = -1;

        for (int r = 0; r < 9; r++)
        {
            for (int c = 0; c < 9; c++)
            {
                GameObject cell = Instantiate(cellPrefab, gridParent);
                cell.name = $"Cell_{r}_{c}";

                cellImages[r, c] = cell.GetComponent<Image>();
                cellTexts[r, c]  = cell.GetComponentInChildren<TextMeshProUGUI>();

                if (cellImages[r, c] == null)
                    Debug.LogError($"[SudokuUIManager] Cell prefab at ({r},{c}) is missing an Image component.");
                if (cellTexts[r, c] == null)
                    Debug.LogError($"[SudokuUIManager] Cell prefab at ({r},{c}) has no TextMeshProUGUI child.");

                int row = r, col = c; // capture loop variables for the closure
                Button btn = cell.GetComponent<Button>();
                if (btn != null)
                    btn.onClick.AddListener(() => OnCellClicked(row, col));
            }
        }

        RefreshGrid();
        UpdateBranchButtons();
    }

    // ─── Grid Refresh (called by OnGridChanged event) ──────────────────────────

    private void RefreshGrid()
    {
        if (gridManager?.PlayerGrid == null) return;

        for (int r = 0; r < 9; r++)
        {
            for (int c = 0; c < 9; c++)
            {
                if (cellTexts[r, c] == null) continue;

                int  val   = gridManager.PlayerGrid[r, c];
                bool given = gridManager.IsGiven[r, c];

                // Text
                cellTexts[r, c].text = val == 0 ? "" : val.ToString();

                // Text color. While branching we hide conflicts — it's a safe what-if space.
                if (given)
                    cellTexts[r, c].color = givenColor;
                else if (!gridManager.IsBranchingActive && gridManager.HasConflict(r, c))
                    cellTexts[r, c].color = conflictColor;
                else if (isBranchCell[r, c])
                    cellTexts[r, c].color = branchColor;     // orange = entered during branch
                else
                    cellTexts[r, c].color = playerColor;     // blue   = normal player entry

                // Background tint
                if (cellImages[r, c] == null) continue;
                bool isSelected = (r == selectedRow && c == selectedCol);
                if (isSelected)
                    cellImages[r, c].color = selectedBgColor;
                else if (gridManager.IsBranchingActive)
                    cellImages[r, c].color = branchBgTint;   // subtle whole-grid tint in branch mode
                else
                    cellImages[r, c].color = defaultBgColor;
            }
        }
    }

    // ─── Cell & Number Interaction ─────────────────────────────────────────────

    private void OnCellClicked(int row, int col)
    {
        selectedRow = row;
        selectedCol = col;
        RefreshGrid(); // Refresh to show/move the yellow selection highlight
    }

    private void OnNumberPressed(int number)
    {
        if (selectedRow < 0 || selectedCol < 0) return;
        if (gridManager?.PlayerGrid == null) return;
        if (gridManager.IsGiven[selectedRow, selectedCol]) return;

        // Tag this cell as a branch entry so it gets the branch color
        if (gridManager.IsBranchingActive)
            isBranchCell[selectedRow, selectedCol] = true;

        gridManager.SetCell(selectedRow, selectedCol, number);
    }

    private void OnErasePressed()
    {
        if (selectedRow < 0 || selectedCol < 0) return;
        if (gridManager?.PlayerGrid == null) return;
        if (gridManager.IsGiven[selectedRow, selectedCol]) return;

        isBranchCell[selectedRow, selectedCol] = false;
        gridManager.SetCell(selectedRow, selectedCol, 0);
    }

    // ─── Branching UI (called by inspector-wired buttons) ─────────────────────

    public void OnStartBranchPressed()  => gridManager?.StartBranchingMode();
    public void OnCommitBranchPressed() => gridManager?.CommitBranch();
    public void OnDiscardBranchPressed()=> gridManager?.DiscardBranch();

    // ─── New Game Buttons ──────────────────────────────────────────────────────

    // Kept for compatibility; difficulty is now driven by SudokuBootstrapper's menu.
    public void OnNewGameEasyPressed()   => StartNewGame((Difficulty)0);
    public void OnNewGameMediumPressed() => StartNewGame((Difficulty)2);
    public void OnNewGameHardPressed()   => StartNewGame((Difficulty)4);

    private void StartNewGame(Difficulty difficulty)
    {
        gridManager.NewGame(difficulty);
        BuildGrid();
    }

    // ─── Event Handlers ────────────────────────────────────────────────────────

    private void HandleBranchingModeChanged(bool isActive)
    {
        if (branchingIndicatorPanel != null)
            branchingIndicatorPanel.SetActive(isActive);

        // When branch ends (commit or discard), clear the orange cell tracking
        if (!isActive)
            ResetBranchCellTracking();

        UpdateBranchButtons();
        RefreshGrid();
    }

    private void HandlePuzzleCompleted()
    {
        // Disable input on completion — extend with a victory panel as needed
        if (startBranchButton  != null) startBranchButton.interactable  = false;
        if (commitBranchButton != null) commitBranchButton.interactable = false;
        if (discardBranchButton!= null) discardBranchButton.interactable= false;
        Debug.Log("[SudokuUIManager] Puzzle complete!");
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    private void WireUpStaticButtons()
    {
        // Number pad 1-9
        if (numberButtons != null)
        {
            for (int i = 0; i < numberButtons.Length && i < 9; i++)
            {
                int num = i + 1; // capture by value
                numberButtons[i]?.onClick.AddListener(() => OnNumberPressed(num));
            }
        }

        eraseButton?.onClick.AddListener(OnErasePressed);

        startBranchButton?.onClick.AddListener(OnStartBranchPressed);
        commitBranchButton?.onClick.AddListener(OnCommitBranchPressed);
        discardBranchButton?.onClick.AddListener(OnDiscardBranchPressed);

        newGameEasyButton?.onClick.AddListener(OnNewGameEasyPressed);
        newGameMediumButton?.onClick.AddListener(OnNewGameMediumPressed);
        newGameHardButton?.onClick.AddListener(OnNewGameHardPressed);
    }

    private void UpdateBranchButtons()
    {
        bool active = gridManager != null && gridManager.IsBranchingActive;
        if (startBranchButton  != null) startBranchButton.interactable  = !active;
        if (commitBranchButton != null) commitBranchButton.interactable = active;
        if (discardBranchButton!= null) discardBranchButton.interactable= active;
    }

    private void ResetBranchCellTracking()
    {
        for (int r = 0; r < 9; r++)
            for (int c = 0; c < 9; c++)
                isBranchCell[r, c] = false;
    }
}
