# Segway Climbing Game

Unity 6 / URP で制作中の、難所を登っていく3Dアクションゲームのプロトタイプです。
Only Up系の長いステージ進行を基調に、重心移動感のあるセグウェイ風の操作を目指しています。

## 開発環境

- Unity 6.5.8f1
- Universal Render Pipeline (URP)
- Unity Input System
- Cinemachine 2.10.7
- ProBuilder 6.1.2
- Splines 2.9.0

Unity Hubから指定バージョンのUnity Editorを導入し、このフォルダをプロジェクトとして開いてください。
パッケージは `Packages/manifest.json` をもとにUnityが復元します。

## 現在の操作

| 入力 | 動作 |
| --- | --- |
| マウスの上下移動 | 前進・後退（カーソル原点からの距離に応じた速度） |
| 左クリック長押し | 左旋回 |
| 右クリック長押し | 右旋回 |
| 左スティック上下 | 前進・後退 |
| WASD / 矢印キー / 右スティック | カメラ操作 |
| Space | ジャンプ |

## 主なスクリプト

- `Assets/Scripts/PlayerController.cs` - プレイヤーの移動、旋回、ジャンプ、入力表示
- `Assets/Scripts/PlayerCameraController.cs` - プレイヤー追従とカメラ回転

## Git運用

`Library/`、`Temp/`、`Logs/`、`UserSettings/`、IDEのローカル状態、Unityが生成するソリューションファイルは `.gitignore` で除外しています。
ゲーム固有の `Assets/`、`Packages/`、`ProjectSettings/` は管理対象です。

## 外部アセットの復元

リポジトリを小さく保ち、外部アセットを再配布しないため、次のフォルダはGit管理外です。

- `Assets/JC_LP_MegaCity/`
- `Assets/AllSkyFree/`
- `Assets/HIVEMIND/`

開発環境では、各アセットを正規の入手元から取得し、上記と同じフォルダ構成でUnityプロジェクトへ導入してください。MegaCityを使用するシーンを開く場合は、先に `Assets/JC_LP_MegaCity/` を復元します。

`Assets/Models/` 内の大きなキャラクターモデルは Git LFS で管理します。クローン後は `git lfs install` と `git lfs pull` を実行して取得してください。

ライセンスは未設定です。公開リポジトリにする場合は、公開前に利用条件に合うライセンスを追加してください。
