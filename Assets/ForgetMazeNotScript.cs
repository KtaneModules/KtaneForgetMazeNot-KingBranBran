using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

public class ForgetMazeNotScript : MonoBehaviour
{
	public KMBombModule module;
#pragma warning disable 108,114
	public KMAudio audio;
#pragma warning restore 108,114
	public KMBossModule boss;
	public KMBombInfo info;
	public MazeDisplayer mazeDisplayer;
	public GameObject mazeLight;

	private static GameObject _light;
	
	public GameObject wall;
	public Material wallMat;
#pragma warning disable 108,114
	public Camera camera;
#pragma warning restore 108,114
	public TextMesh _cellText;
	public TextMesh _stageText;

	public KMSelectable submit;
	public KMSelectable[] arrows;

	private FmnCell _currentCell;
	
	private int _currentStage = 0;
	private int _stages;
	private int _cells;
	private int[] _possibleFactors;
	private int _width;
	private int _height;
	private int totalSolved;
	private int specialCellsRemaining;

	private string[] _ignoredModules;
	private string[] _ignoreModuleListRepo;
	
	private readonly List<string> _solution = new List<string>();
	
	private static int _moduleCounter = 1;
	private int _moduleNumber;

	private bool _solved;
	private bool _init;
	private bool _final;
	private bool _strikeInFinal;
	private bool _interactive;
	private bool arrowsActive;
	private bool submitActive;
	private bool _focused;
	
	private string[,] _maze;
	private readonly List<FmnStage> _stageData = new List<FmnStage>();
	
	// Varibles for testing in unity
	public bool testing;
	public int modules;

	private Coroutine _displayCoroutine;
	private Coroutine _animationCoroutine;

	private const float cameraOffset = 20f;

	private void Awake()
	{
		_cellText.text = "";
		_stageText.text = "";
		
		if (!Application.isEditor)
			testing = false;
	}

	void Start()
	{
		_moduleNumber = _moduleCounter++;

		_ignoreModuleListRepo = boss.GetIgnoredModules(module);
		
		if (_light == null)
		{
			_light = Instantiate(mazeLight);
			_light.GetComponent<Light>().cullingMask = 1 << 30;
			_light.name = "FZNLight";
			float scalar = transform.lossyScale.x;
			_light.GetComponent<Light>().range *= scalar;
		}

		// Need to generate a new material so that there isn't conflict between multiple instances of this module.
		wallMat = new Material(wallMat);
		
		ShowArrows(false);
		submit.gameObject.SetActive(false);
		submitActive = false;
		
		// Setup camera
		var size = (int) Math.Pow(2, 6);
		var renderTexture = new RenderTexture(size, size, 24) {filterMode = FilterMode.Point, isPowerOfTwo = true};
		camera.targetTexture = renderTexture;
		
		var surfaceMat = transform.Find("Static/FixedSurface").GetComponent<MeshRenderer>().material;
		surfaceMat.SetTexture("_MainTex", renderTexture);
		surfaceMat.color = Color.white;

		module.OnActivate += Setup;
		submit.OnInteract += () =>
		{
			SubmitButtonPressed();
			return false;
		};

		for (int i = 0; i < arrows.Length; i++)
		{
			int j = i;
			arrows[j].OnInteract += () =>
			{
				ArrowButtonPressed(j);
				return false;
			};
		}

		GetComponent<KMSelectable>().OnFocus += () => { _focused = true; };
		GetComponent<KMSelectable>().OnDefocus += () => { _focused = false; };
	}

	private int _animationsInProgress = 0;
	IEnumerator DoAnimation(Transform animate, float time,
		Func<float, float, float, float, float> easingMethod,
		Vector3? startPosition = null, Vector3? endPosition = null,
		Vector3? startEulerAngles = null, Vector3? endEulerAngles = null,
		Vector3? startScale = null, Vector3? endScale = null)
	{
		_animationsInProgress++;
		var timer = 0f;
		while (timer < time)
		{
			if (startPosition.HasValue && endPosition.HasValue)
			{
				Vector3 newPosition = new Vector3(
					easingMethod(timer, startPosition.Value.x, endPosition.Value.x, time),
					easingMethod(timer, startPosition.Value.y, endPosition.Value.y, time),
					easingMethod(timer, startPosition.Value.z, endPosition.Value.z, time)
				);
				animate.localPosition = newPosition;
			}
			
			if (startEulerAngles.HasValue && endEulerAngles.HasValue)
			{
				Vector3 newEulerAngles = new Vector3(
					easingMethod(timer, startEulerAngles.Value.x, endEulerAngles.Value.x, time),
					easingMethod(timer, startEulerAngles.Value.y, endEulerAngles.Value.y, time),
					easingMethod(timer, startEulerAngles.Value.z, endEulerAngles.Value.z, time)
				);
				animate.localEulerAngles = newEulerAngles;
			}

			
			if (startScale.HasValue && endScale.HasValue)
			{
				Vector3 newScale = new Vector3(
					easingMethod(timer, startScale.Value.x, endScale.Value.x, time),
					easingMethod(timer, startScale.Value.y, endScale.Value.y, time),
					easingMethod(timer, startScale.Value.z, endScale.Value.z, time)
				);
				animate.localScale = newScale;
			}

			timer += Time.deltaTime;
			yield return null;
		}

		if (endPosition.HasValue)
			animate.localPosition = endPosition.Value;
		
		if (endEulerAngles.HasValue)
			animate.localEulerAngles = endEulerAngles.Value;
		
		if (endScale.HasValue)
			animate.localScale = endScale.Value;
		
		_animationsInProgress--;
	}
	
	private void FixedUpdate()
	{
		if (_init && !testing && !_solved)
		{
			var solvedModules = info.GetSolvedModuleNames();
			int cnt = solvedModules.Count();

			if (cnt != totalSolved)
			{
				var solvedModulesNoIgnore = solvedModules.Count(m => !_ignoredModules.Contains(m));
			
				if (_currentStage != solvedModulesNoIgnore)
				{
					Reset();
					NewStage();
				}

				totalSolved = cnt;
			}
			
		}
	}

