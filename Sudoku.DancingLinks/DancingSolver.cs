using Sudoku.Shared;

namespace Sudoku.DancingLinks
{
    public class DancingSolver :ISudokuSolver
    {
        public SudokuGrid Solve(SudokuGrid s)
    {
        return s.CloneSudoku();
    }

}
}