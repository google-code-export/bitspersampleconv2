[English](http://code.google.com/p/bitspersampleconv2/wiki/PlayPcmWinChangelogEn) | 日本語

# PlayPcmWin 更新履歴 #

### PlayPcmWin 4.0.78 ###
  * 簡体中文翻訳更新

### PlayPcmWin 4.0.77 ###
  * [Issue 60](https://code.google.com/p/bitspersampleconv2/issues/detail?id=60) 極性反転機能作成。
  * [Issue 148](https://code.google.com/p/bitspersampleconv2/issues/detail?id=148) モノラル再生機能を作成。

### PlayPcmWin 4.0.75 ###
  * [Issue 144](https://code.google.com/p/bitspersampleconv2/issues/detail?id=144) アプリがキーボードフォーカスを持っていない時もメディアキーを受け付けるように修正。
  * [Issue 143](https://code.google.com/p/bitspersampleconv2/issues/detail?id=143) ファイル 開く で出るOpenFileDialogで、最後に選択されたフィルターを適用する

### PlayPcmWin 4.0.74 ###
  * 再生停止中、再生一時停止中に次の曲ボタン、前の曲ボタンを押せるようにする
  * 再生中に停止ボタンを押した時、直ちにWASAPIのIAudioClient::Stop()を呼ぶのをやめて、フェードアウトし無音を再生してからWASAPIのIAudioClient::Stop()を呼ぶように修正。

### PlayPcmWin 4.0.73 ###
  * libflac 1.3.1に更新。
  * [Issue 144](https://code.google.com/p/bitspersampleconv2/issues/detail?id=144) メディアキー(再生・一時停止、停止、次の曲、前の曲)に対応。キーイベントを受け取るためにはPPWのウィンドウがフォアグラウンドになっている必要があります。

### PlayPcmWin 4.0.72 ###
  * 詳細設定でFLAC MD5チェックサムの照合を無効にしている時にも照合処理が行われていた不具合を修正。読み出し時FLACデコードCPU負荷が1割程度改善。このバグはPPWにFLAC読み込み機能を追加したとき(PlayPcmWin 1.0.42)から存在。
  * 音楽ファイルの読み込み時にPPWが対応していないID3タグが現れたときにスキップして読み出し処理を続行するように修正。

### PlayPcmWin 4.0.70 ###
  * [Issue 138](https://code.google.com/p/bitspersampleconv2/issues/detail?id=138) 簡体中文版Windowsで、PPWのメニューのファイル、開くを選択するとクラッシュする不具合の修正。このバグはバージョン4.0.66で発生。

### PlayPcmWin 4.0.69 ###
  * [Issue 71](https://code.google.com/p/bitspersampleconv2/issues/detail?id=71) 対応していない種類のLISTチャンクが付いているWAVファイルの読み込みに対応。
  * [Issue 71](https://code.google.com/p/bitspersampleconv2/issues/detail?id=71) fmtチャンクのextensibleSizeにゴミが入っているWAVファイルの読み込みに対応。
  * WAVファイルに付いている"id3 "タグを読み込んでタイトル、アルバム名、アーティスト名、アルバムカバーアート画像を読み込む
  * "対応フォーマット"ボタンを再生リストに項目が存在するときにも押せるように修正。

### PlayPcmWin 4.0.67 ###
  * [Issue 138](https://code.google.com/p/bitspersampleconv2/issues/detail?id=138) 調査用バージョン

### PlayPcmWin 4.0.66 ###
  * 量子化ビット数32ビットで有効ビット数24ビットのWAVファイルを24ビット再生出力時に下位8ビットのデータを使用したノイズシェイピング処理を行わないように修正。
  * 簡体中文訳の更新。

### PlayPcmWin 4.0.64 ###
  * [Issue 136](https://code.google.com/p/bitspersampleconv2/issues/detail?id=136) 量子化ビット数32ビットで有効ビット数24ビットのWAVファイルの再生が4.0.63で出来なくなったのを修正。

### PlayPcmWin 4.0.63 ###
  * [Issue 135](https://code.google.com/p/bitspersampleconv2/issues/detail?id=135) 量子化ビット数を減らす時の処理にノイズシェイピングの他にディザを加える処理を追加。

![http://bitspersampleconv2.googlecode.com/files/SettingsNoiseShapingTypeJP.png](http://bitspersampleconv2.googlecode.com/files/SettingsNoiseShapingTypeJP.png)

↓ディザ付きノイズシェイピング処理が行われた時のログ出力

![http://bitspersampleconv2.googlecode.com/files/LogOutputDitheredNoiseshaping.png](http://bitspersampleconv2.googlecode.com/files/LogOutputDitheredNoiseshaping.png)

### PlayPcmWin 4.0.62 ###
  * 特定のWAVファイルが読めない問題を修正。("fmt "チャンクのサイズが40バイトでextensibleサイズに0バイトが入っているWAVファイル。) ([revision 3253](https://code.google.com/p/bitspersampleconv2/source/detail?r=3253))

### PlayPcmWin 4.0.61 ###
  * 1チャンネルモノラルDSFファイルの再生
  * ファイルサイズが2GBを超えるDSFファイルの再生(64ビット版のみ可能)
  * ファイルのID3タグにUTF-8文字列が入っていた時に文字列の最初の2バイトが切れて読み込まれるバグを修正

### PlayPcmWin 4.0.60 ###
  * ファイルのID3タグにUTF-8文字列が入っていた時に文字列の最初の2バイトが切れて読み込まれるバグを修正したつもりだったが不十分だった。

### PlayPcmWin 4.0.59 ###
  * [Issue 131](https://code.google.com/p/bitspersampleconv2/issues/detail?id=131) “DACが安定するまで無音を送出する”機能が動かなくなった問題を修正。このバグはPlayPcmWin 4.0.52で発生。


### PlayPcmWin 4.0.58 ###
  * [Issue 130](https://code.google.com/p/bitspersampleconv2/issues/detail?id=130) “再生リストの内容を記憶”のチェックを外しPPWを再起動、ファイルをD&Dすると再生リストが更新されないが再生ボタンが有効になって再生ボタンを押すとクラッシュするバグを修正。このバグはPlayPcmWin 4.0.48で発生。

### PlayPcmWin 4.0.57 ###
  * DwmEnableMMCSSの呼び出しを行わないという選択肢を作成し、これをデフォルト設定にする。

### PlayPcmWin 4.0.56 ###
  * [Issue 130](https://code.google.com/p/bitspersampleconv2/issues/detail?id=130) 調査用バージョン

### PlayPcmWin 4.0.54 ###
  * アプリケーションアイコン96x96と256x256を追加。(ディスプレイ設定の テキストサイズ大(L)150%設定用アイコン)

![http://bitspersampleconv2.googlecode.com/files/ppw150percenticon.png](http://bitspersampleconv2.googlecode.com/files/ppw150percenticon.png)

### PlayPcmWin 4.0.53 ###
  * libFLAC.libを作るときの最適化オプションを変更。あまり改善しなかった。

### PlayPcmWin 4.0.52 ###
  * 再生開始時(初期化後、再生開始前)にEndpoint bufferのオーディオフレーム数をログ窓に表示。(IAudioClient::GetBufferSize()の戻す値。http://msdn.microsoft.com/en-us/library/windows/desktop/dd370866%28v=vs.85%29.aspx 参照)

### PlayPcmWin 4.0.50 ###
  * 再生ボタンを押した後のPCMファイル読み込み中にファイルが消えると例外がスローされるがPlayPcmWinがキャッチしないために強制終了する問題を修正。

### PlayPcmWin 4.0.49 ###
  * 起動時にウィンドウを出してから再生リストを読み込む。
  * DwmEnableMMCSS(FALSE)を呼ぶ機能。

### PlayPcmWin 4.0.47 ###
  * DSD 11.2MHz、DSD 22.5MHz、DSD 3.0MHz、DSD 6.1MHz、DSD 12.2MHz、DSD 24.5MHz等のDoP再生
  * 起動時にウィンドウを出してから再生リストを読むようにしたが、再生リストが読み終わるまでの間真っ白いウィンドウが出るので、あまり良くない感じになった。

### PlayPcmWin 4.0.46 ###
  * GPU描画とソフトウェア描画の切り替え機能。([Revision 3023](https://code.google.com/p/bitspersampleconv2/source/detail?r=3023))

### PlayPcmWin 4.0.45 ###
  * [Issue 120](https://code.google.com/p/bitspersampleconv2/issues/detail?id=120) 修正。

### PlayPcmWin 4.0.43 ###
  * 詳細設定画面の整理。([Revision 2960](https://code.google.com/p/bitspersampleconv2/source/detail?r=2960))
  * [Issue 123](https://code.google.com/p/bitspersampleconv2/issues/detail?id=123) 複数ファイルをドロップした時、ファイルの順番をABC順に並び替える機能。
  * [Issue 127](https://code.google.com/p/bitspersampleconv2/issues/detail?id=127) ドロップ時に個々のトラックに[ここまで一括読み込み]を付ける機能。1曲ずつファイルを読み込んで再生するようになる。
  * [Issue 126](https://code.google.com/p/bitspersampleconv2/issues/detail?id=126) FLACファイルの圧縮を解いて出てきたPCMデータについてMD5値を算出しメタデータに埋め込まれたMD5値と照合する機能。

![http://bitspersampleconv2.googlecode.com/files/ppw4043ss2.png](http://bitspersampleconv2.googlecode.com/files/ppw4043ss2.png)

![http://bitspersampleconv2.googlecode.com/files/flacmd5mismatch2.png](http://bitspersampleconv2.googlecode.com/files/flacmd5mismatch2.png)


### PlayPcmWin 4.0.42 ###
  * 詳細設定画面の整理。([Revision 2957](https://code.google.com/p/bitspersampleconv2/source/detail?r=2957))
  * DoP再生の再生シーク時処理の高速化を試みたが改善せず([Revision 2950](https://code.google.com/p/bitspersampleconv2/source/detail?r=2950))。

### PlayPcmWin 4.0.37 ###
  * DoP再生の一時停止と再生シーク、再生曲変更でバチッという音が出るバグを修正。
  * DSD5.6MHz 2ch ステレオ DSDデータのDoP再生に対応。(0x05/0xfa方式)

### PlayPcmWin 4.0.36 ###
  * DoP再生でバチッという音が出るバグ有り。

### PlayPcmWin 4.0.32 ###
  * DSDIFFファイル(2.8MHz 2ch ステレオ“not compressed”DSD)を読み込んでDoPで再生する機能。バチッという音が出るバグ有り。

### PlayPcmWin 4.0.31 ###
  * DSFファイル(2.8MHz 2ch ステレオ DSD)を読み込んでDoPで再生する機能。だがバチッという音が出るバグ有り。

### PlayPcmWin 4.0.29 ###
  * 量子化ビット数自動選択アルゴリズムの改善 [Revision 2869](https://code.google.com/p/bitspersampleconv2/source/detail?r=2869)

### PlayPcmWin 4.0.28 ###
  * [Issue 121](https://code.google.com/p/bitspersampleconv2/issues/detail?id=121) WASAPI初期化時エラーメッセージにDSPeaker Anti-Mode 2.0 Dual Core用設定説明文を追加。
  * [Issue 123](https://code.google.com/p/bitspersampleconv2/issues/detail?id=123) フォルダドロップ時にファイルの並び順をソートする機能作成。

### PlayPcmWin 4.0.27 ###
  * [Revision 2858](https://code.google.com/p/bitspersampleconv2/source/detail?r=2858) ノイズシェイピングが可能な状況で常にログ出力に「Noise shaping...」と表示されるバグを修正。ノイズシェイピングを行った時だけログ出力に「Noise shaping...」と表示するように修正。

### PlayPcmWin 4.0.26 ###
  * WASAPI排他モードで量子化ビット数を減らすときにノイズシェイピングする機能

### PlayPcmWin 4.0.25 ###
  * 最後に再生していた曲の番号を覚える [Revision 2830](https://code.google.com/p/bitspersampleconv2/source/detail?r=2830)

### PlayPcmWin 4.0.24 ###
  * PlayPcmWin 4.0.22で新設したタイマー解像度0.5ms設定は、再生開始後しばらくすると効力が切れてしまうことがわかったので注意書きを付けた([Issue 118](https://code.google.com/p/bitspersampleconv2/issues/detail?id=118))。(タイマー解像度の値を観察するプログラム: http://bitspersampleconv2.googlecode.com/files/TimerResolutionMonitor101.zip )

### PlayPcmWin 4.0.23 ###
  * WASAPI共有モード再生で、最大振幅が0.98になるように音量を下げることでリミッターAPOによって発生する歪を軽減する機能。(この機能はビットパーフェクト再生できるようになる機能ではありません。)

### PlayPcmWin 4.0.22 ###
  * Setup timer resolution to 0.5ms

### PlayPcmWin 4.0.20 ###
  * PlayPcmWinTestBench 音の位相を回転する機能 浮動小数点数形式出力の時音量制限処理を行わないようにした。

### PlayPcmWin 4.0.19 ###
  * PlayPcmWin WASAPI Setup失敗時に表示される文字列を更新。([Issue 41](https://code.google.com/p/bitspersampleconv2/issues/detail?id=41))

  * PlayPcmWinTestBench 離散ヒルベルト変換(ハイパス型)がバグっていることが判明したのでバグ有りと表示。

### PlayPcmWin 4.0.18 ###
  * PlayPcmWinTestBench 音の位相を回転する機能作成。
  * PlayPcmWinTestBench 非常に音が悪いアップサンプル機能 キュービック補間。
  * PlayPcmWinTestBench 解析信号表示が縮小表示されることがある問題を修正。

### PlayPcmWin 4.0.17 ###
  * AssemblyVersionの上げ忘れでPlayPcmWinTestBenchが起動しない問題を修正

### PlayPcmWin 4.0.16 ###
  * [Issue 116](https://code.google.com/p/bitspersampleconv2/issues/detail?id=116) Mp3tagでFLACファイルに付けたアルバム名情報等が表示されない問題を修正
  * PlayPcmWinTestBench 非常に音が悪いアップサンプル機能作成

### PlayPcmWin 4.0.14 ###
  * [Issue 115](https://code.google.com/p/bitspersampleconv2/issues/detail?id=115) CUEシートにperformer情報等が無いときヌルポする問題の修正
  * CUEシート読み込み時の文字コードの設定
![http://bitspersampleconv2.googlecode.com/files/ppwsettings414.png](http://bitspersampleconv2.googlecode.com/files/ppwsettings414.png)

### PlayPcmWin 4.0.12 ###
  * [Issue 115](https://code.google.com/p/bitspersampleconv2/issues/detail?id=115) 再生リストファイルと音楽ファイル両方に曲タイトル情報が存在するとき再生リストの曲タイトルを表示するように修正

### PlayPcmWin 4.0.11 ###
  * [Issue 115](https://code.google.com/p/bitspersampleconv2/issues/detail?id=115) 再生リストの読み込み時、音楽ファイルに曲タイトル情報が存在しても曲タイトル表示が音楽ファイル名になるバグを修正

### PlayPcmWin 4.0.10 ###
  * [Issue 115](https://code.google.com/p/bitspersampleconv2/issues/detail?id=115) 再生リストの読み込み時、音楽ファイルにアルバム情報が存在しても再生リストファイルにアルバム情報が存在しないとアルバム情報が空欄になるバグを修正

### PlayPcmWin 4.0.9 ###
  * [Issue 115](https://code.google.com/p/bitspersampleconv2/issues/detail?id=115) M3U8再生リストの読み込み対応。M3U再生リストも読み込めることがありますがM3U8の使用を推奨
  * アプリケーションアイコンを照らす光源の位置をアイコン向かって手前右上から手前左上に変更
![http://bitspersampleconv2.googlecode.com/files/ppwlogodiff.png](http://bitspersampleconv2.googlecode.com/files/ppwlogodiff.png)

### PlayPcmWin 4.0.8 ###
  * Simplified Chinese translation

### PlayPcmWin 4.0.7 ###
  * 英語64ビット版インストーラーのアンインストール画面用アイコンつけ忘れ修正

### PlayPcmWin 4.0.6 ###
  * アプリケーションアイコン作成

### PlayPcmWin 4.0.4 ###
  * 詳細設定画面のローカライズ漏れの修正

### PlayPcmWin 4.0.3 ###
  * FxCopで出た警告を大方修正
  * Traditional Chinese translation
  * PlayPcmWin 4.0.1以前のバージョンで設定した「出力レイテンシー値」「再生開始前無音送出秒数」等々の設定値がPlayPcmWin4.0.3以降のバージョンへの移行で自動的には引き継がれません！大変お手数ですが、インストール後これらの設定値を設定し直していただくようお願いします。

### PlayPcmWin 4.0.1 ###
  * [Issue 114](https://code.google.com/p/bitspersampleconv2/issues/detail?id=114) (PlayPcmWin 3.0.98で発生したWASAPI共有モード再生でGPFが発生するひどいバグ) 修正

### PlayPcmWin 4.0.0 ###
  * [Issue 113](https://code.google.com/p/bitspersampleconv2/issues/detail?id=113) (PlayPcmWin 3.0.99で発生した一時停止すると再生位置つまみが動かなくなるバグ) 修正

### PlayPcmWin 3.0.99 ###
  * [Issue 112](https://code.google.com/p/bitspersampleconv2/issues/detail?id=112) (WASAPI共有モード再生の再生時間表示が3.0.98でバグった問題) 修正

### PlayPcmWin 3.0.98 ###
  * [Issue 5](https://code.google.com/p/bitspersampleconv2/issues/detail?id=5) (WASAPI共有モードのサンプルレート変換がうまくいかないことがあるのと変換性能が良くない問題) 修正

### PlayPcmWin 3.0.96 ###
  * [Issue 111](https://code.google.com/p/bitspersampleconv2/issues/detail?id=111) (再生一時停止中に曲変更できない問題) 修正

### PlayPcmWin 3.0.94 ###
  * 再生位置スライダーの長さが伸び縮みするようにした。

### PlayPcmWin 3.0.93 ###
  * English UI Output Latency groupbox layout tweak

### PlayPcmWin 3.0.92 ###
  * [Issue 109](https://code.google.com/p/bitspersampleconv2/issues/detail?id=109) スプラッシュスクリーンが消えるタイミングで再生リスト内の曲ファイル読み込みエラー表示も消える問題修正

### PlayPcmWin 3.0.91 ###
  * Fixed English UI layout mess introduced on PlayPcmWin 3.0.89

### PlayPcmWin 3.0.89 ###
  * 再生スレッドタスクタイプ設定を詳細設定画面に移動し出力デバイス一覧の表示領域を広くする

### PlayPcmWin 3.0.88 ###
  * [Issue 108](https://code.google.com/p/bitspersampleconv2/issues/detail?id=108) 再生リストが一部だけ表示されている状態で再生曲が進んでいくと再生曲が画面外に行く問題修正

### PlayPcmWin 3.0.85 ###
  * スプラッシュスクリーン作成。![http://bitspersampleconv2.googlecode.com/svn/trunk/PlayPcmWin/ppwsplash.png](http://bitspersampleconv2.googlecode.com/svn/trunk/PlayPcmWin/ppwsplash.png)
  * PlayPcmWinテストベンチをプログラムメニューに追加しないように修正。
  * [Issue 109](https://code.google.com/p/bitspersampleconv2/issues/detail?id=109) 発生

### PlayPcmWin 3.0.83 ###
  * [Issue 107](https://code.google.com/p/bitspersampleconv2/issues/detail?id=107) 黒地に白のテーマで再生リストの空欄が白で塗られる問題修正。

### PlayPcmWin 3.0.82 ###
  * [Issue 99](https://code.google.com/p/bitspersampleconv2/issues/detail?id=99) 再生シーク時に音をつなげる処理、トラックの最後近辺にシークするとブチッという音が出る問題修正

### PlayPcmWin 3.0.81 ###
  * [Issue 99](https://code.google.com/p/bitspersampleconv2/issues/detail?id=99) 再生シーク時に音を上品につなげるようにした

### PlayPcmWin 3.0.80 ###
  * [Issue 9](https://code.google.com/p/bitspersampleconv2/issues/detail?id=9) マルチディスプレイ環境でウィンドウ左上座標がマイナスの値になるとウィンドウ表示位置が復帰しない問題修正

### PlayPcmWin 3.0.77 ###
  * [Issue 105](https://code.google.com/p/bitspersampleconv2/issues/detail?id=105) 3.0.76でバグったプログレスバーの表示修正

### PlayPcmWin 3.0.76 ###
  * (日本語版)詳細設定画面の3.0.75で追加した部分が日本語化されていない問題修正

### PlayPcmWin 3.0.75 ###
  * [Issue 59](https://code.google.com/p/bitspersampleconv2/issues/detail?id=59) 再生リストの列の並び順を保存する
  * 再生リストのAlternating row background colorを設定できるようにする。

### PlayPcmWin 3.0.74 ###
  * (英語版)コンテキストメニューの英語版リソースが最新状態になっていなかったので修正。[Revision 2395](https://code.google.com/p/bitspersampleconv2/source/detail?r=2395)

### PlayPcmWin 3.0.73 ###
  * [Issue 103](https://code.google.com/p/bitspersampleconv2/issues/detail?id=103) 再生リスト内の曲をダブルクリックすると再生するように修正。再生リスト編集モードにすると従来通り再生リスト項目編集が可能。マウス右クリックでコンテキストメニューが出るようにした。
  * [Issue 104](https://code.google.com/p/bitspersampleconv2/issues/detail?id=104) 再生位置スライダーのつまみではないエリアを左クリックしマウスを素早く横に動かすとつまみが移動せず同じ箇所が何度も再生される問題修正。

### PlayPcmWin 3.0.72 ###
  * [Issue 99](https://code.google.com/p/bitspersampleconv2/issues/detail?id=99) 再生位置スライダーのつまみではない部分を左クリックして再生位置を移動した時に同じ所が2回再生される問題修正
  * [Issue 102](https://code.google.com/p/bitspersampleconv2/issues/detail?id=102) 再生中にスライダーを動かしていると再生時間が表示されないことがある問題修正

### PlayPcmWin 3.0.71 ###
  * [Issue 99](https://code.google.com/p/bitspersampleconv2/issues/detail?id=99) ④ 再生位置スライダーのつまみではない部分を左クリックしても再生位置を移動できるように修正(しかし、まだバグっている)
  * [Issue 101](https://code.google.com/p/bitspersampleconv2/issues/detail?id=101) 再生一時停止中に再生時間表示されない問題修正

### PlayPcmWin 3.0.70 ###
  * 英語32bit版インストーラーパッケージ作成

### PlayPcmWin 3.0.69 ###
  * [Issue 99](https://code.google.com/p/bitspersampleconv2/issues/detail?id=99) 再生位置スライダーを素早く動かした時に再生位置が追従するように修正。一時停止中に再生位置スライダーを動かして再生再開すると再生位置スライダーの位置から再生再開するように修正。

### PlayPcmWin 3.0.68 ###

  * [Issue 99](https://code.google.com/p/bitspersampleconv2/issues/detail?id=99) ①と③を修正しようとしたが失敗

### PlayPcmWin 3.0.67 ###
  * [Issue 100](https://code.google.com/p/bitspersampleconv2/issues/detail?id=100) 浮動小数点数形式PCMデータから範囲外のサンプル値が出てきた回数をログ出力する機能

### PlayPcmWin 3.0.65 ###
  * [Issue 100](https://code.google.com/p/bitspersampleconv2/issues/detail?id=100) 浮動小数点数形式PCMデータに＋1.0が現れたときのブチッという音が出る問題

### PlayPcmWin 3.0.64 ###
  * [Issue 75](https://code.google.com/p/bitspersampleconv2/issues/detail?id=75) “設定”Expanderの矢印の方向が逆方向を向いているバグ修正

### PlayPcmWin 3.0.63 ###
  * [Issue 96](https://code.google.com/p/bitspersampleconv2/issues/detail?id=96) 選択曲を除外していって0曲になると状態が変になるバグ修正
  * [Issue 97](https://code.google.com/p/bitspersampleconv2/issues/detail?id=97) マルチチャンネル再生設定されているデバイスで2チャンネル再生が失敗するバグ修正

### PlayPcmWin 3.0.61 ###
  * [Issue 95](https://code.google.com/p/bitspersampleconv2/issues/detail?id=95) timeBeginPeriod(1)を呼ぶかどうかを設定する機能作成

### PlayPcmWin 3.0.60 ###
  * [Issue 95](https://code.google.com/p/bitspersampleconv2/issues/detail?id=95) timeBeginPeriod(1)バグ発生
  * [Issue 93](https://code.google.com/p/bitspersampleconv2/issues/detail?id=93) HDMIディスプレイの電源が切れると再生が止まるバグ修正
  * [Issue 94](https://code.google.com/p/bitspersampleconv2/issues/detail?id=94) 選択曲除外機能バグ修正
  * [Issue 92](https://code.google.com/p/bitspersampleconv2/issues/detail?id=92) オーディオデバイスが安定するまでの間無音を送出する機能作成
![http://bitspersampleconv2.googlecode.com/files/ppw3060settings.png](http://bitspersampleconv2.googlecode.com/files/ppw3060settings.png)

### PlayPcmWin 3.0.59 ###
  * [Issue 90](https://code.google.com/p/bitspersampleconv2/issues/detail?id=90) 再生リストの曲順を入れ替えると再生順が変になる問題を修正。

### PlayPcmWin 3.0.58 ###
  * [Issue 88](https://code.google.com/p/bitspersampleconv2/issues/detail?id=88) FLAC埋め込みCUEシートの読み込み対応

### PlayPcmWin 3.0.57 ###
  * [Issue 89](https://code.google.com/p/bitspersampleconv2/issues/detail?id=89) 読み取り専用WAVおよびAIFFファイルが読み込めない問題を修正。

### PlayPcmWin 3.0.55 ###
  * 並列読み込み機能を有効にしていてもAIFFとWAVE読み込みは並列化読み込みしないように変更。

### PlayPcmWin 3.0.54 ###
  * 詳細設定画面の並列読み込み設定の英語リソース対応
  * FLAC並列読み込み機能は実験的です(まだ不安定なことがあるようです。とりあえず無効にしておいたほうが無難です。)

### PlayPcmWin 3.0.53 ###
  * 1曲再生モード作成(1曲だけメモリに読み込んで再生するモード)

### PlayPcmWin 3.0.52 ###
  * FLAC読み込み時のCRCエラーメッセージの文章を修正
  * FLAC並列読み込み機能は実験的です(まだ不安定なことがあるようです。とりあえず無効にしておいたほうが無難です。)

### PlayPcmWin 3.0.51 ###
  * FLAC読み込み時のCRCエラー検出後、エラーメッセージ表示が出ない問題を修正([Issue 87](https://code.google.com/p/bitspersampleconv2/issues/detail?id=87))

### PlayPcmWin 3.0.50 ###
  * 並列ファイル読み込み処理でデータ競合により、まれに異常終了する問題を修正。([Issue 86](https://code.google.com/p/bitspersampleconv2/issues/detail?id=86))

### PlayPcmWin 3.0.49 ###
  * PCMデータが途中で切れているAIFFファイルをPlayPcmWinで読み込むと例外が発生する問題を修正 ([Issue 85](https://code.google.com/p/bitspersampleconv2/issues/detail?id=85))
  * 並列ファイル読み込み処理は、まだすっかり安定してはおらず、まれに読込失敗することがあります。([Issue 86](https://code.google.com/p/bitspersampleconv2/issues/detail?id=86))

### PlayPcmWin 3.0.48 ###
  * ファイル読み込み時にウィンドウ右上×ボタンを押すとPlayPcmWinが終了しない問題を修正。
  * 3.0.43 x86版で詳細設定画面を開くとクラッシュする問題を修正

### PlayPcmWin 3.0.43 ###
  * 詳細設定画面で並列ファイル読み込みするかどうかを選択できるようにした。このバージョンは動作が非常に不安定です。
![http://bitspersampleconv2.googlecode.com/files/ppw3049settings.png](http://bitspersampleconv2.googlecode.com/files/ppw3049settings.png)

![http://bitspersampleconv2.googlecode.com/files/ppw3049cpu.png](http://bitspersampleconv2.googlecode.com/files/ppw3049cpu.png)

↑FLACの並列読込

### PlayPcmWin 3.0.42 ###
  * 並列化によるFLAC読み込み処理の高速化。このバージョンは動作が非常に不安定です。

### PlayPcmWin 3.0.41 ###
  * 英語版作成。

### PlayPcmWin 3.0.31 ###
  * [Issue 79](https://code.google.com/p/bitspersampleconv2/issues/detail?id=79) 修正

### PlayPcmWin 3.0.31 ###
  * [Issue 78](https://code.google.com/p/bitspersampleconv2/issues/detail?id=78)(再生前に再生リスト項目をクリックするとPlayPcmWinが強制終了する) 修正

### PlayPcmWin 3.0.29 ###
  * [Issue 77](https://code.google.com/p/bitspersampleconv2/issues/detail?id=77) WASAPI排他モード マルチチャンネルWAV/FLACの再生。(Lynx Aurora/LT-USBで動作することを確認)
    * 1chモノラル (PlayPcmWin 2.0.89で対応)
    * 2chステレオ (PlayPcmWin 1.0.0で対応)
    * 4ch FL FR BL BR (PlayPcmWin 3.0.29で対応)
    * 5.1ch FL FR FC LFE BL BR  (PlayPcmWin 3.0.29で対応)
    * 7.1ch FL FR FC LFE BL BR SL SR (PlayPcmWin 3.0.29で対応)

### PlayPcmWin 3.0.28 ###
  * [Issue 29](https://code.google.com/p/bitspersampleconv2/issues/detail?id=29) シャッフル再生。

### PlayPcmWin 3.0.26 ###
  * [Issue 58](https://code.google.com/p/bitspersampleconv2/issues/detail?id=58) cover.jpg対応。

### PlayPcmWin 3.0.25 ###
  * 再生中の画面再描画処理をやめ、再生中のCPU負荷を最小限にする機能。
![http://bitspersampleconv2.googlecode.com/files/ppw3025ss.png](http://bitspersampleconv2.googlecode.com/files/ppw3025ss.png)

### PlayPcmWin 3.0.24 ###
  * 再生リストにJPEGファイルをドロップしたときの動作を無視するように変更

### PlayPcmWin 3.0.23 ###
  * [Issue 58](https://code.google.com/p/bitspersampleconv2/issues/detail?id=58) Exact Audio Copyが出力するfolder.jpgカバーアート画像の表示

### PlayPcmWin 3.0.21 ###
  * [Issue 58](https://code.google.com/p/bitspersampleconv2/issues/detail?id=58) AIFF カバーアート画像の表示

### PlayPcmWin 3.0.20 ###

  * 再生リストの項目をつまんで再生曲順を入れ替える機能
  * [Issue 58](https://code.google.com/p/bitspersampleconv2/issues/detail?id=58) カバーアート画像表示設定追加
![http://bitspersampleconv2.googlecode.com/files/coverartSettings.png](http://bitspersampleconv2.googlecode.com/files/coverartSettings.png)
  * [Issue 76](https://code.google.com/p/bitspersampleconv2/issues/detail?id=76) 修正

### PlayPcmWin 3.0.18 ###

  * FLAC カバーアート画像の表示

### PlayPcmWin 3.0.16 ###
  * [Issue 74](https://code.google.com/p/bitspersampleconv2/issues/detail?id=74) 最後の方のサンプルデータが切れてなくなっているWAVファイルの再生に対応

### PlayPcmWin 3.0.15 ###
  * [Issue 70](https://code.google.com/p/bitspersampleconv2/issues/detail?id=70) (ppwplファイルをD&Dで取り込めない問題)修正
  * [Issue 73](https://code.google.com/p/bitspersampleconv2/issues/detail?id=73) (Samplitudeが出力したWAVの読み込み)修正

### PlayPcmWin 3.0.13 ###
  * [Issue 72](https://code.google.com/p/bitspersampleconv2/issues/detail?id=72) (Windows Media Player 12の付加するアーティスト名等の文字化け)修正

### PlayPcmWin 3.0.11 ###
  * [Issue 69](https://code.google.com/p/bitspersampleconv2/issues/detail?id=69) 64ビット版で、ファイルサイズが4GBを超えるWAVファイルの読み込みに対応

### PlayPcmWin 3.0.6 ###
  * [Issue 69](https://code.google.com/p/bitspersampleconv2/issues/detail?id=69) 64ビット版で、ファイルサイズが大きい(1ファイルの大きさが2GB～4GBの範囲の)WAVファイルの読み込みが失敗する問題を修正

### PlayPcmWin 3.0.5 ###
  * (PlayPcmWinテストベンチ)解析信号観察画面
![http://bitspersampleconv2.googlecode.com/files/as.png](http://bitspersampleconv2.googlecode.com/files/as.png)

### PlayPcmWin 3.0.2 ###
  * (PlayPcmWinテストベンチ)[Issue 67](https://code.google.com/p/bitspersampleconv2/issues/detail?id=67) (FIR EQとヒルベルト変換FIR 出力WAVファイルのフォーマットが64ビットfloat固定になるバグ)修正

### PlayPcmWin 3.0.1 ###
  * (PlayPcmWinテストベンチ)FIR EQとヒルベルト変換FIR 音量制限発動時にログを出力するようにした。
  * (PlayPcmWinテストベンチ)ヒルベルト変換 位相が90度遅れるようにした

### PlayPcmWin 2.0.99 ###
  * (PlayPcmWinテストベンチ)ヒルベルト変換FIR プログレスバーが進まない問題を修正。

### PlayPcmWin 2.0.98 ###
  * (PlayPcmWinテストベンチ 機能追加)ヒルベルト変換FIR
  * (PlayPcmWinテストベンチ 機能追加)窓関数にカイザー窓追加

### PlayPcmWin 2.0.97 ###
  * 選択曲を再生リストから除外するボタン追加。曲選択は再生リスト左端の曲番号をマウスで押して選択して下さい。SHIFTやCTRLを押しながらマウス選択で複数曲選択もできます。
  * [Issue 30](https://code.google.com/p/bitspersampleconv2/issues/detail?id=30)

### PlayPcmWin 2.0.96 ###
  * (PlayPcmWinテストベンチ)FIR EQ EQ設定保存・読み出し機能追加、F特をなめらかにする機能追加。
![http://bitspersampleconv2.googlecode.com/files/PPW2096ss.png](http://bitspersampleconv2.googlecode.com/files/PPW2096ss.png)

### PlayPcmWin 2.0.94 ###
  * (PlayPcmWinテストベンチ 機能追加)直線位相FIRフィルターによるEQ (パラメーター調整中)
![http://bitspersampleconv2.googlecode.com/files/fir.jpg](http://bitspersampleconv2.googlecode.com/files/fir.jpg)

↓ ↓ ↓ ↓ ↓

![http://bitspersampleconv2.googlecode.com/files/wavespectra.jpg](http://bitspersampleconv2.googlecode.com/files/wavespectra.jpg)

### PlayPcmWin 2.0.93 ###
  * 再生スレッドの処理高速化。レンダーバッファ上で再生データを組み立てることで再生スレッドでのPCMデータのコピー処理回数を2回から1回に減らす。( [revision 1905](https://code.google.com/p/bitspersampleconv2/source/detail?r=1905) )

### PlayPcmWin 2.0.92 ###
  * 再生スレッドの処理を最大限最適化する(流行なので)。2.0.91との違いは、コンパイラの最適化設定のみ。2.0.91の最適化オプションは-O2。

### PlayPcmWin 2.0.91 ###
  * [Issue 50](https://code.google.com/p/bitspersampleconv2/issues/detail?id=50) (読み込めないFLAC問題)修正
  * [Issue 51](https://code.google.com/p/bitspersampleconv2/issues/detail?id=51) (量子化ビット数24ビットのAIFFファイル対応) 修正

### PlayPcmWin 2.0.90 ###
  * [Issue 55](https://code.google.com/p/bitspersampleconv2/issues/detail?id=55) (AIFF-C/sowtフォーマットの読み出し) 作成
  * [Issue 57](https://code.google.com/p/bitspersampleconv2/issues/detail?id=57) (ログ出力の行数制限) 修正

### PlayPcmWin 2.0.89 ###
  * [Issue 54](https://code.google.com/p/bitspersampleconv2/issues/detail?id=54) (モノラル1チャンネル音声の再生) 作成

### PlayPcmWin 2.0.87 ###
  * [Issue 62](https://code.google.com/p/bitspersampleconv2/issues/detail?id=62) (再生リストのビットレート表示計算ミス) 修正

### PlayPcmWin 2.0.86 ###
  * [Issue 61](https://code.google.com/p/bitspersampleconv2/issues/detail?id=61) (起動時ウィンドウサイズの問題) 修正

### PlayPcmWin 2.0.85 ###
  * [Issue 61](https://code.google.com/p/bitspersampleconv2/issues/detail?id=61) (起動時ウィンドウサイズの問題) 新たに発生
  * [Issue 56](https://code.google.com/p/bitspersampleconv2/issues/detail?id=56) (起動時に前回終了時の再生リストの内容を復元する)作成
![http://bitspersampleconv2.googlecode.com/files/ppw2085settings.png](http://bitspersampleconv2.googlecode.com/files/ppw2085settings.png)

### PlayPcmWin 2.0.84 ###
  * 再生制御グループの中のファイル名表示TextBoxを削除( [revision 1856](https://code.google.com/p/bitspersampleconv2/source/detail?r=1856) )
![http://bitspersampleconv2.googlecode.com/files/PPW2084.png](http://bitspersampleconv2.googlecode.com/files/PPW2084.png)

### PlayPcmWin 2.0.83 ###
  * [Issue 53](https://code.google.com/p/bitspersampleconv2/issues/detail?id=53) (wasapi setup失敗時の対処方法説明表示)作成
  * WASAPIの動作モード表示をステータスバーに移動( [revision 1852](https://code.google.com/p/bitspersampleconv2/source/detail?r=1852) )

### PlayPcmWin 2.0.82 ###
  * [Issue 52](https://code.google.com/p/bitspersampleconv2/issues/detail?id=52) (WASAPI設定画面が引っ込むようにする)作成

### PlayPcmWin 2.0.80 ###
  * [Issue 37](https://code.google.com/p/bitspersampleconv2/issues/detail?id=37) 曲情報表示(AIFF)
  * [Issue 49](https://code.google.com/p/bitspersampleconv2/issues/detail?id=49) (AIFFのckSizeのPAD処理忘れ)修正

### PlayPcmWin 2.0.79 ###
  * [Issue 22](https://code.google.com/p/bitspersampleconv2/issues/detail?id=22) (フォルダのドラッグアンドドロップで中のファイルを再生リストに追加)作成
  * [Issue 48](https://code.google.com/p/bitspersampleconv2/issues/detail?id=48) (画像ファイルを再生リストにドロップすると回復不可能なエラーが出る)修正

### PlayPcmWin 2.0.78 ###
  * [Issue 47](https://code.google.com/p/bitspersampleconv2/issues/detail?id=47) (WAV曲情報取得処理を追加した副作用で“fact”ヘッダの付いているWAVファイルが読めなくなった)修正

### PlayPcmWin 2.0.77 ###
  * [Issue 37](https://code.google.com/p/bitspersampleconv2/issues/detail?id=37) 曲情報表示(WAV)
  * [Issue 47](https://code.google.com/p/bitspersampleconv2/issues/detail?id=47) 新たに発生
  * [Issue 48](https://code.google.com/p/bitspersampleconv2/issues/detail?id=48) 新たに発生

### PlayPcmWin 2.0.76 ###
  * [Issue 37](https://code.google.com/p/bitspersampleconv2/issues/detail?id=37) 曲情報表示(FLAC)

### PlayPcmWin 2.0.73 ###
  * [Issue 38](https://code.google.com/p/bitspersampleconv2/issues/detail?id=38) 修正
  * [Issue 34](https://code.google.com/p/bitspersampleconv2/issues/detail?id=34) 一部修正

### PlayPcmWin 2.0.72 ###
  * [Issue 42](https://code.google.com/p/bitspersampleconv2/issues/detail?id=42) (省メモリ化)修正

### PlayPcmWin 2.0.71 ###
  * [Issue 45](https://code.google.com/p/bitspersampleconv2/issues/detail?id=45) と [Issue 46](https://code.google.com/p/bitspersampleconv2/issues/detail?id=46) (一時停止関連バグ)修正

### PlayPcmWin 2.0.70 ###
  * [Issue 43](https://code.google.com/p/bitspersampleconv2/issues/detail?id=43) (cueに書き込まれてる「ここまで一括読み込み（REM KOKOMADE）」が効かない)修正
  * [Issue 44](https://code.google.com/p/bitspersampleconv2/issues/detail?id=44) (wavファイルを直接D&Dして作ったリストがcue保存できない)修正

### PlayPcmWin 2.0.68 ###
  * [Issue 42](https://code.google.com/p/bitspersampleconv2/issues/detail?id=42) (32ビット版のメモリ不足問題)暫定対策を入れた

### PlayPcmWin 2.0.67 ###
  * [Issue 40](https://code.google.com/p/bitspersampleconv2/issues/detail?id=40) 修正 ([GAPをここまで一括読み込みに変換]で曲の終わりが切れて再生されることがある)

### PlayPcmWin 2.0.66 ###
  * [Issue 23](https://code.google.com/p/bitspersampleconv2/issues/detail?id=23) (再生一時停止機能) 作成。再生一時停止中も排他モードでデバイスを占有し続ける

### PlayPcmWin 2.0.65 ###
  * [Issue 36](https://code.google.com/p/bitspersampleconv2/issues/detail?id=36) (再生リストをCUEシートとして保存するとGAPが増えていく)修正

### PlayPcmWin 2.0.64 ###
  * [Issue 35](https://code.google.com/p/bitspersampleconv2/issues/detail?id=35) (再生リストにCUEシート以外のファイルを追加するとヌルポ)修正

### PlayPcmWin 2.0.63 ###
  * [Issue 30](https://code.google.com/p/bitspersampleconv2/issues/detail?id=30) 再生リストの編集、保存 [ファイル][名前を付けて保存]。
  * 再生中の選曲は、再生リスト内の曲番号をクリックして下さい。
  * [Issue 35](https://code.google.com/p/bitspersampleconv2/issues/detail?id=35) バグ発生
  * [Issue 36](https://code.google.com/p/bitspersampleconv2/issues/detail?id=36) バグ発生

### PlayPcmWin 2.0.61 ###
  * [Issue 31](https://code.google.com/p/bitspersampleconv2/issues/detail?id=31) 再生リスト項目の並べ替え機能。修正。

### PlayPcmWin 2.0.60 ###
  * [Issue 31](https://code.google.com/p/bitspersampleconv2/issues/detail?id=31) 再生リスト項目の並べ替え機能。修正。

### PlayPcmWin 2.0.59 ###
  * [Issue 31](https://code.google.com/p/bitspersampleconv2/issues/detail?id=31) 再生リスト項目の並べ替え機能。

### PlayPcmWin 2.0.58 ###
  * [Issue 6](https://code.google.com/p/bitspersampleconv2/issues/detail?id=6) 修正。

### PlayPcmWin 2.0.57 ###
  * PlayPcmWinTestBench 互換性マニフェスト動作テスト機能
  * アプリケーションマニフェストをexeに埋め込むのをやめ、manifestファイルを配置。アプリケーションマニフェストファイルの内容を自由に編集できるようにした。

### PlayPcmWin 2.0.54 ###
  * [Issue 17](https://code.google.com/p/bitspersampleconv2/issues/detail?id=17) 修正その2(デバイス一覧更新後、できるだけ一覧更新前に選択していたデバイスを選択する)。

### PlayPcmWin 2.0.53 ###
  * [Issue 17](https://code.google.com/p/bitspersampleconv2/issues/detail?id=17) 修正。
  * [Issue 33](https://code.google.com/p/bitspersampleconv2/issues/detail?id=33) 修正。

### PlayPcmWin 2.0.52 ###
  * PlayPcmWinテストベンチの[アップサンプリング]機能 出力ビットフォーマット設定機能新規作成。
![http://bitspersampleconv2.googlecode.com/files/PlayPcmWinTB252ss.png](http://bitspersampleconv2.googlecode.com/files/PlayPcmWinTB252ss.png)

### PlayPcmWin 2.0.51 ###
  * PlayPcmWinテストベンチの[アップサンプリング]機能 [Issue 32](https://code.google.com/p/bitspersampleconv2/issues/detail?id=32) 修正。
  * 音質劣化機能 修正。

### PlayPcmWin 2.0.50 ###
  * PlayPcmWinテストベンチの[アップサンプリング]機能 GPU±1048576サンプル 計算誤差(バグ)修正。
  * 音質劣化機能 修正中
![http://bitspersampleconv2.googlecode.com/files/PlayPcmWin250.png](http://bitspersampleconv2.googlecode.com/files/PlayPcmWin250.png)

### PlayPcmWin 2.0.46 ###
  * PlayPcmWinテストベンチの[アップサンプリング]機能 GPU±1048576サンプル 最適化パラメータ調整。
  * 音質劣化機能 修正中

### PlayPcmWin 2.0.45 ###
  * PlayPcmWinテストベンチの[アップサンプリング]機能 新規作成。
  * 音質劣化機能 修正中

### PlayPcmWin 2.0.44 ###
  * PlayPcmWinテストベンチの[音質劣化]機能 たたみ込み計算のGPU処理。エラー処理を少し改善
  * ジッター付加機能 計算が何箇所か間違っている

### PlayPcmWin 2.0.43 ###
  * PlayPcmWinテストベンチの[音質劣化]機能 たたみ込み計算のGPU処理。(GPUはGeForce GTX 570以上必要。この機能を使用するためには、最新のDirectXエンドユーザーランタイムをインストールする必要があります。 http://www.microsoft.com/downloads/details.aspx?FamilyID=2da43d38-db71-4c1b-bc6a-9b6652cd92a3&displayLang=ja )

### PlayPcmWin 2.0.41 ###
  * [Issue 28](https://code.google.com/p/bitspersampleconv2/issues/detail?id=28) 修正。

### PlayPcmWin 2.0.40 ###
  * PlayPcmWinテストベンチの[音質劣化]機能 周期ジッター量指定の単位をp-pの半分の値⇒ピコ秒RMSに変更。
  * PlayPcmWinテストベンチの[音質劣化]機能 ジッター付加処理のCPU負荷が重いのでプロセス優先度を下げる。

### PlayPcmWin 2.0.38 ###
  * PlayPcmWinテストベンチの[音質劣化]機能 リサンプリングの精度選択肢を追加。

### PlayPcmWin 2.0.37 ###
  * PlayPcmWinテストベンチの[音質劣化]タブに、ランダムジッターを増やす機能を追加。

### PlayPcmWin 2.0.35 ###
  * PlayPcmWinテストベンチの[音質劣化]タブの周期ジッターを増やす機能のリミッター処理バグ修正。
![http://bitspersampleconv2.googlecode.com/files/jitterss.png](http://bitspersampleconv2.googlecode.com/files/jitterss.png)

### PlayPcmWin 2.0.34 ###
  * PlayPcmWinテストベンチに[音質劣化]タブを追加。周期ジッターを増やす機能作成。(リミッター処理がバグっている)

### PlayPcmWin 2.0.32 ###
  * WasapiUser.cppからプレイリスト管理プログラムを分離しWWPlayGroup.cppを作成。
  * [Issue 6](https://code.google.com/p/bitspersampleconv2/issues/detail?id=6) を修正しようとして失敗。

### PlayPcmWin 2.0.31 ###
  * 詳細設定[すべてリセット]ボタン。
  * 再生時間表示の文字設定機能。
![http://bitspersampleconv2.googlecode.com/files/PlayPcmWin231.png](http://bitspersampleconv2.googlecode.com/files/PlayPcmWin231.png)

### PlayPcmWin 2.0.29 ###
  * CTRL + マウスホイールでズームするようにした(Internet ExplorerやFirefoxと同様)。

### PlayPcmWin 2.0.28 ###
  * 再生時間表示の文字の大きさを16ポイントに変更。
  * ズーム機能。

### PlayPcmWin 2.0.24 ###
  * PlayPcmWin.exeから実験プログラムを分離独立し、PlayPcmWinTestBench.exeを作成。

### PlayPcmWin 2.0.23 ###
  * ABX Test 結果発表の文を修正(「正解 X=A, Y=B」→「正解は X=A, Y=B」)。

### PlayPcmWin 2.0.22 ###
  * ABX Test機能新規作成。

### PlayPcmWin 2.0.21 ###
  * [Issue 26](https://code.google.com/p/bitspersampleconv2/issues/detail?id=26) 量子化ビット数24ビットフォーマットの自動選択のバグを修正。(Sint32V24に対応しているデバイスで再生失敗)

### PlayPcmWin 2.0.20 ###
  * WASAPIフォーマットの画面表示 見た目を変更。

### PlayPcmWin 2.0.19 ###
  * Windowsクラシックテーマ表示で画面表示が崩れる問題を修正。

### PlayPcmWin 2.0.18 ###
  * [Issue 24](https://code.google.com/p/bitspersampleconv2/issues/detail?id=24) 量子化ビット数24ビットフォーマットの自動選択モード追加。
  * [Issue 25](https://code.google.com/p/bitspersampleconv2/issues/detail?id=25) 現在設定されているWASAPIフォーマットの画面表示。
  * プレイリストの背景色を白に設定。
  * プレイリストの**列の**並べ替えやサイズ変更ができなくなった問題を修正。
![http://bitspersampleconv2.googlecode.com/files/PlayPcmWin2018.jpg](http://bitspersampleconv2.googlecode.com/files/PlayPcmWin2018.jpg)

### PlayPcmWin 2.0.17 ###
  * プログラムアイコン修正。

### PlayPcmWin 2.0.15 ###
  * プログラムアイコンを作成。

### PlayPcmWin 2.0.14 ###
  * [Issue 3](https://code.google.com/p/bitspersampleconv2/issues/detail?id=3) 量子化ビット数32ビットのデータで、直線補間処理がバグることがあるのを修正。

### PlayPcmWin 2.0.12 ###
  * [Issue 3](https://code.google.com/p/bitspersampleconv2/issues/detail?id=3) シークと曲変更を直線補間するようにした。補間時間を50ミリ秒→10ミリ秒に減らすと違和感が減った。

### PlayPcmWin 2.0.11 ###
  * [Issue 3](https://code.google.com/p/bitspersampleconv2/issues/detail?id=3) シークは直線補間するようにした(そうとう違和感あり)。曲変更は補間せず従来通りブチブチ発生する

### PlayPcmWin 2.0.10 ###
  * [Issue 3](https://code.google.com/p/bitspersampleconv2/issues/detail?id=3) を修正しようとしたが、バグっている([revision 1479](https://code.google.com/p/bitspersampleconv2/source/detail?r=1479))

### PlayPcmWin 2.0.9 ###
  * [対応フォーマット]で出力される表を見やすくした　([revision 1465](https://code.google.com/p/bitspersampleconv2/source/detail?r=1465))

### PlayPcmWin 2.0.8 ###
バグ取り
  * USBオーディオの量子化ビット数24ビット再生対応 ([Issue 18](https://code.google.com/p/bitspersampleconv2/issues/detail?id=18) 一部修正)

↓[詳細設定]画面

![http://bitspersampleconv2.googlecode.com/files/PlayPcmWin208Settings.png](http://bitspersampleconv2.googlecode.com/files/PlayPcmWin208Settings.png)

### PlayPcmWin 2.0.4 ###
バグ取り
  * [Issue 16](https://code.google.com/p/bitspersampleconv2/issues/detail?id=16) 修正
  * 出力レイテンシーのデフォルト値を170msに変更
バグ
  * PlayPcmWin 1.0.xから、 PlayPcmWin 2.0.xに詳細設定が引き継がれません

### PlayPcmWin 1.0.89 ###
バグ取り
  * [Issue 13](https://code.google.com/p/bitspersampleconv2/issues/detail?id=13) 修正したつもり
  * [Issue 14](https://code.google.com/p/bitspersampleconv2/issues/detail?id=14) 修正
  * [Issue 15](https://code.google.com/p/bitspersampleconv2/issues/detail?id=15) 修正
  * [読み込み処理を並列化して高速化]をボツにした

### PlayPcmWin 1.0.87 ###
バグ取り
  * [Issue 12](https://code.google.com/p/bitspersampleconv2/issues/detail?id=12) を修正

### PlayPcmWin 1.0.86 ###
機能追加
  * [読み込み処理を並列化して高速化]設定選択肢を追加([Revision 1421](https://code.google.com/p/bitspersampleconv2/source/detail?r=1421))

↓[詳細設定]画面

![http://bitspersampleconv2.googlecode.com/files/PlayPcmWin186.png](http://bitspersampleconv2.googlecode.com/files/PlayPcmWin186.png)

### PlayPcmWin 1.0.84 ###
機能追加
  * CUEシートの演奏者情報の表示([Issue 10](https://code.google.com/p/bitspersampleconv2/issues/detail?id=10))対応
  * 再生時間表示を時:分:秒形式に修正。http://code.google.com/p/bitspersampleconv2/source/diff?spec=svn1412&r=1412&format=side&path=/trunk/PlayPcmWin/MainWindow.xaml.cs

### PlayPcmWin 1.0.83 ###
機能追加
  * 対応フォーマット一覧のサンプリング周波数に352.8kHzと384kHzを追加 http://code.google.com/p/bitspersampleconv2/source/diff?spec=svn1409&r=1408&format=side&path=/trunk/WasapiIODLL/WasapiUser.cpp

バグ取り
  * [Issue 11](https://code.google.com/p/bitspersampleconv2/issues/detail?id=11) を修正

### PlayPcmWin 1.0.75 ###
機能追加
  * [Issue 9](https://code.google.com/p/bitspersampleconv2/issues/detail?id=9) 対応

↓[詳細設定]画面

![http://bitspersampleconv2.googlecode.com/files/PlayPcmWin175Settings.png](http://bitspersampleconv2.googlecode.com/files/PlayPcmWin175Settings.png)

### PlayPcmWin 1.0.74 ###
バグ取り
  * [Issue 7](https://code.google.com/p/bitspersampleconv2/issues/detail?id=7) を修正
機能追加
  * [ファイル][開く]で出てくるファイル選択ダイアログ画面で、複数ファイル選択できるようにした。

### PlayPcmWin 1.0.73 ###
バグ取り
  * 再生リストに[ここまで一括読み込み]が複数ある場合に、[ここまで一括読み込み]を選択すると再生リストの表示が変になる問題を修正。
変更箇所
  * http://code.google.com/p/bitspersampleconv2/source/diff?spec=svn1363&r=1363&format=side&path=/trunk/PlayPcmWin/MainWindow.xaml.cs の1796行目

### PlayPcmWin 1.0.70 ###
機能追加
  * CUEシートを読む際に、GAPを[ここまで一括読み込み]に置き換える機能。

↓[詳細設定]画面

![http://bitspersampleconv2.googlecode.com/files/PlayPcmWin170Settings.jpg](http://bitspersampleconv2.googlecode.com/files/PlayPcmWin170Settings.jpg)

変更箇所
  * http://code.google.com/p/bitspersampleconv2/source/detail?r=1360

### PlayPcmWin 1.0.69 ###
バグ取り
  * [Issue 1](https://code.google.com/p/bitspersampleconv2/issues/detail?id=1) を修正
  * [Issue 2](https://code.google.com/p/bitspersampleconv2/issues/detail?id=2) を修正

### PlayPcmWin 1.0.68 ###
  * PlayPcmWin 32ビット版 AIFFファイル読み込み高速化。
変更箇所
  * http://code.google.com/p/bitspersampleconv2/source/diff?spec=svn1344&r=1344&format=side&path=/trunk/PlayPcmWin/AiffReader.cs

### PlayPcmWin 1.0.65 ###
  * AIFFファイル読み込み高速化。
変更箇所
  * http://code.google.com/p/bitspersampleconv2/source/diff?spec=svn1341&r=1341&format=side&path=/trunk/PlayPcmWin/AiffReader.cs

### PlayPcmWin 1.0.54 ###
機能追加
  * AIFFファイル読み込み対応。

変更箇所
  * http://code.google.com/p/bitspersampleconv2/source/detail?r=1333
  * http://code.google.com/p/bitspersampleconv2/source/detail?r=1334
  * http://code.google.com/p/bitspersampleconv2/source/detail?r=1338

### PlayPcmWin 1.0.51 ###
バグ取り
  * CUEシートの文字コードをUTF-8と想定して読んでいたのを、ANSIと想定して読むように修正。
変更箇所
  * http://code.google.com/p/bitspersampleconv2/source/diff?spec=svn1330&r=1330&format=side&path=/trunk/PlayPcmWin/CueSheetReader.cs

### PlayPcmWin 1.0.50 ###
バグ取り
  * CUEシートに書いてあるファイルが存在しない場合にエラーを表示。
変更箇所
  * http://code.google.com/p/bitspersampleconv2/source/detail?r=1326

### PlayPcmWin 1.0.49 ###
バグ取り
  * Single WAV File CUEシートの読み込みの無駄をなくす。
変更箇所
  * http://code.google.com/p/bitspersampleconv2/source/detail?r=1321

### PlayPcmWin 1.0.48 ###
バグ取り
  * CUEシートの再生開始時間がファイルの最後よりも後を指している場合に異常終了する不具合を修正。
変更箇所
  * http://code.google.com/p/bitspersampleconv2/source/detail?r=1314

### PlayPcmWin 1.0.46 ###
機能追加
  * CUEシート対応(2)単一WAVファイルの一部を切り取るタイプのCUEシートに対応。
変更箇所
  * http://code.google.com/p/bitspersampleconv2/source/detail?r=1310

### PlayPcmWin 1.0.44 ###
機能追加
  * CUEシート対応。
  * MMCSS設定に"Playback"追加。

バグ取り
  * 1.0.43でIAudioClient::Initialize失敗時のメッセージ表示が、バグにより、エラーコードを含まなくなっていたのを修正。

変更箇所
  * http://code.google.com/p/bitspersampleconv2/source/detail?r=1296

### PlayPcmWin 1.0.43 ###
機能追加
  * 量子化ビット数32ビット(SInt32またはSFloat32)のWAVファイル対応。
  * デバイスに送るデータの量子化ビット数固定機能に、Float32に固定する機能を追加。

↓[詳細設定]画面

![http://bitspersampleconv2.googlecode.com/files/PlayPcmWin143Settings.png](http://bitspersampleconv2.googlecode.com/files/PlayPcmWin143Settings.png)

変更箇所
  * http://code.google.com/p/bitspersampleconv2/source/detail?r=1249
  * http://code.google.com/p/bitspersampleconv2/source/detail?r=1267
  * http://code.google.com/p/bitspersampleconv2/source/detail?r=1268
  * http://code.google.com/p/bitspersampleconv2/source/detail?r=1269
  * http://code.google.com/p/bitspersampleconv2/source/detail?r=1270

### PlayPcmWin 1.0.42 ###
機能追加
  * PlayPcmWin 64ビット版のFLAC対応
変更箇所
  * http://code.google.com/p/bitspersampleconv2/source/detail?r=1240
  * http://code.google.com/p/bitspersampleconv2/source/detail?r=1241
  * http://code.google.com/p/bitspersampleconv2/source/detail?r=1242
  * http://code.google.com/p/bitspersampleconv2/source/detail?r=1243
  * http://code.google.com/p/bitspersampleconv2/source/detail?r=1244
  * http://code.google.com/p/bitspersampleconv2/source/detail?r=1245

### PlayPcmWin 1.0.41 ###
機能追加
  * PlayPcmWin 32ビット版FLAC対応(実験的)

### PlayPcmWin 1.0.39 ###
機能追加
  * 詳細設定ダイアログ追加。
  * デバイスの量子化ビット数を固定する機能。
  * 再生リストにサンプリング周波数が異なるファイルを混在できるように修正。
バグ取り
  * 再生中に再生リストの項目を選択し曲変更したときに、再生リストの選択項目表示がバタバタする問題を少し修正。

### PlayPcmWin 1.0.36 ###
バグ取り
  * 最初のフレームが2回再生されて音飛びみたいになる現象を修正。
  * ウィンドウをリサイズするとここまで一括読み込みボタンの位置が変になる問題を修正。
機能追加
  * デバイス選択ボタン、デバイス選択解除ボタンを削除。

### PlayPcmWin 1.0.35 ###
  * 最初のフレームが2回再生されて音飛びみたいになる現象あり。
バグ取り
  * 再生グループをまたぐ曲変更ができない問題を修正。
  * デバイスを選択した後、デバイスを選択解除しもう一度デバイスを選択すると異常終了する問題を修正。
  * プログラム終了時のバックグラウンドスレッド停止待ち合わせ処理がバグッたのを修正。
  * 次の曲、前の曲ボタンがバグっている問題を修正。

### PlayPcmWin 1.0.34 ###
  * とりあえず動作するように修正。

### PlayPcmWin 1.0.33 ###
  * 再生リストの一部をメモリにロードする機能。
  * 設定をIsolatedStorageに保存するようにした。
  * 最初のフレームが2回再生されて音飛びみたいになる現象あり。
  * バグっていて全く動かない。

### PlayPcmWin 1.0.32 ###
  * ビルド構成が1.0.31で間違っていたのを修正。
  * 64ビット版も、ついに修正された。

### PlayPcmWin 1.0.31 ###
  * 再生中にマウスで再生リストの項目を選択すると選択された曲を再生するように修正。
  * ウィンドウサイズを変更するとステータスバーの位置がおかしくなる問題を修正。

### PlayPcmWin 1.0.30 ###
  * バージョン番号表示。

### PlayPcmWin 1.0.29 ###
  * 再生リストを編集できるステートの判定が、実際よりも緩かったのを修正。

### PlayPcmWin 1.0.28 ###
  * ステータスバーに使用方法ガイドを表示。
  * 64ビット版の謎のバグを修正し、動作するようになったと思ったが、まだバグっていて再生ができない。

### PlayPcmWin 1.0.27 ###
  * 32ビット版の、メモリ消費量削減。メモリ消費量がWAVファイルサイズの1倍～2倍ぐらいになった。
  * 1.0.27の64ビット版ビルドは、バグって動作しない

### PlayPcmWin 1.0.26 ###
  * 32ビット版で、メモリ不足発生時に、メッセージを表示するように修正したつもり。
  * 1.0.26の64ビット版ビルドは、バグって動作しない

### PlayPcmWin 1.0.24 ###
  * 再生リスト一覧のクリアー時にGarbage Collectするように修正。

### PlayPcmWin 1.0.23 ###
  * デバイス選択以降にリピートチェックボックスの状態を変更しても反映しない問題を修正。

### PlayPcmWin 1.0.21 ###
  * 再生リストへのファイルの追加が都合により出来なかった場合のメッセージ表示。

### PlayPcmWin 1.0.19 ###
  * Windows Media PlayerでリッピングしたWAVEファイルが読み込めない問題を修正。

### PlayPcmWin 1.0.18 ###
  * 複数ファイルの読み込みと連続再生に対応。

### PlayPcmWin 1.0.17 ###
  * バージョン番号のあげ忘れを修正。

### PlayPcmWin 1.0.16 ###
  * 共有モードイベント駆動が、動くようになった。

### PlayPcmWin 1.0.15 ###
  * 共有モードに対応し始めた。共有モードタイマー駆動は動作する。共有モードイベント駆動はバグっていて動作しない。
  * 共有モードでは、今の作りでは44.1kHzと48kHzしか再生できないようだ。
  * WASAPI排他モード タイマー駆動モードに長らくあった、バッファーオーバーランの不具合を修正。

### PlayPcmWin 1.0.14 ###
  * WAVファイルのドラッグアンドドロップに対応。ただし、1ファイルしかドロップできず、ドロップできる場所も変である。

### PlayPcmWin 1.0.13 ###
  * 再生中の描画負荷を下げた。

### PlayPcmWin 1.0.12 ###
  * バッファ詰めスレッドの優先度タイプ設定「Pro Audio」が選択できるようになった。

### PlayPcmWin 1.0.11 ###
  * PlayPcmWin 1.0.10でイベント駆動モードがバグッたのを修正。
  * 64ビット版を作成。

### PlayPcmWin 1.0.10 ###
  * 「対応フォーマット」ボタンを押したときにIAudioClient::GetDevicePeriodの値を表示するようにした。

### PlayPcmWin 1.0.9 ###
  * 従来のイベント駆動モードに加えてタイマー駆動モードを追加。

### PlayPcmWin 1.0.8 ###
  * インストーラープログラムを修正。

### PlayPcmWin 1.0.7 ###
  * 量子化ビット数24ビット 2チャンネルのWAVファイルにも対応した。量子化ビット数24ビットでビットマッチ出力しているかどうかについては未調査。
  * デフォルトの出力レイテンシーを100msに設定。
  * 対応フォーマット一覧出力機能。

### PlayPcmWin 1.0.5 ###
  * バージョン番号の上げ忘れを修正。

### PlayPcmWin 1.0.4 ###
  * 出力レイテンシーをデフォルトの10ミリ秒以外に設定できるようにした。

### PlayPcmWin 1.0.3 ###
  * サンプリング周波数等の設定に失敗したあとに、もう一度設定しようとすると異常終了する不具合を修正。

### PlayPcmWin 1.0.1 ###
  * コンソールプログラムをやめてウィンドウが出るようにした。
  * .NET Framework 4.0 Client profileを使用。

### バージョン1.2 ###
  * ビットマッチ再生

### バージョン1.1 ###
  * 最終フレームのサイズが中途半端になって失敗する問題を修正。
  * 再生位置の変数をmutexで守るようにした。

### バージョン1.0 ###

  * 量子化ビット数16ビット、2チャンネル、非圧縮PCMのWAVEファイルに対応。
  * 音は一応鳴るが、最終フレームのバッファ確保のサイズ計算に問題があり、ほとんど必ず最後にエラーメッセージが出て終了します。