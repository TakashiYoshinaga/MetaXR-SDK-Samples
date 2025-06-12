# MetaQuestのロコモーションをオフにする

## 0. 本記事の内容

本連載で使用している**OVRInteractionComprehensive**には手やコントローラを使ったインタラクションに関する基本的な設定が済んでいるオブジェクトが内包されています。これらの機能の中にはジョイスティックを使ったテレポートや視点の回転を行うロコモーション（Locomotion）も提供されていて、デフォルトで使用可能な状態になっています。

しかしコンテンツによっては（特にARコンテンツでは）このような機能が不要な場合もあります。本記事では、不要なロコモーション機能を無効化する設定方法について紹介します。

なおVRやARを実現するための基本的な設定は済んでいることを前提に解説を行います。VRやAR開発の基本設定についてご興味がある方は下記の記事もあわせてお読みください。

**[VR版]**

[MetaQuestでオブジェクトを表示](2-quest-vr-object-display.md)

**[AR版]**

[MetaQuestのパススルーを使ったAR表示](3-quest-ar-passthrough.md)

## 1. ロコモーション機能の無効化

**[右手コントローラーのロコモーション無効化]**

- Hierarchyの中から**OVRCameraRig**を見つける
- OVRCameraRigの子要素の**OVRInteractionComprehensive**を開く
- さらに**RightInteractions -> Interactors -> Controller -> LocomotionControllerInteractorGroup**の順に子要素を開く
- LocomotionControllerInteractorGroupの子要素の以下をそれぞれ非アクティブにする
  - **TeleportControllerInteractor**（テレポート機能）
  - **ControllerTurnerInteractor**（視点回転機能）
  - **ControllerSlideInteractor**

![LocomotionControllerInteractorGroup削除画面](https://github.com/TakashiYoshinaga/MetaXR-SDK-Samples/blob/materials/Documents/materials/4/00.jpg?raw=true)

**[左手コントローラーのロコモーション無効化]**

- **LeftInteractions**についても同様の設定を行う
- **LeftInteractions -> Interactors -> Controller -> LocomotionControllerInteractorGroup**の順に子要素を開く
- LocomotionControllerInteractorGroupの子要素の以下をそれぞれ非アクティブにする
  - **TeleportControllerInteractor**
  - **ControllerTurnerInteractor**
  - **ControllerSlideInteractor**

**[Locomotorオブジェクトの無効化]**

- OVRInteractionComprehensiveの子要素の**Locomotor**を非アクティブにする

## 2. 無効化される機能

以上の設定により、以下のロコモーション機能が無効化されます：

| 機能 | 説明 | 対象コンテンツ |
|------|------|----------------|
| **テレポート** | ジョイスティックでの瞬間移動 | VR/AR共通 |
| **視点回転** | ジョイスティックでの視点の左右回転 | 主にVR |
| **連続移動** | ジョイスティックでの連続的な移動 | 主にVR |

これらの機能は特にARアプリケーションでは現実空間との整合性を保つために無効化することが推奨されます。

## 3. 次のステップ

ここまでの内容でロコモーション機能の無効化が完了しました。次はオブジェクトのマニピュレーション機能の準備について解説します。

[MetaQuestでオブジェクトをつかむ（準備編）](5-quest-object-grab-preparation.md)

## 4. Meta XR SDKに関する記事一覧はこちら

[Meta XR SDK連載目次](0-main.md)
