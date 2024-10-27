using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Random = UnityEngine.Random;

public class MatchResult
{
    public List<Symbol> connectedSymbols;
    public MatchDirection direction;
}

public enum MatchDirection
{
    None,
    Vertical,
    Horizontal,
    LongVertical,
    LongHorizontal,
    Super
}

public enum BoardState
{
    Idle,
    ProcessingMove
}

public class Board : SingletonMonoBehaviour<Board>
{
    [Header("References")]
    public Symbol symbolPrefab;
    public Transform symbolsParent;
    public GameObject boardObject;

    [Header("Settings")]
    public int width = 6;
    public int height = 8;
    public int maxTriesToGenerateBoard = 100;
    public float symbolSwapDuration = 0.2f;
    public float delayBetweenMatchProcessing = 0.4f;
    public float extraConnectionMinLength = 2;
    public float threeMatchMultiplier = 1f;
    public float longMatchMultiplier = 1.5f;
    public int cascadeMaxChain = 3;
    public ArrayLayout arrayLayout;

    [Header("Audio")]
    public float matchPitchVariation = 0.1f;
    public AudioClip swapSound;
    public AudioClip selectSound;
    public AudioClip regularMatchSound;
    public AudioClip cascadeOneMatchSound;
    public AudioClip cascadeTwoMatchSound;
    public AudioClip cascadeThreeMatchSound;
    public AudioClip cascadeFourMatchSound;
    public AudioClip ultraCascadeJingleSound;

    [Header("Debug")]
    [SerializeField, ReadOnly] private BoardState state;
    [SerializeField, ReadOnly] private Symbol selectedSymbol;
    [SerializeField, ReadOnly] private List<Symbol> symbolsThatMatched = new();
    [SerializeField, ReadOnly] private int cascadeChainCount = 0;

    private Tile[,] board;
    private float spacingX;
    private float spacingY;

    public BoardState State => state;

    //public static Board Instance { get; private set; }

    //private void OnEnable()
    //{
    //    GameManager.onStageLoaded += OnStageLoaded;
    //}
    //private void OnDisable()
    //{
    //    GameManager.onStageLoaded -= OnStageLoaded;
    //}

    public void OnStageLoaded()
    {
        InitializeBoard();
        CreateBoardWithNoMatches();
    }

    private void Start()
    {
        InitializeBoard();
        CreateBoardWithNoMatches();
    }

    public void InitializeBoard()
    {
        state = BoardState.Idle;
        symbolsThatMatched.Clear();
    }

    public void CreateBoardWithNoMatches()
    {
        // try to generate a board with no matches on start
        int triesToGenerateBoard = 0;
        while (triesToGenerateBoard < maxTriesToGenerateBoard)
        {
            // generate new board
            ClearBoard();
            GenerateBoard();
            triesToGenerateBoard++;

            // this board doesn't contain any match, stop generating
            if (!BoardContainsMatch())
            {
                break;
            }

            // matches were found, regenerate
            //Debug.Log("Found matches when generating the board, regenerating new board.");
        }
    }

