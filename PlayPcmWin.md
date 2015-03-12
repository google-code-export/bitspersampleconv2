[English](http://code.google.com/p/bitspersampleconv2/wiki/PlayPcmWinEn) | 日本語

# PlayPcmWinのページ #

PlayPcmWinはWASAPI排他モードでWAV,AIFF,FLACファイルを再生するプログラムです。

![http://bitspersampleconv2.googlecode.com/files/ppw3070ss2_jp.png](http://bitspersampleconv2.googlecode.com/files/ppw3070ss2_jp.png)

# 特徴 #
  * 量子化ビット数16、24、32ビットの2チャンネルステレオ非圧縮WAVファイル再生に対応。
  * 量子化ビット数16ビットと24ビットの2チャンネルステレオFLACファイル再生に対応。
  * 量子化ビット数16ビットと24ビットの2チャンネルステレオAIFFファイル再生に対応。
  * DSDIFF(DFF)ファイルとDSFファイルをDoPで再生する機能。
  * 再生リスト。ギャップレス再生。
  * CUEシートとM3U8再生リストの読み込みに対応。
  * WASAPI排他モード使用。Bit perfect再生可能。
  * WASAPI排他 タイマー駆動モードとイベント駆動モードの切り替え機能。
  * 再生スレッドの優先度設定機能。
  * 再生スレッドはネイティブC++で書かれており、音声データを全てメモリに読み込んでから再生するため、再生時のCPU負荷が低い。低レイテンシ設定で音切れの可能性が減ります。
  * ユーザーインターフェースのコードは .NET Framework 4.0のC#で書かれています
  * Windows 7に対応しております。Windows XPでは動きません。Vistaは、WASAPI排他モードは動きます。
  * ノンリアルタイム(低速)アップサンプリング機能。

PlayPcmWinはWASAPIの実験プログラムです。音楽を聴くのにもそこそこ使えるようになってきましたが、対応ファイル形式が少ないです。WASAPI排他モードで、いろいろなファイルフォーマットで保存されている音楽を聴きたい場合は、Foobar2000のfoo\_out\_wasapiプラグインを使うのが便利です。Foobar2000のfoo\_out\_wasapiプラグインは、WASAPI排他タイマー駆動モードで動作しているようです。(2012年9月14日追記: Foobar2000 WASAPI出力プラグインバージョン3.0でイベント駆動もできるようになっています)


# ダウンロード #

安定版　(バージョン 4.0.78)
  * 32ビット版 http://sourceforge.net/projects/playpcmwin/files/PlayPcmWin/PlayPcmWin478jp.zip/download
  * 64ビット版 http://sourceforge.net/projects/playpcmwin/files/PlayPcmWin/PlayPcmWin478x64jp.zip/download

非常にバグっぽい開発版
  * なし

その他の版のダウンロード
  * http://sourceforge.net/projects/playpcmwin/files/PlayPcmWin/

古い版のダウンロード
  * http://code.google.com/p/bitspersampleconv2/downloads/list

**(バージョン3.0.52以降に備わっている並列読み込み機能は実験的機能でまだ不安定なことがあるようです。とりあえず無効にしておいたほうが無難です。)**

# 32ビット版と64ビット版の違いについて #

| Windows  | PlayPcmWin | 結果 |
|:---------|:-----------|:-------|
| 32ビット版 | 32ビット版   | ○ |
| 64ビット版 | 64ビット版   | ○ |
| 32ビット版 | 64ビット版   | × |
| 64ビット版 | 32ビット版   | △ |

  * 32ビット版のWindowsの場合は32ビット版のPlayPcmWinを、64ビット版のWindowsの場合は64ビット版のPlayPcmWinを使用してください。
  * 32ビット版のWindowsで64ビット版のPlayPcmWinを動かすことはできません。
  * 64ビット版のWindowsで32ビット版のPlayPcmWinを実行することは可能ですが、使用可能なメモリ量が制限されるため、再生リストに貯められるWAVデータ数が減り、実用性が低下します。( [Issue 42](https://code.google.com/p/bitspersampleconv2/issues/detail?id=42) をご覧下さい)

# 使用方法 #

zipを展開して、中のSetup.exeを実行してインストールします。

インストール時に、マイクロソフト .NET Framework 4.0 Client Profileのインストールが必要になることがあります。

PlayPcmWinを開き、再生するWAVファイルを指定して、
使用する出力デバイスを選択し、再生ボタンを押します。

アンインストールは、[スタート][コントロールパネル][プログラムのアンインストール] または[プログラムと機能]で行うか、もう一度Setup.exeを実行して削除を選びます。

# 更新履歴 #

こちらをご覧下さい。
http://code.google.com/p/bitspersampleconv2/wiki/PlayPcmWinChangelog

# 対応オーディオデバイスについて #

こちらをご覧下さい。
http://code.google.com/p/bitspersampleconv2/wiki/WasapiExclusiveMode

# 既知のバグ一覧 #

最新版に存在する不具合一覧:
http://code.google.com/p/bitspersampleconv2/issues/list?can=2&q=&colspec=ID+Type+Status+Priority+Milestone+Owner+Summary&groupby=&sort=-id&x=&y=&cells=tiles&mode=grid

最新版では修正された不具合も含めて全て表示:
http://code.google.com/p/bitspersampleconv2/issues/list?can=1&q=&sort=-id&colspec=ID%20Type%20Status%20Summary

# MP3は再生できません #

PlayPcmWinはMP3ファイルは再生できません。AAC、WMA、ALAC等々の再生にも対応しておりません。
これらの形式のファイルはfoobar2000等の他の音楽再生ソフトを使用すると再生できます。

# 複数ファイルをエクスプローラーからPlayPcmWinにドラッグアンドドロップする際、エクスプローラーに並んでいる順番に再生リストに並べる方法 #

## 方法1 ##

PlayPcmWin 3.0.20より、エクスプローラーから再生リストにドロップ後、再生リスト項目の任意の列をつまんで移動することができるようになりました。

## 方法2 ##

エクスプローラーの横幅を大きめに広げ、ファイル一覧を表示し、Altキーを押し、[表示][詳細]で詳細表示モードにします。この時点で、再生したい順番にファイルが並んでいることを確認します。並んでいない場合は、[名前]を何度かクリックすると整列すると思います。

先頭の曲をマウスでクリックして選択状態にします。

[CTRL](CTRL.md)を押しながら[A](A.md)キーを押します。すべての曲が選択状態になります。

先頭の曲をマウスで選択し、PlayPcmWinにドロップします。

## 方法3 ##

エクスプローラーの横幅を大きめに広げ、ファイル一覧を表示し、Altキーを押し、[表示][詳細]で詳細表示モードにします。この時点で、再生したい順番にファイルが並んでいることを確認します。並んでいない場合は、[名前]を何度かクリックすると整列すると思います。

ファイル名表示が長すぎて行が画面の右端からはみ出ている場合は、項目名表示を縮めて、最も右側の項目が画面右端よりも左側にある状態にします。

マウスで一番下のファイル項目の下方か右側の何もないところをクリックし、クリックしたまま
そこから上方向になぞってすべての項目を選択状態にします。
その後、最も上の項目をつまんでドロップします。

## 方法4 ##

[CTRL](CTRL.md)を押しながら、エクスプローラー上の曲をマウスでクリックしていきます。

[CTRL](CTRL.md)を離し、1曲目を選択してPlayPcmWinにドロップします。


# .NET Framework 4.0 Client Profile(以下.NETと呼称)について #

.NETは、Windowsの機能を拡張するライブラリー群＋プラットフォーム独立の実行環境というか、うまく説明できてませんが、
要するに、これを利用するとWindowsアプリケーションを作るのが簡単になるという物です。

PlayPcmWinでは、ウィンドウを出したり、マウスからの入力を処理したりするのに.NETを使用しています。

詳しくはこちらをご覧ください。知らないおっさんが出てきて丁寧に解説して下さいます。
http://msdn.microsoft.com/ja-jp/netframework/ee847315.aspx

# [再生]ボタンを押すと、[メモリ不足です…]が出る問題 #

PlayPcmWinは音声データを全て仮想メモリに読み込んでから再生を開始します。

物理メモリが潤沢に利用出来る状態では、PlayPcmがアクセスする仮想メモリ空間が物理メモリにマップされます。

PCの搭載メモリ量が不足していて、しかも、ページファイルの容量が大きめに取ってある場合、
PlayPcmWinが本来意図した動作にならず、PlayPcmWinの使用する仮想メモリ空間の一部がページアウトによって
HDD上のページファイル内に置かれ、スラッシングが発生し、もっさりした動きになり、音切れが起きやすくなります。

目安としては、
32ビット版Windowsでメモリ3GB、64ビット版Windowsでメモリ4GBぐらいあると、PlayPcmWinはそこそこ使える状態になります。

32ビット版で、メモリ2GB以下の場合、PlayPcmWinはCD1枚分のプレイリストも扱えず、
あまり使い勝手がよくないです。

[ここまで一括読み込み]機能を使用すると、再生時に一度にメモリに置く音声データを減らすことができます。

# 量子化ビット数24ビットの音源が再生できるはずのUSBオーディオデバイスで再生失敗する問題について #

このバグはPlayPcmWin 4.0.29で修正しました。

# 量子化ビット数32ビットのファイルが、あまり再生できない問題 #

このバグはPlayPcmWin 4.0.29で修正しました。

# WAVファイルに曲情報を埋め込む方法 #

Windows Media Player12でCDをWAV形式でリッピングするとWAVファイルに曲情報が入ります。

iTunes 10は、CDをAIFF形式でリッピングするとファイルに曲情報が入りますが、WAV形式でリッピングすると入りません。

# WASAPI排他モードとWASAPI共有モードの違いについて #

  * WASAPI共有モードでは、オーディオデバイスのPCMフォーマット(サンプリング周波数と量子化ビット数の組み合わせ)は、オーディオデバイスのプロパティの[規定の形式]で設定した値に固定されています。再生するWAVファイルのフォーマットと、この設定値が一致していれば、問題なく再生できるらしいです。(2010年11月13日追記: WASAPI共有モードではリミッターAPOの処理が入るのと、ディザリング処理が行われるためにビットパーフェクトにはならないそうです。)オーディオデバイスが対応していないサンプリング周波数と量子化ビット数のデータも、フォーマット変換によって再生できる、というか、してもよい雰囲気があるというだけで、アプリケーション開発者がどうにかしてフォーマット変換してからWASAPIに渡す必要があるみたいです(これは大変な作業です。PlayPcmWin 3.0.96以前のバージョンはWASAPI共有モードのサンプルレート変換にWindows7で新設されたIAudioClockAdjustmentを使用しています。これは本来の用途とは違う使い方をしているため性能がいまいちです。PlayPcmWin3.0.98で、Windows7で新設されたResampler MFTを使用してWASAPI共有モードのサンプリングレート変換を行うように変更しました。)。複数の音声再生ソフトが1つのデバイスを共有できます（WASAPIの中でミックスされる)。PlayPcmWinはWASAPI排他モードの対応に力を入れており、WASAPI共有モードには力が入っていません。

  * WASAPI排他モードは、1つの音声再生ソフトがデバイスを占有(排他利用)でき、再生するファイルに応じてオーディオデバイスのサンプリング周波数と量子化ビット数を設定してから音声を再生します。そのためオーディオデバイスが対応しているサンプリング周波数と量子化ビット数のみ再生できます。ビットパーフェクト再生が可能と言われています。WASAPI排他モードで音声を再生したら、どんな場合も必ずビットパーフェクト再生になるというわけではなく、デバイスによっては、ビットパーフェクト再生するための追加設定が要ることがあります。どう設定してもビットパーフェクト再生ができないデバイスもあります。WASAPI排他モードは、他に WASAPI共有モードよりも低レイテンシー動作が可能であるということが言われています。

# イベント駆動モードとタイマー駆動モードの違いについて #

  * イベント駆動モードでは、WASAPIから、そろそろ次のデータを下さいというイベントが来たら再生スレッドが起き、再生スレッドは次のデータをWASAPIに送ります。再生スレッドが起床する時間間隔は、IAudioClient::Initialize時に設定した レイテンシー時間(PlayPcmWinでは、[出力レイテンシー]で設定した時間)で、このとき送るデータのサンプル数は、IAudioClient::Initialize時に設定した レイテンシー時間 x サンプリング周波数 (サンプル)です。

  * タイマー駆動モードは、再生スレッドが自発的に一定間隔で起きて、オーディオデバイスのバッファの空きサイズを調べてその分のデータをWASAPIに送ります。再生スレッドが起床する時間間隔は、IAudioClient::Initialize時に設定した レイテンシー時間 ÷ 2です。

同じレイテンシ設定でも、イベント駆動モードの方がスレッドが起きる頻度が少なくてすみ、
CPU負荷が低くなります。

イベント駆動モードよりもタイマー駆動モードの方が動作する組み合わせ(オーディオデバイス、サンプリング周波数、量子化ビット数の組み合わせ)の数が多いです。

# レンダー(再生)スレッドタスクタイプ"Audio"と"Pro Audio"の違いについて #

これは、
AvSetMmThreadCharacteristics()関数の第1引数です。

この引数の説明はMultimedia Class Scheduler Service(MMCSS)の解説にあります

http://msdn.microsoft.com/en-us/library/ms684247%28v=VS.85%29.aspx

Pro Audioにすると、出力レイテンシーを10ミリ秒よりも短く設定した場合に
音切れの可能性が減るらしいです。
実際に試すと、あまり効果はありませんでした。

[設定なし]を選ぶと、再生スレッドはAvSetMmThreadCharacteristics()を呼びません。
この場合でも、スレッドの状態を観察すると、AvSetMmThreadCharacteristics()を呼ばなくても、IAudioClient::Initialize()呼出によって何らかのMMCSS的な処理が暗黙的に呼ばれているようです。

# RME FireFace 400/800/UC/UFXやM-AUDIO ProFireでWASAPI排他モードを使うときのコツ #

これらのデバイスのドライバは、
WASAPIからデバイスのマスターサンプリングレートの変更ができません。
タスクトレイの「Fireface Settings」や「M-Audio ProFire」でマスターサンプリングレートを、再生するWAVファイルのサンプリングレートに予め設定してください。

Echo AudioFireシリーズも同様のようです。

これらのデバイスがWASAPIからのマスターサンプリングレート変更に対応していない理由は、サンプリングレートが増えるに従って利用可能な入出力チャンネル数が減るためであると推測しますが真相はわかりません。(たとえばマスターサンプリングレートが44.1kHzで18チャンネルI/Oが利用可能で、192kHzでは10チャンネルに減るデバイスで、マスターサンプリングレートが44.1kHzに設定されている状態で18チャンネル目を選択してからWASAPIのAPI呼び出しによってサンプリングレートを192kHzに変更すると18チャンネル目が消滅して大変な不都合が起きます。ASIOでは処理の手順がデバイスとサンプリングレートを設定してから利用可能なチャンネルの一覧を得るようになっているのでこの問題は起きません。)

# Creative X-FiTiHDで44.1kHzと88.2kHzのイベント駆動再生ができない問題 #

Creative X-FiTiHDは、44.1kHzサンプリングと88.2kHzサンプリングでWASAPI排他イベント駆動モードが使えません。
WASAPI排他タイマー駆動モードにすれば動きます。

理由はわかりません。PlayPcmWinのバグの可能性もあります。

# RME FF400で出力レイテンシーをすごく短く設定したい場合 #

WASAPI排他モードを使用します(共有モードでは30ミリ秒よりも短くできません)。
WASAPIイベント駆動モードを使用します(タイマー駆動モードの2倍のイベント間隔になり、
同じレイテンシーでもバッファーサイズを大きくできます)。

タスクトレイの「Fireface Settings」を起動し、Fireface(1)タブのBuffer Size(Latency)を小さい値にします。

レイテンシー設定の組み合わせと音切れの有無(44.1kHz 16bit stereo イベント駆動)

| Fireface Settings の Buffer Size(Latency) | PlayPcmWinの出力レイテンシー設定 | IAudioClient::GetBufferSize()の戻り値|PCが低負荷状態での音切れ有無 |
|:-------------------------------------------|:--------------------------------------------|:-----------------------------------------|:-----------------------------------------|
| 1024 Samples | 13 ミリ秒|573サンプル | 無 |
| 1024 Samples | 12 ミリ秒|529サンプル | 有 |
| 256 Samples | 13 ミリ秒|573サンプル | 無 |
| 256 Samples | 12 ミリ秒|529サンプル | 有 |
| 64 Samples | 4 ミリ秒|176サンプル | 無 |
| 64 Samples | 3 ミリ秒|132サンプル | 有 |
| 48 Samples | 3 ミリ秒|132サンプル | 無 |
| - | 2 ミリ秒|- | 設定不可 |

排他モードでデバイスを占有したまま「Fireface Settings」で、この設定をいじくっていると、BSODが発生しますがデバイスドライバの不具合でありPlayPcmWinの不具合ではありません。

出力レイテンシーを短く設定するのは、音切れの可能性が上がるので、おすすめしません。

3ミリ秒の設定では、再生中にエクスプローラーでフォルダーを開くだけで音切れします。音質以前の問題です。

出力レイテンシーを長く設定すると、再生スレッドのCPU負荷が軽くなります。

# RME FF400でマルチチャンネル再生する方法 #

Windowsの設定が必要です。[Issue 77](https://code.google.com/p/bitspersampleconv2/issues/detail?id=77) のComment 4をご覧下さい。
なおLynx Aurora + Lynx AES16eは特に設定を行わなくてもマルチチャンネル再生できました。

# 実行中のPlayPcmWinのバージョン番号を知る方法 #

PlayPcmWinのメニューバーの[ヘルプ][バージョン情報]を選ぶと、バージョン番号が表示されます。

# ビットパーフェクト(bit exact, bit match…)再生しているかどうか調べる方法 #

デジタル出力の場合は、出力データをPCMレコーダーで録音して、録音後のWAVファイルと
元のWAVファイルをバイナリ比較することで、ビットパーフェクトかどうかを知ることができます。

ビットパーフェクト再生するためのコツ
  * Windowsのボリューム設定が効くWASAPIデバイスの場合は、Windowsのボリューム設定をMAXにする必要があります。
  * さらに、Creative X-Fiシリーズの場合、[Creativeオーディオコントロールパネル]を開き[ビットマッチ]タブで[ビットマッチプレイバックを有効にする]にチェックします。

# DACが安定するまで無音を送出する機能 #

これは、再生開始直後から音が入っている状態でPCMデータを送出すると、最初のほうのデータが捨てられ途中から再生される(ブチッという音がして再生が始まります)場合があるということで、再生開始後しばらくの間無音を送出して、意味のあるデータが捨てられないようにするという対策です。特にPCからデジタルで出し外付けDACやPCMレコーダーを接続する場合に比較的長い値(1秒程度)を入れる必要があるようです。

# 64ビット版Windows上で実行しているプログラムが64ビット版かどうか調べる方法 #

CTRLを押しながらSHIFTを押しながらESCを押してWindows タスクマネージャーを開きます。

Windows タスクマネージャーの[プロセス]タブを開いて、イメージ名一覧を見ます。
名前の最後に ＊32と付いているものは
32ビット版のプログラムで、付いていないものは64ビット版のプログラムです。

# ソースコード #

  * http://code.google.com/p/bitspersampleconv2/source/browse/#svn/trunk/PlayPcmWin
  * http://code.google.com/p/bitspersampleconv2/source/browse/#svn/trunk/WasapiCS
  * http://code.google.com/p/bitspersampleconv2/source/browse/#svn/trunk/WasapiIODLL
  * http://code.google.com/p/bitspersampleconv2/source/browse/#svn/trunk/WavRWLib2

WASAPIの関数を呼び出しているところは、WasapiUser.cppにあります。
http://code.google.com/p/bitspersampleconv2/source/browse/trunk/WasapiIODLL/WasapiUser.cpp

ユーザーインターフェースのコードは、MainWindow.xaml.csです。
http://code.google.com/p/bitspersampleconv2/source/browse/trunk/PlayPcmWin/MainWindow.xaml.cs

ソースコードをコンパイルしてexeを作りたい場合

  * Sourceページに行ってソースコードをTortoiseSVNなどでチェックアウトしてきます。
  * PlayPcmWin/PlayPcmWin.slnをVisual Studio 2010で開いてビルド。
  * 別途libFLACのソースコードと、libFLACをビルドするためのliboggのソースコードが必要です。 http://sourceforge.net/projects/flac/
  * ビルド方法の詳しい説明: PlayPcmWinHowToBuild

# FLAC読み込み対応について #

PlayPcmWin.exeがFLAC読み込み処理で使用しているFlacDecode.exeは
libFLACの機能を使用しています。

libFLACのライセンスは New BSD Licenseです。

ライセンスの全文はここで読むことができます。

http://code.google.com/p/bitspersampleconv2/source/browse/trunk/PlayPcmWin/libFlacLicense.txt

PlayPcmWinをインストールすると、デフォルトで
```
C:\Program Files\yamamoto2002\PlayPcmWin\libFlacLicense.txt
```
に同内容のテキストファイルがコピーされます。

ちなみにPlayPcmWin.exeのライセンスはMITライセンスです。雑誌掲載、無料配布、転載、コピペ、有料販売などを作者に無断で自由に行うことが**できる**というようなライセンスだったと思います。ライセンスの全文
http://code.google.com/p/bitspersampleconv2/source/browse/trunk/PlayPcmWin/PlayPcmWinLicense.txt

量子化ビット数32ビットのFLACファイルの読み込みは対応しておりません。
これは、現時点でlibFLACが対応していないためです。

## ファイルの並列読み込みについて ##

詳細設定画面で[ファイル読込並列化]にチェックを入れるとファイル読み込み処理が並列化されます。デュアルコアCPU、クアッドコアCPU、ヘキサコアCPU等々でFLAC圧縮ファイルの読み込み時間が短くなります。


# 再生スレッドの動作説明 #

再生スレッドは、PCMデータをWASAPIに送るスレッドです。

WasapiUser.cppに書かれています。

全部で200行ほどの短いプログラムです。

http://code.google.com/p/bitspersampleconv2/source/browse/trunk/WasapiIODLL/WasapiUser.cpp#692

## 再生スレッド 関数呼び出しグラフ ##

```
WasapiUser::RenderEntry()…………………… ※1
└WasapiUser::RenderMain()……………………※2
　├::CoInitialize()……………………………※3
　├::timeBeginPeriod()……………………… ※4
　├::AvSetMmThreadCharacteristics()………※5
　├::WaitForMultipleObjects()………………※6
　├WasapiUser::AudioSamplesSendProc()……※7
　│├::WaitForSingleObject()……………… ※8
　│├IAudioClient::GetCurrentPadding()… ※9
　│├IRenderClient::GetBuffer()……………※10
　│├WasapiUser::CreateWritableFrames()…※11
　││└::CopyMemory()…………………………※12
　│├::memset()…………………………………※13
　│├IRenderClient::ReleaseBuffer()………※14
　│└::ReleaseMutex()…………………………※15
　├::AvRevertMmThreadCharacteristics()… ※16
　├::timeEndPeriod()………………………… ※17
　└::CoUninitialize()…………………………※18
```

  * ※1…再生スレッドのエントリーポイントです。staticメンバー関数です。
  * ※2…WasapiUserインスタンスの再生スレッドエントリポイントです。
  * ※3…COMの初期化。※18と対になっています。
  * ※4…マルチメディアタイマーの精度を1ミリ秒に設定します。これにより、タイマー駆動モードのスレッド起床タイミングの精度が高まることが期待できます。※17と対になっています。この処理は詳細設定の[timeBeginPeriod(1)を呼ぶ]がチェックされているときに行われます。
  * ※5…(必要な場合のみ)MMCSSのスレッド優先度設定。※16と対になっています。
  * ※6…タイマー駆動モードの場合、ここで一定時間待ちます。イベント駆動モードの場合、WASAPIが次のデータを要求してくるまで待ちます。どちらの場合も、同時にスレッド停止要求も監視して、スレッド停止要求が発生したら※16に行きます。
  * ※7…WASAPIにPCMデータを送れるだけ送る関数です。
  * ※8…再生位置情報を守るミューテックスを取得します。※15と対になっています。
  * ※9…WASAPIイベント駆動モード以外の場合、WASAPIに送ることができないサンプル数を取得します。
  * ※10…WASAPIデバイスのPCMバッファーを取得します(to)。※14と対になっています。
  * ※11…WASAPIに送るPCMデータをtoに作る関数です。
  * ※12…PCMデータをWWPcmDataから場所toにコピーします。
  * ※13…(必要な場合のみ)無音をtoにセットします。
  * ※14…toをリリースします。
  * ※15…ミューテックスを開放します。※6に行きます。
  * ※16…MMCSSのスレッド優先度設定を元に戻します。
  * ※17…マルチメディアタイマーの精度設定を元に戻します。
  * ※18…COMの使用を終了します。

音声データ送信しているあいだ、再生スレッドは※6で起きて、※7～※15まで実行し、※6に戻って寝る、という処理を繰り返します。

WASAPI排他イベント駆動モードで、出力レイテンシー設定200ミリ秒の場合、※6～※15の処理を、1秒間に5回行います。

# 参考文献 #

http://msdn.microsoft.com/en-us/library/dd370844%28v=VS.85%29.aspx

# 作者のメールアドレス #

yamamoto2002@gmail.com