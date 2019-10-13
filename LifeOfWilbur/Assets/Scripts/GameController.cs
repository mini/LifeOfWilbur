﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Singleton that manages game state.
/// </summary>
[RequireComponent(typeof(LevelTransitionController))]
[RequireComponent(typeof(TimeTravelController))]
[RequireComponent(typeof(LevelReset))]
public class GameController : MonoBehaviour
{
    private const string MENU_SCENE_NAME = "MainMenu";
    private const string END_SCENE_NAME = "ExitScene";

    // TODO(timo): I have defined Level and Room structs for this stuff, so can use that stuff in
    private readonly IReadOnlyDictionary<GameMode, List<string>> GAME_MODE_LEVELS = new Dictionary<GameMode, List<string>>
    {
        [GameMode.Story] = new List<string> 
        {
            "Level1_1",
            "Level1_2",
            "Level1_3",
        },
        [GameMode.SpeedRun] = new List<string>
        {
            "Level1_1",
            "Level1_2",
        },
    };

    private IEnumerator<string> _levelIterator;

    private bool _isTimeTravelling = false;
    private bool _movingToNextLevel = false;

    /// <summary>
    /// The current game mode.
    /// </summary>
    public GameMode CurrentGameMode { get; private set; } = GameMode.NotInGame;
    public string CurrentSceneName { get { return _levelIterator.Current; } }

    void Awake()
    {
        // Don't destroy this object or its immediate children.
        DontDestroyOnLoad(gameObject);

        GetComponent<TimeTravelController>().enabled = false;
        GetComponent<LevelTransitionController>().enabled = true;

        SceneManager.activeSceneChanged += OnSceneLoad;
    }

    void OnSceneLoad(Scene previous, Scene next)
    {
        if(CurrentGameMode.IsInGame())
        {
            _movingToNextLevel = false;
            StartCoroutine(GetComponent<LevelTransitionController>().FadeInFromBlackCoroutine());
            GetComponent<TimeTravelController>().enabled = true;
            GetComponent<TimeTravelController>().RegisterGameObjects();
            GetComponent<TimeTravelController>().UpdateTimeTravelState(true);
            GetComponent<LevelTransitionController>().enabled = true;
        }
        else
        {
            GetComponent<TimeTravelController>().enabled = false;
            GetComponent<LevelTransitionController>().enabled = false;
        }
    }

    void Update()
    {
        // TODO: we should move this to a dedicated "InputController" component
        // along with other input events
        // this is better design(TM)

        if(!CurrentGameMode.IsInGame())
        {
            return;
        }

        if(Input.GetKey(KeyCode.X) && !_isTimeTravelling)
        {
            // User requests TIME TRAVEL.
            // change their time as applicable. The action should not be able to be performed while another time travel event is happening.
            StartCoroutine(DoTimeTravelWithEffect());
        }
    }

    private IEnumerator DoTimeTravelWithEffect()
    {
        _isTimeTravelling = true;
        var transitionController = GetComponent<LevelTransitionController>();
        var timeTravelController = GetComponent<TimeTravelController>();
        yield return StartCoroutine(transitionController.FadeOutToBlack());
        yield return StartCoroutine(timeTravelController.UpdateTimeTravelState(!TimeTravelController.IsInPast));
        yield return StartCoroutine(transitionController.FadeInFromBlackCoroutine());
        _isTimeTravelling = false;
    }

    /// <summary>
    /// Starts a new game. If a game is currently in progress, the game will not be 
    /// </summary>
    /// <param name="gameMode"></param>
    public void StartGame(GameMode gameMode)
    {
        if(!gameMode.IsInGame())
        {
            throw new ArgumentException(nameof(gameMode));
        }

        CurrentGameMode = gameMode;
        _levelIterator = GAME_MODE_LEVELS[gameMode].GetEnumerator();
        NextLevel();
    }

    public void StartGameAt(GameMode gameMode, string sceneName)
    {
        if(!gameMode.IsInGame())
        {
            throw new ArgumentException(nameof(gameMode));
        }

        CurrentGameMode = gameMode;

        _levelIterator = GAME_MODE_LEVELS[gameMode].GetEnumerator();

        while(_levelIterator.MoveNext())
        {
            if(_levelIterator.Current == sceneName)
            {
                SceneManager.LoadScene(sceneName);
                return;
            }
        }

        // Invalid scene.
        throw new ArgumentException(nameof(sceneName));
    }

    public void NextLevel()
    {
        StartCoroutine(NextLevelCoroutine());
    }

    private IEnumerator NextLevelCoroutine()
    {
        if (_movingToNextLevel)
        {
            // don't let this happen multiple times
            // this can happen and would lead to a scene being skipped.
            yield break;
        }

        _movingToNextLevel = true;

        yield return StartCoroutine(GetComponent<LevelTransitionController>().FadeOutToBlack());

        if (_levelIterator.MoveNext())
        {
            Debug.Log($"Loading level: ${_levelIterator.Current}");
            SceneManager.LoadScene(_levelIterator.Current);
        }
        else
        {
            // Going to menu.
            GetComponent<TimeTravelController>().enabled = false;
            GetComponent<LevelTransitionController>().enabled = false;
            CurrentGameMode = GameMode.NotInGame;

            SceneManager.LoadScene(END_SCENE_NAME);
        }
    }

    public void ResetLevel()
    {
        SceneManager.LoadScene(_levelIterator.Current);
    }

    /// <summary>
    /// Returns the game to the main menu.
    /// </summary>
    public void ReturnToMenu()
    {
        SceneManager.LoadScene(MENU_SCENE_NAME);
    }
}
