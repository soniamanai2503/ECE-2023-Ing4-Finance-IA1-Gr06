using Sudoku.Shared;

namespace Sudoku.DancingLinks;
public class DancingSolver : ISudokuSolver
{
    public SudokuGrid Solve(SudokuGrid s)
    {
        MatrixList dlxList = new MatrixList(s.Cells);
        dlxList.search();
        s.Cells = dlxList.convertMatrixSudoku();
        return s;
    }
}
public class DlxSolver:ISudokuSolver
{
    public SudokuGrid Solve(SudokuGrid s)
    {
        MatrixList dlxList = new MatrixList(s.Cells);
        dlxList.search();
        s.Cells = dlxList.convertMatrixSudoku();
        return s;
    }
}
