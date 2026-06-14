using System;
using System.Collections.Generic;

public enum Difficulty { Baslangic, Acemi, Tecrubeli, Uzman, Profesyonel }

/// <summary>
/// Pure C# logic — no MonoBehaviour. Handles board generation, solving, and validation.
/// </summary>
public class SudokuEngine
{
    private static readonly Random Rng = new Random();

    // ─── Public API ────────────────────────────────────────────────────────────

    /// <summary>Generates a fully solved, valid 9x9 board.</summary>
    public int[,] GenerateSolution()
    {
        int[,] grid = new int[9, 9];
        FillGrid(grid);
        return grid;
    }

    /// <summary>
    /// Removes cells from a completed solution to create a playable puzzle.
    /// Guarantees a unique solution via backtracking verification.
    /// </summary>
    public int[,] GeneratePuzzle(int[,] solution, Difficulty difficulty, out bool[,] isGiven)
    {
        int cellsToRemove = difficulty switch
        {
            Difficulty.Baslangic   => 36,
            Difficulty.Acemi       => 42,
            Difficulty.Tecrubeli   => 48,
            Difficulty.Uzman       => 52,
            Difficulty.Profesyonel => 56,
            _                      => 44
        };

        int[,] puzzle = DeepCopy(solution);
        isGiven = new bool[9, 9];

        for (int r = 0; r < 9; r++)
            for (int c = 0; c < 9; c++)
                isGiven[r, c] = true;

        List<int> positions = new List<int>(81);
        for (int i = 0; i < 81; i++) positions.Add(i);
        Shuffle(positions);

        int removed = 0;
        foreach (int pos in positions)
        {
            if (removed >= cellsToRemove) break;

            int row = pos / 9;
            int col = pos % 9;
            int backup = puzzle[row, col];

            puzzle[row, col] = 0;
            isGiven[row, col] = false;

            // Use a copy so CountSolutions doesn't corrupt 'puzzle'
            if (CountSolutions(DeepCopy(puzzle), 0) != 1)
            {
                puzzle[row, col] = backup;
                isGiven[row, col] = true;
            }
            else
            {
                removed++;
            }
        }

        return puzzle;
    }

    /// <summary>Solves the grid in-place via backtracking. Returns true if solvable.</summary>
    public bool Solve(int[,] grid)
    {
        for (int row = 0; row < 9; row++)
        {
            for (int col = 0; col < 9; col++)
            {
                if (grid[row, col] != 0) continue;

                for (int num = 1; num <= 9; num++)
                {
                    if (!IsValid(grid, row, col, num)) continue;
                    grid[row, col] = num;
                    if (Solve(grid)) return true;
                    grid[row, col] = 0;
                }
                return false; // No valid number — backtrack
            }
        }
        return true; // No empty cell found — solved
    }

    /// <summary>Returns true when placing 'num' at (row,col) violates no Sudoku rule.</summary>
    public bool IsValid(int[,] grid, int row, int col, int num)
    {
        for (int i = 0; i < 9; i++)
        {
            if (grid[row, i] == num) return false; // Row check
            if (grid[i, col] == num) return false; // Column check
        }

        int boxRow = (row / 3) * 3;
        int boxCol = (col / 3) * 3;
        for (int r = boxRow; r < boxRow + 3; r++)
            for (int c = boxCol; c < boxCol + 3; c++)
                if (grid[r, c] == num) return false;

        return true;
    }

    // ─── Deep Copy Utilities (public so managers can use them) ─────────────────

    public static int[,] DeepCopy(int[,] source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        int[,] copy = new int[9, 9];
        // Array.Copy handles multidimensional arrays in row-major order
        Array.Copy(source, copy, source.Length);
        return copy;
    }

    public static bool[,] DeepCopy(bool[,] source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        bool[,] copy = new bool[9, 9];
        Array.Copy(source, copy, source.Length);
        return copy;
    }

    // ─── Private Helpers ───────────────────────────────────────────────────────

    private bool FillGrid(int[,] grid)
    {
        for (int row = 0; row < 9; row++)
        {
            for (int col = 0; col < 9; col++)
            {
                if (grid[row, col] != 0) continue;

                foreach (int num in ShuffledNumbers())
                {
                    if (!IsValid(grid, row, col, num)) continue;
                    grid[row, col] = num;
                    if (FillGrid(grid)) return true;
                    grid[row, col] = 0;
                }
                return false;
            }
        }
        return true;
    }

    // Counts solutions up to 2 — anything > 1 means the puzzle is not unique.
    private int CountSolutions(int[,] grid, int count)
    {
        if (count > 1) return count; // Early exit — already non-unique

        for (int row = 0; row < 9; row++)
        {
            for (int col = 0; col < 9; col++)
            {
                if (grid[row, col] != 0) continue;

                for (int num = 1; num <= 9; num++)
                {
                    if (!IsValid(grid, row, col, num)) continue;
                    grid[row, col] = num;
                    count = CountSolutions(grid, count);
                    grid[row, col] = 0;
                    if (count > 1) return count;
                }
                return count; // Dead end — backtrack
            }
        }
        return count + 1; // Reached end with no empty cells — valid solution found
    }

    private List<int> ShuffledNumbers()
    {
        List<int> nums = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        Shuffle(nums);
        return nums;
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Rng.Next(i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}
