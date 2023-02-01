using DlxLib;
using Sudoku.Shared;

namespace Sudoku.DlxLib;
public class DlxLibSolver : ISudokuSolver
{
    public SudokuGrid Solve(SudokuGrid s)
    {
        Dlx.MatrixList dlxList = new Dlx.MatrixList(s.Cells);
        dlxList.search();
        s.Cells = dlxList.convertMatrixSudoku();
        return s;
    }
}
