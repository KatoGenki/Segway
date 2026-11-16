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

## アセットとシーンの復元

`Assets/JC_LP_MegaCity/`、`Assets/AllSkyFree/`、`Assets/HIVEMIND/` の登録済み素材は、クローンとGit LFSで復元します。ゲームの配置はビルド対象の `Assets/JC_LP_MegaCity/Scenes/LP_MegaCity_Demo.unity` に保存しています。

配置を編集した場合は、Unityでシーンを保存し、コミットの差分にシーンファイルが含まれていることを確認してください。初回復元の確認では、`SampleScene` ではなくビルド対象の `LP_MegaCity_Demo` を開きます。

`Assets/` 以下に追加するFBX・Blend・GLB、PNG・PSD・EXR、WAV・MP3、MP4などは、拡張子に応じてGit LFSで管理します。対象は `.gitattributes` に記載しています。Git LFSをインストールし、最初に `git lfs install` を実行してください。その後は通常どおりファイル本体と `.meta` をコミットしてpushします。取得側ではクローンや更新時にLFSファイルが展開されます。展開されていない場合は、プロジェクトのルートで `git lfs pull` を実行してください。

LFSの判定はファイルサイズにかかわらず拡張子で行います。対象外の拡張子で大きなファイルを追加する場合は、コミット前に `.gitattributes` に対象を追加してください。

ライセンスは未設定です。公開リポジトリにする場合は、公開前に利用条件に合うライセンスを追加してください。
