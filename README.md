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

外部アセットの素材本体は、原則としてGit管理外です。

- `Assets/JC_LP_MegaCity/`
- `Assets/AllSkyFree/`
- `Assets/HIVEMIND/`

ゲームの配置はビルド対象の `Assets/JC_LP_MegaCity/Scenes/LP_MegaCity_Demo.unity` に保存しています。このシーンと対応する `.meta`、調整済みの `M_MegaCity_Glass_03.mat` は例外としてGit管理します。

開発環境では、各アセットを正規の入手元から取得し、上記と同じパスに元の `.meta` と一緒に導入してください。MegaCityのインポート時は、上記のシーンとマテリアルおよびそれぞれの `.meta` を上書きしないでください。上書きした場合は、そのファイルだけをGitの確定版から復元してからシーンを開きます。

配置を編集した場合は、Unityでシーンを保存し、コミットの差分にシーンファイルが含まれていることを確認してください。初回復元の確認では、`SampleScene` ではなくビルド対象の `LP_MegaCity_Demo` を開きます。

`Assets/Models/` 内の大きなキャラクターモデルは Git LFS で管理します。クローン後は `git lfs install` と `git lfs pull` を実行して取得してください。

ライセンスは未設定です。公開リポジトリにする場合は、公開前に利用条件に合うライセンスを追加してください。
