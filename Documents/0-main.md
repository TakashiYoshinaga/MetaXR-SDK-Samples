### 1. はじめに

Meta Quest 3の登場により、Mixed Reality（MR）アプリケーション開発は新たな段階に入りました。高解像度パススルー機能により、一台のデバイスでVRとARの両方の体験を提供したり、開発したりできる環境が整ったのです。

これまで本ブログではもOculus XRプラグインを使った Quest開発についての[記事](https://tks-yoshinaga.hatenablog.com/entry/quest-dev-index)を公開してきました。しかし昨今、Meta XR SDKが OpenXRベースのアーキテクチャを標準とするようになったことで、SDKで提供されるプレハブの構成や設定手順が大きく変わりました。既存の記事では最新の環境に対応できなくなったため、新しいMeta XR SDKに対応した連載を一から作り直すことにしました。

本連載の位置づけとしては、初心者が段階的に学習できる実践的なチュートリアルを目指しています。Meta社はBuilding Blocksという、ノーコードでオブジェクトの表示やインタラクションを容易に実現するツールを提供開始しました。これにより、Meta XR SDKが提供する機能を手軽に試すことが可能になりました。

[Building Blocks | Oculus Developers](https://developer.oculus.com/documentation/unity/bb-overview/)

しかし、Building Blocksで作成したサンプルに物足りなさを感じ、サンプルを調整して自分が望む動作を実現しようとする段階になると、SDKの使い方を学ぶ必要がります。Meta XR SDKは豊富なサンプルを提供していてこれらから多くを学ぶことができますが、サンプルを一つ一つ確認することは初心者にとっては情報量が多く、高いハードルとなり得ます。つまり、「ちょっと変更したい」という要望を実現するのが非常に大変なのです。実際、私自身も学習するのに苦労しました。

そこで、Questを使ったオブジェクトの表示やインタラクションを実現するための基本手順を学べる資料があると多くの方の学習の支援になるのではないかと考え、この連載を始めるに至りました。ノーコードのBuilding Blocksと公式サンプルの中間くらいのレベル感で、特定の機能を実現するための最小構成を学べる資料を作成しようと考えています。

### 2. 連載目次

- [Meta XR SDKのインストールとプロジェクトの設定](1-meta-xr-sdk-setup.md)
- [オブジェクトをVR表示](2-quest-vr-object-display.md)
- [パススルー機能を使ったAR表示](3-quest-ar-passthrough.md)
- [ロコモーションをオフにする](4-quest-locomotion-disable.md)
- [オブジェクトをつかむ(準備編)](5-quest-object-grab-preparation.md)
- [近くにあるオブジェクトをつかむ](6-quest-near-object-grab.md)
- [遠くにあるオブジェクトをつかむ](7-quest-far-object-grab.md)
- [Ray交差判定を用いたインタラクション](8-quest-ray-interaction.md)
- [UnityのUI操作 (ボタンを例に)](9-quest-unity-ui-interaction.md)
- [カスタムHand Poseを使った自然な操作](10-quest-hand-pose-manipulation.md)
- [PalmMenuを使った手のひらUI](11-quest-palm-menu.md)

ほか、思いついたネタがあれば掲載していきたいと思います。

記事の間違いのご指摘や、修正のご提案、勉強してみたい内容などがあればぜひご連絡ください。  
X(旧Twitter): https://twitter.com/Taka_Yoshinaga

### 3. GitHub

本記事で作成したサンプルはGitHubでも公開しています。

[GitHub - TakashiYoshinaga/MetaXR-SDK-Samples](https://github.com/TakashiYoshinaga/MetaXR-SDK-Samples)