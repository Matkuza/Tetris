using System.Windows;
using System.Windows.Media;

namespace Tetris;

internal record Tetromino(Point[] Cells, Brush Color, bool IsSquare);
