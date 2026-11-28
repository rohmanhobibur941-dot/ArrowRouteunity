# Arrow Route (Unity 6, Android)

Arrow Route ka Unity port (Kotlin / Jetpack Compose version se). Sirf C#, koi paid asset nahi.
GitHub Actions (game-ci) se APK banta hai, Unity editor me haath se kuch karna nahi padta.

- Package name: `com.jubahir.arrowroute` (portrait, Android 8.0+ / API 26, IL2CPP, ARM64)
- Scenes (Splash, Menu, Game) aur board material **code se bante hain** (`Assets/Editor/SceneBuilder.cs`), build ke time.
- Tunable numbers (arrows per level, difficulty mix): `Assets/Resources/GameConfig.json`

## 1. Repo setup

1. GitHub par naya repo banao (private chalega) aur is folder ka poora content push karo:
   ```
   cd ArrowRouteUnity
   git init && git add -A && git commit -m "Arrow Route Unity"
   git branch -M main
   git remote add origin https://github.com/<user>/<repo>.git
   git push -u origin main
   ```
2. **Unity license (ek baar):**
   - Repo → Actions → **Acquire Unity activation file** → Run workflow. Khatam hone par artifact `Unity_Activation_File` download karo (`.alf` file).
   - https://license.unity3d.com/manual kholo, `.alf` upload karo, Personal license chuno, `.ulf` file download karo.
   - Repo → Settings → Secrets and variables → Actions → New repository secret:
     - `UNITY_LICENSE` = `.ulf` file ka poora text
     - `UNITY_EMAIL` = Unity account ka email
     - `UNITY_PASSWORD` = Unity account ka password
   - (Agar Unity ne Personal license ka tareeka badla ho to game-ci docs dekho: https://game.ci/docs/github/activation. Pro/Plus serial ho to `UNITY_SERIAL` secret bhi chalta hai.)
3. Push karo (ya Actions → **Build Android APK** → Run workflow). Pehli build 20-40 minute le sakti hai (Unity image download + IL2CPP), uske baad Library cache se tez hoti hai.

## 2. APK download

Actions → us build par click → neeche **Artifacts** → `ArrowRoute-apk` → zip kholo → `ArrowRoute.apk`.
Phone me install karne se pehle purani Arrow Route (alag key se signed) uninstall karo.

## 3. Signing

- **Default (push par):** Unity ka debug keystore. Install ho jata hai, par Play Store ke liye nahi.
- **Release keystore (alag workflow `Build signed release APK`, sirf manual):** ye 5 secrets daalo
  - `ANDROID_KEYSTORE_NAME` (jaise `arrowroute-release.jks`)
  - `ANDROID_KEYSTORE_BASE64` (`base64 -w 0 arrowroute-release.jks`)
  - `ANDROID_KEYSTORE_PASS`, `ANDROID_KEYALIAS_NAME`, `ANDROID_KEYALIAS_PASS`

  Phir Actions → **Build signed release APK** → Run workflow. Wahi keystore har baar use karo (backup rakho).

## 4. Ads (AdMob pehle, Unity Ads baad me)

IDs `Assets/Scripts/Ads/AdIds.cs` me hain (wahi jo Android app me the): AdMob App ID, Interstitial, Rewarded; Unity Game ID `800005010`, placements `Rewarded_Android`, `Interstitial_Android`.

- **Default build:** Unity Ads (`com.unity.ads` package, `Assets/csc.rsp` me `ARROW_UNITY_ADS`).
- **AdMob jodna:** Actions → Build Android APK → Run workflow → **admob** tick karo. Ye OpenUPM se Google Mobile Ads plugin + External Dependency Manager jodta hai
  (`.github/scripts/enable_admob.py`; versions wahin badal sakte ho). Repo me hamesha ke liye chahiye to wahi script locally chalao aur changes commit karo.
- Dono ho to waterfall chalta hai: AdMob load hota hai, fail ho to Unity. Preload background me hota hai.
- Unity editor me ads simulate hote hain (reward turant milta hai) taaki flows test ho sakein.
- Apne live ads par click mat karna. Test ke liye `AdIds.UnityTestMode = true`.

## 5. Code ka naksha

| Folder | Kya hai |
|---|---|
| `Scripts/Game` | `LevelGen` (level generator, Kotlin se port), `ShapeMasks` (1/2/3, A/B/C, jaanwar, square, circle), `GameSession` (hearts, stars, hints, taps), `GameConfig` |
| `Scripts/Render` | `BoardView` (touch: tap, drag, pinch), `ArrowDrawer` + `MeshBuilder` (gol corner wale arrows, ek mesh me) |
| `Scripts/Screens` | uGUI screens code se: Splash, Menu, Game, dialogs. Text sab PNG hai (`Resources/UI`) |
| `Scripts/Ads` | `AdService` (waterfall), providers |
| `Editor` | `BuildScript`, `SceneBuilder`, texture import settings, AdMob app id setup |

Save/load: `PlayerPrefs` (level, hints, theme, sound, vibration).

## 6. Jo automatically convert nahi hua / dhyan do

- **Test nahi hua:** yahan Unity nahi tha, to ye project mere paas compile ya run nahi hua. Pehli build fail ho to Actions ka log (Unity ke `error CS...` ya Gradle error) bhejo.
- **Level data:** koi fixed level file nahi hai (levels code se bante hain, Kotlin version ki tarah). Sirf tuning numbers `GameConfig.json` me hain.
- **Fonts/icons:** Android me Compose se draw hone wale icons (home, gear, heart...) ab PNG hain (`ic_*.png`); thode alag dikh sakte hain.
- **Animations:** DOTween nahi (paid/extra), simple code animations hain (exit slide, bump, hint flash).
- **Sound:** generated tones, asli audio files nahi; vibration `Handheld.Vibrate()`.
- **AdMob:** Unity me plugin + EDM4U Gradle resolution CI me test nahi kiya; ye sabse risky hissa hai. Isliye default build me nahi hai.
- **Unity version:** `ProjectSettings/ProjectVersion.txt` me `6000.0.26f1` (game-ci image milne wala version). Naya chahiye to wahi line badlo (`unityVersion: auto` use hota hai).
- **ARMv7:** abhi sirf ARM64 (build tez). Purane 32-bit phones ke liye `BuildScript.cs` me `| AndroidArchitecture.ARMv7` jodo.
- **Play Store:** AAB chahiye to `EditorUserBuildSettings.buildAppBundle = true` aur `androidExportType: androidAppBundle`.
