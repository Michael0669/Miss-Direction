# Setup

## 1. Clone the repo with GitHub Desktop

1. Install GitHub Desktop and sign in with your GitHub account (**File → Options → Accounts**).
2. **File → Clone repository → GitHub.com** tab, pick `michael0669/miss-direction`.
3. Choose a local path, e.g. `Documents/GitHub/miss-direction`, and click **Clone**.

GitHub Desktop sets up Git LFS automatically, which this repo uses for art and audio.

## 2. Turn the folder into a Unity project (one time)

The repo has scripts but not Unity's own project files yet. Unity Hub won't create a project inside a folder that already has files, so:

1. In Unity Hub, **New project → Universal 3D** template (Unity 6), name it `MissDirectionTemp`, and create it **somewhere else** (e.g. your Desktop).
2. Close Unity once it finishes opening.
3. Copy these from `MissDirectionTemp` into your cloned `miss-direction` folder:
   - the `Packages` folder
   - the `ProjectSettings` folder
   - everything inside `Assets` (merge it with the existing `Assets` folder)
4. In Unity Hub, **Add → Add project from disk**, choose the `miss-direction` folder, and open it.
5. Delete `MissDirectionTemp`.

## 3. Commit the Unity files

Back in GitHub Desktop you'll see the new `Packages`, `ProjectSettings` and `.meta` files. Write a summary like "Add Unity project files", click **Commit to main**, then **Push origin**.

Never commit the `Library`, `Temp`, `Logs` or `UserSettings` folders. The `.gitignore` already excludes them.

## 4. Getting new changes

When changes are pushed (including Claude's), open GitHub Desktop and click **Fetch origin**, then **Pull origin**. Unity reloads the scripts when you switch back to it.

If changes arrive on a separate branch, pick it from the **Current branch** dropdown, or merge it into `main` with a pull request on GitHub.

## 5. Try it

In Unity: **Miss Direction → Build Test Playground Scene**, press **Play**, then click **HOST A GAME**.

## 6. Test multiplayer on one computer

Netcode for GameObjects installs automatically from `Packages/manifest.json`. For testing you also need **Multiplayer Play Mode**, which opens extra "virtual player" windows on the same computer:

1. **Window → Package Manager → Unity Registry**, search **Multiplayer Play Mode**, click **Install**.
2. **Window → Multiplayer → Multiplayer Play Mode**. Tick **Player 2** (and 3 or 4 if you like). The first launch of each takes a minute.
3. Press **Play**. In the main editor click **HOST A GAME**. In each virtual player window click **JOIN** (address `127.0.0.1`, port `7777`).

Each window is its own player with its own color and name tag. Click inside a window to control that player.

## 7. Test with a friend on the same Wi-Fi

1. **File → Build Profiles**, make sure `Playground` is in the scene list, and click **Build** (or **Build And Run**).
2. Copy the build folder to the other computer.
3. One player clicks **HOST A GAME**. Their local IP is shown in the bottom-right corner.
4. The other types that IP into **Address** and clicks **JOIN**.

Allow the game through Windows Firewall if asked. Playing over the internet needs Steam (coming next), so don't try port forwarding for now.
