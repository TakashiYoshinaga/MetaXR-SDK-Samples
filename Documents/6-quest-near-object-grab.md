# MetaQuestで近くにあるオブジェクトをつかむ

## 0. 本記事の内容

本記事ではMeta Questで近くの3Dオブジェクト(立方体)をつかんで操作するまでを紹介します。この動画はAR版ですがAR/VR両対応です。  
GitHubで公開している[サンプル](https://github.com/TakashiYoshinaga/MetaXR-SDK-Samples)の02-NearManipulationまたは02-NearManipulation-ARシーンでも動作を確認できます。

[![NearInteraction](https://img.youtube.com/vi/IrYJ2kuhtq0/0.jpg)](https://www.youtube.com/watch?v=IrYJ2kuhtq0)

なお本記事は下記での事前準備が済んでいることを前提に解説を進めます。ご注意ください。

**[準備編]**

[MetaQuestでオブジェクトをつかむ(準備編)](5-quest-object-grab-preparation.md)


## 1. シーンの複製

前回の記事で作成したシーンを編集することも可能ですが、この既存のシーンを破壊せずにマニピュレーションを試すため、シーンを複製する方法を紹介します。不要な場合は読み飛ばしてください。

- 前回までに作成したManipulationのVR版またはAR版のシーンを開く
- **File -> Save As...** をクリックして現在のシーンを新しい名前で保存  
  *本記事では**NearManipulation**とします
- Hierarchyに表示されるシーン名がNearManipulationになっていることを確認

## 2. コントローラや手で近くのオブジェクトをつかむ設定

オブジェクトをつかむ際にコントローラを使う場合と手を使う場合、それぞれの場合について追加するスクリプトとその設定方法を紹介します。両方の設定を行うことでコントローラと手の両方を使用することも可能です。

**[コントローラでつかむ]**

- CubeのInspectorを表示し、Add Componentをクリック
- Grab Interactableで検索し、候補から**Grab Interactable**を選択
- 追加されたGrab Interactableの**Pointable Element**にCubeをドラッグ&ドロップ  
  *Grab InteractableとGrabbalbeを接続し、コントローラでつかんだ情報をGrabbableで設定した挙動(位置・角度・スケールの変化)に反映
- さらにGrab Interactableの**Rigidbody**にCubeをドラッグ&ドロップ  
  *前の記事でCubeに追加したRigidbodyを割り当てることでコントローラとCubeとの接触検知が利用される

![Grab Interactable設定画面](https://github.com/TakashiYoshinaga/MetaXR-SDK-Samples/blob/materials/Documents/materials/6/00.jpg?raw=true)

**[手でつかむ]**

- CubeのInspectorを表示し、Add Componentをクリック
- Grab Interactableで検索し、候補から**Hand Grab Interactable**を選択
- 追加されたHand Grab Interactableの**Pointable Element**にCubeをドラッグ&ドロップ  
  *Hand Grab InteractableとGrabbalbeを接続し、手つかんだ情報をGrabbableで設定した挙動(位置・角度・スケールの変化)に反映
- さらにHand Grab Interactableの**Rigidbody**にCubeをドラッグ&ドロップ  
  *前の記事でCubeに追加したRigidbodyを割り当てることで手とCubeとの接触検知が利用される

![Hand Grab Interactable設定画面](https://github.com/TakashiYoshinaga/MetaXR-SDK-Samples/blob/materials/Documents/materials/6/01.jpg?raw=true)

**[注意！]**  
OVRCameraRigオブジェクトにアタッチされたOVR ManagerのHand Tracking SupportでControllerOnlyを選択しているとハンドトラッキングが動作しないためオブジェクトを手でつかむことができません。設定を必ず確認してください。

## 3. コントローラの設定の微調整 (オプション)

上記の操作によりコントローラでオブジェクトをつかんで動かすことが可能になりました。このときデフォルト設定ではグリップボタン(中指)を使用してつかむようになっています。これを例えばトリガーボタンでもつかめるようにする方法について紹介します。

- HierarchyのOVRCameraRigの子要素のOVRInteractionComprehensiveを開く
- さらに**RightInteractions -> Interactors -> Controller and No Hand -> ControllerGrabInteractor**の順に子要素を開く
- ControllerGrabInteractorの子要素の**GripButtonSelector**を開く
- GripButtonSelectorのInspectorで**ControllerSelector**に注目
- **Controller Button Usage**でTriggerButtonのチェックをON  
  *GripButtonが不要な場合はGripButtonのチェックを外す
- 左手のコントローラ(LeftInteractions)についても同様の操作を行うことで、つかむ際に使用するボタンを設定可能

![Trigger Manipulation](https://github.com/TakashiYoshinaga/MetaXR-SDK-Samples/blob/materials/Documents/materials/6/02.jpg?raw=true)


## 4. 手の挙動の微調整 (オプション)

細かい話になりますが、デフォルト設定では両手でオブジェクトを拡大縮小した際に、手のオブジェクトが実際の手に追従しません。もし手のオブジェクトを追従させたい場合は下記を試してください。

- HierarchyのCubeを選択
- InspectorからCubeに登録されたHand Grab Interactableに注目
- **Hand Alignment**の設定を**None**に変更

## 6. 次のステップ

ここまでの内容で近くのオブジェクトをつかんで操作する方法が実現できました。同じオブジェクトに対して近くでの操作と遠くでの操作を併用することもできます。

遠くのオブジェクトの操作を追加する場合は、次の記事の2章からの手順をさらに追加してください。

[MetaQuestで遠くにあるオブジェクトをつかむ](7-quest-far-object-grab.md)

## 7. Meta XR SDKに関する記事一覧はこちら

[Meta XR SDK連載目次](0-main.md)
