using System;
using System.Collections;
using UnityEngine;

public class MazeDisplayer : MonoBehaviour
{
	private int _width, _height;
	private GameObject _wall;
	private Mesh _wallMesh;
	private Material _wallMat;
	private Camera _camera;
	// private Cell[,] _maze;
	
	private Vector3 _initCameraAngles;
	private Vector3 _initCameraPosition;

	private Coroutine _drawCoroutine;

	public void Init(int width, int height, GameObject wall, Material wallMat, Camera c)
	{
		StopDrawingMaze();
		
		_width = width;
		_height = height;
		_wall = wall;
		_wallMesh = wall.GetComponent<MeshFilter>().sharedMesh;
		_wallMat = wallMat;
		_camera = c;
		_initCameraAngles = c.transform.eulerAngles;
		_initCameraPosition = c.transform.position;
		
		// _maze = new Cell[width, height];
		// CreateCells();
		
		// transform.localPosition = new Vector3((width - 1) / -2f, (height - 1) / 2f);

		c.cullingMask = 1 << 30;
		
		var localPosition = c.transform.localPosition;
		var newCamPosition = new Vector3(
			localPosition.x + (width - 1) / 2f,
			localPosition.y - (height - 1) / 2f,
			localPosition.z
			);
		c.transform.localPosition = newCamPosition;
	}

	public void StartDrawingMaze(string[,] maze)
	{
		StopDrawingMaze();
		_drawCoroutine = StartCoroutine(KeepDrawingMazes(maze));
	}

	public void StopDrawingMaze()
	{
		if (_drawCoroutine != null)
		{
			StopCoroutine(_drawCoroutine);
		}
	}

	IEnumerator KeepDrawingMazes(string[,] maze)
	{
		while (true)
		{
			DrawMaze(maze);
			yield return null;
		}
	}
	
	private void DrawMaze(string[,] maze)
	{
		const float offset = 0.5f;
		
		for (var h = 0; h < _height; h++)
		{
			for (var w = 0; w < _width; w++)
			{
				if (maze[w, h] == null)
				{
					DrawWall(w, -h + offset, 0);
					DrawWall(w - offset, -h, 90);
					DrawWall(w + offset, -h, 90);
					DrawWall(w, -h - offset, 0);
				}
				else
				{
					if (h == 0 && !maze[w, h].Contains("N"))
                    {
                    	DrawWall(w, -h + offset, 0);
                    }
                    if (w == 0 && !maze[w, h].Contains("W"))
                    {
                    	DrawWall(w - offset, -h, 90);
                    }
                    if (!maze[w, h].Contains("E"))
                    {
                    	DrawWall(w + offset, -h, 90);
                    }
                    if (!maze[w, h].Contains("S"))
                    {
                    	DrawWall(w, -h - offset, 0);
                    }
				}
			}
		}
	}

	private void DrawWall(float w, float h, int rotation)
	{
		_wall.transform.localPosition = new Vector3(w, h);
		_wall.transform.localEulerAngles = new Vector3(0, 0, rotation);
		var m = new Material(_wallMat); // This is just so that the color updates when this updates.

		Graphics.DrawMesh(
			_wallMesh,
			_wall.transform.localToWorldMatrix,
			m,
			30,
			_camera,
			0,
			null,
			false,
			false);
	}
	
	// private void CreateCells()
	// {
	// 	for (int h = 0; h < _height; h++)
	// 	{
	// 		for (int w = 0; w < _width; w++)
	// 		{
	// 			_maze[w, h] = new Cell();
	// 		}
	// 	}
	// }

	// public void FormMaze(string[,] maze)
	// {
	// 	for (int h = 0; h < _height; h++)
	// 	{
	// 		for (int w = 0; w < _width; w++)
	// 		{
	// 			_maze[w, h].walls[2].gameObject.SetActive(!maze[w, h].Contains("E"));
	// 			_maze[w, h].walls[3].gameObject.SetActive(!maze[w, h].Contains("S"));
	// 		}
	// 	}
	// }
	//
	//
	// private void CreateWalls()
	// {
	// 	for (int h = 0; h < _height; h++)
	// 	{
	// 		for (int w = 0; w < _width; w++)
	// 		{
	// 			// Generate north wall if touching the north border
	// 			if (h == 0)
	// 			{
	// 				var northWall = Instantiate(_wall, transform);
	// 				northWall.transform.localPosition = new Vector3(w, -h);
	// 				_maze[w, h].walls[0] = northWall;
	// 			}
	// 			
	// 			// Generate west wall if touching the west border
	// 			if (w == 0)
	// 			{
	// 				var westWall = Instantiate(_wall, transform);
	// 				westWall.transform.localPosition = new Vector3(w, -h);
	// 				westWall.transform.localEulerAngles = new Vector3(0, 0, 90);
	// 				_maze[w, h].walls[1] = westWall;
	// 			}
	// 			
	// 			// Generate east walls
	// 			var eastWall = Instantiate(_wall, transform);
	// 			eastWall.transform.localPosition = new Vector3(w, -h);
	// 			eastWall.transform.localEulerAngles = new Vector3(0, 0, -90);
	// 			_maze[w, h].walls[2] = eastWall;
	// 			// Set west wall of next cell if not on east edge
	// 			if (w < _width - 1) 
	// 				_maze[w + 1, h].walls[1] = eastWall;
	// 			
	// 			// Generate south walls
	// 			var southWall = Instantiate(_wall, transform);
	// 			southWall.transform.localPosition = new Vector3(w, -h);
	// 			southWall.transform.localEulerAngles = new Vector3(0, 0, 180);
	// 			_maze[w, h].walls[3] = southWall;
	// 			// Set north wall of below cell if not on south edge
	// 			if (h < _height - 1) 
	// 				_maze[w, h + 1].walls[0] = southWall;
	// 		}
	// 	}
	// }

	// private class Cell
	// {
	// 	public GameObject[] walls;
	//
	// 	public Cell()
	// 	{
	// 		walls = new GameObject[4];
	// 	}
	// }
}