    private void Update()
    {
        // TODO: convert to swiping, and refactor to input script

        // run logic only while in playing state
        if (GameManager.Instance.State != GameState.Playing)
            return;

        // detected player click
        // that is not blocked by a UI element
        if (Input.GetMouseButtonDown(0) &&
            !EventSystem.current.IsPointerOverGameObject())
        {
            // send a raycast
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction);

            // we clicked on a symbol
            if (hit.collider != null && hit.collider.GetComponentInParent<Symbol>())
            {
                // select
                Symbol clickedSymbol = hit.collider.GetComponentInParent<Symbol>();
                OnSymbolClicked(clickedSymbol);
            }
        }
    }

    #region BOARD
    private void ClearBoard()
    {
        // destroy existing symbols
        DestroyAllSymbols();

        // if the board doesn't exist yet no need to clear
        if (board == null)
            return;

        // remove all the symbols from all tiles
        foreach (Tile tile in board)
        {
            if (!tile.IsEmpty())
            {
                // remove the symbol from its tile
                Symbol symbolToRemove = tile.symbol;
                Vector2Int symbolTileIndices = symbolToRemove.GetIndices();
                RemoveSymbolAt(symbolTileIndices);
            }
        }
    }

    private void DestroyAllSymbols()
    {
        // destroy all the spawned symbols
        // all the spawned symbols are under the symbols parent
        foreach (Transform symbol in symbolsParent)
            Destroy(symbol.gameObject);
    }

    private void GenerateBoard()
    {
        // create a new empty board of chosen size
        board = new Tile[width, height];
        
        // calculate spacing between tiles
        spacingX = (float)(width - 1) / 2;
        spacingY = (float)(height - 1) / 2 + 1;

        // generate symbols inside the board
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // generate usable tiles
                // (unticked in inspector)
                if (arrayLayout.rows[y].row[x] == false)
                {
                    // get random symbol
                    SymbolData symbolData = GetRandomSymbol();

                    // spawn the symbol
                    Vector2Int symbolIndices = new(x, y);
                    Vector2 spawnPosition = new(x - spacingX, y - spacingY);
                    Symbol symbolInstance = SpawnSymbol(symbolIndices, spawnPosition, symbolData);

                    // create a new tile in the board for this symbol
                    board[x, y] = new Tile(true, symbolInstance);
                }
                // generate unusable tiles
                // (ticked in inspector)
                else
                {
                    board[x, y] = new Tile(false, null);
                }
            }
        }

        //Debug.Log("Generated new board.");
    }

    private SymbolData GetRandomSymbol()
    {
        // pick a symbol among the current stage's list of symbols
        var stageSymbols = GameManager.Instance.GetCurrentStage().stageSymbols;
        int randomIndex = Random.Range(0, stageSymbols.Length);
        return stageSymbols[randomIndex];
    }

    private Symbol SpawnSymbol(Vector2Int indices, Vector2 spawnPosition, SymbolData symbolData)
    {
        Symbol symbolInstance = Instantiate(symbolPrefab, spawnPosition, Quaternion.identity);
        symbolInstance.SetData(symbolData);
        symbolInstance.SetIndices(indices);
        symbolInstance.gameObject.name = symbolData.name;
        symbolInstance.transform.SetParent(symbolsParent);
        return symbolInstance;
    }
    #endregion

    #region MATCHING
    public bool BoardContainsMatch()
    {
        //Debug.Log("Checking for matches...");
        bool hasMatched = false;

        // clear the matched flag for all tiles
        foreach (Tile tile in board)
        {
            if (!tile.IsEmpty())
                tile.symbol.SetIsMatched(false);
        }

        // clear the list of symbols that matched
        // so we can get a new clean list
        symbolsThatMatched.Clear();

        // check for matches
        for (int x = 0; x < width; x++)
        {
            for(int y = 0; y < height; y++)
            {
                // check if usable tile
                if (!board[x, y].isUsable)
                    continue;

                // get the symbol in this tile
                Symbol symbol = board[x, y].symbol;

                // check if it's a valid symbol
                if (symbol == null)
                    continue;

                // check if it's not already matched
                if (symbol.IsMatched)
                    continue;

                // run some matching logic
                MatchResult matchResult = CheckForMatch(symbol);

                // we found a match
                if (matchResult.connectedSymbols.Count >= 3)
                {
                    // check for complex matching (supers etc.)
                    MatchResult superMatchResult = CheckForSuperMatch(matchResult);

                    // we found a super match
                    if (superMatchResult != null)
                    {
                        // replace the match result with the super match
                        matchResult = superMatchResult;
                    }

                    // add the connected symbols to the list of symbols to consume
                    symbolsThatMatched.AddRange(matchResult.connectedSymbols);

                    // mark connected symbols as matched to avoid using them again for matching logic
                    foreach (Symbol connectedSymbol in matchResult.connectedSymbols)
                        connectedSymbol.SetIsMatched(true);

                    hasMatched = true;
                }
            }
        }

        return hasMatched;
    }

    private MatchResult CheckForMatch(Symbol symbol)
    {
        List<Symbol> connectedSymbols = new();
        SymbolData symbolType = symbol.Data;

        // read our initial symbol
        connectedSymbols.Add(symbol);

        // check for horizontal connections
        CheckDirection(symbol, Vector2Int.right, connectedSymbols);
        CheckDirection(symbol, Vector2Int.left, connectedSymbols);
        // found horizontal match (3)
        if (connectedSymbols.Count == 3)
        {
            //Debug.Log($"Normal horizontal 3 match. Type: {connectedSymbols[0].Data.name}");
            return new MatchResult
            {
                connectedSymbols = connectedSymbols,
                direction = MatchDirection.Horizontal
            };
        }
        // found long horizontal match (> 3)
        else if (connectedSymbols.Count > 3)
        {
            //Debug.Log($"Long horizontal match. Type: {connectedSymbols[0].Data.name}");
            return new MatchResult
            {
                connectedSymbols = connectedSymbols,
                direction = MatchDirection.LongHorizontal
            };
        }

        // clear out the connected symbols to avoid accidental matches
        connectedSymbols.Clear();
        // read our initial symbol
        connectedSymbols.Add(symbol);

        // check for vertical connections
        CheckDirection(symbol, Vector2Int.up, connectedSymbols);
        CheckDirection(symbol, Vector2Int.down, connectedSymbols);
        // found vertical match (3)
        if (connectedSymbols.Count == 3)
        {
            //Debug.Log($"Normal vertical 3 match. Type: {connectedSymbols[0].Data.name}");
            return new MatchResult
            {
                connectedSymbols = connectedSymbols,
                direction = MatchDirection.Vertical
            };
        }
        // found long vertical match (> 3)
        else if (connectedSymbols.Count > 3)
        {
            //Debug.Log($"Long vertical match. Type: {connectedSymbols[0].Data.name}");
            return new MatchResult
            {
                connectedSymbols = connectedSymbols,
                direction = MatchDirection.LongVertical
            };
        }

        // we did not find any match
        return new MatchResult
        {
            connectedSymbols = connectedSymbols,
            direction = MatchDirection.None
        };
    }

    private MatchResult CheckForSuperMatch(MatchResult matchResult)
    {
        // if we have a horizontal or long horizontal match
        if (matchResult.direction == MatchDirection.Horizontal || matchResult.direction == MatchDirection.LongHorizontal)
        {
            foreach (Symbol symbol in matchResult.connectedSymbols)
            {
                // check for another connection vertically
                List<Symbol> extraConnectedSymbols = new();
                CheckDirection(symbol, Vector2Int.up, extraConnectedSymbols);
                CheckDirection(symbol, Vector2Int.down, extraConnectedSymbols);

                // found an extra connection
                if (extraConnectedSymbols.Count >= extraConnectionMinLength)
                {
                    //Debug.Log("Super horizontal match");

                    // add original matched symbols
                    extraConnectedSymbols.AddRange(matchResult.connectedSymbols);

                    return new MatchResult
                    {
                        connectedSymbols = extraConnectedSymbols,
                        direction = MatchDirection.Super
                    };
                }
            }

            // did not find any extra connection
            return null;
        }
        // if we have a vertical or long vertical match
        else if (matchResult.direction == MatchDirection.Vertical || matchResult.direction == MatchDirection.LongVertical)
        {
            foreach (Symbol symbol in matchResult.connectedSymbols)
            {
                // check for another connection horizontally
                List<Symbol> extraConnectedSymbols = new();
                CheckDirection(symbol, Vector2Int.left, extraConnectedSymbols);
                CheckDirection(symbol, Vector2Int.right, extraConnectedSymbols);

                // found an extra connection
                if (extraConnectedSymbols.Count >= extraConnectionMinLength)
                {
                    //Debug.Log("Super vertical match");

                    // add original matched symbols
                    extraConnectedSymbols.AddRange(matchResult.connectedSymbols);

                    return new MatchResult
                    {
                        connectedSymbols = extraConnectedSymbols,
                        direction = MatchDirection.Super
                    };
                }
            }

            // did not find any extra connection
            return null;
        }
        return null;
    }

    private void CheckDirection(Symbol symbol, Vector2Int direction, List<Symbol> connectedSymbols)
    {
        SymbolData symbolType = symbol.Data;

        int x = symbol.X + direction.x;
        int y = symbol.Y + direction.y;

        // check that we're within the boundaries of the board
        while (
            x >= 0 && x < width &&
            y >= 0 && y < height)
        {
            // is a usable tile?
            if (!board[x, y].isUsable)
                break;

            // get the neighbour symbol
            Symbol neighbourSymbol = board[x, y].symbol;

            // is it a valid symbol?
            // if it's a null symbol it means it has been deleted, don't check
            if (neighbourSymbol == null)
                break;

            // do the symbol type match?
            // and is not already matched?
            if (neighbourSymbol.IsMatched || neighbourSymbol.Data != symbolType)
                break;

            // it's a connected symbol
            // add it to the connection list
            connectedSymbols.Add(neighbourSymbol);

            x += direction.x;
            y += direction.y;
        }
    }
    #endregion

    #region CASCADING
    private void RemoveAndRefill(List<Symbol> symbolsToRemove)
    {
        // remove symbols and clear the tiles they were in
        foreach (Symbol symbolToRemove in symbolsToRemove)
        {
            Vector2Int symbolIndices = symbolToRemove.GetIndices();
            RemoveSymbolAt(symbolIndices);
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // the tile isn't empty, don't refill
                if (!board[x, y].IsEmpty())
                    continue;

                // it's an empty tile, refill it
                Vector2Int tileIndices = new(x, y);
                RefillTile(tileIndices);
            }
        }
    }

    private void RemoveSymbolAt(Vector2Int indices)
    {
        // get the symbol
        Symbol symbolToRemove = board[indices.x, indices.y].symbol;

        // clear the matched flag just in case
        symbolToRemove.SetIsMatched(false);

        // destroy the symbol
        Destroy(symbolToRemove.gameObject);

        // empty the tile where the symbol was
        board[indices.x, indices.y] = new Tile(true, null);
    }

    private void RefillTile(Vector2Int tileIndices)
    {
        int x = tileIndices.x;
        int y = tileIndices.y;

        // get the firt symbol above that is not empty
        int yOffset = 1;
        while (y + yOffset < height && board[x, y + yOffset].IsEmpty())
        {
            yOffset++;
        }

        int firstNonEmptyY = y + yOffset;

        // we found a symbol
        if (firstNonEmptyY < height && !board[x, firstNonEmptyY].IsEmpty())
        {
            // get the symbol
            Symbol symbolAbove = board[x, firstNonEmptyY].symbol;

            // move the symbol to the correct location
            Vector2 targetPos = new(x - spacingX, y - spacingY);
            symbolAbove.MoveToPosition(targetPos);
            // update the symbol indices
            symbolAbove.SetIndices(tileIndices);

            // replace the empty tile with the symbol
            board[x, y] = board[x, firstNonEmptyY];

            // empty the tile where the symbol was
            board[x, firstNonEmptyY] = new Tile(true, null);
        }
        // we've hit the top of the board without finding a symbol
        else if (firstNonEmptyY == height)
        {
            SpawnSymbolAtTop(x);
        }
    }

    private void SpawnSymbolAtTop(int x)
    {
        int lowestEmptyTileY = FindLowestEmptyTileY(x);
        int yToMoveTo = height - lowestEmptyTileY;

        // get a random symbol
        SymbolData randomSymbolData = GetRandomSymbol();

        // spawn symbol
        Vector2Int symbolIndices = new(x, lowestEmptyTileY);
        Vector2 spawnPos = new(x - spacingX, height - spacingY);
        Symbol symbolInstance = SpawnSymbol(symbolIndices, spawnPos, randomSymbolData);

        // update the tile where the symbol spawned at
        board[symbolIndices.x, symbolIndices.y] = new Tile(true, symbolInstance);

        // move the symbol to the new location
        Vector2 targetPosition = new(symbolInstance.transform.position.x, symbolInstance.transform.position.y - yToMoveTo);
        symbolInstance.MoveToPosition(targetPosition);
    }

    private int FindLowestEmptyTileY(int x)
    {
        int lowestEmptyTileY = 99;
        for (int y = (height - 1); y >= 0; y--)
        {
            if (board[x, y].IsEmpty())
                lowestEmptyTileY = y;
        }
        return lowestEmptyTileY;
    }
    #endregion

    #region SWAPPING
    public void OnSymbolClicked(Symbol symbol)
    {
        // if we don't have any symbol currently selected, then select it
        if (selectedSymbol == null)
        {
            SelectSymbol(symbol);
        }
        // if we selected the same symbol twice, deselect it
        else if (selectedSymbol == symbol)
        {
            DeselectCurrentSymbol();
        }
        // if we selected a different symbol while we have a symbol selected,
        // either swap them or change current selection
        else if (selectedSymbol != symbol)
        {
            // we're in the process of moving tiles around, so don't do anything
            //if (state == BoardState.ProcessingMove)
            //    return;

            // check if the symbols are adjacent to eachother
            var currentSelectedSymbol = selectedSymbol;
            var symbolsAreAdjacent = IsAdjacent(currentSelectedSymbol, symbol);

            // if they are adjacent, swap them*
            // we only execute this if there's no ongoing swapping
            if (symbolsAreAdjacent && state != BoardState.ProcessingMove)
            {
                state = BoardState.ProcessingMove;

                SwapSymbols(currentSelectedSymbol, symbol);
                StartCoroutine(ProcessMatchesAfterSwap(currentSelectedSymbol, symbol));
            }
            // otherwise select the new symbol
            else
            {
                DeselectCurrentSymbol();
                SelectSymbol(symbol);
            }
        }
    }

    private void SelectSymbol(Symbol symbol)
    {
        selectedSymbol = symbol;
        AudioManager.Instance.PlaySound2DOneShot(selectSound, pitchVariation: 0.1f);
    }

    private void DeselectCurrentSymbol() => selectedSymbol = null;

    private bool IsAdjacent(Symbol firstSymbol, Symbol secondSymbol)
    {
        return Mathf.Abs(firstSymbol.X - secondSymbol.X) + Mathf.Abs(firstSymbol.Y - secondSymbol.Y) == 1;
    }

    private void SwapSymbols(Symbol firstSymbol, Symbol secondSymbol)
    {
        Symbol temp = board[firstSymbol.X, firstSymbol.Y].symbol;

        // swap symbols
        board[firstSymbol.X, firstSymbol.Y].symbol = board[secondSymbol.X, secondSymbol.Y].symbol;
        board[secondSymbol.X, secondSymbol.Y].symbol = temp;

        // update indices
        int tempX = firstSymbol.X;
        int tempY = firstSymbol.Y;
        firstSymbol.SetIndices(new Vector2Int(secondSymbol.X, secondSymbol.Y));
        secondSymbol.SetIndices(new Vector2Int(tempX, tempY));

        // move the symbols to their new position
        Vector3 firstSymbolPos = board[firstSymbol.X, firstSymbol.Y].symbol.transform.position;
        Vector3 secondSymbolPos = board[secondSymbol.X, secondSymbol.Y].symbol.transform.position;
        firstSymbol.MoveToPosition(secondSymbolPos);
        secondSymbol.MoveToPosition(firstSymbolPos);

        // play swap sound
        AudioManager.Instance.PlaySound2DOneShot(swapSound, 0.1f);
    }
    #endregion

    #region TURNS
    private IEnumerator ProcessMatchesAfterSwap(Symbol firstSymbol, Symbol secondSymbol)
    {
        state = BoardState.ProcessingMove;

        // wait for the symbols to finish swapping their position
        yield return new WaitForSeconds(symbolSwapDuration);

        // if a match was found, process it
        if (BoardContainsMatch())
        {
            StartCoroutine(ProcessTurnWithMatch());
        }
        // if there was no match, move the symbols back to their starting tile
        else
        {
            // swap the symbols back to their original tiles
            SwapSymbols(firstSymbol, secondSymbol);

            // wait for the symbols to finish swapping their position
            yield return new WaitForSeconds(symbolSwapDuration);

            // consume one move after an invalid swap action
            if (GameManager.Instance.State == GameState.Playing)
            {
                GameManager.Instance.ProcessTurn();
            }

            // go back to idle state
            state = BoardState.Idle;
        }
    }

    public IEnumerator ProcessTurnWithMatch()
    {
        // set match flag to false on the symbols we're about the remove
        foreach (Symbol symbolThatMatched in symbolsThatMatched)
            symbolThatMatched.SetIsMatched(false);

        // add match score
        if (GameManager.Instance.State == GameState.Playing)
        {
            int matchScore = CalculateMatchScore(symbolsThatMatched);
            if (matchScore > 0)
                SessionManager.Instance.AddScore(matchScore);
        }

        // remove the matched symbols and refill them
        RemoveAndRefill(symbolsThatMatched);

        // play match sound
        if (cascadeChainCount == 0)
            AudioManager.Instance.PlaySound2DOneShot(regularMatchSound, matchPitchVariation);
        else if (cascadeChainCount == 1)
            AudioManager.Instance.PlaySound2DOneShot(cascadeOneMatchSound, matchPitchVariation);
        else if (cascadeChainCount == 2)
            AudioManager.Instance.PlaySound2DOneShot(cascadeTwoMatchSound, matchPitchVariation);
        else if (cascadeChainCount == 3)
            AudioManager.Instance.PlaySound2DOneShot(cascadeThreeMatchSound, matchPitchVariation);
        else if (cascadeChainCount >= 4)
        {
            AudioManager.Instance.PlaySound2DOneShot(cascadeFourMatchSound, matchPitchVariation);
            AudioManager.Instance.PlaySound2DOneShot(ultraCascadeJingleSound, matchPitchVariation);
        }

        // wait for a delay before processing another match
        yield return new WaitForSeconds(delayBetweenMatchProcessing);

        // is there another match that was found after refilling symbols?
        // trigger cascade logic
        if (BoardContainsMatch())
        {
            // increase cascade combo
            cascadeChainCount += 1;

            // process the turn once again (cascade), but without consuming a move
            StartCoroutine(ProcessTurnWithMatch());
        }
        // no more matches found, go back to idle state
        else
        {
            // reset cascade combo
            cascadeChainCount = 0;

            // clear stored matched symbols
            symbolsThatMatched.Clear();

            // process the turn after we ended processing all the matches
            // this will take care of consuming a move and triggering post-match logic such as winning or game over
            if (GameManager.Instance.State == GameState.Playing)
            {
                GameManager.Instance.ProcessTurn();
            }

            // the board goes back in idle state
            state = BoardState.Idle;
        }
    }

    public float GetCascadeComboMultiplier() => Mathf.Min(cascadeMaxChain, 1f + cascadeChainCount);

    private int CalculateMatchScore(List<Symbol> symbolsThatMatched)
    {
        // get the multiplier based on the number of symbols that matched
        float matchLengthMultiplier = 1f;
        if (symbolsThatMatched.Count == 3)
            matchLengthMultiplier = threeMatchMultiplier;
        else if (symbolsThatMatched.Count >= 4)
            matchLengthMultiplier = longMatchMultiplier;

        // get the cascade combo multiplier
        float cascadeComboMultiplier = GetCascadeComboMultiplier();

        // get match score
        // cumulate the symbols score values
        SymbolData symbolType = symbolsThatMatched[0].Data;
        int matchScore = symbolType.scoreValue * symbolsThatMatched.Count;

        // calculate the final score for this match
        int totalScore = Mathf.CeilToInt((matchScore * matchLengthMultiplier) * cascadeComboMultiplier);
        return totalScore;
    }
    #endregion
}
