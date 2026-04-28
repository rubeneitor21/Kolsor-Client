# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

Kolsor is a Unity multiplayer implementation of Orlog — the dice minigame from Assassin's Creed Valhalla. It is a client-only Unity project; the server runs at `kolsor.garcalia.com`.

## How to build and run

Open the project in **Unity 6** (URP pipeline, Input System). There are no CLI build scripts — use the Unity Editor directly.

- **Play in Editor**: Open `Assets/Scenes/LoginScene.unity` and press Play.
- **Scene order**: LoginScene → LobbyScene → GameScene (loaded with `SceneManager.LoadScene`).
- **No automated tests** are in use despite the Test Framework package being present.

## Architecture

### Scene and manager lifecycle

Three scenes share a set of singleton `MonoBehaviour` managers that use `DontDestroyOnLoad`:

| Manager | Responsibility |
|---|---|
| `AuthManager` | REST login/register → stores JWT + user ID |
| `WebSocketManager` | Single persistent WebSocket to `wss://kolsor.garcalia.com` |
| `LobbyManager` | Matchmaking flow; buffers the first `game-rolls` in `PendingRollsBody` |
| `PingManager` | Sends a ping every 4 s (server disconnects after 6 s of silence) |

Scene-local singletons (destroyed with the scene):

| Manager | Responsibility |
|---|---|
| `GameManager` | Owns `MyDice` / `EnemyDice` lists and game state; handles input |
| `BoardManager` | Instantiates and repositions dice/stone GameObjects |

### Network protocol

All WebSocket messages are plain text with format `TYPE\nBODY` (type on line 1, JSON body on line 2). `WebSocketManager` parses this and raises `OnMessageReceived(string type, string body)`. For `game-rolls` messages, it injects the `user` field from the root message into the body before forwarding.

Authentication uses a separate REST call (`UnityWebRequest`) to `https://kolsor.garcalia.com`; the JWT token is then forwarded over the WebSocket as an `auth` message.

**Relevant message types:**

| Direction | Type | Meaning |
|---|---|---|
| S→C | `game-rolls` | Dice rolled for a player; body contains `user`, `rolls[]`, optionally `state` |
| C→S | `select-rolls` | Player confirms kept dice; body contains `rolls[]` |
| S→C | `game-start` | Match found; contains player/opponent IDs and names |
| C→S | `matchmaking-search` | Begin searching for an opponent |

### JSON parsing

The project does **not** use Newtonsoft.Json or full `JsonUtility` deserialization for game messages. All parsing is done with manual string helpers in `GameManager`:

- `ExtractArray(json, key)` — returns the raw JSON array string for a key
- `ExtractObject(json, key)` — returns the raw JSON object string for a key
- `ExtractStringValue(json, key)` — returns the string value of a simple key

Do not introduce Newtonsoft or add `[JsonProperty]` attributes — keep using these helpers for consistency.

### Dice state model

```
DiceData          — data only (face, energy, kept, isMyDice)
DiceController    — MonoBehaviour on each dice GameObject; owns visual state
BoardManager      — owns all dice GameObject lists; handles all positioning
GameManager       — owns DiceData lists; handles input and server messages
```

**Four GameObject lists in `BoardManager`:**

| List | Contents |
|---|---|
| `_myDiceObjects` | My current bowl (destroyed and re-created each roll) |
| `_enemyDiceObjects` | Enemy's current bowl (replaced each enemy roll) |
| `_myConfirmedObjects` | My kept dice (persist across rolls until round end) |
| `_enemyConfirmedObjects` | Enemy's last roll, shown in their confirmed row when my turn starts |

`ClearDice()` only destroys bowl lists; confirmed lists must be cleared explicitly via `ClearConfirmedDices()` / `ClearEnemyConfirmedDices()`.

### Dice display flow

1. **Enemy rolls** → `game-rolls` (enemy userId) arrives → enemy dice spawn in `enemyBowlCenter`.
2. **My turn** → `game-rolls` (my userId) arrives → `FinalizeEnemyDice()` moves enemy bowl objects to `enemyKeptRowOrigin` with gold emission; then my dice spawn in `myBowlCenter`.
3. **I select** → click toggles `DiceData.kept`; `RefreshKeptRows()` repositions visually.
4. **I confirm (SPACE / button)** → `SendKeptDicesToLeft()` transfers kept objects to `_myConfirmedObjects` with `keptConfirmedShift` offset; `ConfirmSelection()` sends `select-rolls` and sets `_waitingServer = true`.

### Static cross-scene state

`GameData` (static class) holds `MyId`, `OpponentId`, `OpponentName`, and `PlayerStartId` between scenes. `LobbyManager.PendingRollsBody` (static string) buffers the first `game-rolls` that may arrive before `GameScene` finishes loading.

## Key Inspector references (GameScene)

`BoardManager` needs these Transform references assigned in the Inspector:

- `myBowlCenter` / `enemyBowlCenter` — center of each player's dice bowl
- `myKeptRowOrigin` / `enemyKeptRowOrigin` — start of each player's confirmed-dice row (rotation determines row direction via `-transform.right`)
- `keptConfirmedShift` — world-space offset applied when dice move from right row to left (confirmed) position (set to `(0, 0, 4)` in current scene)
- `keptYOffset` — extra Y lift for kept/confirmed dice to avoid clipping the board
