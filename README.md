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
Unityプロジェクトとして必要な `Assets/`、`Packages/`、`ProjectSettings/` は管理対象です。

ライセンスは未設定です。公開リポジトリにする場合は、公開前に利用条件に合うライセンスを追加してください。
