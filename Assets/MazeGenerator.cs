using System.Linq;
using UnityEngine;

public static class MazeGenerator {
	// -------------------------------------------------------------------------------------------------- //
	// Maze coordinate is (w, h) where w is columns from left to right, and h are rows from top to bottom //
	// -------------------------------------------------------------------------------------------------- //
	
	/*        0 1 2 3 w
	 *      0 +----->
	 *      1 |
	 *      2 |
	 *		3 V
	 * 		h
	 */
	
	public static string[,] GenerateMaze(int width, int height)
	{
		string[,] grid = new string[width, height];

		if (width == 1 && height == 1)
		{
			return new [,] {{""}};
		}
		
		TakeSteps(Random.Range(0, width), Random.Range(0, height), grid);
		return grid;
	}
	
	private static void TakeSteps(int x, int y, string[,] maze)
	{
		var directions = new[] {"N", "S", "E", "W"}.Shuffle();
		
		foreach (var d in directions)
		{
			int nx, ny;
			string opposite;

			switch (d)
			{
				case "N":
					nx = x;
					ny = y - 1;
					opposite = "S";
					break;
				case "S":
					nx = x;
					ny = y + 1;
					opposite = "N";
					break;
				case "E":
					nx = x + 1;
					ny = y;
					opposite = "W";
					break;
				default:
					nx = x - 1;
					ny = y;
					opposite = "E";
					break;
			}
			
			if (nx >= 0 && ny >= 0 && nx < maze.GetLength(0) && ny < maze.GetLength(1) && maze[nx, ny] == null)
			{
				maze[x, y] += d;
				maze[nx, ny] += opposite;
				TakeSteps(nx, ny, maze);
			}
		}
	}
}