	private void Update()
	{
		if (!_focused) return;
		if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) ArrowButtonPressed(0);
		if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) ArrowButtonPressed(1);
		if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) ArrowButtonPressed(2);
		if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) ArrowButtonPressed(3);
		if (Input.GetKeyDown(KeyCode.Space)) SubmitButtonPressed();
	}

	void Setup()
	{
		_ignoredModules = info.GetSolvableModuleNames()
			.Where(m => _ignoreModuleListRepo.Contains(m) || _ignoreModulesList.Contains(m)).ToArray();
		
		if (!testing && _ignoredModules.Length == info.GetSolvableModuleNames().Count)
		{
			_solved = true;
			DebugLog("The maze was so small that the module solved itself.");
			StartCoroutine(WaitABitThenSolve());
			return;
		}

		_init = true;
		
		_stages = testing ? modules : info.GetSolvableModuleNames().Count(m => !_ignoredModules.Contains(m) && !m.Equals("Forget Maze Not"));

		// var multiplier = 3;

		// if (_stages < 2)
		// {
		// 	multiplier = 4;
		// }
		
		// Get good dimensions for a random amount of cells ranging from (stages - stages * multiplier)
		const float minimumRatio = .5f;
		var possibleCellAmounts = Enumerable.Range((int) (_stages * 1.5), _stages + 1).ToArray() // Count is stages * (multiplier - 1) + 1
			.Where(n => n > 3)
			.ToArray()
			.Shuffle()
			.ToList();
			
		for (int i = possibleCellAmounts.Count; i > 0; i--)
		{
			var amount = possibleCellAmounts[0];
			possibleCellAmounts.RemoveAt(0);
			
			var goodFactors = Enumerable.Range(1, amount)
				.Where(n => amount % n == 0 && n <= amount / n && n / ((float) amount / n) >= minimumRatio)
				.ToArray();

			if (goodFactors.Any())
			{
				_possibleFactors = goodFactors;
				_cells = amount;
				break;
			}
			
		}

		DebugLog("There will be {0} {1}, with {2} cells.", _stages, _stages == 1 ? "stage" : "stages", _cells);

		// Randomly assign maze height / width
		var rnd = Random.Range(0, 2);
		_possibleFactors = _possibleFactors.Shuffle();

		var one = _possibleFactors[0];
		var two =_cells / _possibleFactors[0];

		_width = rnd == 0 ? one : two;
		_height = rnd == 0 ? two : one;
		
		// Generate the maze
		_maze = MazeGenerator.GenerateMaze(_width, _height);
		DebugLog("The maze has a size of {0} * {1}", _width, _height);
		DebugLog("Maze:\n{0}", MazeToString(_maze));

		var coordinateList = new List<string>();
		// Get an array of coordinates for maze
		for (int w = 0; w < _width; w++)
		{
			for (int h = 0; h < _height; h++)
			{
				coordinateList.Add(w + " " + h);
			}
		}
		coordinateList = coordinateList.Shuffle();

		var stageCellAmounts = new List<int>();

		var remainingCells = _cells;
		for (int currentStage = 0; currentStage < _stages; currentStage++)
		{
			// Idk why it works but I think this always ends up with nicely spread out number of cells per stage.
			var amount = remainingCells / (_stages - currentStage);
			stageCellAmounts.Add(amount);
			remainingCells -= amount;
		}

		stageCellAmounts = stageCellAmounts.Shuffle();

		// var solutionCellAmount = (_stages - 1) / 5 + 1;
		// var stageNumbers = Enumerable.Range(0, _stages).ToList().Shuffle();
		// List<int> solutionStages = new List<int>();

		// for (var i = 0; i < solutionCellAmount; i++)
		// {
		// 	solutionStages.Add(stageNumbers.First());
		// 	stageNumbers.RemoveAt(0);
		// }

		var solutionCoordinates = GenerateSolution();

		// Generate the stage info
		for (int currentStage = 0; currentStage < _stages; currentStage++)
		{
			var stageCellAmount = stageCellAmounts.First();
			stageCellAmounts.RemoveAt(0);

			// Add the normal / solution cells
			var stageCells = new List<FmnCell>();
			for (int c = 0; c < stageCellAmount; c++)
			{
				var coords = coordinateList[0].Split().Select(int.Parse).ToArray();
				coordinateList.RemoveAt(0);
				var x = coords[0];
				var y = coords[1];
				var ct = solutionCoordinates.Contains(x + y * _width) ? CellType.Solution : CellType.Normal; // Add solution cell if solution stage and no solution cell in SAME STAGE already.
				
				stageCells.Add(new FmnCell(new Vector2(x, y), _maze[x, y], ct));

				if (ct == CellType.Solution)
				{
					_solution.Add(CoordinateToString(new Vector2(x, y)));
				}
			}

			var amountOfSpecialCells = Random.Range(0, 2) == 0 ? 1 : 0;
			
			// Calculate amount of special cells in that stage: 1 / 4 chance for 1, another 1 / 4 chance for 2, etc.
			// for (var n = 0; n < 5; n++)
			// {
			// 	if (Random.Range(0, 4) == 0)
			// 	{
			// 		amountOfSpecialCells++;
			// 		continue;
			// 	}
			// 	
			// 	break;
			// }

			var availableSpecials = Enumerable.Range(0, 5).ToList().Shuffle();
			// var availableSpecials = new List<int>() {2};
			// Randomly add the special stages
			for (var n = 0; n < amountOfSpecialCells; n++)
			{
				var randomSpecial = availableSpecials.First();
				availableSpecials.RemoveAt(0);

				switch (randomSpecial)
				{
					case 0: // Interactive
						var coord1 = new Vector2(Random.Range(0, 7), Random.Range(0, 7));
						stageCells.Add(new FmnCell(coord1));
						break;
					
					case 1: // Wall
						if (currentStage != 0)
						{
							var randomStage = Random.Range(0, currentStage);
							var randomCell = _stageData[randomStage].Cells
								.Where(c => c.CellType == CellType.Solution || c.CellType == CellType.Normal)
								.PickRandom();

							stageCells.Add(new FmnCell(randomCell._coordinate1, randomCell._data, CellType.Wall));
						}
						break;
					case 2: // Coords
						if (currentStage != 0)
						{
							var randomStage = Random.Range(0, currentStage);
							var randomCell = _stageData[randomStage].Cells
								.Where(c => c.CellType == CellType.Solution || c.CellType == CellType.Normal)
								.PickRandom();
							
							var coordArray = Random.Range(0, 2) == 0
								? new[]
								{
									GeneralExtensions.DecimalToArbitrarySystem((long) randomCell._coordinate1.x, 26).Replace('0', 'A'),
									"?"
								}
								: new[]
								{
									"?",
									"" + ((int) randomCell._coordinate1.y + 1)
								};

							stageCells.Add(new FmnCell(randomCell._data, coordArray));
						}
						break;
					case 3: // Maze
						stageCells.Add(new FmnCell(CellType.Maze));
						break;
					case 4: // Negative
						stageCells.Add(new FmnCell(CellType.Negative));
						break;
				}
			}
			
			// Shuffle the cells and add it to the stage list
			_stageData.Add(new FmnStage(stageCells.Shuffle().ToArray())); 
		}
		
		// We are done setting up finally :D
		DebugStageData();
		Init();
	}

	// Generate a solution by wandering through the maze and placing solutions.
	List<int> GenerateSolution() {
		List<int> solutions = new List<int>();
		var solutionCellAmount = (_stages - 1) / 5 + 1;
		int x = Random.Range(0, _width);
		int y = Random.Range(0, _height);

		for (int i = 0; i < solutionCellAmount; i++) {
			int steps = Random.Range(5, 11);
			int solution = TakeSteps(steps, x, y, ' ');
			while (solution < 0) solution = TakeSteps(--steps, x, y, ' ');
			solutions.Add(solution);
			x = solution % _width;
			y = solution / _width;
		}
		return solutions;
	}

	// Recursively find a random amount of moves from a point in the maze so that there is no backtracking.
	int TakeSteps(int amount, int x, int y, char disable) {
		if (amount == 0) return x + _width * y; // We successfully took the amount of steps required!
		var possibleDirections = _maze[x, y].ToList().Shuffle();
		possibleDirections.Remove(disable);

		foreach (var c in possibleDirections) {
			int nx, ny;
			switch (c) {
				case 'N':
					nx = x;
					ny = y-1;
					break;
				case 'S':
					nx = x;
					ny = y+1;
					break;
				case 'E':
					nx = x+1;
					ny = y;
					break;
				default:
					nx = x-1;
					ny = y;
					break;
			}
			int result = TakeSteps(--amount, nx, ny, opposite(c)); // Disable c so we cannot go backwards and count that as a step.
			if (result >= 0) return result; // If we found a result do not take anymore steps.
		}
		return -1; // We hit some kind of dead end so try again sometime before.
	}

	char opposite(char c) {
		switch (c) {
			case 'N':
				return 'S';
			case 'S':
				return 'N';
			case 'E':
				return 'W';
			case 'W':
				return 'E';
			default:
				return ' ';
		}
	}

	// This is to prevent an error from solving right as the bomb starts.
	IEnumerator WaitABitThenSolve()
	{
		yield return null;
		module.HandlePass();
	}

	void DebugStageData()
	{
		for (int i = 0; i < _stageData.Count; i++)
		{
			DebugLog("[STAGE {0}]", i);
			foreach (var cell in _stageData[i].Cells)
			{
				var ct = cell.CellType;
				if (ct == CellType.Normal || ct == CellType.Solution || ct == CellType.Wall || ct == CellType.Coord)
				{
					var ctName = new[] {"Normal", "Green", "Red", "Yellow", "Blue", "Magenta", "Cyan"}[(int) ct];
					var cell1 = cell;
					string walls = ct == CellType.Wall ? new[] {"N", "E", "S", "W"}.Where(d => !cell1._data.Contains(d)).Join("") : cell._data;
					DebugLog("{0}: ({1}) {2}", ctName, ct == CellType.Coord ? cell._coord.Join("") : CoordinateToString(cell._coordinate1), walls);
				}
				else if (ct == CellType.Interactive)
				{
					DebugLog("Red: ({0})", CoordinateToString(cell._coordinate1));
				}
				else
				{
					DebugLog(new[]{"Normal", "Green", "Red", "Yellow", "Blue", "Magenta", "Cyan"}[(int) ct]);
				}
			}
		}
	}

	string CoordinateToString(Vector2 coordinate)
	{
		var firstCoordToLetter = GeneralExtensions.DecimalToArbitrarySystem((long) coordinate.x, 26);
		return firstCoordToLetter.Replace('0', 'A') + ((int) coordinate.y + 1);
	}

	void Init()
	{
		audio.PlaySoundAtTransform("StartUp", transform);
		mazeDisplayer.Init(1, 1, wall, wallMat, camera);
		mazeDisplayer.StartDrawingMaze(new [,] {{""}});
		_animationCoroutine = StartCoroutine(DoAnimation(camera.transform, 1.5f, Easing.InOutCubic,
			new Vector3(0, 0, 0 + cameraOffset), new Vector3(0, 0, -5 + cameraOffset),
			new Vector3(0, 0, -270), new Vector3(0, 0, 0)));
		
		mazeDisplayer.Init(1, 1, wall, wallMat, camera);
		mazeDisplayer.StartDrawingMaze(new [,] {{""}});
		_displayCoroutine = StartCoroutine(DisplayStageInfo());

		var specialStages = _stageData.Select((d, i) => i + "(" + d.Cells
				.Count(c => c.CellType != CellType.Normal && c.CellType != CellType.Solution) + ")")
				.Where(n => int.Parse(n[n.IndexOf("(") + 1].ToString()) != 0).Join();
		
		Debug.LogFormat("Special Stages: {0}", specialStages);

		specialCellsRemaining = _stageData[_currentStage].Cells.Count(c => (int) c.CellType > 1);
	}

	IEnumerator DisplayStageInfo()
	{
		yield return new WaitUntil(() => _animationsInProgress == 0);
		
		var lastTimerDigit1 = info.GetTime() % 10;
		yield return new WaitUntil(() => (int) lastTimerDigit1 != (int) info.GetTime() % 10);

		submit.gameObject.SetActive(true);
		submitActive = true;
		
		_stageText.text = _currentStage + "";
		var currentStageCells = _stageData[_currentStage].Cells;
		while (true)
		{
			for (var i = 0; i < currentStageCells.Length; i++)
			{
				var lastTimerDigit = info.GetTime() % 10;
				var cell = currentStageCells[i];
				_currentCell = cell;
				
				var wallData = "";
				var color = Color.white;
				var text = "";
				var fontSize = 100;

				switch (cell.CellType)
				{
					case CellType.Solution:
					case CellType.Normal:
						wallData = cell._data;
						text = CoordinateToString(cell._coordinate1);
						color = cell.CellType == CellType.Solution ? Color.green : Color.white;
						break;
					
					case CellType.Interactive:
						color = new Color(255, 55, 55, 255) / 255; // A lighter red.
						text = "!";
						break;
					
					case CellType.Wall:
						color = Color.yellow;
						text = CoordinateToString(cell._coordinate1);
						break;
					
					case CellType.Coord:
						color = new Color(55, 55, 255, 255) / 255; // A lighter blue.
						wallData = cell._data;
						text = cell._coord.Join("");
						break;
					
					case CellType.Maze:
					case CellType.Negative:
						color = cell.CellType == CellType.Maze ? Color.magenta : Color.cyan;
						text = "!";
						break;
				}
				
				// I hope to never see the need to have a case for 5 characters...
				switch (text.Length)
				{
					case 3:
						fontSize = 70;
						break;
					case 4:
						fontSize = 55;
						break;
				}
				
				mazeDisplayer.StartDrawingMaze(new [,] {{wallData}});
				_cellText.text = text;
				_cellText.fontSize = fontSize;
				_cellText.color = wallMat.color = color;
				
				yield return new WaitUntil(() => (int) lastTimerDigit != (int) info.GetTime() % 10);
			}
		}
	}

	void NewStage()
	{
		_interactive = false;
		bool badStage = false;
		
		// If there are any special cells still left to do.
		if (specialCellsRemaining > 0)
		{
			DebugLog("You didn't complete all the special cells on stage {0}! Strike.", _currentStage);
			badStage = true;
			module.HandleStrike();
		}
		
		_currentStage++;
		if (_displayCoroutine != null)
			StopCoroutine(_displayCoroutine);

		if (_currentStage == _stages)
		{
			audio.PlaySoundAtTransform("FinalStage", transform);
			FinalStage();
			_final = true;
			DebugLog("Its the final stage! Submit these cells in order: {0}", _solution.Join());
			return;
		}
		if (_displayCoroutine != null)
			StopCoroutine(_displayCoroutine);
		_displayCoroutine = StartCoroutine(DisplayStageInfo());
		
		audio.PlaySoundAtTransform(badStage ? "BadStage" : "Stage", transform);
		
		specialCellsRemaining = _stageData[_currentStage].Cells.Count(c => (int) c.CellType > 1);
	}

	void FinalStage()
	{
		mazeDisplayer.StopDrawingMaze();

		ShowArrows(true);
		submit.gameObject.SetActive(true);
		submitActive = true;
		
		// Only give random coords if the module is entering final mode for the first time.
		if (!_final) 
			_currentPos = new Vector2(Random.Range(0, _width), Random.Range(0, _height));
		
		_cellText.text = CoordinateToString(_currentPos);
		_cellText.color = wallMat.color = Color.white;
		_stageText.text = "";
		_stageText.color = Color.green;
		_strikeInFinal = false;
	}

	private Vector2 _currentPos;
	private string[,] _specialMaze;
	private bool _specialMazeOn;

	IEnumerator SpecialMazeAnimation()
	{
		_animationsInProgress++;
		StartCoroutine(DoAnimation(camera.transform, 1f, Easing.InCubic, new Vector3(0, 0, -5 + cameraOffset), new Vector3(0, 0, 0 + cameraOffset)));
		yield return new WaitUntil(() => _animationsInProgress == 1);
		mazeDisplayer.Init(5, 5, wall, wallMat, camera);
		mazeDisplayer.StartDrawingMaze(_specialMaze);
		StartCoroutine(DoAnimation(camera.transform, 1f, Easing.OutCubic, new Vector3(_currentPos.x, -_currentPos.y, 0 + cameraOffset), new Vector3(_currentPos.x, -_currentPos.y, -5 + cameraOffset)));
		yield return new WaitUntil(() => _animationsInProgress == 1);
		_animationsInProgress--;
	}

	IEnumerator ShowArrowsAfterAnimations() 
	{
		yield return new WaitUntil(() => _animationsInProgress == 0);
		ShowArrows(true);
	}
	
	void SubmitButtonPressed()
	{
		if (!submit.gameObject.activeSelf) return;
		
		audio.PlaySoundAtTransform("Button", transform);
		if (_solved)
			return;
		
		if (testing && (int) _currentCell.CellType < 2 && !_final)
		{
			NewStage();
			return;
		}

		if (!_final && (int) _currentCell.CellType > 1 && !_interactive)
		{
			StopCoroutine(_displayCoroutine);
			_interactive = true;
			_lastMinigame = _currentCell.CellType;
			
			audio.PlaySoundAtTransform("Special", transform);
			
			switch (_currentCell.CellType)
			{
				case CellType.Interactive:
					_currentPos = new Vector2(Random.Range(0, 7), Random.Range(0, 7));
					_stageText.text = CoordinateToString(_currentCell._coordinate1);
					_cellText.text = CoordinateToString(_currentPos);
					break;
				case CellType.Wall:
					break;
				case CellType.Coord:
					_stageText.text = _currentCell._coord[0].Equals("?") ? "A" : "0";
					break;
				case CellType.Maze:
					submit.gameObject.SetActive(false);
					submitActive = false;
					_specialMazeOn = true;
					_currentPos = new Vector2(Random.Range(0, 5), Random.Range(0, 5));
					_stageText.text = "";
					_cellText.text = "■";
					_specialMaze = MazeGenerator.GenerateMaze(5, 5);
					CreateExitInMaze();
					DebugLog("You generated a maze:\n{0}", MazeToString(_specialMaze));
					mazeDisplayer.StartDrawingMaze(new string[1,1]);
					StartCoroutine(SpecialMazeAnimation());
					break;
				case CellType.Negative:
					submit.gameObject.SetActive(false);
					submitActive = false;
					_specialMazeOn = true;
					_currentPos = new Vector2(Random.Range(0, 5), Random.Range(0, 5));
					_stageText.text = "";
					_cellText.text = "■";
					_specialMaze = MazeGenerator.GenerateMaze(5, 5);
					CreateExitInMaze();
					InvertMaze();
					DebugLog("You generated an inverted maze:\n{0}", MazeToString(_specialMaze));
					mazeDisplayer.StartDrawingMaze(new string[1,1]);
					StartCoroutine(SpecialMazeAnimation());
					break;
			}

			StartCoroutine(ShowArrowsAfterAnimations());
			return;
		}

		if (_interactive)
		{
			var success = false;
			var msg = "";
			var msg2 = "";
			
			submit.gameObject.SetActive(false);
			submitActive = false;

			switch (_currentCell.CellType)
			{
				case CellType.Interactive:
					msg = "Red";
					success = _currentPos == _currentCell._coordinate1;
					msg2 = String.Format("Looking for {0}, but got {1}", CoordinateToString(_currentCell._coordinate1), CoordinateToString(_currentPos));
					break;
				
				case CellType.Wall:
					msg = "Yellow";
					// Really long stupid statement that basically checks if the arrow states on the module are equal to the state wanted.
					var directions = new[] {"N", "E", "S", "W"};
					success = arrows.Select((a, i) => a.GetComponent<SpriteRenderer>().color == Color.yellow ? i + 1 : -(i + 1))
						.All(n => n < 0
							? _currentCell._data.Contains(directions[-(n + 1)])
							: !_currentCell._data.Contains(directions[n - 1]));

					var expected = directions.Where(d => !_currentCell._data.Contains(d)).Join("");
					var playerInput = arrows.Select((a, i) => a.GetComponent<SpriteRenderer>().color == Color.yellow ? i + 1 : -(i + 1)).Where(n => n >= 0).Select(n => directions[n - 1]).Join("");
					msg2 = String.Format("Looking for {0}, but got {1}", expected, playerInput);
					break;
				case CellType.Coord:
					int number;
					Vector2 inputCoord;
					var isLetterCoord = !int.TryParse(_stageText.text, out number);
					if (isLetterCoord)
					{
						number = (int) GeneralExtensions.BackToDecimal(_stageText.text) - 1;
						inputCoord = new Vector2(number, int.Parse(_currentCell._coord[1]) - 1);
					}
					else
					{
						number -= 1; // Put the number into zero index.
						inputCoord = new Vector2(GeneralExtensions.BackToDecimal(_currentCell._coord[0]) - 1, number);
					}

					bool badCoords = inputCoord.x > _width - 1 || inputCoord.y > _height - 1 || inputCoord.x < 0 || inputCoord.y < 0;

					string coordWalls = "WHAT?!";
					if (!badCoords)
					{
						coordWalls = _maze[(int) inputCoord.x, (int) inputCoord.y];
					}
					
					success = !badCoords && _currentCell._data.All(c => coordWalls.Contains(c)) &&
					          _currentCell._data.Length == coordWalls.Length;
					msg = "Blue";
					msg2 = String.Format("Looking for {0}, but got {1} ({2})", _currentCell._data, coordWalls, CoordinateToString(inputCoord));
					break;
				case CellType.Maze:
					msg = "Magenta";
					msg2 = "You do not submit anything in a Magenta cell...";
					break;
				case CellType.Negative:
					msg = "Cyan";
					msg2 = "You do not submit anything in a Cyan cell...";
					break;
			}

			if (!success)
			{
				audio.PlaySoundAtTransform("Strike", transform);
				DebugLog("Whoops! You submitted the wrong answer on the {0} cell in stage {1}. ({2})", msg, _currentStage, msg2);
				module.HandleStrike();
			}
			else
			{
				_minigameSolve = true;
				audio.PlaySoundAtTransform("Good", transform);
			}

			specialCellsRemaining--;
			Reset();
		}

		if (_final)
		{
			if (_solution.First().Equals(_cellText.text))
			{
				_solution.RemoveAt(0);
				if (_solution.Any())
					audio.PlaySoundAtTransform("Good", transform);
				DebugLog("You submitted {0} which is correct.", _cellText.text);
			}
			else
			{
				module.HandleStrike();
				_strikeInFinal = true;
				audio.PlaySoundAtTransform("Strike", transform);
				DebugLog("You submitted {0} which is incorrect. Strike!", _cellText.text);
				DisplayHints();
			}

			if (!_solution.Any())
			{
				DebugLog("You submitted everything! Module solved!");
				module.HandlePass();
				audio.HandlePlaySoundAtTransform("Solve", transform);
				ShowArrows(false);
				submit.gameObject.SetActive(false);
				submitActive = false;
				_stageText.text = "";
				_cellText.text = "";
				_solved = true;
				StartCoroutine(SolveAnimation());
			}
		}
	}

	IEnumerator SolveAnimation()
	{
		mazeDisplayer.Init(_width, _height, wall, wallMat, camera);
		mazeDisplayer.StartDrawingMaze(_maze);

		var goalx = (_width - 1) / 2f;
		var goaly = (_height - 1) / 2f;

		var duration = 3.9f;

		StartCoroutine(DoAnimation(camera.transform, duration, Easing.InOutSine, new Vector3(goalx, -goaly, 0 + cameraOffset),
			new Vector3(goalx, -goaly, -0.25f * _stages - 10 + cameraOffset) ));

		float time = 0f;
		var h = (float) Random.Range(0, 256);
		while (time < duration)
		{
			time += Time.deltaTime;
			time %= 5f * 2;
			
			wallMat.color = Color.HSVToRGB((h += Time.deltaTime * 5) % 255 / 255, 130 / 255f, 1);
			h %= 255;
			yield return null;
		}
		
		mazeDisplayer.StopDrawingMaze();
		// ReSharper disable once IteratorNeverReturns
	}

	private bool _disableArrows = false;
	private Coroutine _arrowCoroutine;
	void ArrowButtonPressed(int arrowNumber)
	{
		if (!transform.Find("Arrows").gameObject.activeSelf) return;

		audio.PlaySoundAtTransform("Button", transform);
		if (_solved || _disableArrows)
			return;
		
		if (_interactive && !_solved)
		{
			var strike = false;
			var x = -1;
			var y = -1;
			var nx = -1;
			var ny = -1;
			var ns = "";

			switch (_currentCell.CellType)
			{
				case CellType.Interactive:
				case CellType.Maze:
				case CellType.Negative:
				{
					x = (int) _currentPos.x;
					y = (int) _currentPos.y;
					
					var newCoord = GetNewCoord(arrowNumber);
					nx = newCoord[0];
					ny = newCoord[1];

					var maze = _currentCell.CellType == CellType.Interactive ? ActualInteractiveMaze : _specialMaze;

					if (_currentCell.CellType != CellType.Negative)
					{
						if (!maze[(int) _currentPos.x, (int) _currentPos.y].Contains(new[] {"N", "E", "S", "W"}[arrowNumber]))
							strike = true;
					}
					else if (maze[(int) _currentPos.x, (int) _currentPos.y]
						.Contains(new[] {"N", "E", "S", "W"}[arrowNumber]))
					{
						strike = true;
					}
						
					break;
				}
				case CellType.Coord:
				{
					var coordIsLetter = _currentCell._coord[0].Equals("?");
					var characters = coordIsLetter ? "ABCDEFGHIJKLMNOPQRSTUVWXYZ" : "0123456789";
					var characterArray = characters.Select(c => c.ToString()).ToArray();

					var displayNumbers = _stageText.text.Select(c => c.ToString()).ToArray();
					var number = coordIsLetter
						? displayNumbers[displayNumbers.Length - 1][0] - 65
						: int.Parse(displayNumbers[displayNumbers.Length - 1]);
					var arrayLength = characterArray.Length;
					
					switch (arrowNumber)
					{
						case 0: // Up
							displayNumbers[displayNumbers.Length - 1] = 
								characterArray[(number + 1) % arrayLength]; // Set last number to + 1;
							ns = displayNumbers.Join("");
							break;
						case 1: // Right
							ns = _stageText.text;
							ns = ns.Length >= 7 ? ns : ns += characterArray[0];
							break;
						case 2: // Down
							displayNumbers[displayNumbers.Length - 1] =
								characterArray[(number + arrayLength - 1) % arrayLength]; // Set last number to - 1;
							ns = displayNumbers.Join("");
							break;
						case 3: // Left
							ns = _stageText.text;
							ns = ns.Length <= 1 ? ns : ns.Substring(0, ns.Length - 1);
							break;
					}

					_stageText.text = ns;
					return;
				}
				case CellType.Wall:
				{
					var sr = arrows[arrowNumber].transform.GetComponent<SpriteRenderer>();
					sr.color = sr.color == Color.yellow ? Color.white : Color.yellow;
					return;
				}
			}

			if (strike)
			{
				var color = "";
				switch (_currentCell.CellType)
				{
					case CellType.Interactive:
						color = "Red";
						break;
					case CellType.Maze:
						color = "Magenta";
						break;
					case CellType.Negative:
						color = "Cyan";
						break;
				}
				
				var directions = new [] {"North", "East", "South", "West"};
				DebugLog("Ouch! While doing a {0} cell, you hit a wall trying to go {1} at {2}", color, directions[arrowNumber], CoordinateToString(_currentPos));
				audio.PlaySoundAtTransform("Strike", transform);
				module.HandleStrike();
				specialCellsRemaining--;
				StartCoroutine(WaitForAnimationThenReset());
				return;
			}
			
			_currentPos = new Vector2(nx, ny);
			
			if (_currentCell.CellType != CellType.Interactive)
			{
				if (_animationCoroutine != null)
				{
					StopCoroutine(_animationCoroutine);
					_animationsInProgress = 0;
				}
				
				_animationCoroutine = StartCoroutine(DoAnimation(camera.transform, .2f, Easing.OutCubic, new Vector3(x, -y, -5 + cameraOffset), new Vector3(nx, -ny, -5 + cameraOffset)));
				
				if (_currentPos.x < 0 || _currentPos.x > 4 || _currentPos.y < 0 || _currentPos.y > 4) // Check if they escaped the maze
				{
					_disableArrows = true;
					_minigameSolve = true;
					audio.PlaySoundAtTransform("Good", transform);
					specialCellsRemaining--;
					StartCoroutine(WaitForAnimationThenReset());
				}
			}
			else
			{
				_cellText.text = CoordinateToString(_currentPos);
			}
		}

		if (_final)
		{
			if (_strikeInFinal)
				FinalStage();
			
			int nx, ny;
			var newCoord = GetNewCoord(arrowNumber);
			nx = newCoord[0];
			ny = newCoord[1];

			var directions = new[] {"N", "E", "S", "W"};
			
			if (!_maze[(int) _currentPos.x, (int) _currentPos.y].Contains(directions[arrowNumber]))
			{
				audio.PlaySoundAtTransform("Strike", transform);
				module.HandleStrike();
				_strikeInFinal = true;
				DebugLog("You ran into a wall trying to go {0} at {1}", new[] {"North", "East", "South", "West"}[arrowNumber], CoordinateToString(_currentPos));
				DisplayHints();
				return;
			}
			
			_currentPos = new Vector2(nx, ny);
			_cellText.text = CoordinateToString(_currentPos);
		}
	}

	IEnumerator WaitForAnimationThenReset()
	{
		yield return new WaitUntil(() => _animationsInProgress == 0);
		Reset();
	}

	void DisplayHints()
	{
		var walls = _maze[(int) _currentPos.x, (int) _currentPos.y];
		mazeDisplayer.StartDrawingMaze(new[,] {{walls}});
		_stageText.text = _solution.First();
	}

	private int[] GetNewCoord(int arrowNumber)
	{
		int nx, ny;
		
		switch (arrowNumber)
		{
			case 0: // North
				nx = (int) _currentPos.x;
				ny = (int) _currentPos.y - 1;
				break;
			case 1: // East
				nx = (int) _currentPos.x + 1;
				ny = (int) _currentPos.y;
				break;
			case 2: // South
				nx = (int) _currentPos.x;
				ny = (int) _currentPos.y + 1;
				break;
			case 3: // West
				nx = (int) _currentPos.x - 1;
				ny = (int) _currentPos.y;
				break;
			default:
				nx = (int) _currentPos.x;
				ny = (int) _currentPos.y;
				break;
		}

		return new[] {nx, ny};
	}

	void CreateExitInMaze()
	{
		var minDistance = Vector2.Distance(new Vector2(0, 0), new Vector2(2, 2));
			
		var possibleCells = Enumerable.Range(0, 25).Select(x => new Vector2(x % 5, x / 5))
			.Where(v => Vector2.Distance(v, _currentPos) >= minDistance && new[] {(int) v.x, (int) v.y}.Any(c => c == 0 || c == 4))
			.ToArray().Shuffle();

		var chosenCell = possibleCells.First();
		var possibleWalls = "";

		switch ((int) chosenCell.x)
		{
			case 0:
				possibleWalls += "W";
				break;
			case 4:
				possibleWalls += "E";
				break;
		}
		
		switch ((int) chosenCell.y)
		{
			case 0:
				possibleWalls += "N";
				break;
			case 4:
				possibleWalls += "S";
				break;
		}
		
		var chosenWall = possibleWalls.PickRandom();
		_specialMaze[(int) chosenCell.x, (int) chosenCell.y] += chosenWall;
	}

	void InvertMaze()
	{
		for (int x = 0; x < 5; x++)
		{
			for (int y = 0; y < 5; y++)
			{
				var directions = new[] {'N', 'E', 'S', 'W'};
				_specialMaze[x, y] = directions.Where(d => !_specialMaze[x, y].Contains(d.ToString())).Join("");
			}
		}
	}

	void Reset()
	{
		ShowArrows(false);
		_disableArrows = false;

		if (_specialMazeOn)
		{
			mazeDisplayer.Init(1, 1, wall, wallMat, camera);
			camera.transform.localPosition = new Vector3(0, 0, -5 + cameraOffset);
			_specialMazeOn = false;
		}
		
		// Since yellow changes colors of arrows, we need to reset them.
		foreach (var arrow in arrows)
		{
			arrow.GetComponent<SpriteRenderer>().color = Color.white;
		}

		// Remove from the stage list even if they striked.
		_stageData[_currentStage].Cells = _stageData[_currentStage].Cells
			.Where(c => c.CellType != _currentCell.CellType).ToArray();

		_interactive = false;
		
		if (_displayCoroutine != null)
			StopCoroutine(_displayCoroutine);
		_displayCoroutine = StartCoroutine(DisplayStageInfo());
	}

	void ShowArrows(bool show)
	{
		transform.Find("Arrows").gameObject.SetActive(show);
		arrowsActive = show;
	}
	
	private void DebugLog(string log, params object[] args)
	{
		var logData = string.Format(log, args);
		Debug.LogFormat("[Forget Maze Not #{0}] {1}", _moduleNumber, logData);
	}

	private string MazeToString(string[,] maze)
	{
		var mazeString = "";
		for (var h = 0; h < maze.GetLength(1); h++)
		{
			for (var w = 0; w < maze.GetLength(0); w++)
			{
				mazeString += maze[w, h] + " ";
			}

			mazeString += "\n";
		}

		return mazeString;
	}

