using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton MonoBehaviour. Owns all game-state data and the Branching system.
/// UI and other systems must never touch the arrays directly — only through this class.
/// </summary>
public class SudokuGridManager : MonoBehaviour
{
    // ─── Singleton ─────────────────────────────────────────────────────────────

    public static SudokuGridManager Instance { get; private set; }

    // ─── Public Read-Only State ────────────────────────────────────────────────

    public int[,]  PlayerGrid   { get; private set; }
    public int[,]  SolutionGrid { get; private set; }
    public bool[,] IsGiven      { get; private set; }
    public bool    IsBranchingActive { get; private set; }

    public int  Mistakes     { get; private set; }
    public int  MistakeLimit { get; set; } = 3;
    public bool IsGameOver   { get; private set; }

    // ─── Events (UIManager subscribes to these) ────────────────────────────────

    /// <summary>Fired whenever any cell value changes.</summary>
    public event Action OnGridChanged;

    /// <summary>Fired when branching mode is activated or deactivated.</summary>
    public event Action<bool> OnBranchingModeChanged;

    /// <summary>Fired when the player completes the puzzle correctly.</summary>
    public event Action OnPuzzleCompleted;

    /// <summary>Fired after each wrong entry, with the running mistake count.</summary>
    public event Action<int> OnMistake;

    /// <summary>Fired once the mistake limit is reached.</summary>
    public event Action OnGameOver;

    // ─── Private State ─────────────────────────────────────────────────────────

    private int[,] mainRealityBackup; // null when not branching
    private SudokuEngine engine;

    // Undo history of player moves: (row, col, previous value).
    private readonly Stack<(int row, int col, int prev)> undoStack = new Stack<(int, int, int)>();