#pragma warning disable 414
	string TwitchHelpMessage = "Use '!{0} [color]' to press the middle button on that color. Use '!{0} [directions]' to press the buttons, using 'M' as the middle button. You can chain the commands using space. Example: '!{0} cyan UDRL NSEW M'";
#pragma warning restore 414

	private bool _minigameSolve = false;
	private CellType _lastMinigame;
	
	IEnumerator ProcessTwitchCommand(string command)
	{
		var colors = new[] {"red", "yellow", "blue", "magenta", "cyan", "white", "green"};
		var directions = "nsewudrlm";
		
		var split = command.ToLowerInvariant().Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);

		if (split.All(s => colors.Contains(s) || s.All(c => directions.Contains(c.ToString()))))
		{
			yield return null;

			const float pauseTime = .1f;

			foreach (var s in split)
			{
				if (colors.Contains(s))
				{
					CellType targetCellType;

					if (_interactive)
					{
						var message = "in interactive mode";
						if (_final)
							message = "in the final stage";	
						
						yield return string.Format("sendtochaterror You cannot use colors {0}! Use 'M' instead!", message);
						yield break;
					}

					switch (s)
					{
						case "red":
							targetCellType = CellType.Interactive;
							break;
						case "yellow":
							targetCellType = CellType.Wall;
							break;
						case "blue":
							targetCellType = CellType.Coord;
							break;
						case "magenta":
							targetCellType = CellType.Maze;
							break;
						case "cyan":
							targetCellType = CellType.Negative;
							break;
						case "green":
							targetCellType = CellType.Solution;
							break;
						default:
							targetCellType = CellType.Normal;
							break;
					}

					do
					{
						yield return "trycancel";
					} 
					while (_currentCell.CellType != targetCellType || !submitActive);

					SubmitButtonPressed();
					yield return new WaitForSeconds(pauseTime);
				}
				else
				{
					if (!_interactive && !_final)
					{
						yield return "sendtochaterror You cannot use directions outside of interactive mode! Press a color first!";
						yield break;
					}
					
					yield return new WaitUntil(() => _animationsInProgress == 0 && arrowsActive);

					foreach (var c in s)
					{
						yield return "trycancel";
						
						switch (c.ToString())
						{
							case "n":
							case "u":
								ArrowButtonPressed(0);
								break;
							case "e":
							case "r":
								ArrowButtonPressed(1);
								break;
							case "s":
							case "d":
								ArrowButtonPressed(2);
								break;
							case "w":
							case "l":
								ArrowButtonPressed(3);
								break;
							default:
								SubmitButtonPressed();
								break;
						}
						
						yield return new WaitForSeconds(pauseTime);
					}
					
					if (_minigameSolve)
					{
						var points = new[] {0, 0, 2, 1, 1, 3, 3};
						yield return "awardpoints " + points[(int) _lastMinigame];
						_minigameSolve = false;
					}
				}
			}
		}
	}

	IEnumerator TwitchHandleForcedSolve()
	{
		if (_solved)
			yield break;
		
		StopAllCoroutines();
		DebugLog("Module forced solve.");
		module.HandlePass();
		audio.HandlePlaySoundAtTransform("Solve", transform);
		ShowArrows(false);
		submit.gameObject.SetActive(false);
		submitActive = false;
		_stageText.text = "";
		_cellText.text = "";
		_solved = true;
		StartCoroutine(SolveAnimation());
		yield return null;
	}

	private class FmnStage
	{
		public FmnCell[] Cells { get; set; }

		public FmnStage(FmnCell[] cells)
		{
			Cells = cells;
		}
	}
	
	private class FmnCell
	{
		public CellType CellType { get; set; }
		public Vector2 _coordinate1 { get; set; } // Used by Interactive
		public string _data { get; set; } // Used by Normal, Solution, Wall
		public string[] _coord; // Used by Coord

		// Normal, Solution, Wall
		public FmnCell(Vector2 coordinate1, string data, CellType cellType)
		{
			_coordinate1 = coordinate1;
			_data = data;
			CellType = cellType;
		}

		// Interactive
		public FmnCell(Vector2 coordinate1)
		{
			_coordinate1 = coordinate1;
			CellType = CellType.Interactive;
		}

		// Coord
		public FmnCell(string data, string[] coord)
		{
			CellType = CellType.Coord;
			_data = data;
			_coord = coord;
		}

		// Maze, Negative
		public FmnCell(CellType cellType)
		{
			CellType = cellType;
		}
	}
	
	private enum CellType
	{
		Normal,
		Solution,
		Interactive,
		Wall,
		Coord,
		Maze,
		Negative
	}

	// I switched the coordinates (dangit bran)
	private static readonly string[,] InteractiveMaze =
	{
		{"ES", "WE", "WES", "WE", "WS", "SE", "WS"},
		{"NS", "S", "NE", "WS", "NE", "WN", "NS"},
		{"NS", "NE", "EW", "NW", "ES", "EWS", "NW"},
		{"NE", "WE", "WE", "WS", "NS", "NE", "W"},
		{"SE", "WE", "WS", "NS", "NS", "SE", "WS"},
		{"NE", "WS", "NSE", "WN", "NE", "WN", "NS"},
		{"E", "NW", "N", "E", "WE", "WE", "NW"},
	};

	private static readonly string[,] ActualInteractiveMaze = FindActualInteractiveMaze();

	// big brain fix
	private static string[,] FindActualInteractiveMaze()
	{
		var maze = new string[7, 7];
		for (var i = 0; i < 7; i++)
		{
			for (var j = 0; j < 7; j++)
			{
				maze[i, j] = InteractiveMaze[j, i];
			}
		}

		return maze;
	}
	
	private readonly string[] _ignoreModulesList =
	{
		"14",
		"42",
		"501",
		"A>N<D",
		"Bamboozling Time Keeper",
		"Brainf---",
		"Busy Beaver",
		"Don't Touch Anything",
		"Forget Any Color",
		"Forget Enigma",
		"Forget Everything",
		"Forget Infinity",
		"Forget It Not",
		"Forget Me Later",
		"Forget Me Not",
		"Forget Perspective",
		"Forget The Colors",
		"Forget Them All",
		"Forget This",
		"Forget Us Not",
		"Iconic",
		"Keypad Directionality",
		"Kugelblitz",
		"Multitask",
		"OmegaDestroyer",
		"OmegaForget",
		"Organization",
		"Password Destroyer",
		"Purgatory",
		"RPS Judging",
		"Security Council",
		"Shoddy Chess",
		"Simon Forgets",
		"Simon's Stages",
		"Souvenir",
		"Tallordered Keys",
		"The Time Keeper",
		"The Troll",
		"The Twin",
		"The Very Annoying Button",
		"Timing is Everything",
		"Turn The Key",
		"Ultimate Custom Night",
		"Übermodule",
		"Whiteout",
		"Forget Maze Not"
	};
}