    // ─── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        engine = new SudokuEngine();
    }

    // ─── Game Control ──────────────────────────────────────────────────────────

    public void NewGame(Difficulty difficulty)
    {
        // Always reset branching state before starting a new game
        IsBranchingActive = false;
        mainRealityBackup = null;
        undoStack.Clear();
        Mistakes = 0;
        IsGameOver = false;

        SolutionGrid = engine.GenerateSolution();
        PlayerGrid   = engine.GeneratePuzzle(SolutionGrid, difficulty, out bool[,] given);
        IsGiven      = given;

        OnBranchingModeChanged?.Invoke(false);
        OnGridChanged?.Invoke();
    }

    // ─── Cell Interaction ──────────────────────────────────────────────────────

    /// <summary>
    /// Places a number in a cell. Pass 0 to erase.
    /// Silently ignores attempts to overwrite a given (pre-filled) cell.
    /// </summary>
    public void SetCell(int row, int col, int value)
    {
        if (PlayerGrid == null) return;
        if (IsGameOver)         return;
        if (IsGiven[row, col])  return;
        if (value < 0 || value > 9) return;
        if (PlayerGrid[row, col] == value) return; // no change

        undoStack.Push((row, col, PlayerGrid[row, col])); // record for Undo
        PlayerGrid[row, col] = value;

        // Mistake tracking — a wrong entry (outside branching) costs a life.
        if (value != 0 && !IsBranchingActive && SolutionGrid != null && value != SolutionGrid[row, col])
        {
            Mistakes++;
            OnMistake?.Invoke(Mistakes);
            if (Mistakes >= MistakeLimit)
            {
                IsGameOver = true;
                OnGridChanged?.Invoke();
                OnGameOver?.Invoke();
                return;
            }
        }

        OnGridChanged?.Invoke();

        if (value != 0 && IsPuzzleComplete())
            OnPuzzleCompleted?.Invoke();
    }

    // ─── Branching System (Core Feature) ──────────────────────────────────────

    /// <summary>
    /// Freezes the current board into mainRealityBackup and enters branching mode.
    /// All subsequent SetCell calls are treated as experimental until Commit or Discard.
    /// </summary>
    public void StartBranchingMode()
    {
        if (IsBranchingActive)
        {
            Debug.LogWarning("[SudokuGridManager] StartBranchingMode called while already branching.");
            return;
        }
        if (PlayerGrid == null)
        {
            Debug.LogWarning("[SudokuGridManager] StartBranchingMode called before a game was started.");
            return;
        }

        // Deep copy — NOT a reference copy. mainRealityBackup is a completely independent array.
        mainRealityBackup = SudokuEngine.DeepCopy(PlayerGrid);
        IsBranchingActive = true;
        undoStack.Clear();

        OnBranchingModeChanged?.Invoke(true);
        Debug.Log("[SudokuGridManager] Branching mode STARTED. Main reality backed up.");
    }

    /// <summary>
    /// Accepts the current branch: clears the backup and exits branching mode.
    /// The player keeps everything they entered during the branch.
    /// </summary>
    public void CommitBranch()
    {
        if (!IsBranchingActive)
        {
            Debug.LogWarning("[SudokuGridManager] CommitBranch called while not branching.");
            return;
        }

        mainRealityBackup = null; // GC will collect it — no longer needed
        IsBranchingActive = false;
        undoStack.Clear();

        OnBranchingModeChanged?.Invoke(false);
        OnGridChanged?.Invoke();
        Debug.Log("[SudokuGridManager] Branch COMMITTED. Branch merged into main reality.");
    }

    /// <summary>
    /// Cancels the branch: restores PlayerGrid from mainRealityBackup and exits branching mode.
    /// Everything entered during the branch is erased.
    /// </summary>
    public void DiscardBranch()
    {
        if (!IsBranchingActive)
        {
            Debug.LogWarning("[SudokuGridManager] DiscardBranch called while not branching.");
            return;
        }
        if (mainRealityBackup == null)
        {
            Debug.LogError("[SudokuGridManager] mainRealityBackup is null during DiscardBranch — this should never happen.");
            IsBranchingActive = false;
            OnBranchingModeChanged?.Invoke(false);
            return;
        }

        // Deep copy back — writing a new array into PlayerGrid, not sharing the backup reference.
        PlayerGrid        = SudokuEngine.DeepCopy(mainRealityBackup);
        mainRealityBackup = null;
        IsBranchingActive = false;
        undoStack.Clear();

        OnBranchingModeChanged?.Invoke(false);
        OnGridChanged?.Invoke();
        Debug.Log("[SudokuGridManager] Branch DISCARDED. Main reality restored.");
    }

    // ─── Undo ──────────────────────────────────────────────────────────────────

    /// <summary>True when there is at least one player move that can be undone.</summary>
    public bool CanUndo => undoStack.Count > 0;

    /// <summary>Reverts the most recent player move (set or erase). Givens are never affected.</summary>
    public void Undo()
    {
        if (PlayerGrid == null || undoStack.Count == 0) return;

        var (row, col, prev) = undoStack.Pop();
        PlayerGrid[row, col] = prev;
        OnGridChanged?.Invoke();
    }

    // ─── Validation Helpers ────────────────────────────────────────────────────

    public bool IsPuzzleComplete()
    {
        if (PlayerGrid == null || SolutionGrid == null) return false;

        for (int r = 0; r < 9; r++)
            for (int c = 0; c < 9; c++)
                if (PlayerGrid[r, c] != SolutionGrid[r, c]) return false;

        return true;
    }

    /// <summary>Returns true if the value at (row,col) conflicts with any other cell.</summary>
    public bool HasConflict(int row, int col)
    {
        if (PlayerGrid == null) return false;
        int val = PlayerGrid[row, col];
        if (val == 0) return false;

        for (int i = 0; i < 9; i++)
        {
            if (i != col && PlayerGrid[row, i] == val) return true;
            if (i != row && PlayerGrid[i, col] == val) return true;
        }

        int br = (row / 3) * 3, bc = (col / 3) * 3;
        for (int r = br; r < br + 3; r++)
            for (int c = bc; c < bc + 3; c++)
                if ((r != row || c != col) && PlayerGrid[r, c] == val) return true;

        return false;
    }
}
