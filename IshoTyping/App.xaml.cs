using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Xml;
using System.Windows.Input;
using Microsoft.Research.DynamicDataDisplay;
using Microsoft.Research.DynamicDataDisplay.DataSources;
using Microsoft.Research.DynamicDataDisplay.PointMarkers;
using TweetSharp;



namespace IshoTyping
{
    /// <summary>
    /// App.xaml の相互作用ロジック
    /// </summary>
    /// 


    public partial class App : Application
    {
        static string Version = "1.3.1";
        static bool SV = false; // サテライト可能の場合のみ true にする
        static string path = "app_a.config"; // データ保存先

        string consumerkey = "nQlUAeqx2VmNhsMEuzmjg";
        string consumersecret = "4IZfxxdCwpdQhmZigvdpVFavKzztsm9eHqDGEbpFkyI";

        static string symbols = "!\"#$%&'()-^\\@[;:],./=~～〜|`{+*}<>?_ 　・･、､。｡「」｢｣゛ﾞ゜ﾟ"
            + "！”＃＄％＆’（）－―＾￥｜＠｀［｛；＋：＊］｝＜＞？＿"
            + "ー1234567890abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ←↑→↓"; // 入力可能な記号
        static string symbolsafter = "!\"#$%&'()-^\\@[;:],./=~～～|`{+*}<>?_  ・・、、。。「」「」゛゛゜゜"
            + "!\"#$%&'()ーー^\\|@`[{;+:*]}<>?_ー1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZABCDEFGHIJKLMNOPQRSTUVWXYZ←↑→↓";

        List<FolderList> flist;
        List<List<MusicList>> fmlist = new List<List<MusicList>>();

        /// <summary>
        /// 歌詞データを保存する
        /// サテライトの時は1以降も使うが、ソロプレイは0のみ
        /// 
        /// </summary>
        List<List<LyricsData>> lyricsdata = new List<List<LyricsData>>();
        int folderid, selectedid;
        int lyricsdata0needtype;
        int lyricsdata0needkpm;
        int lyricsdata0highscoredatanum = -1;
        string lyricsdata0hashcode;

        int musicoffset = 0;

        List<HighScoreData> highscores = new List<HighScoreData>();

        MainWindow mainWindow;

        uint experimentalvalue;


        // 音楽再生関係の変数
        MediaTimeline _audioTimeline;
        MediaClock _audioClock;
        MediaPlayer _audioPlayer;
        double volume = 1.0;


        // 設定関係

        /// <summary>
        /// ローマ字の設定 詳細は別
        /// </summary>
        byte[] romajisetting = new byte[32];

        /// <summary>
        /// kpmの計算方法を設定
        /// false:初速を含める　true:初速を含めない（より正確）
        /// </summary>
        bool kpmswitch = false;

        /// <summary>
        /// 正打での加点
        /// </summary>
        int correctadd = 10;

        /// <summary>
        /// ミスでの減点
        /// </summary>
        int missminus = 5;

        /// <summary>
        /// 1秒での点数
        /// </summary>
        int pps = 100;

        /// <summary>
        /// スキップでどこまで飛ばすか
        /// 現在のところ不安定なのでstatic扱い
        /// </summary>
        static int skiprest = 3000;

        int settingoffset = 0;

        double fontsize = 30.0;

        static string Accestoken = "";
        static string Accestokensecret = "";





        // プレイ関係の変数

        int points, correct, miss, complete, failed, nowcombo, maxcombo;

        int viewpoints;

        /// <summary>
        /// 打鍵に要している時間
        /// </summary>
        int typedmillisecond;

        /// <summary>
        /// 初速の合計
        /// </summary>
        int firstmillisecond;

        /// <summary>
        /// 現在の行数
        /// </summary>
        int nowline;

        /// <summary>
        /// 現在の行の進行状況
        /// -1:打つ文字がもともとない
        /// 0:打ち終わった
        /// 1:打ち終わっていない
        /// </summary>
        int linemode;

        /// <summary>
        /// ひらがなで今打っている文字の位置。
        /// 「いえないことば」の「な」は「2」
        /// </summary>
        int cursor;

        /// <summary>
        /// ひらがなブロックの文字数。sを抜かさない。
        /// つ→1 ちゅ→2 っぴょ→3 など。
        /// 「いっしょだね」の「っしょ」は cursor=2, blockcounts=3
        /// </summary>
        int blockcounts;

        /// <summary>
        /// 次に打てるものを並べる。一番前が一番優先。大文字。
        /// 「っしょ」→「SLX」など
        /// </summary>
        string nextstring = "";

        /// <summary>
        /// nextstringの一番前に対する残りのもの。大文字。
        /// 「っしょ」→「SHO」など
        /// </summary>
        string nextstringa = "";

        /// <summary>
        /// ひらがなブロックの中ですでに打ち込んだローマ字。
        /// </summary>
        string blocktyped = "";

        /// <summary>
        /// N を2回打っていい時かどうかを判断する。
        /// </summary>
        bool ndouble = false;

        /// <summary>
        /// 何も打たないで「failed」になった行の数
        /// </summary>
        int nothingfailed;

        /// <summary>
        /// 結果をtweetする際の内容（コメントは含まない。tweet時に無理矢理くっつける）
        /// </summary>
        string resulttweetcontent1 = "";
        string resulttweetcontent2 = "";
        string resulttweetcontent3 = "";



        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // これをしないとグラフを書くときにエラーを吐いてしまう（よくわからん）
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Critical;

            initialize(0);
        }

        private void EndSelect(object sender, EventArgs e)
        {
            MessageBoxResult messageBoxResult = MessageBox.Show("Are you sure to end IshoTyping?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);
            switch (messageBoxResult)
            {
                case MessageBoxResult.Yes:
                    Environment.Exit(0);
                    break;
                case MessageBoxResult.No:
                    break;
            }
        }

        private void initialize(int mode)
        {
            mainWindow = new MainWindow();
            mainWindow.Title = "IshoTyping " + Version;

            if (SV)
                mainWindow.VersionTextBlock.Text = "IshoTyping " + Version + " Special Edition";
            else
                mainWindow.VersionTextBlock.Text = "IshoTyping " + Version;

            // フォルダリスト読み込み
            if (!File.Exists("folderlist.xml"))
            {
                MessageBox.Show("folderlist.xmlが読み込めません。");
                Environment.Exit(0);
            }
            flist = new List<FolderList>();
            List<string> list = new List<string>();
            FileStream fStream = new FileStream("folderlist.xml", FileMode.Open, FileAccess.Read);
            XmlTextReader reader = new XmlTextReader(fStream);
            try
            {
                while (reader.Read())
                {
                    reader.MoveToContent();
                    if (reader.NodeType == XmlNodeType.Element && reader.HasAttributes)
                    {
                        if (reader.Name == "folder")
                        {
                            string n = "";
                            string p = "";
                            for (int i = 0; i < reader.AttributeCount; i++)
                            {
                                reader.MoveToAttribute(i);
                                if (reader.Name == "path")
                                {
                                    p = reader.Value;
                                }
                                else if (reader.Name == "name")
                                {
                                    n = reader.Value;
                                }
                            }
                            flist.Add(new FolderList(n, p));
                            list.Add(n);
                        }
                    }
                }
            }
            catch (XmlException xmlex)
            {
                MessageBox.Show("folderlist.xmlを読み込む際にエラーが発生しました。\n\n" + xmlex);
                Environment.Exit(0);
            }
            reader.Close();
            fStream.Close();

            for (int i = 0; i < flist.Count; i++)
            {
                string musicxmlpath = flist[i].directory;

                if (!File.Exists(musicxmlpath))
                {
                    flist.RemoveAt(i);
                    list.RemoveAt(i);
                    --i;
                }
                else
                {
                    List<MusicList> mlist = new List<MusicList>();
                    fStream = new FileStream(musicxmlpath, FileMode.Open, FileAccess.Read);
                    reader = new XmlTextReader(fStream);

                    MusicList tmpm = new MusicList();
                    try
                    {
                        while (reader.Read())
                        {
                            //reader.MoveToContent();
                            if (reader.NodeType == XmlNodeType.Element)
                            {
                                if (reader.Name == "musicinfo")
                                {
                                    tmpm = new MusicList();
                                    string xp = "";
                                    string mp = "";
                                    for (int j = 0; j < reader.AttributeCount; j++)
                                    {
                                        reader.MoveToAttribute(j);
                                        if (reader.Name == "xmlpath")
                                        {
                                            xp = reader.Value;
                                        }
                                        else if (reader.Name == "musicpath")
                                        {
                                            mp = reader.Value;
                                        }
                                    }
                                    tmpm.xmlpath = xp;
                                    tmpm.musicpath = mp;
                                }
                                else if (reader.Name == "musicname")
                                {
                                    try
                                    {
                                        tmpm.name = reader.ReadString();
                                    }
                                    catch (XmlException xe)
                                    {
                                        Console.WriteLine(xe.StackTrace);
                                    }
                                }
                                else if (reader.Name == "artist")
                                {
                                    try
                                    {
                                        tmpm.artist = reader.ReadString();
                                    }
                                    catch (XmlException xe)
                                    {
                                        Console.WriteLine(xe.StackTrace);
                                    }
                                }
                                else if (reader.Name == "genre")
                                {
                                    try
                                    {
                                        tmpm.genre = reader.ReadString();
                                    }
                                    catch (XmlException xe)
                                    {
                                        Console.WriteLine(xe.StackTrace);
                                    }
                                }
                            }
                            else if (reader.NodeType == XmlNodeType.EndElement && reader.Name == "musicinfo")
                            {
                                mlist.Add(tmpm);
                            }
                        }
                    }
                    catch (XmlException xe)
                    {
                        MessageBox.Show(musicxmlpath + "に問題があります。\n\n" + xe.ToString());
                    }
                    reader.Close();
                    fStream.Close();
                    fmlist.Add(mlist);
                }
            }

            mainWindow.folderlistbox.ItemsSource = list;

            // イベントリスナの実装
            mainWindow.folderlistbox.SelectionChanged += folderlistbox_SelectionChanged;
            mainWindow.musiclistview.SelectionChanged += musiclistview_SelectionChanged;
            mainWindow.searchbox.KeyDown += searchbox_KeyDown;
            mainWindow.PlayButton.Click += PlayButton_Click;
            mainWindow.Copy_Hash_Value.Click += Copy_Hash_Value_Click;
            mainWindow.DeleteHighScoreButton.Click += DeleteHighScoreButton_Click;

            mainWindow.ReadyGridPanel.KeyDown += ReadyGridPanel_KeyDown;
            mainWindow.ReadyGridPanel.IsVisibleChanged += ReadyGridPanel_IsVisibleChanged;
            mainWindow.ReadyGridPanel.LostKeyboardFocus += ReadyGridPanel_LostKeyboardFocus;
            mainWindow.PlayMainGridPanel.KeyDown += PlayMainGridPanel_KeyDown;
            mainWindow.PlayMainGridPanel.LostKeyboardFocus += PlayMainGridPanel_LostKeyboardFocus;
            mainWindow.CharacterSizeSlider.ValueChanged += CharacterSizeSlider_ValueChanged;
            mainWindow.VolumeSlider.ValueChanged += VolumeSlider_ValueChanged;
            mainWindow.TweetCommentTextBox.TextChanged += TweetCommentTextBox_TextChanged;
            mainWindow.ResultCopyButton.Click += ResultCopyButton_Click;
            mainWindow.TweetButton.Click += TweetButton_Click;
            mainWindow.ReplayButton.Click += ReplayButton_Click;

            mainWindow.KpmSwitchCheckBox.Click += KpmSwitchCheckBox_Click;
            mainWindow.OffsetSlider.ValueChanged += OffsetSlider_ValueChanged;
            mainWindow.AuthorizationButton.Click += AuthorizationButton_Click;


            mainWindow.Closed += mainWindow_Closed;

            mainWindow.PreviewKeyDown += mainWindow_PreviewKeyDown;

            romajieventset();

            loadsave();


            // Twitterの認証らへん
            if (Accestoken == "")
            {
                mainWindow.AuthorizationTextBlock.Text = "認証されていません";
                mainWindow.TweetButton.IsEnabled = false;
            }
            else
            {
                mainWindow.AuthorizationTextBlock.Text = "認証されています";
                mainWindow.AuthorizationButton.Content = "再認証";
            }

            // 表示
            mainWindow.Show();
        }



        private void loadsave()
        {
            if (!File.Exists(path))
                return;

            try
            {
                FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                BinaryFormatter bf = new BinaryFormatter();
                Saver s = (Saver)bf.Deserialize(fs);
                fs.Close();

                highscores = s.highscores;
                romajisetting = s.romajisetting;
                fontsize = s.fontsize;
                volume = s.volume;
                kpmswitch = s.kpmswitch;
                Accestoken = s.accestoken;
                Accestokensecret = s.accesstokensecret;
                settingoffset = s.settingoffset;

                reflectromajisetting();

                mainWindow.CharacterSizeSlider.Value = fontsize;
                fontsizereflect();

                mainWindow.VolumeSlider.Value = volume * mainWindow.VolumeSlider.Maximum;
                volumereflect();

                mainWindow.KpmSwitchCheckBox.IsChecked = kpmswitch;

                experimentalvalue = s.experimentalvalue;
                mainWindow.ExperimentalValueTextBlock.Text = "exp:" + experimentalvalue;

                mainWindow.OffsetSlider.Value = settingoffset;
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception);
            }
        }

        private void datasave()
        {
            FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            BinaryFormatter bf = new BinaryFormatter();
            bf.Serialize(fs,
                new Saver(highscores, romajisetting, fontsize, volume, kpmswitch, Accestoken, Accestokensecret, experimentalvalue, settingoffset));
            fs.Close();
        }



        #region event

        void mainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (mainWindow.MainTabPanel.SelectedIndex)
            {
                case 2: // Result
                    switch (e.Key)
                    {
                        case Key.Insert:
                            if (_audioPlayer != null)
                            {
                                _audioClock.Controller.Stop();
                            }
                            allreset(true);

                            mainWindow.ReadyGridPanel.Visibility = Visibility.Visible;
                            tabmove(1);

                            nowline = 0;
                            linemode = 0;
                            lineupdate(0);
                            break;

                        case Key.C:
                            if (Keyboard.Modifiers == ModifierKeys.Control)
                                ResultCopyButton_Click(null, null);
                            break;

                        case Key.T:
                            if (Keyboard.Modifiers == ModifierKeys.Control)
                                TweetButton_Click(null, null);
                            break;

                        case Key.End:
                            tabmove(0);
                            break;

                    }
                    break;
            }
        }


        void folderlistbox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            mainWindow.musiclistview.DataContext = fmlist[mainWindow.folderlistbox.SelectedIndex];
        }

        void musiclistview_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (mainWindow.MainTabPanel.SelectedIndex != 0)
                return;

            folderid = mainWindow.folderlistbox.SelectedIndex;
            selectedid = mainWindow.musiclistview.SelectedIndex;

            if (fmlist[folderid].Count <= selectedid || selectedid <= -1)
            {
                mainWindow.infotext.Text = "";
                mainWindow.PlayButton.IsEnabled = false;
                mainWindow.Copy_Hash_Value.IsEnabled = false;
                mainWindow.DeleteHighScoreButton.IsEnabled = false;
                return;
            }

            mainWindow.infotext.Text = "";

            mainWindow.infotext.Inlines.Add(new Run() { Text = "Name", FontSize = 12 });
            mainWindow.infotext.Inlines.Add(new LineBreak());
            mainWindow.infotext.Inlines.Add(new Run() { Text = fmlist[folderid][selectedid].name, FontSize = 15, FontWeight = FontWeights.Bold });
            mainWindow.infotext.Inlines.Add(new LineBreak());
            mainWindow.infotext.Inlines.Add(new LineBreak());

            mainWindow.infotext.Inlines.Add(new Run() { Text = "Artist", FontSize = 12 });
            mainWindow.infotext.Inlines.Add(new LineBreak());
            mainWindow.infotext.Inlines.Add(new Run() { Text = fmlist[folderid][selectedid].artist, FontSize = 15, FontWeight = FontWeights.Bold });
            mainWindow.infotext.Inlines.Add(new LineBreak());
            mainWindow.infotext.Inlines.Add(new LineBreak());

            mainWindow.infotext.Inlines.Add(new Run() { Text = "Genre", FontSize = 12 });
            mainWindow.infotext.Inlines.Add(new LineBreak());
            mainWindow.infotext.Inlines.Add(new Run() { Text = fmlist[folderid][selectedid].genre, FontSize = 15, FontWeight = FontWeights.Bold });
            mainWindow.infotext.Inlines.Add(new LineBreak());
            mainWindow.infotext.Inlines.Add(new LineBreak());

            // ファイルが存在しない場合の処理（最優先）
            if (!File.Exists(fmlist[folderid][selectedid].xmlpath))
            {
                mainWindow.infotext.Inlines.Add(new Run()
                {
                    Text = "Xml File Doesn't Exist!",
                    FontSize = 15,
                    FontWeight = FontWeights.Bold,
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red)
                });
                mainWindow.PlayButton.IsEnabled = false;
                mainWindow.Copy_Hash_Value.IsEnabled = false;
                mainWindow.DeleteHighScoreButton.IsEnabled = false;
                return;
            }

            if (!File.Exists(fmlist[folderid][selectedid].musicpath) && fmlist[folderid][selectedid].musicpath.ToLower() != "none")
            {
                mainWindow.infotext.Inlines.Add(new Run()
                {
                    Text = "Music File Doesn't Exist!",
                    FontSize = 15,
                    FontWeight = FontWeights.Bold,
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red)
                });
                mainWindow.PlayButton.IsEnabled = false;
                mainWindow.Copy_Hash_Value.IsEnabled = false;
                mainWindow.DeleteHighScoreButton.IsEnabled = false;
                return;
            }
            mainWindow.PlayButton.IsEnabled = true;
            mainWindow.Copy_Hash_Value.IsEnabled = true;

            List<string> nihongoword = new List<string>();
            List<string> word = new List<string>();
            List<int> interval = new List<int>();
            List<Object> color = new List<Object>();
            FileStream fStream = new FileStream(fmlist[folderid][selectedid].xmlpath, FileMode.Open, FileAccess.Read);
            XmlTextReader reader = new XmlTextReader(fStream);

            string hashstring = "";

            try
            {
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        if (reader.Name == "nihongoword")
                        {
                            for (int i = 0; i < reader.AttributeCount; i++)
                            {
                                reader.MoveToAttribute(i);
                                if (reader.Name == "color")
                                {
                                    try
                                    {
                                        System.Drawing.Color c = System.Drawing.ColorTranslator.FromHtml(reader.Value);
                                        color.Add(Color.FromRgb(c.R, c.G, c.B));
                                    }
                                    catch (Exception ex) { }
                                }
                            }

                            string str = reader.ReadString();
                            if (str == "@")
                                nihongoword.Add("");
                            else
                                nihongoword.Add(str);

                            if (nihongoword.Count != color.Count)
                                color.Add(null);
                        }
                        else if (reader.Name == "word")
                        {
                            string str = reader.ReadString();
                            if (str == "@")
                                word.Add("");
                            else
                                word.Add(str);
                        }
                        else if (reader.Name == "interval")
                        {
                            interval.Add(int.Parse(reader.ReadString()));
                        }
                        else if (reader.Name == "manual")
                        {
                            for (int i = 0; i < reader.AttributeCount; i++)
                            {
                                reader.MoveToAttribute(i);
                                if (reader.Name == "offset")
                                {
                                    try
                                    {
                                        musicoffset = int.Parse(reader.Value);
                                    }
                                    catch (Exception ex) { }
                                }
                            }
                        }
                    }
                }
            }
            catch (XmlException xe)
            {
                mainWindow.infotext.Inlines.Add(new Run()
                {
                    Text = "Broken XML File!",
                    FontSize = 15,
                    FontWeight = FontWeights.Bold,
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red)
                });
                mainWindow.PlayButton.IsEnabled = false;
                mainWindow.Copy_Hash_Value.IsEnabled = false;
                mainWindow.DeleteHighScoreButton.IsEnabled = false;
                reader.Close();
                fStream.Close();
                return;
            }
            reader.Close();
            fStream.Close();

            if (interval.Count == 0)
            {
                mainWindow.infotext.Inlines.Add(new Run()
                {
                    Text = "Can't Play This File!",
                    FontSize = 15,
                    FontWeight = FontWeights.Bold,
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red)
                });
                mainWindow.PlayButton.IsEnabled = false;
                mainWindow.Copy_Hash_Value.IsEnabled = false;
                mainWindow.DeleteHighScoreButton.IsEnabled = false;
                return;
            }

            if (interval.Count > nihongoword.Count)
                while (interval.Count > nihongoword.Count)
                {
                    nihongoword.Add("");
                    color.Add(null);
                }

            if (interval.Count > word.Count)
                while (interval.Count > word.Count)
                    word.Add("");

            List<LyricsData> ltmp = new List<LyricsData>();
            int begintime = 0;

            int typecount = 0;
            int typelength = 0;

            for (int i = 0; i < interval.Count; i++)
            {
                LyricsData l;
                if (color[i] == null)
                    l = new LyricsData(word[i], nihongoword[i], interval[i], begintime);
                else if (word[i] == "")
                    l = new LyricsData(word[i], nihongoword[i], interval[i], begintime, Colors.Black, (Color)color[i]);
                else
                    l = new LyricsData(word[i], nihongoword[i], interval[i], begintime, (Color)color[i], Colors.SlateGray);
                ltmp.Add(l);
                begintime += interval[i];
                if (!l.isuntypeline())
                {
                    typecount += hiraganatoromaji(word[i], true).Length;
                    typelength += interval[i];
                    hashstring += word[i] + "\n" + interval[i] + "\n";
                }
            }

            if (lyricsdata.Count == 0)
                lyricsdata.Add(ltmp);
            else
                lyricsdata[0] = ltmp;

            int kpm = (int)(typecount * 60000 / typelength);
            int level = kpm / 15 - 11;

            lyricsdata0needtype = typecount;
            lyricsdata0needkpm = kpm;

            mainWindow.infotext.Inlines.Add(new Run() { Text = "Level", FontSize = 12 });
            mainWindow.infotext.Inlines.Add(new LineBreak());
            mainWindow.infotext.Inlines.Add(new Run() { Text = level + " （" + kpm + "kpm）", FontSize = 15, FontWeight = FontWeights.Bold });
            mainWindow.infotext.Inlines.Add(new LineBreak());
            mainWindow.infotext.Inlines.Add(new LineBreak());

            var sha256 = System.Security.Cryptography.SHA256CryptoServiceProvider.Create();
            lyricsdata0hashcode = BitConverter.ToString(sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(hashstring))).ToLower().Replace("-", "");

            lyricsdata0highscoredatanum = -1;

            for (int i = 0; i < highscores.Count(); i++)
            {
                if (highscores[i].hashcode == lyricsdata0hashcode)
                {
                    lyricsdata0highscoredatanum = i;
                    highscores[i].xmlpath = fmlist[folderid][selectedid].xmlpath;
                    break;
                }
            }

            if (lyricsdata0highscoredatanum == -1)
            {
                for (int i = 0; i < highscores.Count(); i++)
                {
                    if (highscores[i].xmlpath == fmlist[folderid][selectedid].xmlpath)
                    {
                        lyricsdata0highscoredatanum = i;
                        highscores[i].hashcode = lyricsdata0hashcode;
                        break;
                    }
                }
            }

            mainWindow.infotext.Inlines.Add(new Run() { Text = "HighScore", FontSize = 12 });
            mainWindow.infotext.Inlines.Add(new LineBreak());

            if (lyricsdata0highscoredatanum == -1)
            {
                mainWindow.infotext.Inlines.Add(new Run() { Text = "No Data", FontSize = 15, FontWeight = FontWeights.Bold });
                mainWindow.DeleteHighScoreButton.IsEnabled = false;
            }
            else
            {
                int highkpm = 0;

                if (highscores[lyricsdata0highscoredatanum].typedmillisecond != 0)
                {
                    if (kpmswitch)
                        highkpm = (highscores[lyricsdata0highscoredatanum].correct - highscores[lyricsdata0highscoredatanum].complete
                            - highscores[lyricsdata0highscoredatanum].failed + highscores[lyricsdata0highscoredatanum].nothingfailed)
                            * 60000 / (highscores[lyricsdata0highscoredatanum].typedmillisecond + highscores[lyricsdata0highscoredatanum].firstmillisecond);
                    else
                        highkpm = (highscores[lyricsdata0highscoredatanum].correct - highscores[lyricsdata0highscoredatanum].complete
                            - highscores[lyricsdata0highscoredatanum].failed + highscores[lyricsdata0highscoredatanum].nothingfailed)
                            * 60000 / (highscores[lyricsdata0highscoredatanum].typedmillisecond);
                }
                else
                    highkpm = 0;

                mainWindow.infotext.Inlines.Add
                    (new Run()
                    {
                        Text = highscores[lyricsdata0highscoredatanum].points + "  "
                            + classcalc(lyricsdata0needtype, lyricsdata0needkpm, highkpm, highscores[lyricsdata0highscoredatanum].miss, highscores[lyricsdata0highscoredatanum].correct),
                        FontSize = 15,
                        FontWeight = FontWeights.Bold
                    }
                    );
                mainWindow.infotext.Inlines.Add(new LineBreak());
                mainWindow.infotext.Inlines.Add
                    (new Run()
                    {
                        Text = "correct:" + highscores[lyricsdata0highscoredatanum].correct + " miss:" + highscores[lyricsdata0highscoredatanum].miss
                            + "  kpm:" + highkpm + " combo:" + highscores[lyricsdata0highscoredatanum].combo
                            + "  complete:" + highscores[lyricsdata0highscoredatanum].complete + " failed:" + highscores[lyricsdata0highscoredatanum].failed,
                        FontSize = 12,
                    }
                    );
                mainWindow.DeleteHighScoreButton.IsEnabled = true;
            }
            mainWindow.infotext.Inlines.Add(new LineBreak());
            mainWindow.infotext.Inlines.Add(new LineBreak());
            mainWindow.infotext.Inlines.Add(new Run()
            {
                Text = lyricsdata0hashcode,
                FontSize = 10,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray)
            });
        }

        void searchbox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Return)
                return;

            System.Globalization.CompareInfo ci = System.Globalization.CultureInfo.CurrentCulture.CompareInfo;
            System.Globalization.CompareOptions co = System.Globalization.CompareOptions.Ordinal;

            int folderid = mainWindow.folderlistbox.SelectedIndex;
            int selectedid = mainWindow.musiclistview.SelectedIndex;
            string searchstring = searchstringprovider(mainWindow.searchbox.Text);
            string[] searchstrings = searchstring.Split(' ');

            if (folderid == -1)
            {
                if (mainWindow.beyondfoldercheck.IsChecked == false)
                {
                    return;
                }

                folderid = 0;
                selectedid = 0;
            }
            else if (selectedid == -1)
            {
                selectedid = 0;
            }

            for (int i = selectedid + 1; i < fmlist[folderid].Count; i++)
            {
                bool b = true;
                for (int j = 0; j < searchstrings.Length; j++)
                {
                    if (ci.IndexOf(searchstringprovider(fmlist[folderid][i].name),searchstrings[j], co) == -1 &&
                        ci.IndexOf(searchstringprovider(fmlist[folderid][i].artist), searchstrings[j], co) == -1 &&
                        ci.IndexOf(searchstringprovider(fmlist[folderid][i].genre), searchstrings[j], co) == -1)
                    {
                        b = false;
                        break;
                    }
                }
                if (b)
                {
                    mainWindow.musiclistview.SelectedIndex = i;
                    mainWindow.musiclistview.ScrollIntoView(mainWindow.musiclistview.Items[i]);
                    return;
                }
            }

            if (mainWindow.beyondfoldercheck.IsChecked == true)
            {
                // search beyond folders
                for (int i = folderid + 1; i < flist.Count; i++)
                {
                    for (int j = 0; j < fmlist[i].Count; j++)
                    {
                        bool b = true;
                        for (int k = 0; k < searchstrings.Length; k++)
                        {
                            if (ci.IndexOf(searchstringprovider(fmlist[i][j].name), searchstrings[k], co) == -1 &&
                                ci.IndexOf(searchstringprovider(fmlist[i][j].artist), searchstrings[k], co) == -1 &&
                                ci.IndexOf(searchstringprovider(fmlist[i][j].genre), searchstrings[k], co) == -1)
                            {
                                b = false;
                                break;
                            }
                        }
                        if (b)
                        {
                            mainWindow.folderlistbox.SelectedIndex = i;
                            mainWindow.folderlistbox.ScrollIntoView(mainWindow.folderlistbox.Items[i]);
                            mainWindow.musiclistview.SelectedIndex = j;
                            mainWindow.musiclistview.ScrollIntoView(mainWindow.musiclistview.Items[j]);
                            return;
                        }
                    }
                }

                for (int i = 0; i < folderid + 1; i++)
                {
                    for (int j = 0; j < fmlist[i].Count; j++)
                    {
                        bool b = true;
                        for (int k = 0; k < searchstrings.Length; k++)
                        {
                            if (ci.IndexOf(searchstringprovider(fmlist[i][j].name), searchstrings[k], co) == -1 &&
                                ci.IndexOf(searchstringprovider(fmlist[i][j].artist), searchstrings[k], co) == -1 &&
                                ci.IndexOf(searchstringprovider(fmlist[i][j].genre), searchstrings[k], co) == -1)
                            {
                                b = false;
                                break;
                            }
                        }
                        if (b)
                        {
                            mainWindow.folderlistbox.SelectedIndex = i;
                            mainWindow.folderlistbox.ScrollIntoView(mainWindow.folderlistbox.Items[i]);
                            mainWindow.musiclistview.SelectedIndex = j;
                            mainWindow.musiclistview.ScrollIntoView(mainWindow.musiclistview.Items[j]);
                            return;
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < selectedid; i++)
                {
                    bool b = true;
                    for (int j = 0; j < searchstrings.Length; j++)
                    {
                        if (ci.IndexOf(searchstringprovider(fmlist[folderid][i].name), searchstrings[j], co) == -1 &&
                            ci.IndexOf(searchstringprovider(fmlist[folderid][i].artist), searchstrings[j], co) == -1 &&
                            ci.IndexOf(searchstringprovider(fmlist[folderid][i].genre), searchstrings[j], co) == -1)
                        {
                            b = false;
                            break;
                        }
                    }
                    if (b)
                    {
                        mainWindow.musiclistview.SelectedIndex = i;
                        mainWindow.musiclistview.ScrollIntoView(mainWindow.musiclistview.Items[i]);
                        return;
                    }
                }
            }
        }

        void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (_audioPlayer != null)
            {
                _audioClock.Controller.Stop();
            }


            allreset();

            int foldernumber = mainWindow.folderlistbox.SelectedIndex;
            int musicnumber = mainWindow.musiclistview.SelectedIndex;

            folderid = foldernumber;
            selectedid = musicnumber;

            mainWindow.MusicNameTextBlock.Text = fmlist[foldernumber][musicnumber].name;
            mainWindow.ArtistTextBlock.Text = fmlist[foldernumber][musicnumber].artist;
            mainWindow.GenreTextBlock.Text = fmlist[foldernumber][musicnumber].genre;
            mainWindow.MusicBarRightTextBlock.Text = "Music  0:00 / "
                + timetostring((int)(lyricsdata[0][lyricsdata[0].Count - 1].endtime() + musicoffset + settingoffset) / 1000);
            mainWindow.MusicProgressBar.Maximum = lyricsdata[0][lyricsdata[0].Count - 1].endtime() + musicoffset + settingoffset;

            mainWindow.ReadyGridPanel.Visibility = Visibility.Visible;
            tabmove(1);

            nowline = 0;
            linemode = 0;
            lineupdate(0);
        }

        void Copy_Hash_Value_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(lyricsdata0hashcode);
        }

        void DeleteHighScoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (lyricsdata0highscoredatanum == -1 || mainWindow.musiclistview.SelectedIndex == -1)
                return;

            MessageBoxResult msr = MessageBox.Show("Are you sure to delete the high score?", "Confirmation",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            switch (msr)
            {
                case MessageBoxResult.Yes:
                    highscores.RemoveAt(lyricsdata0highscoredatanum);
                    musiclistview_SelectionChanged(null, null);
                    break;
            }
        }


        void ReadyGridPanel_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (mainWindow.ReadyGridPanel.IsVisible == true)
                Keyboard.Focus(mainWindow.ReadyGridPanel);
        }

        void ReadyGridPanel_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Space:
                    mainWindow.ReadyGridPanel.Visibility = Visibility.Hidden;

                    _audioTimeline = new MediaTimeline();
                    _audioTimeline.Source = new Uri(System.IO.Path.GetFullPath(fmlist[folderid][selectedid].musicpath));
                    _audioClock = _audioTimeline.CreateClock();
                    _audioPlayer = new MediaPlayer();
                    _audioPlayer.Clock = _audioClock;

                    _audioPlayer.Volume = volume;

                    _audioClock.CurrentTimeInvalidated += TimeChanged;
                    _audioClock.Controller.Begin();

                    Keyboard.Focus(mainWindow.PlayMainGridPanel);

                    break;

                case Key.Escape:
                    tabmove(0);
                    break;
            }
        }

        void ReadyGridPanel_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (e.NewFocus != mainWindow.PlayMainGridPanel && mainWindow.MainTabPanel.SelectedIndex == 1)
                Keyboard.Focus(mainWindow.ReadyGridPanel);
        }

        /// <summary>
        /// フォーカスを失った時に無理矢理戻すためだけのイベントメソッド
        /// </summary>
        void PlayMainGridPanel_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (mainWindow.MainTabPanel.SelectedIndex == 1)
                Keyboard.Focus(mainWindow.PlayMainGridPanel);
        }

        /// <summary>
        /// プレイ中のキー操作に関するメソッド
        /// </summary>
        void PlayMainGridPanel_KeyDown(object sender, KeyEventArgs e)
        {
            string keystr = e.Key.ToString();
            string typedstr = "";

            if (e.Key == Key.Escape)
            {
                viewresult();
                return;
            }

            else if (e.Key == Key.Insert)
            {
                replay();
                return;
            }

            if (linemode == 1) // 打ち終わっていない時は文字判定
            {
                bool ismiss = true;

                if (keystr.Length == 1) // アルファベット
                {
                    if (ndouble && keystr == "N") // 2回連続のN
                    {
                        ++correct;
                        ++experimentalvalue;
                        ++nowcombo;
                        points += correctadd;
                        lyricsdata[0][nowline].typed += "N";
                        ndouble = false;
                        repaint();
                        return;
                    }

                    if (nextstring.IndexOf(keystr) != -1)
                    {
                        typedstr = keystr;
                        ismiss = false;
                    }
                }

                else if ((keystr == "D" + nextstring && Keyboard.Modifiers != ModifierKeys.Shift) || keystr == "NumPad" + nextstring ||
                    (keystr == "D" + ("!\"#$%&'()".IndexOf(nextstring) + 1) && Keyboard.Modifiers == ModifierKeys.Shift) ||
                    (keystr == "Space" && nextstring == " ") || (keystr == "Decimal" && nextstring == ".") ||
                    (keystr == "OemMinus" && (nextstring == "-" || nextstring == "ー") && Keyboard.Modifiers != ModifierKeys.Shift) ||
                    (keystr == "OemMinus" && nextstring == "=" && Keyboard.Modifiers == ModifierKeys.Shift) ||
                    (keystr == "OemQuotes" && nextstring == "^" && Keyboard.Modifiers != ModifierKeys.Shift) ||
                    (keystr == "OemQuotes" && (nextstring == "~" || nextstring == "～") && Keyboard.Modifiers == ModifierKeys.Shift) ||
                    (keystr == "Oem5" && nextstring == "\\" && Keyboard.Modifiers != ModifierKeys.Shift) ||
                    (keystr == "Oem5" && nextstring == "|" && Keyboard.Modifiers == ModifierKeys.Shift) ||
                    (keystr == "Oem3" && nextstring == "@" && Keyboard.Modifiers != ModifierKeys.Shift) ||
                    (keystr == "Oem3" && nextstring == "`" && Keyboard.Modifiers == ModifierKeys.Shift) ||
                    (keystr == "Oem3" && nextstring == "゛" && Keyboard.Modifiers == ModifierKeys.Shift) ||
                    (keystr == "OemOpenBrackets" && nextstring == "[" && Keyboard.Modifiers != ModifierKeys.Shift) ||
                    (keystr == "OemOpenBrackets" && nextstring == "「" && Keyboard.Modifiers != ModifierKeys.Shift) ||
                    (keystr == "OemOpenBrackets" && nextstring == "{" && Keyboard.Modifiers == ModifierKeys.Shift) ||
                    (keystr == "OemOpenBrackets" && nextstring == "゜" && Keyboard.Modifiers == ModifierKeys.Shift) ||
                    (keystr == "OemPlus" && nextstring == ";" && Keyboard.Modifiers != ModifierKeys.Shift) ||
                    (keystr == "OemPlus" && nextstring == "+" && Keyboard.Modifiers == ModifierKeys.Shift) ||
                    (keystr == "Oem1" && nextstring == ":" && Keyboard.Modifiers != ModifierKeys.Shift) ||
                    (keystr == "Oem1" && nextstring == "*" && Keyboard.Modifiers == ModifierKeys.Shift) ||
                    (keystr == "Oem6" && nextstring == "]" && Keyboard.Modifiers != ModifierKeys.Shift) ||
                    (keystr == "Oem6" && nextstring == "」" && Keyboard.Modifiers != ModifierKeys.Shift) ||
                    (keystr == "Oem6" && nextstring == "}" && Keyboard.Modifiers == ModifierKeys.Shift) ||
                    (keystr == "OemComma" && nextstring == "," && Keyboard.Modifiers != ModifierKeys.Shift) ||
                    (keystr == "OemComma" && nextstring == "、" && Keyboard.Modifiers != ModifierKeys.Shift) ||
                    (keystr == "OemComma" && nextstring == "<" && Keyboard.Modifiers == ModifierKeys.Shift) ||
                    (keystr == "OemPeriod" && nextstring == "." && Keyboard.Modifiers != ModifierKeys.Shift) ||
                    (keystr == "OemPeriod" && nextstring == "。" && Keyboard.Modifiers != ModifierKeys.Shift) ||
                    (keystr == "OemPeriod" && nextstring == ">" && Keyboard.Modifiers == ModifierKeys.Shift) ||
                    (keystr == "OemQuestion" && nextstring == "/" && Keyboard.Modifiers != ModifierKeys.Shift) ||
                    (keystr == "OemQuestion" && nextstring == "・" && Keyboard.Modifiers != ModifierKeys.Shift) ||
                    (keystr == "OemQuestion" && nextstring == "?" && Keyboard.Modifiers == ModifierKeys.Shift) ||
                    (keystr == "OemBackslash" && nextstring == "\\" && Keyboard.Modifiers != ModifierKeys.Shift) ||
                    (keystr == "OemBackslash" && nextstring == "_" && Keyboard.Modifiers == ModifierKeys.Shift) ||
                    (keystr == "Divide" && nextstring == "/") || (keystr == "Multiply" && nextstring == "*") ||
                    (keystr == "Subtract" && nextstring == "-") || (keystr == "Add" && nextstring == "+") ||
                    (keystr == "Up" && nextstring == "↑") || (keystr == "Down" && nextstring == "↓") ||
                    (keystr == "Left" && nextstring == "←") || (keystr == "Right" && nextstring == "→"))
                {
                    typedstr = nextstring;
                    ismiss = false;
                }

                else if (keystr == "LeftShift" || keystr == "RightShift" || keystr == "LeftCtrl" || keystr == "RightCtrl" ||
                    keystr == "LWin" || keystr == "System" || keystr == "ImeNonConvert" || keystr == "Apps" || keystr == "Pause" ||
                    keystr == "Delete" || keystr == "Back" || keystr == "Return" || (keystr.Length == 2 && keystr[0] == 'F') ||
                    keystr == "NumLock" || keystr == "Scroll" || keystr == "Home" || keystr == "End" || keystr == "PageUp" ||
                    keystr == "Next" || keystr == "ImeConvert" || keystr == "OemCopy" || keystr == "OemAttn" || keystr == "OemEnlw" || 
                    keystr == "Tab" || keystr == "DbeSbcsChar")
                {
                    // ミスには換算しない
                    return;
                }

                if (!ismiss) // 正打
                {
                    ++correct;
                    ++experimentalvalue;
                    ++nowcombo;
                    points += correctadd;
                    lyricsdata[0][nowline].typed += typedstr;
                    blocktyped += typedstr;
                    ndouble = false;


                    // 時間関係の処理

                    int totalMilliseconds = (int)((TimeSpan)(_audioClock.CurrentTime)).TotalMilliseconds;
                    if (lyricsdata[0][nowline].typed == typedstr)
                    {
                        lyricsdata[0][nowline].firsttime = totalMilliseconds - (lyricsdata[0][nowline].begintime + musicoffset + settingoffset);
                        if (lyricsdata[0][nowline].firsttime < 0)
                            lyricsdata[0][nowline].firsttime = 0;
                        firstmillisecond += lyricsdata[0][nowline].firsttime;
                    }
                    else
                    {
                        lyricsdata[0][nowline].typingtime = totalMilliseconds -
                            (lyricsdata[0][nowline].begintime + musicoffset + settingoffset) - lyricsdata[0][nowline].firsttime;
                    }

                    while (typingpermission())
                    {
                        blocktyped = "";
                        if (cursor + blockcounts == lyricsdata[0][nowline].yomigana.Length)
                        {
                            linemode = 0;
                            typedmillisecond += lyricsdata[0][nowline].typingtime;
                            ++complete;
                            points += (lyricsdata[0][nowline].endtime() + musicoffset + settingoffset - totalMilliseconds) * pps / 1000;
                            if (nowline != lyricsdata[0].Count - 1)
                            {
                                if (lyricsdata[0][nowline + 1].isuntypeline())
                                    mainWindow.IntervalBarLeftTextBlock.Text = "Necessary kpm : -";
                                else
                                    mainWindow.IntervalBarLeftTextBlock.Text = "Necessary kpm : "
                                        + necessarykpm(lyricsdata[0][nowline + 1].yomigana, (int)lyricsdata[0][nowline + 1].interval);
                            }
                            break;
                        }
                        cursor += blockcounts;
                        blockcounts = blockcount(lyricsdata[0][nowline].yomigana.Substring(cursor));
                    }

                    repaint();
                }
                else // ミス
                {
                    ++miss;
                    points -= missminus;
                    nowcombo = 0;
                    if (lyricsdata[0][nowline].misscursor.Count() == 0 ||
                        lyricsdata[0][nowline].misscursor[lyricsdata[0][nowline].misscursor.Count() - 1] != lyricsdata[0][nowline].typed.Length)
                        lyricsdata[0][nowline].misscursor.Add(lyricsdata[0][nowline].typed.Length);
                    repaint();
                }
            }
            else
            {
                if (e.Key == Key.Tab)
                    skip();
            }
        }

        void CharacterSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            fontsize = e.NewValue;
            fontsizereflect();
            Keyboard.Focus(mainWindow.PlayMainGridPanel);
        }

        void fontsizereflect()
        {
            double _fontsize = fontsize;

            mainWindow.HiraganaTextBlock.FontSize = _fontsize * 2 / 3;
            mainWindow.KanjiTextBlock.FontSize = _fontsize;
            mainWindow.RomajiTextBlock.FontSize = _fontsize * 14 / 15;

            _fontsize = _fontsize * 11 / 15;
            mainWindow.PointsTextBlock.FontSize = _fontsize;
            mainWindow.RankTextBlock.FontSize = _fontsize;
            mainWindow.CorrectTextBlock.FontSize = _fontsize;
            mainWindow.MissTextBlock.FontSize = _fontsize;
            mainWindow.LKpmTextBlock.FontSize = _fontsize;
            mainWindow.AKpmTextBlock.FontSize = _fontsize;
            mainWindow.ComboTextBlock.FontSize = _fontsize;
            mainWindow.MComboTextBlock.FontSize = _fontsize;
            mainWindow.CompleteTextBlock.FontSize = _fontsize;
            mainWindow.FailedTextBlock.FontSize = _fontsize;

            _fontsize = _fontsize * 7 / 11;
            mainWindow.PointsTextBlockUnder.FontSize = _fontsize;
            mainWindow.RankTextBlockUnder.FontSize = _fontsize;
            mainWindow.CorrectTextBlockUnder.FontSize = _fontsize;
            mainWindow.MissTextBlockUnder.FontSize = _fontsize;
            mainWindow.LKpmTextBlockUnder.FontSize = _fontsize;
            mainWindow.AKpmTextBlockUnder.FontSize = _fontsize;
            mainWindow.ComboTextBlockUnder.FontSize = _fontsize;
            mainWindow.MComboTextBlockUnder.FontSize = _fontsize;
            mainWindow.CompleteTextBlockUnder.FontSize = _fontsize;
            mainWindow.FailedTextBlockUnder.FontSize = _fontsize;

        }

        void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            volume = e.NewValue / mainWindow.VolumeSlider.Maximum;
            volumereflect();
            Keyboard.Focus(mainWindow.PlayMainGridPanel);
        }

        void volumereflect()
        {
            if (_audioPlayer != null)
                _audioPlayer.Volume = volume;
        }

        void TweetCommentTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            string resulttweetcontent = "";
            if (mainWindow.IncludeArtistCheckBox.IsChecked == true)
                resulttweetcontent = resulttweetcontent1 + "\n" + resulttweetcontent2 + "\n" + resulttweetcontent3;
            else
                resulttweetcontent = resulttweetcontent1 + "\n" + resulttweetcontent3;
            int remain = 140;
            remain -= 8;
            remain -= resulttweetcontent.Length;
            remain -= mainWindow.TweetCommentTextBox.Text.Length;
            mainWindow.TweetCommentRemainTextBlock.Text = remain + "";
            if (remain < 0)
            {
                mainWindow.TweetCommentRemainTextBlock.Foreground = Brushes.Red;
                mainWindow.TweetButton.IsEnabled = false;
            }
            else
            {
                mainWindow.TweetCommentRemainTextBlock.Foreground = Brushes.Black;
                mainWindow.TweetButton.IsEnabled = true;
            }
        }

        void ResultCopyButton_Click(object sender, RoutedEventArgs e)
        {
            string resulttweetcontent = "";
            if (mainWindow.IncludeArtistCheckBox.IsChecked == true)
                resulttweetcontent = resulttweetcontent1 + "\n" + resulttweetcontent2 + "\n" + resulttweetcontent3;
            else
                resulttweetcontent = resulttweetcontent1 + "\n" + resulttweetcontent3;
            Clipboard.SetText(resulttweetcontent);
        }

        void TweetButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TwitterService service = new TwitterService(consumerkey, consumersecret);
                service.AuthenticateWith(Accestoken, Accestokensecret);

                string resulttweetcontent = "";
                if(mainWindow.IncludeArtistCheckBox.IsChecked == true)
                    resulttweetcontent = resulttweetcontent1 + "\n" + resulttweetcontent2 + "\n" + resulttweetcontent3;
                else
                    resulttweetcontent = resulttweetcontent1 + "\n" + resulttweetcontent3;

                if (mainWindow.TweetCommentTextBox.Text.Length == 0)
                        service.SendTweet(new SendTweetOptions { Status = resulttweetcontent + "\n#イショタイ" });
                else
                    service.SendTweet(new SendTweetOptions { Status = resulttweetcontent + "\n" + mainWindow.TweetCommentTextBox.Text + "\n#イショタイ" });
            }
            catch (System.Net.WebException exception)
            {
                MessageBox.Show("Twitter投稿中にエラーが発生しました。\n\n" + exception.ToString());
            }
        }

        void ReplayButton_Click(object sender, RoutedEventArgs e)
        {
            if (_audioPlayer != null)
            {
                _audioClock.Controller.Stop();
            }


            allreset(true);
            mainWindow.ReadyGridPanel.Visibility = Visibility.Visible;
            tabmove(1);

            nowline = 0;
            linemode = 0;
            lineupdate(0);
        }

        TwitterService ts;
        OAuthRequestToken oart;
        AuthWindow aw;

        void AuthorizationButton_Click(object sender, RoutedEventArgs e)
        {

            aw = new AuthWindow();

            ts = new TwitterService(consumerkey, consumersecret);
            oart = ts.GetRequestToken();
            aw.browser.Source = ts.GetAuthorizationUri(oart);

            aw.AuthButton.Click += AuthButton_Click;
            aw.PinTextBox.KeyDown += PinTextBox_KeyDown;
            aw.ShowDialog();
        }

        void PinTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
                AuthButton_Click(null, null);
        }

        void AuthButton_Click(object sender, RoutedEventArgs e)
        {
            var _ac = Accestoken;
            var _as = Accestokensecret;

            try
            {
                OAuthAccessToken oaat = ts.GetAccessToken(oart, aw.PinTextBox.Text);

                Accestoken = oaat.Token;
                Accestokensecret = oaat.TokenSecret;
                mainWindow.TweetButton.IsEnabled = true;
                mainWindow.AuthorizationTextBlock.Text = "認証されています";
                mainWindow.AuthorizationButton.Content = "再認証";
            }
            catch (Exception exception)
            {
                MessageBox.Show("認証できませんでした。");
                Accestoken = _ac;
                Accestokensecret = _as;
            }

            aw.Visibility = Visibility.Collapsed;
            aw = null;
        }


        void KpmSwitchCheckBox_Click(object sender, RoutedEventArgs e)
        {
            kpmswitch = (bool)mainWindow.KpmSwitchCheckBox.IsChecked;
        }

        void OffsetSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            settingoffset = (int)e.NewValue;
        }


        void mainWindow_Closed(object sender, EventArgs e)
        {
            datasave();

            Environment.Exit(0);
        }

        #endregion


        #region play

        /// <summary>
        /// ミュージックの時刻変更に反応するイベントメソッド
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void TimeChanged(object sender, EventArgs e)
        {
            if (_audioClock == null || _audioClock.CurrentTime == null || mainWindow.MainTabPanel.SelectedIndex != 1)
                return;

            if (points - viewpoints > 10)
            {
                Random rnd = new Random();
                viewpoints += 1 + rnd.Next(12);
                mainWindow.PointsTextBlock.Text = viewpoints + "";
            }
            else if (viewpoints != points)
            {
                viewpoints = points;
                mainWindow.PointsTextBlock.Text = viewpoints + "";
            }

            double totalMilliseconds = ((TimeSpan)(_audioClock.CurrentTime)).TotalMilliseconds;

            mainWindow.MusicBarRightTextBlock.Text = "Music  " + timetostring((int)(totalMilliseconds / 1000)) +
                " / " + timetostring((int)(lyricsdata[0][lyricsdata[0].Count - 1].endtime() + musicoffset + settingoffset) / 1000);
            mainWindow.MusicProgressBar.Value = totalMilliseconds;

            mainWindow.IntervalBarRightTextBlock.Text = "Interval Remain  "
                + timetostring2((int)(lyricsdata[0][nowline].endtime() + musicoffset + settingoffset - totalMilliseconds));
            mainWindow.IntervalProgressBar.Value = totalMilliseconds - (lyricsdata[0][nowline].begintime + musicoffset + settingoffset);

            if (nowline == lyricsdata[0].Count - 1)
            {
                // 最終行だったときの処理
                if (totalMilliseconds > lyricsdata[0][nowline].endtime() + musicoffset + settingoffset)
                {
                    viewresult();
                }
            }
            else
            {
                bool firstbreak = true;
                int skiplines = 0;

                // 0msインターバルがあることを想定しておく
                while (true)
                {
                    if (totalMilliseconds > lyricsdata[0][nowline].endtime() + musicoffset + settingoffset && nowline < lyricsdata[0].Count - 1)
                    {
                        ++nowline;
                        ++skiplines;
                        firstbreak = false;
                    }
                    else
                        break;
                }

                // 行変更が行われた場合
                if (!firstbreak)
                {
                    lineupdate(skiplines - 1);
                }
            }


            // デバッグ用に使うべし（ほぼ1秒ごとに調査できる）
            /*if (((TimeSpan)(_audioClock.CurrentTime)).Milliseconds < 10)
            {
                Debug.WriteLine(((TimeSpan)_audioClock.CurrentTime).TotalMilliseconds + " - " + _audioPlayer.NaturalDuration.TimeSpan.TotalMilliseconds);
            }*/
        }

        /// <summary>
        /// 行が変更された際にその行を処理するメソッド
        /// 表示関係ではなく、実際のプレイ時間と行表示の一致を図るものである
        /// 打ち終わった後の次行表示はまた他で処理する
        /// </summary>
        /// <param name="skiplines">スキップした行数．特殊な処理</param>
        void lineupdate(int skiplines)
        {
            ndouble = false;

            mainWindow.MusicBarLeftTextBlock.Text = (nowline + 1) + " / " + lyricsdata[0].Count;
            mainWindow.IntervalBarRightTextBlock.Text = "Interval Remain  " + timetostring2((int)(lyricsdata[0][nowline].interval));
            mainWindow.IntervalProgressBar.Maximum = lyricsdata[0][nowline].interval;

            if (lyricsdata[0][nowline].isuntypeline())
            {
                // この行でなにも打つことがない場合
                if (linemode == 1 && nowline > 0)
                {
                    // 前の（打鍵可能）行で打ち切れていなかった場合
                    ++failed;
                    typedmillisecond += lyricsdata[0][nowline - (skiplines + 1)].typingtime;
                    lyricsdata[0][nowline - (skiplines + 1)].remain = nextstring[0]
                        + nextstringa + hiraganatoromaji(lyricsdata[0][nowline - (skiplines + 1)].yomigana.Substring(cursor + blockcounts));
                    if (lyricsdata[0][nowline - (skiplines + 1)].typed.Length == 0)
                        ++nothingfailed;
                }
                linemode = -1;
                if (nowline == lyricsdata[0].Count - 1)
                {
                    mainWindow.IntervalBarLeftTextBlock.Text = "Necessary kpm : -";
                }
                else
                {
                    if (lyricsdata[0][nowline + 1].isuntypeline())
                        mainWindow.IntervalBarLeftTextBlock.Text = "Necessary kpm : -";
                    else
                        mainWindow.IntervalBarLeftTextBlock.Text = "Necessary kpm : "
                            + necessarykpm(lyricsdata[0][nowline + 1].yomigana, (int)lyricsdata[0][nowline + 1].interval);
                }
            }
            else
            {
                if (linemode == 1 && nowline > 0)
                {
                    // 前の行で打ち切れていなかった場合
                    ++failed;
                    typedmillisecond += lyricsdata[0][nowline - (skiplines + 1)].typingtime;
                    lyricsdata[0][nowline - (skiplines + 1)].remain = nextstring[0]
                        + nextstringa + hiraganatoromaji(lyricsdata[0][nowline - (skiplines + 1)].yomigana.Substring(cursor + blockcounts));
                    if (lyricsdata[0][nowline - (skiplines + 1)].typed.Length == 0)
                        ++nothingfailed;
                }
                mainWindow.IntervalBarLeftTextBlock.Text = "Necessary kpm : " + necessarykpm(lyricsdata[0][nowline].yomigana, (int)lyricsdata[0][nowline].interval);
                linemode = 1;
                cursor = 0;
                blockcounts = blockcount(lyricsdata[0][nowline].yomigana);
                blocktyped = "";
                typingpermission();
            }
            repaint();

            // 1行以上スキップしている場合（0インターバル行があった場合）
            for (int i = 0; i < skiplines; i++)
            {
                if (!lyricsdata[0][nowline - 1 - i].isuntypeline())
                {
                    ++failed;
                    ++nothingfailed;
                    lyricsdata[0][nowline - 1 - i].remain = hiraganatoromaji(lyricsdata[0][nowline - 1 - i].yomigana);
                }
            }
        }

        /// <summary>
        /// あるひらがな文字列が最初から何文字まででブロック化できるかを調べるメソッド
        /// </summary>
        int blockcount(string str)
        {
            if (str.Length <= 1)
                return str.Length;

            else
            {
                // 処理が変な順番なのは、ひらがなの出現回数順に沿ったためである

                string ss =
                    "あえおかけこがげごさせそざずぜぞただづなぬねのはへほばぶべぼぱぷぺぽまむめもらるれろやゆよわゐゑをんぁぃぅぇぉヵゕヶゖゃゅょゎ" // 63
                    + "い" + "うつ" + "きぎしじちぢてでにひびぴみり" + "くふ" + "っ" + "ぐすとど" + "ヴゔ";

                string ss2 = "ぁぃぅぇぉゃゅょ";

                int indexof = ss.IndexOf(str[0]);
                int indexof2 = ss2.IndexOf(str[1]);

                if (indexof < 63)
                    return 1;

                else if (indexof == 63) // い
                    if (indexof2 == 3)
                        return 2; // いぇ
                    else
                        return 1;

                else if (indexof < 66) // うつ
                    switch (indexof2)
                    {
                        case 0:
                        case 1:
                        case 3:
                        case 4:
                            return 2; // うぁ、うぃ、うぇ、うぉ
                        default:
                            return 1;
                    }

                else if (indexof < 80) // きぎ……り
                    switch (indexof2)
                    {
                        case -1:
                        case 0:
                        case 2:
                        case 4:
                            return 1;
                        default:
                            return 2; // きぃ、きぇ、きゃ、きゅ、きょ
                    }

                else if (indexof < 82) // くふ
                    if (indexof2 == -1)
                        return 1;
                    else
                        return 2; // くぁ、くぃ、くぅ、くぇ、くぉ、くゃ、くゅ、くょ

                else if (indexof == 82) // っ
                {
                    string sst = "かけこがげごさせそざずぜぞただづはへほばぶべぼぱぷぺぽまむめもらるれろやゆよわゐゑをぁぃぅぇぉヵゕヶゖっゃゅょゎ"
                        + "つ" + "きぎしじちぢてでひびぴみり" + "くふ" + "ぐすとど" + "ヴゔ" + "い" + "う";
                    int indexoft = sst.IndexOf(str[1]);

                    if (indexoft == -1)
                        return 1;

                    else if (indexoft < 56)
                        return 2;

                    else
                        if (str.Length == 2)
                            return 2;
                        else
                        {
                            int indexoft2 = ss2.IndexOf(str[2]);

                            if (indexoft == 56) // つ
                                switch (indexoft2)
                                {
                                    case 0:
                                    case 1:
                                    case 3:
                                    case 4:
                                        return 3; // っつぁ、っつぃ、っつぇ、っつぉ
                                    default:
                                        return 2;
                                }

                            else if (indexoft < 70) // きぎ……り
                                switch (indexoft2)
                                {
                                    case -1:
                                    case 0:
                                    case 2:
                                    case 4:
                                        return 2;
                                    default:
                                        return 3; // っきぃ、っきぇ、っきゃ、っきゅ、っきょ
                                }

                            else if (indexoft < 72) // くふ
                                if (indexoft2 == -1)
                                    return 2;
                                else
                                    return 3; // っくぁ、っくぃ……

                            else if (indexoft < 76) // ぐすとど
                                if (indexoft2 == -1 || indexoft2 >= 5)
                                    return 2;
                                else
                                    return 3; // っぐぁ、っぐぃ……

                            else if (indexoft < 78) // ヴゔ
                                if (indexoft2 != -1 && indexoft2 != 2)
                                    return 3; // っヴぁ、っヴぃ……
                                else
                                    return 2;

                            else if (indexoft == 78) // い
                                if (indexoft2 != -1 && indexoft2 != 3)
                                    return 3; // っいぇ
                                else
                                    return 2;

                            else // う
                                if (indexoft2 >= 0 && indexoft2 <= 4 && indexoft2 != 2)
                                    return 3; // っうぁ、っうぃ……
                                else
                                    return 2;
                        }
                }

                else if (indexof < 87) // ぐすとど
                    if (indexof2 == -1 || indexof2 >= 5)
                        return 1;
                    else
                        return 2; // ぐぁ、ぐぃ、ぐぅ、ぐぇ、ぐぉ

                else // ヴゔ
                    if (indexof2 != -1 && indexof != 2)
                        return 2; // ヴぁ、ヴぃ、ヴぇ、ヴぉ、ヴゃ、ヴゅ、ヴょ
                    else
                        return 1;

            }

        }

        /// <summary>
        /// cursor と blockcounts から nextstring と nextstringa を作るメソッド。
        /// </summary>
        bool typingpermission()
        {
            if (blockcounts == 0)
                Environment.Exit(1);

            string str = lyricsdata[0][nowline].yomigana.Substring(cursor, blockcounts);

            // 母音+子音の組み合わせが1通り
            string sp = "がきぎぐけげごさざすずぜそぞただぢづてでとどなにぬねのはばぱひびぴぶぷへべぺほぼぽまみむめもやゆよらりるれろわをヴゔ";
            string sps = "GKGGKGGSZSZZSZTDDDTDTDNNNNNHBPHBPBPHBPHBPMMMMMYYYRRRRRWWVV";
            string spb = "AIIUEEOAAUUEOOAAIUEEOOAIUEOAAAIIIUUEEEOOOAIUEOAUOAIUEOAOUU";

            int onecharacterindexof = ("あえお" + symbols).IndexOf(str);
            int twocharacterindexof = sp.IndexOf(str);

            // 一回打てば終わってしまうパターン
            if (onecharacterindexof != -1)
                if (blocktyped.Length == 0)
                    nextstringset(("AEO" + symbolsafter)[onecharacterindexof] + "", "");
                else
                    return true;

            else if (str == "゛" || str == "ﾞ") // これだけどうしても上で処理できなかったので
                if (blocktyped.Length == 0)
                    nextstringset("゛", "");
                else
                    return true;
            else if (str == "｣") // これもどうしても上で処理できなかったので
                if (blocktyped.Length == 0)
                    nextstringset("」", "");
                else
                    return true;

            else if (str == "い") // i, yi
                switch (blocktyped)
                {
                    case "":
                        nextstringset("IY", "");
                        break;
                    case "Y":
                        nextstringset("I", "");
                        break;
                    default:
                        return true;
                }

            else if (str == "う") // u, wu, whu
                switch (blocktyped)
                {
                    case "":
                        nextstringset("UW", "");
                        break;
                    case "W":
                        nextstringset("UH", "");
                        break;
                    case "WH":
                        nextstringset("U", "");
                        break;
                    default:
                        return true;
                }

            else if (str == "ん") // nn, xn
                if (lyricsdata[0][nowline].yomigana.Length - 1 == cursor) // 最後の一文字
                    switch (blocktyped)
                    {
                        case "":
                            nextstringset("NX", "N");
                            break;
                        case "N":
                        case "X":
                            nextstringset("N", "");
                            break;
                        default:
                            return true;
                    }

                else
                {
                    char char2 = lyricsdata[0][nowline].yomigana[cursor + 1];
                    if ("あいえおなにぬねのやゆよNn".IndexOf(char2) != -1)
                        switch (blocktyped)
                        {
                            case "":
                                nextstringset("NX", "N");
                                break;
                            case "N":
                            case "X":
                                nextstringset("N", "");
                                break;
                            default:
                                return true;
                        }

                    else if (char2 == 'う') // 「う」を「wu」「whu」で打つ際は N は1回でいい
                        if (lyricsdata[0][nowline].yomigana.Length - 2 == cursor ||
                            "ぁぃぇぉ".IndexOf(lyricsdata[0][nowline].yomigana[cursor + 2]) == -1)
                            switch (blocktyped)
                            {
                                case "":
                                    nextstringset("NX", "N");
                                    break;
                                case "N":
                                    nextstringset("NW", "");
                                    break;
                                case "X":
                                    nextstringset("N", "");
                                    break;
                                case "NW":
                                    ++cursor;
                                    lyricsdata[0][nowline].typed += "N";
                                    blocktyped = "W";
                                    nextstringset("UH", "");
                                    break;
                                default:
                                    return true;
                            }
                        else // 「んうぁ」など
                            switch (blocktyped)
                            {
                                case "":
                                    nextstringset("NX", "");
                                    break;
                                case "N":
                                    nextstringset("NW", "");
                                    break;
                                case "NW":
                                    ++cursor;
                                    blockcounts = 2;
                                    lyricsdata[0][nowline].typed += "N";
                                    blocktyped = "W";
                                    switch (lyricsdata[0][nowline].yomigana[cursor + 2])
                                    {
                                        case 'ぁ':
                                            nextstringset("HU", "A");
                                            break;
                                        case 'ぃ':
                                            nextstringset("IUH", "");
                                            break;
                                        case 'ぇ':
                                            nextstringset("EUH", "");
                                            break;
                                        case 'ぉ':
                                            nextstringset("HU", "O");
                                            break;
                                    }
                                    break;
                                case "X":
                                    nextstringset("N", "");
                                    break;
                                default:
                                    return true;
                            }
                    else
                        switch (blocktyped)
                        {
                            case "":
                                nextstringset("NX", "");
                                break;
                            case "N":
                                ndouble = true;
                                return true;
                            case "X":
                                nextstringset("N", "");
                                break;
                            default:
                                return true;
                        }

                }
            // 「ん」の処理終わり

            else if (str == "し") // No.8
                switch (blocktyped)
                {
                    case "":
                        switch (romajisetting[8])
                        {
                            case 0:
                                nextstringset("SC", "I");
                                break;
                            case 1:
                                nextstringset("SC", "HI");
                                break;
                            case 2:
                                nextstringset("CS", "I");
                                break;
                        }
                        break;
                    case "S":
                        if (romajisetting[8] == 1)
                            nextstringset("HI", "I");
                        else
                            nextstringset("IH", "");
                        break;
                    case "C":
                    case "SH":
                        nextstringset("I", "");
                        break;
                    default:
                        return true;
                }

            else if (str == "か") // No.5
                switch (blocktyped)
                {
                    case "":
                        switch (romajisetting[5])
                        {
                            case 0:
                                nextstringset("KC", "A");
                                break;
                            case 1:
                                nextstringset("CK", "A");
                                break;
                        }
                        break;
                    case "K":
                    case "C":
                        nextstringset("A", "");
                        break;
                    default:
                        return true;
                }

            else if (twocharacterindexof != -1) // 母音+子音の組み合わせが1通りのもの
                switch (blocktyped.Length)
                {
                    case 0:
                        nextstringset(sps[twocharacterindexof].ToString(), spb[twocharacterindexof].ToString());
                        break;
                    case 1:
                        nextstringset(spb[twocharacterindexof].ToString(), "");
                        break;
                    default:
                        return true;
                }

            else if (str == "く") // No.6
                switch (blocktyped)
                {
                    case "":
                        switch (romajisetting[6])
                        {
                            case 0:
                                nextstringset("KCQ", "U");
                                break;
                            case 1:
                                nextstringset("CKQ", "U");
                                break;
                            case 2:
                                nextstringset("QKC", "U");
                                break;
                        }
                        break;
                    case "K":
                    case "C":
                    case "Q":
                        nextstringset("U", "");
                        break;
                    default:
                        return true;
                }

            else if (str == "こ") // No.7
                switch (blocktyped)
                {
                    case "":
                        switch (romajisetting[7])
                        {
                            case 0:
                                nextstringset("KC", "O");
                                break;
                            case 1:
                                nextstringset("CK", "O");
                                break;
                        }
                        break;
                    case "K":
                    case "C":
                        nextstringset("O", "");
                        break;
                    default:
                        return true;
                }

            else if (str == "じ") // No.12
                switch (blocktyped)
                {
                    case "":
                        switch (romajisetting[12])
                        {
                            case 0:
                                nextstringset("JZ", "I");
                                break;
                            case 1:
                                nextstringset("ZJ", "I");
                                break;
                        }
                        break;
                    case "J":
                    case "Z":
                        nextstringset("I", "");
                        break;
                    default:
                        return true;
                }

            else if (str == "つ")
                switch (blocktyped)
                {
                    case "":
                        nextstringset("T", "U");
                        break;
                    case "T":
                        nextstringset("US", "");
                        break;
                    case "TS":
                        nextstringset("U", "");
                        break;
                    default:
                        return true;
                }

            else if (str == "ち") // No.18
                switch (blocktyped)
                {
                    case "":
                        switch (romajisetting[18])
                        {
                            case 0:
                                nextstringset("TC", "I");
                                break;
                            case 1:
                                nextstringset("CT", "HI");
                                break;
                        }
                        break;
                    case "T":
                    case "CH":
                        nextstringset("I", "");
                        break;
                    case "C":
                        nextstringset("H", "I");
                        break;
                    default:
                        return true;
                }

            else if (str == "せ") // No.17
                switch (blocktyped)
                {
                    case "":
                        switch (romajisetting[17])
                        {
                            case 0:
                                nextstringset("SC", "E");
                                break;
                            case 1:
                                nextstringset("CS", "E");
                                break;
                        }
                        break;
                    case "S":
                    case "C":
                        nextstringset("E", "");
                        break;
                    default:
                        return true;
                }

            else if (str == "ふ") // No.23
                switch (blocktyped)
                {
                    case "":
                        switch (romajisetting[23])
                        {
                            case 0:
                                nextstringset("HF", "U");
                                break;
                            case 1:
                                nextstringset("FH", "U");
                                break;
                        }
                        break;
                    case "H":
                    case "F":
                        nextstringset("U", "");
                        break;
                    default:
                        return true;
                }

            else if (str == "ゐ") // wyi
                switch (blocktyped)
                {
                    case "":
                        nextstringset("W", "YI");
                        break;
                    case "W":
                        nextstringset("Y", "I");
                        break;
                    case "WY":
                        nextstringset("I", "");
                        break;
                    default:
                        return true;
                }

            else if (str == "ゑ") // wye
                switch (blocktyped)
                {
                    case "":
                        nextstringset("W", "YE");
                        break;
                    case "W":
                        nextstringset("Y", "E");
                        break;
                    case "WY":
                        nextstringset("E", "");
                        break;
                    default:
                        return true;
                }

            else if (str == "いぇ") // ye i- yi-
                switch (blocktyped)
                {
                    case "":
                        nextstringset("YI", "E");
                        break;
                    case "Y":
                        nextstringset("EI", "");
                        break;
                    case "YE":
                        return true;
                    default: // i, yi
                        ++cursor;
                        blockcounts = 1;
                        blocktyped = "";
                        switch (romajisetting[3])
                        {
                            case 0:
                                nextstringset("LX", "E");
                                break;
                            case 1:
                                nextstringset("XL", "E");
                                break;
                        }
                        break;
                }

            else if (str == "うぃ" || str == "うぇ") // wi whi u- wu- whu-
            {
                int n = "ぃぇ".IndexOf(str[1]);
                string s = "IE";

                switch (blocktyped)
                {
                    case "":
                        nextstringset("WU", s[n] + "");
                        break;
                    case "W":
                        nextstringset(s[n] + "HU", "");
                        break;
                    case "WH":
                        nextstringset(s[n] + "U", "");
                        break;
                    case "U":
                    case "WU":
                    case "WHU":
                        ++cursor;
                        blockcounts = 1;
                        blocktyped = "";
                        switch (romajisetting[1 + 2 * n])
                        {
                            case 0:
                                nextstringset("LX", s[n] + "");
                                break;
                            case 1:
                                nextstringset("XL", s[n] + "");
                                break;
                        }
                        break;
                    default:
                        return true;
                }
            }

            else if (str == "うぁ" || str == "うぉ") // wha u- wu- whu-
            {
                int n = "ぁぉ".IndexOf(str[1]);
                string s = "AO";

                switch (blocktyped)
                {
                    case "":
                        nextstringset("WU", "H" + s[n]);
                        break;
                    case "W":
                        nextstringset("HU", s[n] + "");
                        break;
                    case "WH":
                        nextstringset(s[n] + "U", "");
                        break;
                    case "U":
                    case "WU":
                    case "WHU":
                        ++cursor;
                        blockcounts = 1;
                        blocktyped = "";
                        switch (romajisetting[4 * n])
                        {
                            case 0:
                                nextstringset("LX", s[n] + "");
                                break;
                            case 1:
                                nextstringset("XL", s[n] + "");
                                break;
                        }
                        break;
                    default:
                        return true;
                }
            }

            else if (str == "しゃ" || str == "しゅ" || str == "しょ") // No.9 No.10 No.11
            {
                int n = "ゃゅょ".IndexOf(str[1]);
                string s = "AUO";

                switch (blocktyped)
                {
                    case "":
                        switch (romajisetting[9 + n])
                        {
                            case 0:
                                nextstringset("SC", "H" + s[n]);
                                break;
                            case 1:
                                nextstringset("SC", "Y" + s[n]);
                                break;
                        }
                        break;
                    case "S":
                        switch (romajisetting[9 + n])
                        {
                            case 0:
                                nextstringset("HYI", s[n] + "");
                                break;
                            case 1:
                                nextstringset("YHI", s[n] + "");
                                break;
                        }
                        break;
                    case "C":
                        blockcounts = 1;
                        nextstringset("I", "");
                        break;
                    case "SH":
                        nextstringset(s[n] + "I", "");
                        break;
                    case "SY":
                        nextstringset(s[n] + "", "");
                        break;
                    case "SI":
                    case "SHI":
                        ++cursor;
                        blockcounts = 1;
                        blocktyped = "";
                        switch (romajisetting[24 + n])
                        {
                            case 0:
                                nextstringset("LX", "Y" + s[n]);
                                break;
                            case 1:
                                nextstringset("XL", "Y" + s[n]);
                                break;
                        }
                        break;
                    default:
                        return true;
                }
            }

            else if (str == "しぃ") // syi si- shi- ci-
                switch (blocktyped)
                {
                    case "":
                        nextstringset("SC", "YI");
                        break;
                    case "S":
                        nextstringset("YIH", "I");
                        break;
                    case "C":
                        blockcounts = 1;
                        nextstringset("I", "");
                        break;
                    case "SY":
                        nextstringset("I", "");
                        break;
                    case "SI":
                        ++cursor;
                        blockcounts = 1;
                        blocktyped = "";
                        switch (romajisetting[1])
                        {
                            case 0:
                                nextstringset("LX", "I");
                                break;
                            case 1:
                                nextstringset("XL", "I");
                                break;
                        }
                        break;
                    case "SH":
                        blockcounts = 1;
                        nextstringset("I", "");
                        break;
                    default:
                        return true;
                }

            else if (str == "しぇ") // No.27
            {
                switch (blocktyped)
                {
                    case "":
                        switch (romajisetting[27])
                        {
                            case 0:
                                nextstringset("SC", "HE");
                                break;
                            case 1:
                                nextstringset("SC", "YE");
                                break;
                        }
                        break;
                    case "S":
                        switch (romajisetting[27])
                        {
                            case 0:
                                nextstringset("HYI", "E");
                                break;
                            case 1:
                                nextstringset("YHI", "E");
                                break;
                        }
                        break;
                    case "C":
                        blockcounts = 1;
                        nextstringset("I", "");
                        break;
                    case "SH":
                        nextstringset("EI", "");
                        break;
                    case "SY":
                        nextstringset("E", "");
                        break;
                    case "SI":
                    case "SHI":
                        ++cursor;
                        blockcounts = 1;
                        blocktyped = "";
                        switch (romajisetting[3])
                        {
                            case 0:
                                nextstringset("LX", "E");
                                break;
                            case 1:
                                nextstringset("XL", "E");
                                break;
                        }
                        break;
                    default:
                        return true;
                }
            }

            else if (str.Length == 2 && (str[0] == 'ぐ' || str[0] == 'す' || str[0] == 'と' || str[0] == 'ど') &&
                (str[1] == 'ぁ' || str[1] == 'ぃ' || str[1] == 'ぅ' || str[1] == 'ぇ' || str[1] == 'ぉ')) // gwa gu-
            {
                int n0 = "ぐすとど".IndexOf(str[0]);
                string s0 = "GSTD";
                string s1 = "UUOO";

                int n2 = "ぁぃぅぇぉ".IndexOf(str[1]);
                string s2 = "AIUEO";

                switch (blocktyped.Length)
                {
                    case 0:
                        nextstringset(s0[n0] + "", "W" + s2[n2]);
                        break;
                    case 1:
                        nextstringset("W" + s1[n0], s2[n2] + "");
                        break;
                    case 2:
                        if (blocktyped[1] == 'W')
                            nextstringset(s2[n2] + "", "");
                        else
                        {
                            ++cursor;
                            blockcounts = 1;
                            blocktyped = "";
                            switch (romajisetting[n2])
                            {
                                case 0:
                                    nextstringset("LX", s2[n2] + "");
                                    break;
                                case 1:
                                    nextstringset("XL", s2[n2] + "");
                                    break;
                            }
                            break;
                        }
                        break;
                    default:
                        return true;
                }
            }

            else if (str.Length == 2 && (str[0] == 'て' || str[0] == 'で') && (str[1] == 'ゃ' || str[1] == 'ゅ' || str[1] == 'ょ')) // tha te-
            {
                int n0 = "てで".IndexOf(str[0]);
                string s0 = "TD";

                int n1 = "ゃゅょ".IndexOf(str[1]);
                string s1 = "AUO";

                switch (blocktyped.Length)
                {
                    case 0:
                        nextstringset(s0[n0] + "", "H" + s1[n1]);
                        break;
                    case 1:
                        nextstringset("HE", s1[n1] + "");
                        break;
                    case 2:
                        if (blocktyped[1] == 'H')
                            nextstringset(s1[n1] + "", "");
                        else
                        {
                            ++cursor;
                            blockcounts = 1;
                            blocktyped = "";
                            switch (romajisetting[24 + n1])
                            {
                                case 0:
                                    nextstringset("LX", "Y" + s1[n1]);
                                    break;
                                case 1:
                                    nextstringset("XL", "Y" + s1[n1]);
                                    break;
                            }
                            break;
                        }
                        break;
                    default:
                        return true;
                }
            }

            else if (str.Length == 2 && (str[0] == 'て' || str[0] == 'で') && (str[1] == 'ぃ' || str[1] == 'ぇ')) // thi te-
            {
                int n0 = "てで".IndexOf(str[0]);
                string s0 = "TD";

                int n1 = "ぃぇ".IndexOf(str[1]);
                string s1 = "IE";

                switch (blocktyped.Length)
                {
                    case 0:
                        nextstringset(s0[n0] + "", "H" + s1[n1]);
                        break;
                    case 1:
                        nextstringset("HE", s1[n1] + "");
                        break;
                    case 2:
                        if (blocktyped[1] == 'H')
                            nextstringset(s1[n1] + "", "");
                        else
                        {
                            ++cursor;
                            blockcounts = 1;
                            blocktyped = "";
                            switch (romajisetting[1 + 2 * n1])
                            {
                                case 0:
                                    nextstringset("LX", s1[n1] + "");
                                    break;
                                case 1:
                                    nextstringset("XL", s1[n1] + "");
                                    break;
                            }
                            break;
                        }
                        break;
                    default:
                        return true;
                }
            }

            else if (str == "くゃ" || str == "くゅ" || str == "くょ") // qya ku- qu- cu-
            {
                int n = "ゃゅょ".IndexOf(str[1]);
                string s = "AUO";

                switch (blocktyped)
                {
                    case "":
                        nextstringset("QKC", "Y" + s[n]);
                        break;
                    case "Q":
                        nextstringset("YU", s[n] + "");
                        break;
                    case "K":
                    case "C":
                        blockcounts = 1;
                        nextstringset("U", "");
                        break;
                    case "QY":
                        nextstringset(s[n] + "", "");
                        break;
                    case "QU":
                        ++cursor;
                        blockcounts = 1;
                        blocktyped = "";
                        switch (romajisetting[24 + n])
                        {
                            case 0:
                                nextstringset("LX", "Y" + s[n]);
                                break;
                            case 1:
                                nextstringset("XL", "Y" + s[n]);
                                break;
                        }
                        break;
                    default:
                        return true;
                }
            }

            else if (str == "くぃ" || str == "くぇ") // qi qwi qyi ku- qu- cu-
            {
                int n = "ぃぇ".IndexOf(str[1]);
                string s = "IE";

                switch (blocktyped)
                {
                    case "":
                        nextstringset("QKC", s[n] + "");
                        break;
                    case "Q":
                        nextstringset(s[n] + "WYU", "");
                        break;
                    case "C":
                    case "K":
                        blockcounts = 1;
                        nextstringset("U", "");
                        break;
                    case "QW":
                    case "QY":
                        nextstringset(s[n] + "", "");
                        break;
                    case "QU":
                        ++cursor;
                        blockcounts = 1;
                        blocktyped = "";
                        switch (romajisetting[1 + 2 * n])
                        {
                            case 0:
                                nextstringset("LX", s[n] + "");
                                break;
                            case 1:
                                nextstringset("XL", s[n] + "");
                                break;
                        }
                        break;
                    default:
                        return true;
                }
            }

            else if (str == "くぁ")
                switch (blocktyped)
                {
                    case "":
                        switch (romajisetting[28])
                        {
                            case 0:
                                nextstringset("QKC", "A");
                                break;
                            case 1:
                                nextstringset("KQC", "WA");
                                break;
                        }
                        break;
                    case "Q":
                        nextstringset("AWU", "");
                        break;
                    case "K":
                        nextstringset("WU", "A");
                        break;
                    case "C":
                        blockcounts = 1;
                        nextstringset("U", "");
                        break;
                    case "QW":
                        nextstringset("A", "");
                        break;
                    case "KU":
                    case "QU":
                        ++cursor;
                        blockcounts = 1;
                        blocktyped = "";
                        switch (romajisetting[0])
                        {
                            case 0:
                                nextstringset("LX", "A");
                                break;
                            case 1:
                                nextstringset("XL", "A");
                                break;
                        }
                        break;
                    default:
                        return true;
                }

            else if (str == "くぉ") // qo qwo ku- qu- cu-
                switch (blocktyped)
                {
                    case "":
                        nextstringset("QKC", "O");
                        break;
                    case "Q":
                        nextstringset("OWU", "");
                        break;
                    case "K":
                    case "C":
                        blockcounts = 1;
                        nextstringset("U", "");
                        break;
                    case "QW":
                        nextstringset("O", "");
                        break;
                    case "QU":
                        ++cursor;
                        blockcounts = 1;
                        blocktyped = "";
                        switch (romajisetting[4])
                        {
                            case 0:
                                nextstringset("LX", "O");
                                break;
                            case 1:
                                nextstringset("XL", "O");
                                break;
                        }
                        break;
                    default:
                        return true;
                }

            else if (str == "くぅ") // qwu ku- qu- cu-
                switch (blocktyped)
                {
                    case "":
                        nextstringset("QKC", "WU");
                        break;
                    case "Q":
                        nextstringset("WU", "U");
                        break;
                    case "K":
                    case "C":
                        blockcounts = 1;
                        nextstringset("U", "");
                        break;
                    case "QW":
                        nextstringset("U", "");
                        break;
                    case "QU":
                        ++cursor;
                        blockcounts = 1;
                        blocktyped = "";
                        switch (romajisetting[2])
                        {
                            case 0:
                                nextstringset("LX", "U");
                                break;
                            case 1:
                                nextstringset("XL", "U");
                                break;
                        }
                        break;
                    default:
                        return true;
                }

            else if (str.Length == 2 && (str[0] == 'き' || str[0] == 'ぎ' || str[0] == 'ぢ' || str[0] == 'に'
                || str[0] == 'ひ' || str[0] == 'び' || str[0] == 'ぴ' || str[0] == 'み' || str[0] == 'り') &&
                (str[1] == 'ゃ' || str[1] == 'ゅ' || str[1] == 'ょ')) // kya ki-
            {
                int n0 = "きぎぢにひびぴみり".IndexOf(str[0]);
                string s0 = "KGDNHBPMR";

                int n1 = "ゃゅょ".IndexOf(str[1]);
                string s1 = "AUO";

                switch (blocktyped.Length)
                {
                    case 0:
                        nextstringset(s0[n0] + "", "Y" + s1[n1]);
                        break;
                    case 1:
                        nextstringset("YI", s1[n1] + "");
                        break;
                    case 2:
                        if (blocktyped[1] == 'Y')
                            nextstringset(s1[n1] + "", "");
                        else
                        {
                            ++cursor;
                            blockcounts = 1;
                            blocktyped = "";
                            switch (romajisetting[24 + n1])
                            {
                                case 0:
                                    nextstringset("LX", "Y" + s1[n1]);
                                    break;
                                case 1:
                                    nextstringset("XL", "Y" + s1[n1]);
                                    break;
                            }
                            break;
                        }
                        break;
                    default:
                        return true;
                }
            }

            else if (str.Length == 2 && (str[0] == 'き' || str[0] == 'ぎ' || str[0] == 'ぢ' || str[0] == 'に'
                || str[0] == 'ひ' || str[0] == 'び' || str[0] == 'ぴ' || str[0] == 'み' || str[0] == 'り') &&
                (str[1] == 'ぃ' || str[1] == 'ぇ')) // kyi ki-
            {
                int n0 = "きぎぢにひびぴみり".IndexOf(str[0]);
                string s0 = "KGDNHBPMR";

                int n1 = "ぃぇ".IndexOf(str[1]);
                string s1 = "IE";

                switch (blocktyped.Length)
                {
                    case 0:
                        nextstringset(s0[n0] + "", "Y" + s1[n1]);
                        break;
                    case 1:
                        nextstringset("YI", s1[n1] + "");
                        break;
                    case 2:
                        if (blocktyped[1] == 'Y')
                            nextstringset(s1[n1] + "", "");
                        else
                        {
                            ++cursor;
                            blockcounts = 1;
                            blocktyped = "";
                            switch (romajisetting[1 + 2 * n1])
                            {
                                case 0:
                                    nextstringset("LX", s1[n1] + "");
                                    break;
                                case 1:
                                    nextstringset("XL", s1[n1] + "");
                                    break;
                            }
                            break;
                        }
                        break;
                    default:
                        return true;
                }
            }

            else if (str == "じゃ" || str == "じゅ" || str == "じょ") // No.14, 15, 16
            {
                int n = "ゃゅょ".IndexOf(str[1]);
                string s = "AUO";

                switch (blocktyped)
                {
                    case "":
                        switch (romajisetting[14 + n])
                        {
                            case 0:
                                nextstringset("JZ", s[n] + "");
                                break;
                            case 1:
                                nextstringset("ZJ", "Y" + s[n]);
                                break;
                        }
                        break;
                    case "J":
                        nextstringset(s[n] + "YI", "");
                        break;
                    case "Z":
                        nextstringset("YI", s[n] + "");
                        break;
                    case "JY":
                    case "ZY":
                        nextstringset(s[n] + "", "");
                        break;
                    case "JI":
                    case "ZI":
                        ++cursor;
                        blockcounts = 1;
                        blocktyped = "";
                        switch (romajisetting[24 + n])
                        {
                            case 0:
                                nextstringset("LX", "Y" + s[n]);
                                break;
                            case 1:
                                nextstringset("XL", "Y" + s[n]);
                                break;
                        }
                        break;
                    default:
                        return true;
                }
            }

            else if (str == "じぃ") // No.29 jyi zyi ji- zi-
                switch (blocktyped)
                {
                    case "":
                        switch (romajisetting[29])
                        {
                            case 0:
                                nextstringset("JZ", "YI");
                                break;
                            case 1:
                                nextstringset("ZJ", "YI");
                                break;
                        }
                        break;
                    case "J":
                    case "Z":
                        nextstringset("YI", "I");
                        break;
                    case "JY":
                    case "ZY":
                        nextstringset("I", "");
                        break;
                    case "JI":
                    case "ZI":
                        ++cursor;
                        blockcounts = 1;
                        blocktyped = "";
                        switch (romajisetting[1])
                        {
                            case 0:
                                nextstringset("LX", "I");
                                break;
                            case 1:
                                nextstringset("XL", "I");
                                break;
                        }
                        break;
                    default:
                        return true;
                }

            else if (str == "じぇ") // No.13 je jye zye ji- zi-
                switch (blocktyped)
                {
                    case "":
                        switch (romajisetting[13])
                        {
                            case 0:
                                nextstringset("JZ", "E");
                                break;
                            case 1:
                                nextstringset("ZJ", "YE");
                                break;
                        }
                        break;
                    case "J":
                        nextstringset("EYI", "");
                        break;
                    case "Z":
                        nextstringset("YI", "E");
                        break;
                    case "JY":
                    case "ZY":
                        nextstringset("E", "");
                        break;
                    case "JI":
                    case "ZI":
                        ++cursor;
                        blockcounts = 1;
                        blocktyped = "";
                        switch (romajisetting[1])
                        {
                            case 0:
                                nextstringset("LX", "E");
                                break;
                            case 1:
                                nextstringset("XL", "E");
                                break;
                        }
                        break;
                    default:
                        return true;
                }

            else if (str == "つぁ" || str == "つぃ" || str == "つぇ" || str == "つぉ") // tsa tu- tsu-
            {
                int n = "ぁぃぅぇぉ".IndexOf(str[1]);
                string s = "AIUEO";

                switch (blocktyped)
                {
                    case "":
                        nextstringset("T", "S" + s[n]);
                        break;
                    case "T":
                        nextstringset("SU", s[n] + "");
                        break;
                    case "TS":
                        nextstringset(s[n] + "U", "");
                        break;
                    case "TU":
                    case "TSU":
                        ++cursor;
                        blockcounts = 1;
                        blocktyped = "";
                        switch (romajisetting[n])
                        {
                            case 0:
                                nextstringset("LX", s[n] + "");
                                break;
                            case 1:
                                nextstringset("XL", s[n] + "");
                                break;
                        }
                        break;
                    default:
                        return true;
                }
            }

            else if (str == "ちゃ" || str == "ちゅ" || str == "ちょ") // No.19 No.20 No.21
            {
                int n = "ゃゅょ".IndexOf(str[1]);
                string s = "AUO";

                switch (blocktyped)
                {
                    case "":
                        switch (romajisetting[19 + n])
                        {
                            case 0:
                                nextstringset("CT", "H" + s[n]);
                                break;
                            case 1:
                                nextstringset("CT", "Y" + s[n]);
                                break;
                            case 2:
                                nextstringset("TC", "Y" + s[n]);
                                break;
                        }
                        break;
                    case "C":
                        switch (romajisetting[19 + n])
                        {
                            case 0:
                                nextstringset("HY", s[n] + "");
                                break;
                            default:
                                nextstringset("YH", s[n] + "");
                                break;
                        }
                        break;
                    case "T":
                        nextstringset("YI", s[n] + "");
                        break;
                    case "CH":
                        nextstringset(s[n] + "I", "");
                        break;
                    case "CY":
                    case "TY":
                        nextstringset(s[n] + "", "");
                        break;
                    case "TI":
                    case "CHI":
                        ++cursor;
                        blockcounts = 1;
                        blocktyped = "";
                        switch (romajisetting[24 + n])
                        {
                            case 0:
                                nextstringset("LX", "Y" + s[n]);
                                break;
                            case 1:
                                nextstringset("XL", "Y" + s[n]);
                                break;
                        }
                        break;
                    default:
                        return true;
                }
            }

            else if (str == "ちぃ") // No.30 cyi tyi ti- chi-
                switch (blocktyped)
                {
                    case "":
                        switch (romajisetting[30])
                        {
                            case 0:
                                nextstringset("CT", "YI");
                                break;
                            case 1:
                                nextstringset("TC", "YI");
                                break;
                        }
                        break;
                    case "C":
                        nextstringset("YH", "I");
                        break;
                    case "T":
                        nextstringset("YI", "I");
                        break;
                    case "CY":
                    case "TY":
                        nextstringset("I", "");
                        break;
                    case "CH":
                        blockcounts = 1;
                        nextstringset("I", "");
                        break;
                    case "TI":
                        ++cursor;
                        blockcounts = 1;
                        blocktyped = "";
                        switch (romajisetting[1])
                        {
                            case 0:
                                nextstringset("LX", "I");
                                break;
                            case 1:
                                nextstringset("XL", "I");
                                break;
                        }
                        break;
                    default:
                        return true;
                }

            else if (str == "ちぇ") // No.31 che cye tye ti- chi-
                switch (blocktyped)
                {
                    case "":
                        switch (romajisetting[31])
                        {
                            case 0:
                                nextstringset("CT", "HE");
                                break;
                            case 1:
                                nextstringset("CT", "YE");
                                break;
                            case 2:
                                nextstringset("TC", "YE");
                                break;
                        }
                        break;
                    case "C":
                        switch (romajisetting[31])
                        {
                            case 0:
                                nextstringset("HY", "E");
                                break;
                            default:
                                nextstringset("YH", "E");
                                break;
                        }
                        break;
                    case "T":
                        nextstringset("YI", "E");
                        break;
                    case "CH":
                        nextstringset("EI", "");
                        break;
                    case "CY":
                    case "TY":
                        nextstringset("E", "");
                        break;
                    case "TI":
                    case "CHI":
                        ++cursor;
                        blockcounts = 1;
                        blocktyped = "";
                        switch (romajisetting[3])
                        {
                            case 0:
                                nextstringset("LX", "E");
                                break;
                            case 1:
                                nextstringset("XL", "E");
                                break;
                        }
                        break;
                    default:
                        return true;
                }

            else if (str == "ふゃ" || str == "ふゅ" || str == "ふょ") // fya hu- fu-
            {
                int n = "ゃゅょ".IndexOf(str[1]);
                string s = "AUO";

                switch (blocktyped)
                {
                    case "":
                        nextstringset("FH", "Y" + s[n]);
                        break;
                    case "F":
                        nextstringset("YU", s[n] + "");
                        break;
                    case "H":
                        blockcounts = 1;
                        nextstringset("U", "");
                        break;
                    case "FY":
                        nextstringset(s[n] + "", "");
                        break;
                    case "FU":
                        ++cursor;
                        blockcounts = 1;
                        blocktyped = "";
                        switch (romajisetting[24 + n])
                        {
                            case 0:
                                nextstringset("LX", "Y" + s[n]);
                                break;
                            case 1:
                                nextstringset("XL", "Y" + s[n]);
                                break;
                        }
                        break;
                    default:
                        return true;
                }
            }

            else if (str == "ふぃ" || str == "ふぇ") // fi fwi fyi fu- hu-
            {
                int n = "ぃぇ".IndexOf(str[1]);
                string s = "IE";

                switch (blocktyped)
                {
                    case "":
                        nextstringset("FH", s[n] + "");
                        break;
                    case "F":
                        nextstringset(s[n] + "WYU", "");
                        break;
                    case "H":
                        blockcounts = 1;
                        nextstringset("U", "");
                        break;
                    case "FW":
                    case "FY":
                        nextstringset(s[n] + "", "");
                        break;
                    case "FU":
                        ++cursor;
                        blockcounts = 1;
                        blocktyped = "";
                        switch (romajisetting[1 + 2 * n])
                        {
                            case 0:
                                nextstringset("LX", s[n] + "");
                                break;
                            case 1:
                                nextstringset("XL", s[n] + "");
                                break;
                        }
                        break;
                    default:
                        return true;
                }
            }

            else if (str == "ふぁ") // fa fwa hu- fu-
                switch (blocktyped)
                {
                    case "":
                        nextstringset("FH", "A");
                        break;
                    case "F":
                        nextstringset("AWU", "");
                        break;
                    case "H":
                        blockcounts = 1;
                        nextstringset("U", "");
                        break;
                    case "FW":
                        nextstringset("A", "");
                        break;
                    case "FU":
                        ++cursor;
                        blockcounts = 1;
                        blocktyped = "";
                        switch (romajisetting[0])
                        {
                            case 0:
                                nextstringset("LX", "A");
                                break;
                            case 1:
                                nextstringset("XL", "A");
                                break;
                        }
                        break;
                    default:
                        return true;
                }

            else if (str == "ふぉ") // fo fwo hu- fu-
                switch (blocktyped)
                {
                    case "":
                        nextstringset("FH", "O");
                        break;
                    case "F":
                        nextstringset("OWU", "");
                        break;
                    case "H":
                        blockcounts = 1;
                        nextstringset("U", "");
                        break;
                    case "FW":
                        nextstringset("O", "");
                        break;
                    case "FU":
                        ++cursor;
                        blockcounts = 1;
                        blocktyped = "";
                        switch (romajisetting[4])
                        {
                            case 0:
                                nextstringset("LX", "O");
                                break;
                            case 1:
                                nextstringset("XL", "O");
                                break;
                        }
                        break;
                    default:
                        return true;
                }

            else if (str == "ふぅ") // fwu fu- hu-
                switch (blocktyped)
                {
                    case "":
                        nextstringset("FH", "WU");
                        break;
                    case "F":
                        nextstringset("WU", "U");
                        break;
                    case "H":
                        blockcounts = 1;
                        nextstringset("U", "");
                        break;
                    case "FW":
                        nextstringset("U", "");
                        break;
                    case "FU":
                        ++cursor;
                        blockcounts = 1;
                        blocktyped = "";
                        switch (romajisetting[2])
                        {
                            case 0:
                                nextstringset("LX", "U");
                                break;
                            case 1:
                                nextstringset("XL", "U");
                                break;
                        }
                        break;
                    default:
                        return true;
                }

            else if (str == "ヴゃ" || str == "ヴゅ" || str == "ヴょ" || str == "ゔゃ" || str == "ゔゅ" || str == "ゔょ")
            {
                int n = "ゃゅょ".IndexOf(str[1]);
                string s = "AUO";

                switch (blocktyped)
                {
                    case "":
                        nextstringset("V", "Y" + s[n]);
                        break;
                    case "V":
                        nextstringset("YU", s[n] + "");
                        break;
                    case "VY":
                        nextstringset(s[n] + "", "");
                        break;
                    case "VU":
                        ++cursor;
                        blockcounts = 1;
                        blocktyped = "";
                        switch (romajisetting[24 + n])
                        {
                            case 0:
                                nextstringset("LX", "Y" + s[n]);
                                break;
                            case 1:
                                nextstringset("XL", "Y" + s[n]);
                                break;
                        }
                        break;
                    default:
                        return true;
                }
            }

            else if (str == "ヴぃ" || str == "ヴぇ" || str == "ゔぃ" || str == "ゔぇ") // vi vyi vu-
            {
                int n = "ぃぇ".IndexOf(str[1]);
                string s = "IE";

                switch (blocktyped)
                {
                    case "":
                        nextstringset("V", s[n] + "");
                        break;
                    case "V":
                        nextstringset(s[n] + "YU", "");
                        break;
                    case "VY":
                        nextstringset(s[n] + "", "");
                        break;
                    case "VU":
                        ++cursor;
                        blockcounts = 1;
                        blocktyped = "";
                        switch (romajisetting[1 + 2 * n])
                        {
                            case 0:
                                nextstringset("LX", s[n] + "");
                                break;
                            case 1:
                                nextstringset("XL", s[n] + "");
                                break;
                        }
                        break;
                    default:
                        return true;
                }
            }

            else if (str == "ヴぁ" || str == "ヴぉ" || str == "ゔぁ" || str == "ゔぉ")
            {
                int n = "ぁぉ".IndexOf(str[1]);
                string s = "AO";

                switch (blocktyped)
                {
                    case "":
                        nextstringset("V", s[n] + "");
                        break;
                    case "V":
                        nextstringset(s[n] + "U", "");
                        break;
                    case "VU":
                        ++cursor;
                        blockcounts = 1;
                        blocktyped = "";
                        switch (romajisetting[n * 4])
                        {
                            case 0:
                                nextstringset("LX", s[n] + "");
                                break;
                            case 1:
                                nextstringset("XL", s[n] + "");
                                break;
                        }
                        break;
                    default:
                        return true;
                }
            }

            else if (str == "ゃ" || str == "ゅ" || str == "ょ") // No.24, 25, 26
            {
                int n = "ゃゅょ".IndexOf(str);
                string s = "AUO";

                switch (blocktyped)
                {
                    case "":
                        switch (romajisetting[24 + n])
                        {
                            case 0:
                                nextstringset("LX", "Y" + s[n]);
                                break;
                            case 1:
                                nextstringset("XL", "Y" + s[n]);
                                break;
                        }
                        break;
                    case "L":
                    case "X":
                        nextstringset("Y", s[n] + "");
                        break;
                    case "LY":
                    case "XY":
                        nextstringset(s[n] + "", "");
                        break;
                    default:
                        return true;
                }
            }

            else if (str == "ぁ" || str == "ぅ" || str == "ぉ") // No.0, 2, 4
            {
                int n = "ぁぅぉ".IndexOf(str);
                string s = "AUO";

                switch (blocktyped)
                {
                    case "":
                        switch (romajisetting[n * 2])
                        {
                            case 0:
                                nextstringset("LX", s[n] + "");
                                break;
                            case 1:
                                nextstringset("XL", s[n] + "");
                                break;
                        }
                        break;
                    case "L":
                    case "X":
                        nextstringset(s[n] + "", "");
                        break;
                    default:
                        return true;
                }
            }

            else if (str == "ぃ" || str == "ぇ") // No.1, 3
            {
                int n = "ぃぇ".IndexOf(str);
                string s = "IE";

                switch (blocktyped)
                {
                    case "":
                        switch (romajisetting[1 + 2 * n])
                        {
                            case 0:
                                nextstringset("LX", s[n] + "");
                                break;
                            case 1:
                                nextstringset("XL", s[n] + "");
                                break;
                        }
                        break;
                    case "L":
                    case "X":
                        nextstringset(s[n] + "Y", "");
                        break;
                    case "LY":
                    case "XY":
                        nextstringset(s[n] + "", "");
                        break;
                    default:
                        return true;
                }
            }

            else if (str == "ゎ")
                switch (blocktyped)
                {
                    case "":
                        nextstringset("LX", "WA");
                        break;
                    case "L":
                    case "X":
                        nextstringset("W", "A");
                        break;
                    case "LW":
                    case "XW":
                        nextstringset("A", "");
                        break;
                    default:
                        return true;
                }

            else if (str[0] == 'っ')
            {
                if (str.Length == 1)
                {
                    switch (blocktyped)
                    {
                        case "":
                            switch (romajisetting[22])
                            {
                                case 0:
                                    nextstringset("LX", "TU");
                                    break;
                                case 1:
                                    nextstringset("XL", "TU");
                                    break;
                            }
                            break;
                        case "L":
                        case "X":
                            nextstringset("T", "U");
                            break;
                        case "LT":
                        case "XT":
                            nextstringset("US", "");
                            break;
                        case "LTS":
                        case "XTS":
                            nextstringset("U", "");
                            break;
                        default:
                            return true;
                    }
                }

                else if (str.Length == 2)
                {
                    if (str[1] == 'い')
                        switch (blocktyped)
                        {
                            case "":
                                nextstringset("YLX", "YI");
                                break;
                            case "Y":
                                nextstringset("Y", "I");
                                break;
                            case "L":
                            case "X":
                                blockcounts = 1;
                                nextstringset("T", "U");
                                break;
                            case "YY":
                                nextstringset("I", "");
                                break;
                            default:
                                return true;
                        }

                    else if (str[1] == 'う')
                        switch (blocktyped)
                        {
                            case "":
                                nextstringset("WLX", "WU");
                                break;
                            case "W":
                                nextstringset("W", "U");
                                break;
                            case "L":
                            case "X":
                                blockcounts = 1;
                                nextstringset("T", "U");
                                break;
                            case "WW":
                                nextstringset("UH", "");
                                break;
                            case "WWH":
                                nextstringset("U", "");
                                break;
                            default:
                                return true;
                        }

                    else if (str[1] == 'し') // No.8
                        switch (blocktyped)
                        {
                            case "":
                                switch (romajisetting[8])
                                {
                                    case 0:
                                        nextstringset("SCLX", "SI");
                                        break;
                                    case 1:
                                        nextstringset("SCLX", "SHI");
                                        break;
                                    case 2:
                                        nextstringset("CSLX", "CI");
                                        break;
                                }
                                break;
                            case "S":
                                if (romajisetting[8] == 1)
                                    nextstringset("S", "HI");
                                else
                                    nextstringset("S", "I");
                                break;
                            case "C":
                                nextstringset("C", "I");
                                break;
                            case "L":
                            case "X":
                                blockcounts = 1;
                                nextstringset("T", "U");
                                break;
                            case "SS":
                                if (romajisetting[8] == 1)
                                    nextstringset("HI", "I");
                                else
                                    nextstringset("IH", "");
                                break;
                            case "SSH":
                            case "CC":
                                nextstringset("I", "");
                                break;
                            default:
                                return true;
                        }

                    else if (str[1] == 'つ')
                        switch (blocktyped)
                        {
                            case "":
                                nextstringset("TLX", "TU");
                                break;
                            case "T":
                                nextstringset("T", "U");
                                break;
                            case "L":
                            case "X":
                                blockcounts = 1;
                                nextstringset("T", "U");
                                break;
                            case "TT":
                                nextstringset("US", "");
                                break;
                            case "TTS":
                                nextstringset("U", "");
                                break;
                            default:
                                return true;
                        }

                    else if (str[1] == 'ち') // No.18
                        switch (blocktyped)
                        {
                            case "":
                                switch (romajisetting[18])
                                {
                                    case 0:
                                        nextstringset("TCLX", "TI");
                                        break;
                                    case 1:
                                        nextstringset("CTLX", "CHI");
                                        break;
                                }
                                break;
                            case "T":
                                nextstringset("T", "I");
                                break;
                            case "C":
                                nextstringset("C", "HI");
                                break;
                            case "L":
                            case "X":
                                blockcounts = 1;
                                nextstringset("T", "U");
                                break;
                            case "CC":
                                nextstringset("H", "I");
                                break;
                            case "TT":
                            case "CCH":
                                nextstringset("I", "");
                                break;
                            default:
                                return true;
                        }

                    else if (str[1] == 'く') // No.6
                        switch (blocktyped)
                        {
                            case "":
                                switch (romajisetting[6])
                                {
                                    case 0:
                                        nextstringset("KCQLX", "KU");
                                        break;
                                    case 1:
                                        nextstringset("CKQLX", "CU");
                                        break;
                                    case 2:
                                        nextstringset("QKCLX", "QU");
                                        break;
                                }
                                break;
                            case "K":
                            case "C":
                            case "Q":
                                nextstringset(blocktyped, "U");
                                break;
                            case "L":
                            case "X":
                                blockcounts = 1;
                                nextstringset("T", "U");
                                break;
                            case "KK":
                            case "CC":
                            case "QQ":
                                nextstringset("U", "");
                                break;
                            default:
                                return true;
                        }

                    else if (str[1] == 'ゐ')
                        switch (blocktyped)
                        {
                            case "":
                                nextstringset("WLX", "WYI");
                                break;
                            case "W":
                                nextstringset("W", "YI");
                                break;
                            case "L":
                            case "X":
                                blockcounts = 1;
                                nextstringset("T", "U");
                                break;
                            case "WW":
                                nextstringset("Y", "I");
                                break;
                            case "WWY":
                                nextstringset("I", "");
                                break;
                            default:
                                return true;
                        }

                    else if (str[1] == 'ゑ')
                        switch (blocktyped)
                        {
                            case "":
                                nextstringset("WLX", "WYE");
                                break;
                            case "W":
                                nextstringset("W", "YE");
                                break;
                            case "L":
                            case "X":
                                blockcounts = 1;
                                nextstringset("T", "U");
                                break;
                            case "WW":
                                nextstringset("Y", "E");
                                break;
                            case "WWY":
                                nextstringset("E", "");
                                break;
                            default:
                                return true;
                        }

                    else if (str[1] == 'ゃ' || str[1] == 'ゅ' || str[1] == 'ょ') // No.24, 25, 26
                    {
                        int n = "ゃゅょ".IndexOf(str[1]);
                        string s = "AUO";

                        switch (blocktyped)
                        {
                            case "":
                                switch (romajisetting[24 + n])
                                {
                                    case 0:
                                        nextstringset("LX", "LY" + s[n]);
                                        break;
                                    case 1:
                                        nextstringset("XL", "XY" + s[n]);
                                        break;
                                }
                                break;
                            case "L":
                            case "X":
                                nextstringset(blocktyped + "T", "Y" + s[n]);
                                break;
                            case "LL":
                            case "XX":
                                nextstringset("Y", s[n] + "");
                                break;
                            case "LT":
                            case "XT":
                                blockcounts = 1;
                                nextstringset("US", "");
                                break;
                            default:
                                return true;
                        }
                    }

                    else if (str[1] == 'ぁ' || str[1] == 'ぅ' || str[1] == 'ぉ') // No.0, 2, 4
                    {
                        int n = "ぁぅぉ".IndexOf(str[1]);
                        string s = "AUO";

                        switch (blocktyped)
                        {
                            case "":
                                switch (romajisetting[n * 2])
                                {
                                    case 0:
                                        nextstringset("LX", "L" + s[n]);
                                        break;
                                    case 1:
                                        nextstringset("XL", "X" + s[n]);
                                        break;
                                }
                                break;
                            case "L":
                            case "X":
                                nextstringset(blocktyped + "T", s[n] + "");
                                break;
                            case "LL":
                            case "XX":
                                nextstringset(s[n] + "", "");
                                break;
                            case "LT":
                            case "XT":
                                blockcounts = 1;
                                nextstringset("US", "");
                                break;
                            default:
                                return true;
                        }
                    }

                    else if (str[1] == 'ぃ' || str[1] == 'ぇ') // No.1, 3
                    {
                        int n = "ぃぇ".IndexOf(str[1]);
                        string s = "IE";

                        switch (blocktyped)
                        {
                            case "":
                                switch (romajisetting[1 + 2 * n])
                                {
                                    case 0:
                                        nextstringset("LX", "L" + s[n]);
                                        break;
                                    case 1:
                                        nextstringset("XL", "X" + s[n]);
                                        break;
                                }
                                break;
                            case "L":
                            case "X":
                                nextstringset(blocktyped + "T", s[n] + "");
                                break;
                            case "LL":
                            case "XX":
                                nextstringset(s[n] + "Y", "");
                                break;
                            case "LT":
                            case "XT":
                                blockcounts = 1;
                                nextstringset("US", "");
                                break;
                            case "LLY":
                            case "XXY":
                                nextstringset(s[n] + "", "");
                                break;
                            default:
                                return true;
                        }
                    }

                    else if (str[1] == 'ゎ')
                        switch (blocktyped)
                        {
                            case "":
                                nextstringset("LX", "LWA");
                                break;
                            case "L":
                            case "X":
                                nextstringset(blocktyped + "T", "WA");
                                break;
                            case "LL":
                            case "XX":
                                nextstringset("W", "A");
                                break;
                            case "LT":
                            case "XT":
                                blockcounts = 1;
                                nextstringset("US", "");
                                break;
                            case "LLW":
                            case "XXW":
                                nextstringset("A", "");
                                break;
                            default:
                                return true;
                        }

                    else if (str[1] == 'っ')
                        switch (blocktyped)
                        {
                            case "":
                                if (romajisetting[22] == 0)
                                    nextstringset("LX", "LTU");
                                else
                                    nextstringset("XL", "XTU");
                                break;
                            case "L":
                            case "X":
                                nextstringset(blocktyped + "T", "TU");
                                break;
                            case "LL":
                            case "XX":
                                nextstringset("T", "U");
                                break;
                            case "LT":
                            case "XT":
                                blockcounts = 1;
                                nextstringset("US", "");
                                break;
                            case "LLT":
                            case "XXT":
                                nextstringset("US", "");
                                break;
                            case "LLTS":
                            case "XXTS":
                                nextstringset("U", "");
                                break;
                            default:
                                return true;
                        }

                    else if (blocktyped == "")
                    {
                        int onecharacterindexoft = sp.IndexOf(str[1]);

                        if (onecharacterindexoft != -1)
                        {
                            nextstringset(sps[onecharacterindexoft] + "LX", sps[onecharacterindexoft] + "" + spb[onecharacterindexoft]);
                        }
                        else
                        {
                            string sp2 = "かこじせふ";
                            string sp2s = "KKJSH";
                            string sp2s2 = "CCZCF";
                            string sp2b = "AOIEU";
                            int[] sp2i = { 5, 7, 12, 17, 23 };

                            int twocharacterindexoft = sp2.IndexOf(str[1]);

                            switch (romajisetting[sp2i[twocharacterindexoft]])
                            {
                                case 0:
                                    nextstringset(sp2s[twocharacterindexoft] + "" + sp2s2[twocharacterindexoft] + "LX",
                                        sp2s[twocharacterindexoft] + "" + sp2b[twocharacterindexoft]);
                                    break;
                                case 1:
                                    nextstringset(sp2s2[twocharacterindexoft] + "" + sp2s[twocharacterindexoft] + "LX",
                                        sp2s2[twocharacterindexoft] + "" + sp2b[twocharacterindexoft]);
                                    break;
                            }
                        }
                    }

                    else if (blocktyped == "L" || blocktyped == "X")
                    {
                        blockcounts = 1;
                        nextstringset("T", "U");
                    }

                    else if (blocktyped.Length == 1)
                        nextstringset(blocktyped, (spb + "AOIEU")[(sp + "かこじせふ").IndexOf(str[1])] + "");

                    else if (blocktyped.Length == 2)
                        nextstringset((spb + "AOIEU")[(sp + "かこじせふ").IndexOf(str[1])] + "", "");

                    else if (blocktyped.Length == 3)
                        return true;
                }

                else if (str.Length == 3)
                {
                    if (blocktyped == "L" || blocktyped == "X")
                    {
                        blockcounts = 1;
                        nextstringset("T", "U");
                    }
                    else
                    {
                        if (str == "っいぇ")
                            switch (blocktyped)
                            {
                                case "":
                                    nextstringset("YLX", "YE");
                                    break;
                                case "Y":
                                    nextstringset("Y", "E");
                                    break;
                                case "YY":
                                    nextstringset("EI", "");
                                    break;
                                case "YYI":
                                    cursor += 2;
                                    blockcounts = 1;
                                    blocktyped = "";
                                    switch (romajisetting[3])
                                    {
                                        case 0:
                                            nextstringset("LX", "E");
                                            break;
                                        case 1:
                                            nextstringset("XL", "E");
                                            break;
                                    }
                                    break;
                                default:
                                    return true;
                            }

                        else if (str == "っうぃ" || str == "っうぇ") // wwi wwhi wwu- wwhu-
                        {
                            int n = "ぃぇ".IndexOf(str[2]);
                            string s = "IE";

                            switch (blocktyped)
                            {
                                case "":
                                    nextstringset("WLX", "W" + s[n]);
                                    break;
                                case "W":
                                    nextstringset("W", s[n] + "");
                                    break;
                                case "WW":
                                    nextstringset(s[n] + "HU", "");
                                    break;
                                case "WWH":
                                    nextstringset(s[n] + "U", "");
                                    break;
                                case "WWU":
                                case "WWHU":
                                    cursor += 2;
                                    blockcounts = 1;
                                    blocktyped = "";
                                    switch (romajisetting[1 + 2 * n])
                                    {
                                        case 0:
                                            nextstringset("LX", s[n] + "");
                                            break;
                                        case 1:
                                            nextstringset("XL", s[n] + "");
                                            break;
                                    }
                                    break;
                                default:
                                    return true;
                            }
                        }

                        else if (str == "っうぁ" || str == "っうぉ") // wwha wwu- wwhu-
                        {
                            int n = "ぁぉ".IndexOf(str[2]);
                            string s = "AO";

                            switch (blocktyped)
                            {
                                case "":
                                    nextstringset("WLX", "WH" + s[n]);
                                    break;
                                case "W":
                                    nextstringset("W", "H" + s[n]);
                                    break;
                                case "WW":
                                    nextstringset("HU", s[n] + "");
                                    break;
                                case "WWH":
                                    nextstringset(s[n] + "U", "");
                                    break;
                                case "WWU":
                                case "WWHU":
                                    cursor += 2;
                                    blockcounts = 1;
                                    blocktyped = "";
                                    switch (romajisetting[4 * n])
                                    {
                                        case 0:
                                            nextstringset("LX", s[n] + "");
                                            break;
                                        case 1:
                                            nextstringset("XL", s[n] + "");
                                            break;
                                    }
                                    break;
                                default:
                                    return true;
                            }
                        }

                        else if (str == "っしゃ" || str == "っしゅ" || str == "っしょ") // No.9 No.10 No.11
                        {
                            int n = "ゃゅょ".IndexOf(str[2]);
                            string s = "AUO";

                            switch (blocktyped)
                            {
                                case "":
                                    switch (romajisetting[9 + n])
                                    {
                                        case 0:
                                            nextstringset("SLX", "SH" + s[n]);
                                            break;
                                        case 1:
                                            nextstringset("SLX", "SY" + s[n]);
                                            break;
                                    }
                                    break;
                                case "S":
                                    switch (romajisetting[9 + n])
                                    {
                                        case 0:
                                            nextstringset("S", "H" + s[n]);
                                            break;
                                        case 1:
                                            nextstringset("S", "Y" + s[n]);
                                            break;
                                    }
                                    break;
                                case "SS":
                                    switch (romajisetting[9 + n])
                                    {
                                        case 0:
                                            nextstringset("HYI", s[n] + "");
                                            break;
                                        case 1:
                                            nextstringset("YHI", s[n] + "");
                                            break;
                                    }
                                    break;
                                case "SSH":
                                    nextstringset(s[n] + "I", "");
                                    break;
                                case "SSY":
                                    nextstringset(s[n] + "", "");
                                    break;
                                case "SSI":
                                case "SSHI":
                                    cursor += 2;
                                    blockcounts = 1;
                                    blocktyped = "";
                                    switch (romajisetting[24 + n])
                                    {
                                        case 0:
                                            nextstringset("LX", "Y" + s[n]);
                                            break;
                                        case 1:
                                            nextstringset("XL", "Y" + s[n]);
                                            break;
                                    }
                                    break;
                                default:
                                    return true;
                            }
                        }

                        else if (str == "っしぃ") // ssyi ssi- sshi- cci-
                            switch (blocktyped)
                            {
                                case "":
                                    nextstringset("SCLX", "SYI");
                                    break;
                                case "S":
                                    nextstringset("S", "YI");
                                    break;
                                case "C":
                                    blockcounts = 2;
                                    nextstringset("C", "I");
                                    break;
                                case "SS":
                                    nextstringset("YIH", "I");
                                    break;
                                case "SSY":
                                    nextstringset("I", "");
                                    break;
                                case "SSI":
                                    cursor += 2;
                                    blockcounts = 1;
                                    blocktyped = "";
                                    switch (romajisetting[1])
                                    {
                                        case 0:
                                            nextstringset("LX", "I");
                                            break;
                                        case 1:
                                            nextstringset("XL", "I");
                                            break;
                                    }
                                    break;
                                case "SSH":
                                    blockcounts = 2;
                                    nextstringset("I", "");
                                    break;
                                default:
                                    return true;
                            }

                        else if (str == "っしぇ") // No.27
                        {
                            switch (blocktyped)
                            {
                                case "":
                                    switch (romajisetting[27])
                                    {
                                        case 0:
                                            nextstringset("SCLX", "SHE");
                                            break;
                                        case 1:
                                            nextstringset("SCLX", "SYE");
                                            break;
                                    }
                                    break;
                                case "S":
                                    switch (romajisetting[27])
                                    {
                                        case 0:
                                            nextstringset("S", "HE");
                                            break;
                                        case 1:
                                            nextstringset("S", "YE");
                                            break;
                                    }
                                    break;
                                case "C":
                                    blockcounts = 2;
                                    nextstringset("C", "I");
                                    break;
                                case "SS":
                                    switch (romajisetting[27])
                                    {
                                        case 0:
                                            nextstringset("HYI", "E");
                                            break;
                                        case 1:
                                            nextstringset("YHI", "E");
                                            break;
                                    }
                                    break;
                                case "SSH":
                                    nextstringset("EI", "");
                                    break;
                                case "SSY":
                                    nextstringset("E", "");
                                    break;
                                case "SSI":
                                case "SSHI":
                                    cursor += 2;
                                    blockcounts = 1;
                                    blocktyped = "";
                                    switch (romajisetting[3])
                                    {
                                        case 0:
                                            nextstringset("LX", "E");
                                            break;
                                        case 1:
                                            nextstringset("XL", "E");
                                            break;
                                    }
                                    break;
                                default:
                                    return true;
                            }
                        }

                        else if ((str[1] == 'ぐ' || str[1] == 'す' || str[1] == 'と' || str[1] == 'ど') &&
                            (str[2] == 'ぁ' || str[2] == 'ぃ' || str[2] == 'ぅ' || str[2] == 'ぇ' || str[2] == 'ぉ')) // ggwa ggu-
                        {
                            int n0 = "ぐすとど".IndexOf(str[1]);
                            string s0 = "GSTD";
                            string s1 = "UUOO";

                            int n2 = "ぁぃぅぇぉ".IndexOf(str[2]);
                            string s2 = "AIUEO";

                            switch (blocktyped.Length)
                            {
                                case 0:
                                    nextstringset(s0[n0] + "LX", s0[n0] + "W" + s2[n2]);
                                    break;
                                case 1:
                                    nextstringset(s0[n0] + "", "W" + s2[n2]);
                                    break;
                                case 2:
                                    nextstringset("W" + s1[n0], s2[n2] + "");
                                    break;
                                case 3:
                                    if (blocktyped[2] == 'W')
                                        nextstringset(s2[n2] + "", "");
                                    else
                                    {
                                        cursor += 2;
                                        blockcounts = 1;
                                        blocktyped = "";
                                        switch (romajisetting[n2])
                                        {
                                            case 0:
                                                nextstringset("LX", s2[n2] + "");
                                                break;
                                            case 1:
                                                nextstringset("XL", s2[n2] + "");
                                                break;
                                        }
                                        break;
                                    }
                                    break;
                                default:
                                    return true;
                            }
                        }

                        else if ((str[1] == 'て' || str[1] == 'で') && (str[2] == 'ゃ' || str[2] == 'ゅ' || str[2] == 'ょ')) // ttha tte-
                        {
                            int n0 = "てで".IndexOf(str[1]);
                            string s0 = "TD";

                            int n1 = "ゃゅょ".IndexOf(str[2]);
                            string s1 = "AUO";

                            switch (blocktyped.Length)
                            {
                                case 0:
                                    nextstringset(s0[n0] + "LX", s0[n0] + "H" + s1[n1]);
                                    break;
                                case 1:
                                    nextstringset(s0[n0] + "", "H" + s1[n1]);
                                    break;
                                case 2:
                                    nextstringset("HE", s1[n1] + "");
                                    break;
                                case 3:
                                    if (blocktyped[2] == 'H')
                                        nextstringset(s1[n1] + "", "");
                                    else
                                    {
                                        cursor += 2;
                                        blockcounts = 1;
                                        blocktyped = "";
                                        switch (romajisetting[24 + n1])
                                        {
                                            case 0:
                                                nextstringset("LX", "Y" + s1[n1]);
                                                break;
                                            case 1:
                                                nextstringset("XL", "Y" + s1[n1]);
                                                break;
                                        }
                                        break;
                                    }
                                    break;
                                default:
                                    return true;
                            }
                        }

                        else if ((str[1] == 'て' || str[1] == 'で') && (str[2] == 'ぃ' || str[2] == 'ぇ')) // tthi tte-
                        {
                            int n0 = "てで".IndexOf(str[1]);
                            string s0 = "TD";

                            int n1 = "ぃぇ".IndexOf(str[2]);
                            string s1 = "IE";

                            switch (blocktyped.Length)
                            {
                                case 0:
                                    nextstringset(s0[n0] + "LX", s0[n0] + "H" + s1[n1]);
                                    break;
                                case 1:
                                    nextstringset(s0[n0] + "", "H" + s1[n1]);
                                    break;
                                case 2:
                                    nextstringset("HE", s1[n1] + "");
                                    break;
                                case 3:
                                    if (blocktyped[2] == 'H')
                                        nextstringset(s1[n1] + "", "");
                                    else
                                    {
                                        cursor += 2;
                                        blockcounts = 1;
                                        blocktyped = "";
                                        switch (romajisetting[1 + 2 * n1])
                                        {
                                            case 0:
                                                nextstringset("LX", s1[n1] + "");
                                                break;
                                            case 1:
                                                nextstringset("XL", s1[n1] + "");
                                                break;
                                        }
                                        break;
                                    }
                                    break;
                                default:
                                    return true;
                            }
                        }

                        else if (str == "っくゃ" || str == "っくゅ" || str == "っくょ") // qqya kku- qqu- ccu-
                        {
                            int n = "ゃゅょ".IndexOf(str[2]);
                            string s = "AUO";

                            switch (blocktyped)
                            {
                                case "":
                                    nextstringset("QKCLX", "QY" + s[n]);
                                    break;
                                case "Q":
                                    nextstringset("Q", "Y" + s[n]);
                                    break;
                                case "K":
                                case "C":
                                    blockcounts = 2;
                                    nextstringset(blocktyped, "U");
                                    break;
                                case "QQ":
                                    nextstringset("YU", s[n] + "");
                                    break;
                                case "QQY":
                                    nextstringset(s[n] + "", "");
                                    break;
                                case "QQU":
                                    cursor += 2;
                                    blockcounts = 1;
                                    blocktyped = "";
                                    switch (romajisetting[24 + n])
                                    {
                                        case 0:
                                            nextstringset("LX", "Y" + s[n]);
                                            break;
                                        case 1:
                                            nextstringset("XL", "Y" + s[n]);
                                            break;
                                    }
                                    break;
                                default:
                                    return true;
                            }
                        }

                        else if (str == "っくぃ" || str == "っくぇ") // qqi qqwi qqyi kku- qqu- ccu-
                        {
                            int n = "ぃぇ".IndexOf(str[2]);
                            string s = "IE";

                            switch (blocktyped)
                            {
                                case "":
                                    nextstringset("QKCLX", "Q" + s[n]);
                                    break;
                                case "Q":
                                    nextstringset("Q", s[n] + "");
                                    break;
                                case "C":
                                case "K":
                                    blockcounts = 2;
                                    nextstringset(blocktyped, "U");
                                    break;
                                case "QQ":
                                    nextstringset(s[n] + "WYU", "");
                                    break;
                                case "QQW":
                                case "QQY":
                                    nextstringset(s[n] + "", "");
                                    break;
                                case "QQU":
                                    cursor += 2;
                                    blockcounts = 1;
                                    blocktyped = "";
                                    switch (romajisetting[1 + 2 * n])
                                    {
                                        case 0:
                                            nextstringset("LX", s[n] + "");
                                            break;
                                        case 1:
                                            nextstringset("XL", s[n] + "");
                                            break;
                                    }
                                    break;
                                default:
                                    return true;
                            }
                        }

                        else if (str == "っくぁ")
                            switch (blocktyped)
                            {
                                case "":
                                    switch (romajisetting[28])
                                    {
                                        case 0:
                                            nextstringset("QKCLX", "QA");
                                            break;
                                        case 1:
                                            nextstringset("KQCLX", "KWA");
                                            break;
                                    }
                                    break;
                                case "Q":
                                    nextstringset("Q", "A");
                                    break;
                                case "K":
                                    nextstringset("K", "WA");
                                    break;
                                case "C":
                                    blockcounts = 2;
                                    nextstringset("C", "U");
                                    break;
                                case "QQ":
                                    nextstringset("AWU", "");
                                    break;
                                case "KK":
                                    nextstringset("WU", "A");
                                    break;
                                case "QQW":
                                case "KKW":
                                    nextstringset("A", "");
                                    break;
                                case "KKU":
                                case "QQU":
                                    cursor += 2;
                                    blockcounts = 1;
                                    blocktyped = "";
                                    switch (romajisetting[0])
                                    {
                                        case 0:
                                            nextstringset("LX", "A");
                                            break;
                                        case 1:
                                            nextstringset("XL", "A");
                                            break;
                                    }
                                    break;
                                default:
                                    return true;
                            }

                        else if (str == "っくぉ") // qo qwo ku- qu- cu-
                            switch (blocktyped)
                            {
                                case "":
                                    nextstringset("QKCLX", "QO");
                                    break;
                                case "Q":
                                    nextstringset("Q", "O");
                                    break;
                                case "K":
                                case "C":
                                    blockcounts = 2;
                                    nextstringset(blocktyped, "U");
                                    break;
                                case "QQ":
                                    nextstringset("OWU", "");
                                    break;
                                case "QQW":
                                    nextstringset("O", "");
                                    break;
                                case "QQU":
                                    cursor += 2;
                                    blockcounts = 1;
                                    blocktyped = "";
                                    switch (romajisetting[4])
                                    {
                                        case 0:
                                            nextstringset("LX", "O");
                                            break;
                                        case 1:
                                            nextstringset("XL", "O");
                                            break;
                                    }
                                    break;
                                default:
                                    return true;
                            }

                        else if (str == "っくぅ") // qwu ku- qu- cu-
                            switch (blocktyped)
                            {
                                case "":
                                    nextstringset("QKCLX", "QWU");
                                    break;
                                case "Q":
                                    nextstringset("Q", "WU");
                                    break;
                                case "K":
                                case "C":
                                    blockcounts = 2;
                                    nextstringset(blocktyped, "U");
                                    break;
                                case "QQ":
                                    nextstringset("WU", "U");
                                    break;
                                case "QQW":
                                    nextstringset("U", "");
                                    break;
                                case "QQU":
                                    cursor += 2;
                                    blockcounts = 1;
                                    blocktyped = "";
                                    switch (romajisetting[2])
                                    {
                                        case 0:
                                            nextstringset("LX", "U");
                                            break;
                                        case 1:
                                            nextstringset("XL", "U");
                                            break;
                                    }
                                    break;
                                default:
                                    return true;
                            }

                        else if ((str[1] == 'き' || str[1] == 'ぎ' || str[1] == 'ぢ'
                            || str[1] == 'ひ' || str[1] == 'び' || str[1] == 'ぴ' || str[1] == 'み' || str[1] == 'り') &&
                            (str[2] == 'ゃ' || str[2] == 'ゅ' || str[2] == 'ょ')) // kya ki-
                        {
                            int n0 = "きぎぢひびぴみり".IndexOf(str[1]);
                            string s0 = "KGDHBPMR";

                            int n1 = "ゃゅょ".IndexOf(str[2]);
                            string s1 = "AUO";

                            switch (blocktyped.Length)
                            {
                                case 0:
                                    nextstringset(s0[n0] + "LX", s0[n0] + "Y" + s1[n1]);
                                    break;
                                case 1:
                                    nextstringset(s0[n0] + "", "Y" + s1[n1]);
                                    break;
                                case 2:
                                    nextstringset("YI", s1[n1] + "");
                                    break;
                                case 3:
                                    if (blocktyped[2] == 'Y')
                                        nextstringset(s1[n1] + "", "");
                                    else
                                    {
                                        cursor += 2;
                                        blockcounts = 1;
                                        blocktyped = "";
                                        switch (romajisetting[24 + n1])
                                        {
                                            case 0:
                                                nextstringset("LX", "Y" + s1[n1]);
                                                break;
                                            case 1:
                                                nextstringset("XL", "Y" + s1[n1]);
                                                break;
                                        }
                                        break;
                                    }
                                    break;
                                default:
                                    return true;
                            }
                        }

                        else if ((str[1] == 'き' || str[1] == 'ぎ' || str[1] == 'ぢ'
                            || str[1] == 'ひ' || str[1] == 'び' || str[1] == 'ぴ' || str[1] == 'み' || str[1] == 'り') &&
                            (str[2] == 'ぃ' || str[2] == 'ぇ')) // kyi ki-
                        {
                            int n0 = "きぎぢひびぴみり".IndexOf(str[1]);
                            string s0 = "KGDHBPMR";

                            int n1 = "ぃぇ".IndexOf(str[2]);
                            string s1 = "IE";

                            switch (blocktyped.Length)
                            {
                                case 0:
                                    nextstringset(s0[n0] + "LX", s0[n0] + "Y" + s1[n1]);
                                    break;
                                case 1:
                                    nextstringset(s0[n0] + "", "Y" + s1[n1]);
                                    break;
                                case 2:
                                    nextstringset("YI", s1[n1] + "");
                                    break;
                                case 3:
                                    if (blocktyped[2] == 'Y')
                                        nextstringset(s1[n1] + "", "");
                                    else
                                    {
                                        cursor += 2;
                                        blockcounts = 1;
                                        blocktyped = "";
                                        switch (romajisetting[1 + 2 * n1])
                                        {
                                            case 0:
                                                nextstringset("LX", s1[n1] + "");
                                                break;
                                            case 1:
                                                nextstringset("XL", s1[n1] + "");
                                                break;
                                        }
                                        break;
                                    }
                                    break;
                                default:
                                    return true;
                            }
                        }

                        else if (str == "っじゃ" || str == "っじゅ" || str == "っじょ") // No.14, 15, 16
                        {
                            int n = "ゃゅょ".IndexOf(str[2]);
                            string s = "AUO";

                            switch (blocktyped)
                            {
                                case "":
                                    switch (romajisetting[14 + n])
                                    {
                                        case 0:
                                            nextstringset("JZLX", "J" + s[n] + "");
                                            break;
                                        case 1:
                                            nextstringset("ZJLX", "ZY" + s[n]);
                                            break;
                                    }
                                    break;
                                case "J":
                                    nextstringset("J", s[n] + "");
                                    break;
                                case "Z":
                                    nextstringset("Z", "Y" + s[n]);
                                    break;
                                case "JJ":
                                    nextstringset(s[n] + "YI", "");
                                    break;
                                case "ZZ":
                                    nextstringset("YI", s[n] + "");
                                    break;
                                case "JJY":
                                case "ZZY":
                                    nextstringset(s[n] + "", "");
                                    break;
                                case "JJI":
                                case "ZZI":
                                    cursor += 2;
                                    blockcounts = 1;
                                    blocktyped = "";
                                    switch (romajisetting[24 + n])
                                    {
                                        case 0:
                                            nextstringset("LX", "Y" + s[n]);
                                            break;
                                        case 1:
                                            nextstringset("XL", "Y" + s[n]);
                                            break;
                                    }
                                    break;
                                default:
                                    return true;
                            }
                        }

                        else if (str == "っじぃ") // No.29 jyi zyi ji- zi-
                            switch (blocktyped)
                            {
                                case "":
                                    switch (romajisetting[29])
                                    {
                                        case 0:
                                            nextstringset("JZLX", "JYI");
                                            break;
                                        case 1:
                                            nextstringset("ZJLX", "ZYI");
                                            break;
                                    }
                                    break;
                                case "J":
                                case "Z":
                                    nextstringset(blocktyped, "YI");
                                    break;
                                case "JJ":
                                case "ZZ":
                                    nextstringset("YI", "I");
                                    break;
                                case "JJY":
                                case "ZZY":
                                    nextstringset("I", "");
                                    break;
                                case "JJI":
                                case "ZZI":
                                    cursor += 2;
                                    blockcounts = 1;
                                    blocktyped = "";
                                    switch (romajisetting[1])
                                    {
                                        case 0:
                                            nextstringset("LX", "I");
                                            break;
                                        case 1:
                                            nextstringset("XL", "I");
                                            break;
                                    }
                                    break;
                                default:
                                    return true;
                            }

                        else if (str == "っじぇ") // No.13 je jye zye ji- zi-
                            switch (blocktyped)
                            {
                                case "":
                                    switch (romajisetting[13])
                                    {
                                        case 0:
                                            nextstringset("JZLX", "JE");
                                            break;
                                        case 1:
                                            nextstringset("ZJLX", "ZYE");
                                            break;
                                    }
                                    break;
                                case "J":
                                    nextstringset("J", "E");
                                    break;
                                case "Z":
                                    nextstringset("Z", "YE");
                                    break;
                                case "JJ":
                                    nextstringset("EYI", "");
                                    break;
                                case "ZZ":
                                    nextstringset("YI", "E");
                                    break;
                                case "JJY":
                                case "ZZY":
                                    nextstringset("E", "");
                                    break;
                                case "JJI":
                                case "ZZI":
                                    cursor += 2;
                                    blockcounts = 1;
                                    blocktyped = "";
                                    switch (romajisetting[1])
                                    {
                                        case 0:
                                            nextstringset("LX", "E");
                                            break;
                                        case 1:
                                            nextstringset("XL", "E");
                                            break;
                                    }
                                    break;
                                default:
                                    return true;
                            }

                        else if (str == "っつぁ" || str == "っつぃ" || str == "っつぇ" || str == "っつぉ") // tsa tu- tsu-
                        {
                            int n = "ぁぃぅぇぉ".IndexOf(str[2]);
                            string s = "AIUEO";

                            switch (blocktyped)
                            {
                                case "":
                                    nextstringset("TLX", "TS" + s[n]);
                                    break;
                                case "T":
                                    nextstringset("T", "S" + s[n]);
                                    break;
                                case "TT":
                                    nextstringset("SU", s[n] + "");
                                    break;
                                case "TTS":
                                    nextstringset(s[n] + "U", "");
                                    break;
                                case "TTU":
                                case "TTSU":
                                    cursor += 2;
                                    blockcounts = 1;
                                    blocktyped = "";
                                    switch (romajisetting[n])
                                    {
                                        case 0:
                                            nextstringset("LX", s[n] + "");
                                            break;
                                        case 1:
                                            nextstringset("XL", s[n] + "");
                                            break;
                                    }
                                    break;
                                default:
                                    return true;
                            }
                        }

                        else if (str == "っちゃ" || str == "っちゅ" || str == "っちょ") // No.19 No.20 No.21
                        {
                            int n = "ゃゅょ".IndexOf(str[2]);
                            string s = "AUO";

                            switch (blocktyped)
                            {
                                case "":
                                    switch (romajisetting[19 + n])
                                    {
                                        case 0:
                                            nextstringset("CTLX", "CH" + s[n]);
                                            break;
                                        case 1:
                                            nextstringset("CTLX", "CY" + s[n]);
                                            break;
                                        case 2:
                                            nextstringset("TCLX", "TY" + s[n]);
                                            break;
                                    }
                                    break;
                                case "C":
                                    switch (romajisetting[19 + n])
                                    {
                                        case 0:
                                            nextstringset("C", "H" + s[n]);
                                            break;
                                        default:
                                            nextstringset("C", "Y" + s[n]);
                                            break;
                                    }
                                    break;
                                case "T":
                                    nextstringset("T", "Y" + s[n]);
                                    break;
                                case "CC":
                                    switch (romajisetting[19 + n])
                                    {
                                        case 0:
                                            nextstringset("HY", s[n] + "");
                                            break;
                                        default:
                                            nextstringset("YH", s[n] + "");
                                            break;
                                    }
                                    break;
                                case "TT":
                                    nextstringset("YI", s[n] + "");
                                    break;
                                case "CCH":
                                    nextstringset(s[n] + "I", "");
                                    break;
                                case "CCY":
                                case "TTY":
                                    nextstringset(s[n] + "", "");
                                    break;
                                case "TTI":
                                case "CCHI":
                                    cursor += 2;
                                    blockcounts = 1;
                                    blocktyped = "";
                                    switch (romajisetting[24 + n])
                                    {
                                        case 0:
                                            nextstringset("LX", "Y" + s[n]);
                                            break;
                                        case 1:
                                            nextstringset("XL", "Y" + s[n]);
                                            break;
                                    }
                                    break;
                                default:
                                    return true;
                            }
                        }

                        else if (str == "っちぃ") // No.30 cyi tyi ti- chi-
                            switch (blocktyped)
                            {
                                case "":
                                    switch (romajisetting[30])
                                    {
                                        case 0:
                                            nextstringset("CTLX", "CYI");
                                            break;
                                        case 1:
                                            nextstringset("TCLX", "TYI");
                                            break;
                                    }
                                    break;
                                case "C":
                                    nextstringset("C", "YI");
                                    break;
                                case "T":
                                    nextstringset("T", "YI");
                                    break;
                                case "CC":
                                    nextstringset("YH", "I");
                                    break;
                                case "TT":
                                    nextstringset("YI", "I");
                                    break;
                                case "CCY":
                                case "TTY":
                                    nextstringset("I", "");
                                    break;
                                case "CCH":
                                    blockcounts = 2;
                                    nextstringset("I", "");
                                    break;
                                case "TTI":
                                    cursor += 2;
                                    blockcounts = 1;
                                    blocktyped = "";
                                    switch (romajisetting[1])
                                    {
                                        case 0:
                                            nextstringset("LX", "I");
                                            break;
                                        case 1:
                                            nextstringset("XL", "I");
                                            break;
                                    }
                                    break;
                                default:
                                    return true;
                            }

                        else if (str == "っちぇ") // No.31 che cye tye ti- chi-
                            switch (blocktyped)
                            {
                                case "":
                                    switch (romajisetting[31])
                                    {
                                        case 0:
                                            nextstringset("CTLX", "CHE");
                                            break;
                                        case 1:
                                            nextstringset("CTLX", "CYE");
                                            break;
                                        case 2:
                                            nextstringset("TCLX", "TYE");
                                            break;
                                    }
                                    break;
                                case "C":
                                    switch (romajisetting[31])
                                    {
                                        case 0:
                                            nextstringset("C", "HE");
                                            break;
                                        default:
                                            nextstringset("C", "YE");
                                            break;
                                    }
                                    break;
                                case "T":
                                    nextstringset("T", "YE");
                                    break;
                                case "CC":
                                    switch (romajisetting[31])
                                    {
                                        case 0:
                                            nextstringset("HY", "E");
                                            break;
                                        case 1:
                                            nextstringset("YH", "E");
                                            break;
                                    }
                                    break;
                                case "TT":
                                    nextstringset("YI", "E");
                                    break;
                                case "CCH":
                                    nextstringset("EI", "");
                                    break;
                                case "CCY":
                                case "TTY":
                                    nextstringset("E", "");
                                    break;
                                case "TTI":
                                case "CCHI":
                                    cursor += 2;
                                    blockcounts = 1;
                                    blocktyped = "";
                                    switch (romajisetting[3])
                                    {
                                        case 0:
                                            nextstringset("LX", "E");
                                            break;
                                        case 1:
                                            nextstringset("XL", "E");
                                            break;
                                    }
                                    break;
                                default:
                                    return true;
                            }

                        else if (str == "っふゃ" || str == "っふゅ" || str == "っふょ") // fya hu- fu-
                        {
                            int n = "ゃゅょ".IndexOf(str[2]);
                            string s = "AUO";

                            switch (blocktyped)
                            {
                                case "":
                                    nextstringset("FHLX", "FY" + s[n]);
                                    break;
                                case "F":
                                    nextstringset("F", "Y" + s[n]);
                                    break;
                                case "H":
                                    blockcounts = 2;
                                    nextstringset("H", "U");
                                    break;
                                case "FF":
                                    nextstringset("YU", s[n] + "");
                                    break;
                                case "FFY":
                                    nextstringset(s[n] + "", "");
                                    break;
                                case "FFU":
                                    cursor += 2;
                                    blockcounts = 1;
                                    blocktyped = "";
                                    switch (romajisetting[24 + n])
                                    {
                                        case 0:
                                            nextstringset("LX", "Y" + s[n]);
                                            break;
                                        case 1:
                                            nextstringset("XL", "Y" + s[n]);
                                            break;
                                    }
                                    break;
                                default:
                                    return true;
                            }
                        }

                        else if (str == "っふぃ" || str == "っふぇ") // fi fwi fyi fu- hu-
                        {
                            int n = "ぃぇ".IndexOf(str[2]);
                            string s = "IE";

                            switch (blocktyped)
                            {
                                case "":
                                    nextstringset("FHLX", "F" + s[n]);
                                    break;
                                case "F":
                                    nextstringset("F", s[n] + "");
                                    break;
                                case "H":
                                    blockcounts = 2;
                                    nextstringset("H", "U");
                                    break;
                                case "FF":
                                    nextstringset(s[n] + "WYU", "");
                                    break;
                                case "FFW":
                                case "FFY":
                                    nextstringset(s[n] + "", "");
                                    break;
                                case "FFU":
                                    cursor += 2;
                                    blockcounts = 1;
                                    blocktyped = "";
                                    switch (romajisetting[1 + 2 * n])
                                    {
                                        case 0:
                                            nextstringset("LX", s[n] + "");
                                            break;
                                        case 1:
                                            nextstringset("XL", s[n] + "");
                                            break;
                                    }
                                    break;
                                default:
                                    return true;
                            }
                        }

                        else if (str == "っふぁ") // fa fwa hu- fu-
                            switch (blocktyped)
                            {
                                case "":
                                    nextstringset("FHLX", "FA");
                                    break;
                                case "F":
                                    nextstringset("F", "A");
                                    break;
                                case "H":
                                    blockcounts = 2;
                                    nextstringset("H", "U");
                                    break;
                                case "FF":
                                    nextstringset("AWU", "");
                                    break;
                                case "FFW":
                                    nextstringset("A", "");
                                    break;
                                case "FFU":
                                    cursor += 2;
                                    blockcounts = 1;
                                    blocktyped = "";
                                    switch (romajisetting[0])
                                    {
                                        case 0:
                                            nextstringset("LX", "A");
                                            break;
                                        case 1:
                                            nextstringset("XL", "A");
                                            break;
                                    }
                                    break;
                                default:
                                    return true;
                            }

                        else if (str == "っふぉ") // fo fwo hu- fu-
                            switch (blocktyped)
                            {
                                case "":
                                    nextstringset("FHLX", "FO");
                                    break;
                                case "F":
                                    nextstringset("F", "O");
                                    break;
                                case "H":
                                    blockcounts = 2;
                                    nextstringset("H", "U");
                                    break;
                                case "FF":
                                    nextstringset("OWU", "");
                                    break;
                                case "FFW":
                                    nextstringset("O", "");
                                    break;
                                case "FFU":
                                    cursor += 2;
                                    blockcounts = 1;
                                    blocktyped = "";
                                    switch (romajisetting[4])
                                    {
                                        case 0:
                                            nextstringset("LX", "O");
                                            break;
                                        case 1:
                                            nextstringset("XL", "O");
                                            break;
                                    }
                                    break;
                                default:
                                    return true;
                            }

                        else if (str == "っふぅ") // fwu fu- hu-
                            switch (blocktyped)
                            {
                                case "":
                                    nextstringset("FHLX", "FWU");
                                    break;
                                case "F":
                                    nextstringset("F", "WU");
                                    break;
                                case "H":
                                    blockcounts = 2;
                                    nextstringset("H", "U");
                                    break;
                                case "FF":
                                    nextstringset("WU", "U");
                                    break;
                                case "FFW":
                                    nextstringset("U", "");
                                    break;
                                case "FFU":
                                    cursor += 2;
                                    blockcounts = 1;
                                    blocktyped = "";
                                    switch (romajisetting[2])
                                    {
                                        case 0:
                                            nextstringset("LX", "U");
                                            break;
                                        case 1:
                                            nextstringset("XL", "U");
                                            break;
                                    }
                                    break;
                                default:
                                    return true;
                            }

                        else if (str == "っヴゃ" || str == "っヴゅ" || str == "っヴょ" || str == "っゔゃ" || str == "っゔゅ" || str == "っゔょ")
                        {
                            int n = "ゃゅょ".IndexOf(str[2]);
                            string s = "AUO";

                            switch (blocktyped)
                            {
                                case "":
                                    nextstringset("VLX", "VY" + s[n]);
                                    break;
                                case "V":
                                    nextstringset("V", "Y" + s[n]);
                                    break;
                                case "VV":
                                    nextstringset("YU", s[n] + "");
                                    break;
                                case "VVY":
                                    nextstringset(s[n] + "", "");
                                    break;
                                case "VVU":
                                    cursor += 2;
                                    blockcounts = 1;
                                    blocktyped = "";
                                    switch (romajisetting[24 + n])
                                    {
                                        case 0:
                                            nextstringset("LX", "Y" + s[n]);
                                            break;
                                        case 1:
                                            nextstringset("XL", "Y" + s[n]);
                                            break;
                                    }
                                    break;
                                default:
                                    return true;
                            }
                        }

                        else if (str == "っヴぃ" || str == "っヴぇ" || str == "っゔぃ" || str == "っゔぇ") // vi vyi vu-
                        {
                            int n = "ぃぇ".IndexOf(str[2]);
                            string s = "IE";

                            switch (blocktyped)
                            {
                                case "":
                                    nextstringset("VLX", "V" + s[n]);
                                    break;
                                case "V":
                                    nextstringset("V", s[n] + "");
                                    break;
                                case "VV":
                                    nextstringset(s[n] + "YU", "");
                                    break;
                                case "VVY":
                                    nextstringset(s[n] + "", "");
                                    break;
                                case "VVU":
                                    cursor += 2;
                                    blockcounts = 1;
                                    blocktyped = "";
                                    switch (romajisetting[1 + 2 * n])
                                    {
                                        case 0:
                                            nextstringset("LX", s[n] + "");
                                            break;
                                        case 1:
                                            nextstringset("XL", s[n] + "");
                                            break;
                                    }
                                    break;
                                default:
                                    return true;
                            }
                        }

                        else if (str == "っヴぁ" || str == "っヴぉ" || str == "っゔぁ" || str == "っゔぉ")
                        {
                            int n = "ぁぉ".IndexOf(str[2]);
                            string s = "AO";

                            switch (blocktyped)
                            {
                                case "":
                                    nextstringset("VLX", "V" + s[n]);
                                    break;
                                case "V":
                                    nextstringset("V", s[n] + "");
                                    break;
                                case "VV":
                                    nextstringset(s[n] + "U", "");
                                    break;
                                case "VVU":
                                    cursor += 2;
                                    blockcounts = 1;
                                    blocktyped = "";
                                    switch (romajisetting[n * 4])
                                    {
                                        case 0:
                                            nextstringset("LX", s[n] + "");
                                            break;
                                        case 1:
                                            nextstringset("XL", s[n] + "");
                                            break;
                                    }
                                    break;
                                default:
                                    return true;
                            }
                        }
                    }
                }
            }

            else // なんか打てなかった
                return true;

            return false;
        }

        /// <summary>
        /// プレイ上で必要な値をすべて初期化
        /// </summary>
        void allreset(bool replaying = false)
        {
            if (!replaying)
            {
                _audioClock = null;
                _audioPlayer = null;
                _audioTimeline = null;
            }

            viewpoints = 0;
            points = 0;
            correct = 0;
            miss = 0;
            complete = 0;
            failed = 0;
            nowcombo = 0;
            maxcombo = 0;

            typedmillisecond = 0;
            firstmillisecond = 0;

            nowline = 0;
            linemode = 0;
            cursor = 0;
            blockcounts = 0;
            nextstring = "";
            nextstringa = "";
            blocktyped = "";
            ndouble = false;

            nothingfailed = 0;

            for (int i = 0; i < lyricsdata[0].Count; i++)
            {
                lyricsdata[0][i].typed = "";
                lyricsdata[0][i].typingtime = 0;
                lyricsdata[0][i].firsttime = 0;
                lyricsdata[0][i].misscursor = new List<int>();
                lyricsdata[0][i].remain = "";
            }

            mainWindow.PointsTextBlock.Text = "0";
            mainWindow.LKpmTextBlock.Text = "0";
            mainWindow.MusicProgressBar.Value = 0;
            mainWindow.IntervalProgressBar.Value = 0;
        }

        /// <summary>
        /// プレイ中の様々な表示を再描画する
        /// コンボの合算もこちらで行う
        /// </summary>
        void repaint()
        {
            if (nowcombo > maxcombo)
                maxcombo = nowcombo;

            mainWindow.HiraganaTextBlock.Text = "";
            mainWindow.KanjiTextBlock.Text = "";
            mainWindow.RomajiTextBlock.Text = "";

            if (nowline != lyricsdata[0].Count - 1)
            {
                switch (linemode)
                {
                    case -1:
                        mainWindow.HiraganaTextBlock.Inlines.Add(new Run()
                        {
                            Text = lyricsdata[0][nowline + 1].yomigana,
                            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.SlateGray)
                        });
                        mainWindow.KanjiTextBlock.Inlines.Add(new Run()
                        {
                            Text = lyricsdata[0][nowline + 1].kanji,
                            Foreground = new System.Windows.Media.SolidColorBrush(lyricsdata[0][nowline + 1].waitingcolor)
                        });
                        break;
                    case 0:
                        mainWindow.HiraganaTextBlock.Inlines.Add(new Run()
                        {
                            Text = lyricsdata[0][nowline + 1].yomigana,
                            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.SlateGray)
                        });
                        mainWindow.KanjiTextBlock.Inlines.Add(new Run()
                        {
                            Text = lyricsdata[0][nowline + 1].kanji,
                            Foreground = new System.Windows.Media.SolidColorBrush(lyricsdata[0][nowline + 1].waitingcolor)
                        });
                        mainWindow.RomajiTextBlock.Inlines.Add(new Run()
                        {
                            Text = lyricsdata[0][nowline].typed,
                            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.SlateGray)
                        });
                        break;
                    case 1:
                        mainWindow.HiraganaTextBlock.Inlines.Add(new Run()
                        {
                            Text = lyricsdata[0][nowline].yomigana.Substring(0, cursor),
                            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.SlateGray)
                        });
                        mainWindow.HiraganaTextBlock.Inlines.Add(new Run()
                        {
                            Text = lyricsdata[0][nowline].yomigana.Substring(cursor, blockcounts),
                            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DeepPink)
                        });
                        mainWindow.HiraganaTextBlock.Inlines.Add(new Run()
                        {
                            Text = lyricsdata[0][nowline].yomigana.Substring(cursor + blockcounts),
                            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black)
                        });

                        mainWindow.KanjiTextBlock.Inlines.Add(new Run()
                        {
                            Text = lyricsdata[0][nowline].kanji,
                            Foreground = new System.Windows.Media.SolidColorBrush(lyricsdata[0][nowline].foregroundcolor)
                        });

                        mainWindow.RomajiTextBlock.Inlines.Add(new Run()
                        {
                            Text = lyricsdata[0][nowline].typed,
                            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.SlateGray)
                        });
                        if (nextstring.Length > 0)
                            mainWindow.RomajiTextBlock.Inlines.Add(new Run()
                            {
                                Text = nextstring[0].ToString(),
                                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DeepPink)
                            });
                        mainWindow.RomajiTextBlock.Inlines.Add(new Run()
                        {
                            Text = nextstringa + hiraganatoromaji(lyricsdata[0][nowline].yomigana.Substring(cursor + blockcounts)),
                            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black)
                        });

                        break;
                }
            }
            else
            {
                switch (linemode)
                {
                    case -1:
                        mainWindow.KanjiTextBlock.Inlines.Add(new Run()
                        {
                            Text = "END",
                            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.SlateGray)
                        });
                        break;
                    case 0:
                        mainWindow.KanjiTextBlock.Inlines.Add(new Run()
                        {
                            Text = "END",
                            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.SlateGray)
                        });
                        mainWindow.RomajiTextBlock.Inlines.Add(new Run()
                        {
                            Text = lyricsdata[0][nowline].typed,
                            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.SlateGray)
                        });
                        break;
                    case 1:
                        mainWindow.HiraganaTextBlock.Inlines.Add(new Run()
                        {
                            Text = lyricsdata[0][nowline].yomigana.Substring(0, cursor),
                            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.SlateGray)
                        });
                        mainWindow.HiraganaTextBlock.Inlines.Add(new Run()
                        {
                            Text = lyricsdata[0][nowline].yomigana.Substring(cursor, blockcounts),
                            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DeepPink)
                        });
                        mainWindow.HiraganaTextBlock.Inlines.Add(new Run()
                        {
                            Text = lyricsdata[0][nowline].yomigana.Substring(cursor + blockcounts),
                            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black)
                        });

                        mainWindow.KanjiTextBlock.Inlines.Add(new Run()
                        {
                            Text = lyricsdata[0][nowline].kanji,
                            Foreground = new System.Windows.Media.SolidColorBrush(lyricsdata[0][nowline].foregroundcolor)
                        });

                        mainWindow.RomajiTextBlock.Inlines.Add(new Run()
                        {
                            Text = lyricsdata[0][nowline].typed,
                            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.SlateGray)
                        });
                        if (nextstring.Length > 0)
                            mainWindow.RomajiTextBlock.Inlines.Add(new Run()
                            {
                                Text = nextstring[0].ToString(),
                                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DeepPink)
                            });
                        mainWindow.RomajiTextBlock.Inlines.Add(new Run()
                        {
                            Text = nextstringa + hiraganatoromaji(lyricsdata[0][nowline].yomigana.Substring(cursor + blockcounts)),
                            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black)
                        });

                        break;
                }
            }

            mainWindow.PointsTextBlock.Text = viewpoints + "";
            mainWindow.CorrectTextBlock.Text = correct + "";
            mainWindow.MissTextBlock.Text = miss + "";
            mainWindow.ComboTextBlock.Text = nowcombo + "";
            mainWindow.MComboTextBlock.Text = maxcombo + "";

            mainWindow.CompleteTextBlock.Text = complete + "";
            mainWindow.FailedTextBlock.Text = failed + "";

            if (typedmillisecond + lyricsdata[0][nowline].typingtime != 0)
            {
                int kpm = 0;

                if (linemode == 1 && lyricsdata[0][nowline].typed.Length != 0)
                    if (kpmswitch)
                        kpm = (correct - complete - failed + nothingfailed - 1) * 60000 / (typedmillisecond + lyricsdata[0][nowline].typingtime + firstmillisecond);
                    else
                        kpm = (correct - complete - failed + nothingfailed - 1) * 60000 / (typedmillisecond + lyricsdata[0][nowline].typingtime);
                else
                    if (kpmswitch)
                        kpm = (correct - complete - failed + nothingfailed) * 60000 / (typedmillisecond + firstmillisecond);
                    else
                        kpm = (correct - complete - failed + nothingfailed) * 60000 / (typedmillisecond);

                mainWindow.AKpmTextBlock.Text = kpm + "";

                mainWindow.RankTextBlock.Text = classcalc(lyricsdata0needtype, lyricsdata0needkpm, kpm, miss, correct);
            }
            else
            {
                mainWindow.AKpmTextBlock.Text = "0";
                mainWindow.RankTextBlock.Text = rank[0];
            }

            if (lyricsdata[0][nowline].typingtime != 0)
            {
                int lkpm = 0;
                if (kpmswitch)
                    lkpm = (lyricsdata[0][nowline].typed.Length - 1) * 60000 / (lyricsdata[0][nowline].typingtime + lyricsdata[0][nowline].firsttime);
                else
                    lkpm = (lyricsdata[0][nowline].typed.Length - 1) * 60000 / lyricsdata[0][nowline].typingtime;
                mainWindow.LKpmTextBlock.Text = lkpm + "";
            }
            else if (lyricsdata[0][nowline].firsttime != 0)
            {
                int lkpm = 60000 / lyricsdata[0][nowline].firsttime;
                mainWindow.LKpmTextBlock.Text = lkpm + "";
            }
        }

        /// <summary>
        /// リザルト表示メソッド。Tabの遷移も任せる。
        /// </summary>
        private void viewresult()
        {
            tabmove(2);
            mainWindow.ExperimentalValueTextBlock.Text = "exp:" + experimentalvalue;

            byte highscoremode = 0; // 0:アップデートできず 1:アップデート 2:新規保存
            HighScoreData rhs = null; // 過去のデータ

            // 最高記録の保存
            if (lyricsdata0highscoredatanum == -1)
            {
                highscoremode = 2;
                lyricsdata0highscoredatanum = highscores.Count();
                HighScoreData hs = new HighScoreData(fmlist[folderid][selectedid].xmlpath, lyricsdata0hashcode);
                hs.memory(points, maxcombo, typedmillisecond, firstmillisecond, correct, miss, complete, failed, nothingfailed);
                highscores.Add(hs);
            }
            else
            {
                // HighScoreDataは参照型なので、あたらしいオブジェクトは頑張って作る
                rhs = new HighScoreData(highscores[lyricsdata0highscoredatanum].xmlpath, highscores[lyricsdata0highscoredatanum].hashcode);
                rhs.memory(highscores[lyricsdata0highscoredatanum].points, highscores[lyricsdata0highscoredatanum].combo,
                    highscores[lyricsdata0highscoredatanum].typedmillisecond, highscores[lyricsdata0highscoredatanum].firstmillisecond,
                    highscores[lyricsdata0highscoredatanum].correct, highscores[lyricsdata0highscoredatanum].miss,
                    highscores[lyricsdata0highscoredatanum].complete, highscores[lyricsdata0highscoredatanum].failed,
                    highscores[lyricsdata0highscoredatanum].nothingfailed);
                if (highscores[lyricsdata0highscoredatanum]
                    .memory(points, maxcombo, typedmillisecond, firstmillisecond, correct, miss, complete, failed, nothingfailed))
                    highscoremode = 1;
            }


            mainWindow.ResultTextBlock.Text = "";

            mainWindow.ResultTextBlock.Inlines.Add(new Run()
            {
                Text = mainWindow.MusicNameTextBlock.Text,
                FontSize = 18,
                FontWeight = FontWeights.Bold
            });

            if (highscoremode != 0)
                mainWindow.ResultTextBlock.Inlines.Add(new Run()
                {
                    Text = "  Update!",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.DeepPink
                });

            mainWindow.ResultTextBlock.Inlines.Add(new LineBreak());
            mainWindow.ResultTextBlock.Inlines.Add(new LineBreak());
            mainWindow.ResultTextBlock.Inlines.Add(new Run()
            {
                Text = "Points  ",
                FontSize = 14
            });
            if (highscoremode != 2)
                mainWindow.ResultTextBlock.Inlines.Add(new Run()
                {
                    Text = rhs.points + " → ",
                    FontSize = 14
                });
            mainWindow.ResultTextBlock.Inlines.Add(new Run()
            {
                Text = points + "",
                FontSize = 18,
                FontWeight = FontWeights.Bold
            });
            mainWindow.ResultTextBlock.Inlines.Add(new LineBreak());

            int kpm = 0;

            if (typedmillisecond + lyricsdata[0][nowline].typingtime != 0)
            {
                if (linemode == 1)
                    if (kpmswitch)
                        kpm = (correct - complete - failed + nothingfailed - 1) * 60000 / (typedmillisecond + lyricsdata[0][nowline].typingtime + firstmillisecond);
                    else
                        kpm = (correct - complete - failed + nothingfailed - 1) * 60000 / (typedmillisecond + lyricsdata[0][nowline].typingtime);
                else
                    if (kpmswitch)
                        kpm = (correct - complete - failed + nothingfailed) * 60000 / (typedmillisecond + firstmillisecond);
                    else
                        kpm = (correct - complete - failed + nothingfailed) * 60000 / (typedmillisecond);
            }
            else
                kpm = 0;

            int highkpm = 0;

            if (highscoremode != 2)
            {
                if (rhs.typedmillisecond != 0)
                {
                    if (kpmswitch)
                        highkpm = (rhs.correct - rhs.complete - rhs.failed + rhs.nothingfailed) * 60000 / (rhs.typedmillisecond + rhs.firstmillisecond);
                    else
                        highkpm = (rhs.correct - rhs.complete - rhs.failed + rhs.nothingfailed) * 60000 / (rhs.typedmillisecond);
                }
                else
                    highkpm = 0;
            }

            mainWindow.ResultTextBlock.Inlines.Add(new Run()
            {
                Text = "Rank  ",
                FontSize = 14
            });
            if (highscoremode != 2)
                mainWindow.ResultTextBlock.Inlines.Add(new Run()
                {
                    Text = classcalc(lyricsdata0needtype, lyricsdata0needkpm, highkpm, rhs.miss, rhs.correct) + " → ",
                    FontSize = 14
                });
            mainWindow.ResultTextBlock.Inlines.Add(new Run()
            {
                Text = classcalc(lyricsdata0needtype, lyricsdata0needkpm, kpm, miss, correct),
                FontSize = 18,
                FontWeight = FontWeights.Bold
            });
            mainWindow.ResultTextBlock.Inlines.Add(new LineBreak());
            mainWindow.ResultTextBlock.Inlines.Add(new LineBreak());
            mainWindow.ResultTextBlock.Inlines.Add(new Run()
            {
                Text = "Correct  ",
                FontSize = 14
            });
            if (highscoremode != 2)
                mainWindow.ResultTextBlock.Inlines.Add(new Run()
                {
                    Text = rhs.correct + " → ",
                    FontSize = 14
                });
            mainWindow.ResultTextBlock.Inlines.Add(new Run()
            {
                Text = correct + "",
                FontSize = 18,
                FontWeight = FontWeights.Bold
            });
            mainWindow.ResultTextBlock.Inlines.Add(new LineBreak());
            mainWindow.ResultTextBlock.Inlines.Add(new Run()
            {
                Text = "Miss  ",
                FontSize = 14
            });
            if (highscoremode != 2)
                mainWindow.ResultTextBlock.Inlines.Add(new Run()
                {
                    Text = rhs.miss + " → ",
                    FontSize = 14
                });
            mainWindow.ResultTextBlock.Inlines.Add(new Run()
            {
                Text = miss + "",
                FontSize = 18,
                FontWeight = FontWeights.Bold
            });
            mainWindow.ResultTextBlock.Inlines.Add(new LineBreak());
            mainWindow.ResultTextBlock.Inlines.Add(new Run()
            {
                Text = "Accuracy  ",
                FontSize = 14
            });

            int accuracy = 0;
            if (correct + miss != 0)
                accuracy = correct * 100 / (correct + miss);
            else
                accuracy = 0;

            int highaccuracy = 0;
            if (highscoremode != 2)
            {
                if (rhs.correct + rhs.miss != 0)
                    highaccuracy = rhs.correct * 100 / (rhs.correct + rhs.miss);
                else
                    highaccuracy = 0;
            }

            if (highscoremode != 2)
                mainWindow.ResultTextBlock.Inlines.Add(new Run()
                {
                    Text = highaccuracy + "% → ",
                    FontSize = 14
                });
            mainWindow.ResultTextBlock.Inlines.Add(new Run()
            {
                Text = accuracy + "%",
                FontSize = 18,
                FontWeight = FontWeights.Bold
            });
            mainWindow.ResultTextBlock.Inlines.Add(new LineBreak());
            mainWindow.ResultTextBlock.Inlines.Add(new LineBreak());
            mainWindow.ResultTextBlock.Inlines.Add(new Run()
            {
                Text = "kpm  ",
                FontSize = 14
            });
            if (highscoremode != 2)
                mainWindow.ResultTextBlock.Inlines.Add(new Run()
                {
                    Text = highkpm + " → ",
                    FontSize = 14
                });
            mainWindow.ResultTextBlock.Inlines.Add(new Run()
            {
                Text = kpm + "",
                FontSize = 18,
                FontWeight = FontWeights.Bold
            });
            mainWindow.ResultTextBlock.Inlines.Add(new LineBreak());
            mainWindow.ResultTextBlock.Inlines.Add(new Run()
            {
                Text = "Combo  ",
                FontSize = 14
            });
            if (highscoremode != 2)
                mainWindow.ResultTextBlock.Inlines.Add(new Run()
                {
                    Text = rhs.combo + " → ",
                    FontSize = 14
                });
            mainWindow.ResultTextBlock.Inlines.Add(new Run()
            {
                Text = maxcombo + "",
                FontSize = 18,
                FontWeight = FontWeights.Bold
            });
            mainWindow.ResultTextBlock.Inlines.Add(new LineBreak());
            mainWindow.ResultTextBlock.Inlines.Add(new LineBreak());
            mainWindow.ResultTextBlock.Inlines.Add(new Run()
            {
                Text = "Complete  ",
                FontSize = 14
            });
            if (highscoremode != 2)
                mainWindow.ResultTextBlock.Inlines.Add(new Run()
                {
                    Text = rhs.complete + " → ",
                    FontSize = 14
                });
            mainWindow.ResultTextBlock.Inlines.Add(new Run()
            {
                Text = complete + "",
                FontSize = 18,
                FontWeight = FontWeights.Bold
            });
            mainWindow.ResultTextBlock.Inlines.Add(new LineBreak());
            mainWindow.ResultTextBlock.Inlines.Add(new Run()
            {
                Text = "Failed  ",
                FontSize = 14
            });
            if (highscoremode != 2)
                mainWindow.ResultTextBlock.Inlines.Add(new Run()
                {
                    Text = rhs.failed + " → ",
                    FontSize = 14
                });
            mainWindow.ResultTextBlock.Inlines.Add(new Run()
            {
                Text = failed + "",
                FontSize = 18,
                FontWeight = FontWeights.Bold
            });

            resulttweetcontent1 = mainWindow.MusicNameTextBlock.Text;
            resulttweetcontent2 = mainWindow.ArtistTextBlock.Text;
            resulttweetcontent3 = ""
                 + "Pnts:" + points + "\n" + "Rank:" + classcalc(lyricsdata0needtype, lyricsdata0needkpm, kpm, miss, correct) + "\n"
                 + "Crct/Miss:" + correct + "/" + miss + "（" + accuracy + "%）" + "\n"
                 + "kpm:" + kpm + "\n" + "Cmb:" + maxcombo + "\n"
                 + "Comp/Fail:" + complete + "/" + failed;

            mainWindow.TweetCommentTextBox.Text = "";
            TweetCommentTextBox_TextChanged(null, null);


            // Detailの処理
            // とりあえず打つ行がどれかを探しておく
            List<int> li = new List<int>();
            for (int i = 0; i < lyricsdata[0].Count(); i++)
                if (!lyricsdata[0][i].isuntypeline())
                    li.Add(i);

            int[] hor = new int[li.Count()]; // 数字を入れるだけ
            double[] ver = new double[li.Count()]; // kpmが入る

            int maxkpm = 0;
            int minkpm = int.MaxValue;

            for (int i = 0; i < li.Count(); i++)
            {
                hor[i] = i + 1;

                switch (lyricsdata[0][li[i]].typed.Length)
                {
                    case 0:
                        ver[i] = 0;
                        break;
                    case 1:
                        ver[i] = 60000 / lyricsdata[0][li[i]].firsttime;
                        break;
                    default:
                        if (kpmswitch)
                            ver[i] = (lyricsdata[0][li[i]].typed.Length - 1) * 60000 / (lyricsdata[0][li[i]].typingtime + lyricsdata[0][li[i]].firsttime);
                        else
                            ver[i] = (lyricsdata[0][li[i]].typed.Length - 1) * 60000 / lyricsdata[0][li[i]].typingtime;
                        break;
                }

                if (ver[i] > maxkpm)
                    maxkpm = (int)ver[i];
                if (ver[i] < minkpm)
                    minkpm = (int)ver[i];
            }

            mainWindow.DetailResultTextBlock.Text = "";

            for (int i = 0; i < li.Count(); i++)
            {
                mainWindow.DetailResultTextBlock.Inlines.Add(new Run()
                {
                    Text = hor[i] + "/" + li.Count() + " : "
                });

                if ((int)ver[i] == maxkpm)
                    mainWindow.DetailResultTextBlock.Inlines.Add(new Run()
                    {
                        Text = (int)ver[i] + "kpm",
                        Foreground = Brushes.DeepPink
                    });
                else if ((int)ver[i] == minkpm)
                    mainWindow.DetailResultTextBlock.Inlines.Add(new Run()
                    {
                        Text = (int)ver[i] + "kpm",
                        Foreground = Brushes.DodgerBlue
                    });
                else
                    mainWindow.DetailResultTextBlock.Inlines.Add(new Run()
                    {
                        Text = (int)ver[i] + "kpm"
                    });

                mainWindow.DetailResultTextBlock.Inlines.Add(new LineBreak());

                mainWindow.DetailResultTextBlock.Inlines.Add(new Run()
                {
                    Text = lyricsdata[0][li[i]].kanji
                });

                mainWindow.DetailResultTextBlock.Inlines.Add(new LineBreak());

                if (lyricsdata[0][li[i]].misscursor.Count() == 0)
                {
                    mainWindow.DetailResultTextBlock.Inlines.Add(new Run()
                    {
                        Text = lyricsdata[0][li[i]].typed
                    });
                    mainWindow.DetailResultTextBlock.Inlines.Add(new Run()
                    {
                        Text = lyricsdata[0][li[i]].remain,
                        Foreground = Brushes.Gray
                    });
                }
                else
                {
                    mainWindow.DetailResultTextBlock.Inlines.Add(new Run()
                    {
                        Text = lyricsdata[0][li[i]].typed.Substring(0, lyricsdata[0][li[i]].misscursor[0])
                    });
                    for (int j = 0; j < lyricsdata[0][li[i]].misscursor.Count() - 1; j++)
                    {
                        mainWindow.DetailResultTextBlock.Inlines.Add(new Run()
                        {
                            Text = lyricsdata[0][li[i]].typed[lyricsdata[0][li[i]].misscursor[j]] + "",
                            Foreground = Brushes.DeepPink
                        });
                        mainWindow.DetailResultTextBlock.Inlines.Add(new Run()
                        {
                            Text = lyricsdata[0][li[i]].typed.Substring(lyricsdata[0][li[i]].misscursor[j] + 1,
                                lyricsdata[0][li[i]].misscursor[j + 1] - lyricsdata[0][li[i]].misscursor[j] - 1)
                        });
                    }
                    if (lyricsdata[0][li[i]].misscursor[lyricsdata[0][li[i]].misscursor.Count() - 1] !=
                        lyricsdata[0][li[i]].typed.Length)
                    {
                        mainWindow.DetailResultTextBlock.Inlines.Add(new Run()
                        {
                            Text = lyricsdata[0][li[i]].typed[lyricsdata[0][li[i]].misscursor[lyricsdata[0][li[i]].misscursor.Count() - 1]] + "",
                            Foreground = Brushes.DeepPink
                        });
                        mainWindow.DetailResultTextBlock.Inlines.Add(new Run()
                        {
                            Text = lyricsdata[0][li[i]].typed.Substring(lyricsdata[0][li[i]].misscursor[lyricsdata[0][li[i]].misscursor.Count() - 1] + 1)
                        });
                        mainWindow.DetailResultTextBlock.Inlines.Add(new Run()
                        {
                            Text = lyricsdata[0][li[i]].remain,
                            Foreground = Brushes.Gray
                        });
                    }
                    else
                    {
                        if (lyricsdata[0][li[i]].remain.Length == 1)
                        {
                            mainWindow.DetailResultTextBlock.Inlines.Add(new Run()
                            {
                                Text = lyricsdata[0][li[i]].remain,
                                Foreground = Brushes.Gray
                            });
                        }
                        else if (lyricsdata[0][li[i]].remain.Length != 0)
                        {
                            mainWindow.DetailResultTextBlock.Inlines.Add(new Run()
                            {
                                Text = lyricsdata[0][li[i]].remain[0] + "",
                                Foreground = Brushes.PaleVioletRed
                            });
                            mainWindow.DetailResultTextBlock.Inlines.Add(new Run()
                            {
                                Text = lyricsdata[0][li[i]].remain.Substring(1),
                                Foreground = Brushes.Gray
                            });
                        }
                    }
                }

                mainWindow.DetailResultTextBlock.Inlines.Add(new LineBreak());
                mainWindow.DetailResultTextBlock.Inlines.Add(new LineBreak());

            }

            // グラフの処理
            // ここから

            var plotter = new ChartPlotter();

            var source = new ObservableDataSource<Point>();
            source.SetXYMapping(point => point);
            plotter.AddLineGraph(source, Colors.DeepSkyBlue, 2.0, "kpm");

            plotter.FocusVisualStyle = null;
            plotter.HorizontalAxis = new Microsoft.Research.DynamicDataDisplay.Charts.Axes.HorizontalIntegerAxis();
            plotter.VerticalAxis = new Microsoft.Research.DynamicDataDisplay.Charts.Axes.VerticalIntegerAxis();

            var hat = new HorizontalAxisTitle();
            hat.Content = "Line";
            var vat = new VerticalAxisTitle();
            vat.Content = "kpm";

            plotter.Children.Add(hat);
            plotter.Children.Add(vat);

            mainWindow.plotterpanel.Children.RemoveAt(0);
            mainWindow.plotterpanel.Children.Add(plotter);

            Thread simulation = new Thread(
                () =>
                {
                    for (int i = 0; i < hor.Length; i++)
                    {
                        source.AppendAsync(this.Dispatcher, new Point((i + 1), ver[i]));
                        Thread.Sleep(80);
                    }
               }
            )
            {
                IsBackground = true
            };
            simulation.Start();
            /*
            EnumerableDataSource<int> horDataSource = null;
            EnumerableDataSource<double> verDataSource = null;


            // どうやらエラーを吐きながら恐ろしいことになる処理らしいので、非同期にする

            var graphtask = System.Threading.Tasks.Task.Factory.StartNew(
                () =>
                {
                    horDataSource = new EnumerableDataSource<int>(hor);
                    horDataSource.SetXMapping(x => x);

                    verDataSource = new EnumerableDataSource<double>(ver);
                    verDataSource.SetYMapping(y => y);
                }
            );

            System.Threading.Tasks.Task.WaitAll(graphtask);

            plotter.FocusVisualStyle = null;
            plotter.HorizontalAxis = new Microsoft.Research.DynamicDataDisplay.Charts.Axes.HorizontalIntegerAxis();
            plotter.VerticalAxis = new Microsoft.Research.DynamicDataDisplay.Charts.Axes.VerticalIntegerAxis();

            var hat = new HorizontalAxisTitle();
            hat.Content = "Line";
            var vat = new VerticalAxisTitle();
            vat.Content = "kpm";

            plotter.Children.Add(hat);
            plotter.Children.Add(vat);

            plotter.AddLineGraph(
                new CompositeDataSource(horDataSource, verDataSource),
                new Pen(Brushes.DeepSkyBlue, 3),
                new CircleElementPointMarker { Size = 8.0, Fill = Brushes.Orange, Brush = Brushes.Orange },
                new PenDescription("kpm"));
            */
            datasave();
            return;

        }

        /// <summary>
        /// プレイ中からのリスタート用
        /// </summary>
        void replay()
        {
            if (_audioPlayer == null)
                return;

            _audioClock.Controller.Stop();
            allreset(true);
            nowline = 0;
            linemode = 0;
            lineupdate(0);
            _audioClock.Controller.Begin();


        }

        /// <summary>
        /// スキップ
        /// </summary>
        void skip()
        {
            if (mainWindow.MainTabPanel.SelectedIndex != 1)
                return;

            if (_audioClock == null || _audioClock.CurrentTime == null)
                return;

            int totalMilliseconds = (int)((TimeSpan)(_audioClock.CurrentTime)).TotalMilliseconds;

            if (nowline == lyricsdata[0].Count() - 1) // 最終行
                viewresult();

            else
            {
                int i = nowline + 1;
                while (lyricsdata[0][i].isuntypeline())
                {
                    ++i;
                    if (i == lyricsdata[0].Count())
                    {
                        viewresult();
                        return;
                    }
                }
                if (lyricsdata[0][i].begintime + musicoffset + settingoffset - skiprest > totalMilliseconds)
                {
                    _audioClock.Controller.SeekAlignedToLastTick(TimeSpan.FromMilliseconds(lyricsdata[0][i].begintime + musicoffset + settingoffset - skiprest),
                        System.Windows.Media.Animation.TimeSeekOrigin.BeginTime);
                }
            }

        }

        #endregion


        #region general method

        public void tabmove(int index)
        {
            switch (index)
            {
                case 0:
                case 3:
                    mainWindow.SelectMusicTab.IsEnabled = true;
                    mainWindow.PlayViewTab.IsEnabled = false;
                    mainWindow.ResultTab.IsEnabled = false;
                    break;
                case 1:
                    mainWindow.SelectMusicTab.IsEnabled = false;
                    mainWindow.PlayViewTab.IsEnabled = true;
                    mainWindow.ResultTab.IsEnabled = false;
                    break;
                case 2:
                    mainWindow.SelectMusicTab.IsEnabled = true;
                    mainWindow.PlayViewTab.IsEnabled = false;
                    mainWindow.ResultTab.IsEnabled = true;
                    break;
            }
            mainWindow.MainTabPanel.SelectedIndex = index;
        }

        /// <summary>
        /// 文字列検索簡素化用のメソッド
        /// </summary>
        /// <param name="str"></param>
        /// <returns>パラメータをすべてひらがなおよび半角小文字英数字に直したもの</returns>
        public string searchstringprovider(string str)
        {

            string after = "あいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわゐゑをん" +
                "ゔがぎぐげござじずぜぞだぢづでどばびぶべぼぱぴぷぺぽぁぃぅぇぉっゃゅょゎ" +
                "abcdefghijklmnopqrstuvwxyzABCDEFGHIJIKLMNOPQRSTUVWXYZ1234567890 !\"#$%&'()-=^~\\|@`[{;+:*]},<.>/?_";
            string before = "アイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヰヱヲン" +
                "ヴガギグゲゴザジズゼゾダヂヅデドバビブベボパピプペポァィゥェォッャュョヮ" +
                "ａｂｃｄｅｆｇｈｉｊｋｌｍｎｏｐｑｒｓｔｕｖｗｘｙｚＡＢＣＤＥＦＧＨＩＪＫＬＭＮＯＰＱＲＳＴＵＶＷＸＹＺ" +
                "１２３４５６７８９０　！”＃＄％＆’（）ー＝＾～￥｜＠｀「｛；＋：＊」｝、＜。＞／？＿";

            for (int i = 0; i < before.Length; i++)
            {
                str = str.Replace(before[i], after[i]);
            }

            str = str.ToLower();

            return str;
        }

        /// <summary>
        /// ひらがなをすべてローマ字に直す。ただの処理なので換算等に使うべし。
        /// </summary>
        /// <param name="hiragana"></param>
        /// <returns></returns>
        public string hiraganatoromaji(string hiragana, bool Short = false)
        {
            hiragana = hiragana.Replace('ゔ', 'ヴ');

            hiragana = hiragana.ToLower();

            hiragana = hiragana.Replace('　', ' ');
            hiragana = hiragana.Replace('〜', '～');
            hiragana = hiragana.Replace('！', '!');
            hiragana = hiragana.Replace('”', '\"');
            hiragana = hiragana.Replace('＃', '#');
            hiragana = hiragana.Replace('＄', '$');
            hiragana = hiragana.Replace('％', '%');
            hiragana = hiragana.Replace('＆', '&');
            hiragana = hiragana.Replace('’', '\'');
            hiragana = hiragana.Replace('（', '(');
            hiragana = hiragana.Replace('）', ')');
            hiragana = hiragana.Replace('－', 'ー');
            hiragana = hiragana.Replace('―', 'ー');
            hiragana = hiragana.Replace('＝', '=');
            hiragana = hiragana.Replace('＾', '^');
            hiragana = hiragana.Replace('￥', '\\');
            hiragana = hiragana.Replace('｜', '|');
            hiragana = hiragana.Replace('＠', '@');
            hiragana = hiragana.Replace('｀', '`');
            hiragana = hiragana.Replace('［', '[');
            hiragana = hiragana.Replace('｛', '{');
            hiragana = hiragana.Replace('；', ';');
            hiragana = hiragana.Replace('＋', '+');
            hiragana = hiragana.Replace('：', ':');
            hiragana = hiragana.Replace('＊', '*');
            hiragana = hiragana.Replace('］', ']');
            hiragana = hiragana.Replace('｝', '}');
            hiragana = hiragana.Replace('＜', '<');
            hiragana = hiragana.Replace('＞', '>');
            hiragana = hiragana.Replace('？', '?');
            hiragana = hiragana.Replace('＿', '_');
            hiragana = hiragana.Replace('､', '、');
            hiragana = hiragana.Replace('｡', '。');
            hiragana = hiragana.Replace('｢', '「');
            hiragana = hiragana.Replace('｣', '」');
            hiragana = hiragana.Replace('ﾞ', '゛');
            hiragana = hiragana.Replace('ﾟ', '゜');


            // 2文字の処理
            hiragana = hiragana.Replace("いぇ", "YE");
            hiragana = hiragana.Replace("うぁ", "WHA");
            hiragana = hiragana.Replace("うぃ", "WI");
            hiragana = hiragana.Replace("うぇ", "WE");
            hiragana = hiragana.Replace("うぉ", "WHO");
            hiragana = hiragana.Replace("きゃ", "KYA");
            hiragana = hiragana.Replace("きぃ", "KYI");
            hiragana = hiragana.Replace("きゅ", "KYU");
            hiragana = hiragana.Replace("きぇ", "KYE");
            hiragana = hiragana.Replace("きょ", "KYO");
            hiragana = hiragana.Replace("くぁ", "QA");
            hiragana = hiragana.Replace("くぃ", "QI");
            hiragana = hiragana.Replace("くぅ", "QWU");
            hiragana = hiragana.Replace("くぇ", "QE");
            hiragana = hiragana.Replace("くぉ", "QO");
            hiragana = hiragana.Replace("くゃ", "QYA");
            hiragana = hiragana.Replace("くゅ", "QYU");
            hiragana = hiragana.Replace("くょ", "QYO");
            switch (romajisetting[9])
            {
                case 0:
                    hiragana = hiragana.Replace("しゃ", "SHA");
                    break;
                case 1:
                    hiragana = hiragana.Replace("しゃ", "SYA");
                    break;
            }
            hiragana = hiragana.Replace("しぃ", "SYI");
            switch (romajisetting[10])
            {
                case 0:
                    hiragana = hiragana.Replace("しゅ", "SHU");
                    break;
                case 1:
                    hiragana = hiragana.Replace("しゅ", "SYU");
                    break;
            }
            switch (romajisetting[27])
            {
                case 0:
                    hiragana = hiragana.Replace("しぇ", "SHE");
                    break;
                case 1:
                    hiragana = hiragana.Replace("しぇ", "SYE");
                    break;
            }
            switch (romajisetting[11])
            {
                case 0:
                    hiragana = hiragana.Replace("しょ", "SHO");
                    break;
                case 1:
                    hiragana = hiragana.Replace("しょ", "SYO");
                    break;
            }
            hiragana = hiragana.Replace("すぁ", "SWA");
            hiragana = hiragana.Replace("すぃ", "SWI");
            hiragana = hiragana.Replace("すぅ", "SWU");
            hiragana = hiragana.Replace("すぇ", "SWE");
            hiragana = hiragana.Replace("すぉ", "SWO");
            switch (romajisetting[19])
            {
                case 0:
                    hiragana = hiragana.Replace("ちゃ", "CHA");
                    break;
                case 1:
                    hiragana = hiragana.Replace("ちゃ", "CYA");
                    break;
                case 2:
                    hiragana = hiragana.Replace("ちゃ", "TYA");
                    break;
            }
            switch (romajisetting[30])
            {
                case 0:
                    hiragana = hiragana.Replace("ちぃ", "CYI");
                    break;
                case 1:
                    hiragana = hiragana.Replace("ちぃ", "TYI");
                    break;
            }
            switch (romajisetting[20])
            {
                case 0:
                    hiragana = hiragana.Replace("ちゅ", "CHU");
                    break;
                case 1:
                    hiragana = hiragana.Replace("ちゅ", "CYU");
                    break;
                case 2:
                    hiragana = hiragana.Replace("ちゅ", "TYU");
                    break;
            }
            switch (romajisetting[31])
            {
                case 0:
                    hiragana = hiragana.Replace("ちぇ", "CHE");
                    break;
                case 1:
                    hiragana = hiragana.Replace("ちぇ", "CYE");
                    break;
                case 2:
                    hiragana = hiragana.Replace("ちぇ", "TYE");
                    break;
            }
            switch (romajisetting[21])
            {
                case 0:
                    hiragana = hiragana.Replace("ちょ", "CHO");
                    break;
                case 1:
                    hiragana = hiragana.Replace("ちょ", "CYO");
                    break;
                case 2:
                    hiragana = hiragana.Replace("ちょ", "TYO");
                    break;
            }
            hiragana = hiragana.Replace("つぁ", "TSA");
            hiragana = hiragana.Replace("つぃ", "TSI");
            hiragana = hiragana.Replace("つぇ", "TSE");
            hiragana = hiragana.Replace("つぉ", "TSO");
            hiragana = hiragana.Replace("てゃ", "THA");
            hiragana = hiragana.Replace("てぃ", "THI");
            hiragana = hiragana.Replace("てゅ", "THU");
            hiragana = hiragana.Replace("てぇ", "THE");
            hiragana = hiragana.Replace("てょ", "THO");
            hiragana = hiragana.Replace("とぁ", "TWA");
            hiragana = hiragana.Replace("とぃ", "TWI");
            hiragana = hiragana.Replace("とぅ", "TWU");
            hiragana = hiragana.Replace("とぇ", "TWE");
            hiragana = hiragana.Replace("とぉ", "TWO");
            hiragana = hiragana.Replace("にゃ", "NYA");
            hiragana = hiragana.Replace("にぃ", "NYI");
            hiragana = hiragana.Replace("にゅ", "NYU");
            hiragana = hiragana.Replace("にぇ", "NYE");
            hiragana = hiragana.Replace("にょ", "NYO");
            hiragana = hiragana.Replace("ひゃ", "HYA");
            hiragana = hiragana.Replace("ひぃ", "HYI");
            hiragana = hiragana.Replace("ひゅ", "HYU");
            hiragana = hiragana.Replace("ひぇ", "HYE");
            hiragana = hiragana.Replace("ひょ", "HYO");
            hiragana = hiragana.Replace("ふぁ", "FA");
            hiragana = hiragana.Replace("ふぃ", "FI");
            hiragana = hiragana.Replace("ふぅ", "FWU");
            hiragana = hiragana.Replace("ふぇ", "FE");
            hiragana = hiragana.Replace("ふぉ", "FO");
            hiragana = hiragana.Replace("ふゃ", "FYA");
            hiragana = hiragana.Replace("ふゅ", "FYU");
            hiragana = hiragana.Replace("ふょ", "FYO");
            hiragana = hiragana.Replace("みゃ", "MYA");
            hiragana = hiragana.Replace("みぃ", "MYI");
            hiragana = hiragana.Replace("みゅ", "MYU");
            hiragana = hiragana.Replace("みぇ", "MYE");
            hiragana = hiragana.Replace("みょ", "MYO");
            hiragana = hiragana.Replace("りゃ", "RYA");
            hiragana = hiragana.Replace("りぃ", "RYI");
            hiragana = hiragana.Replace("りゅ", "RYU");
            hiragana = hiragana.Replace("りぇ", "RYE");
            hiragana = hiragana.Replace("りょ", "RYO");
            hiragana = hiragana.Replace("ぎゃ", "GYA");
            hiragana = hiragana.Replace("ぎぃ", "GYI");
            hiragana = hiragana.Replace("ぎゅ", "GYU");
            hiragana = hiragana.Replace("ぎぇ", "GYE");
            hiragana = hiragana.Replace("ぎょ", "GYO");
            hiragana = hiragana.Replace("ぐぁ", "GWA");
            hiragana = hiragana.Replace("ぐぃ", "GWI");
            hiragana = hiragana.Replace("ぐぅ", "GWU");
            hiragana = hiragana.Replace("ぐぇ", "GWE");
            hiragana = hiragana.Replace("ぐぉ", "GWO");
            if (Short)
                hiragana = hiragana.Replace("じゃ", "JA");
            else
                switch (romajisetting[14])
                {
                    case 0:
                        hiragana = hiragana.Replace("じゃ", "JA");
                        break;
                    case 1:
                        hiragana = hiragana.Replace("じゃ", "ZYA");
                        break;
                }
            switch (romajisetting[29])
            {
                case 0:
                    hiragana = hiragana.Replace("じぃ", "JYI");
                    break;
                case 1:
                    hiragana = hiragana.Replace("じぃ", "ZYI");
                    break;
            }
            if (Short)
                hiragana = hiragana.Replace("じゅ", "JU");
            else
                switch (romajisetting[15])
                {
                    case 0:
                        hiragana = hiragana.Replace("じゅ", "JU");
                        break;
                    case 1:
                        hiragana = hiragana.Replace("じゅ", "ZYU");
                        break;
                }
            if (Short)
                hiragana = hiragana.Replace("じぇ", "JE");
            else
                switch (romajisetting[13])
                {
                    case 0:
                        hiragana = hiragana.Replace("じぇ", "JE");
                        break;
                    case 1:
                        hiragana = hiragana.Replace("じぇ", "ZYE");
                        break;
                }
            if (Short)
                hiragana = hiragana.Replace("じょ", "JO");
            else
                switch (romajisetting[16])
                {
                    case 0:
                        hiragana = hiragana.Replace("じょ", "JO");
                        break;
                    case 1:
                        hiragana = hiragana.Replace("じょ", "ZYO");
                        break;
                }
            hiragana = hiragana.Replace("ぢゃ", "DYA");
            hiragana = hiragana.Replace("ぢぃ", "DYI");
            hiragana = hiragana.Replace("ぢゅ", "DYU");
            hiragana = hiragana.Replace("ぢぇ", "DYE");
            hiragana = hiragana.Replace("ぢょ", "DYO");
            hiragana = hiragana.Replace("でゃ", "DHA");
            hiragana = hiragana.Replace("でぃ", "DHI");
            hiragana = hiragana.Replace("でゅ", "DHU");
            hiragana = hiragana.Replace("でぇ", "DHE");
            hiragana = hiragana.Replace("でょ", "DHO");
            hiragana = hiragana.Replace("どぁ", "DWA");
            hiragana = hiragana.Replace("どぃ", "DWI");
            hiragana = hiragana.Replace("どぅ", "DWU");
            hiragana = hiragana.Replace("どぇ", "DWE");
            hiragana = hiragana.Replace("どぉ", "DWO");
            hiragana = hiragana.Replace("びゃ", "BYA");
            hiragana = hiragana.Replace("びぃ", "BYI");
            hiragana = hiragana.Replace("びゅ", "BYU");
            hiragana = hiragana.Replace("びぇ", "BYE");
            hiragana = hiragana.Replace("びょ", "BYO");
            hiragana = hiragana.Replace("ぴゃ", "PYA");
            hiragana = hiragana.Replace("ぴぃ", "PYI");
            hiragana = hiragana.Replace("ぴゅ", "PYU");
            hiragana = hiragana.Replace("ぴぇ", "PYE");
            hiragana = hiragana.Replace("ぴょ", "PYO");
            hiragana = hiragana.Replace("ヴぁ", "VA");
            hiragana = hiragana.Replace("ヴぃ", "VI");
            hiragana = hiragana.Replace("ヴぇ", "VE");
            hiragana = hiragana.Replace("ヴぉ", "VO");
            hiragana = hiragana.Replace("ヴゃ", "VYA");
            hiragana = hiragana.Replace("ヴゅ", "VYU");
            hiragana = hiragana.Replace("ヴょ", "VYO");

            hiragana = hiragana.Replace("っい", "YYI");
            hiragana = hiragana.Replace("っう", "WWU");

            // ん・っ以外のひらがなの処理
            hiragana = hiragana.Replace("あ", "A");
            hiragana = hiragana.Replace("い", "I");
            hiragana = hiragana.Replace("う", "U");
            hiragana = hiragana.Replace("え", "E");
            hiragana = hiragana.Replace("お", "O");
            switch (romajisetting[5])
            {
                case 0:
                    hiragana = hiragana.Replace("か", "KA");
                    break;
                case 1:
                    hiragana = hiragana.Replace("か", "CA");
                    break;
            }
            hiragana = hiragana.Replace("き", "KI");
            switch (romajisetting[6])
            {
                case 0:
                    hiragana = hiragana.Replace("く", "KU");
                    break;
                case 1:
                    hiragana = hiragana.Replace("く", "CU");
                    break;
                case 2:
                    hiragana = hiragana.Replace("く", "QU");
                    break;
            }
            hiragana = hiragana.Replace("け", "KE");
            switch (romajisetting[7])
            {
                case 0:
                    hiragana = hiragana.Replace("こ", "KO");
                    break;
                case 1:
                    hiragana = hiragana.Replace("こ", "CO");
                    break;
            }
            hiragana = hiragana.Replace("さ", "SA");
            if (Short)
                hiragana = hiragana.Replace("し", "SI");
            else
                switch (romajisetting[8])
                {
                    case 0:
                        hiragana = hiragana.Replace("し", "SI");
                        break;
                    case 1:
                        hiragana = hiragana.Replace("し", "SHI");
                        break;
                    case 2:
                        hiragana = hiragana.Replace("し", "CI");
                        break;
                }
            hiragana = hiragana.Replace("す", "SU");
            switch (romajisetting[17])
            {
                case 0:
                    hiragana = hiragana.Replace("せ", "SE");
                    break;
                case 1:
                    hiragana = hiragana.Replace("せ", "CE");
                    break;
            }
            hiragana = hiragana.Replace("そ", "SO");
            hiragana = hiragana.Replace("た", "TA");
            if (Short)
                hiragana = hiragana.Replace("ち", "CHI");
            else
                switch (romajisetting[18])
                {
                    case 0:
                        hiragana = hiragana.Replace("ち", "TI");
                        break;
                    case 1:
                        hiragana = hiragana.Replace("ち", "CHI");
                        break;
                }
            hiragana = hiragana.Replace("つ", "TU");
            hiragana = hiragana.Replace("て", "TE");
            hiragana = hiragana.Replace("と", "TO");
            hiragana = hiragana.Replace("な", "NA");
            hiragana = hiragana.Replace("に", "NI");
            hiragana = hiragana.Replace("ぬ", "NU");
            hiragana = hiragana.Replace("ね", "NE");
            hiragana = hiragana.Replace("の", "NO");
            hiragana = hiragana.Replace("は", "HA");
            hiragana = hiragana.Replace("ひ", "HI");
            switch (romajisetting[23])
            {
                case 0:
                    hiragana = hiragana.Replace("ふ", "HU");
                    break;
                case 1:
                    hiragana = hiragana.Replace("ふ", "FU");
                    break;
            }
            hiragana = hiragana.Replace("へ", "HE");
            hiragana = hiragana.Replace("ほ", "HO");
            hiragana = hiragana.Replace("ま", "MA");
            hiragana = hiragana.Replace("み", "MI");
            hiragana = hiragana.Replace("む", "MU");
            hiragana = hiragana.Replace("め", "ME");
            hiragana = hiragana.Replace("も", "MO");
            hiragana = hiragana.Replace("や", "YA");
            hiragana = hiragana.Replace("ゆ", "YU");
            hiragana = hiragana.Replace("よ", "YO");
            hiragana = hiragana.Replace("ら", "RA");
            hiragana = hiragana.Replace("り", "RI");
            hiragana = hiragana.Replace("る", "RU");
            hiragana = hiragana.Replace("れ", "RE");
            hiragana = hiragana.Replace("ろ", "RO");
            hiragana = hiragana.Replace("わ", "WA");
            hiragana = hiragana.Replace("ゐ", "WYI");
            hiragana = hiragana.Replace("ゑ", "WYE");
            hiragana = hiragana.Replace("を", "WO");
            hiragana = hiragana.Replace("が", "GA");
            hiragana = hiragana.Replace("ぎ", "GI");
            hiragana = hiragana.Replace("ぐ", "GU");
            hiragana = hiragana.Replace("げ", "GE");
            hiragana = hiragana.Replace("ご", "GO");
            hiragana = hiragana.Replace("ざ", "ZA");
            switch (romajisetting[12])
            {
                case 0:
                    hiragana = hiragana.Replace("じ", "JI");
                    break;
                case 1:
                    hiragana = hiragana.Replace("じ", "ZI");
                    break;
            }
            hiragana = hiragana.Replace("ず", "ZU");
            hiragana = hiragana.Replace("ぜ", "ZE");
            hiragana = hiragana.Replace("ぞ", "ZO");
            hiragana = hiragana.Replace("だ", "DA");
            hiragana = hiragana.Replace("ぢ", "DI");
            hiragana = hiragana.Replace("づ", "DU");
            hiragana = hiragana.Replace("で", "DE");
            hiragana = hiragana.Replace("ど", "DO");
            hiragana = hiragana.Replace("ば", "BA");
            hiragana = hiragana.Replace("び", "BI");
            hiragana = hiragana.Replace("ぶ", "BU");
            hiragana = hiragana.Replace("べ", "BE");
            hiragana = hiragana.Replace("ぼ", "BO");
            hiragana = hiragana.Replace("ぱ", "PA");
            hiragana = hiragana.Replace("ぴ", "PI");
            hiragana = hiragana.Replace("ぷ", "PU");
            hiragana = hiragana.Replace("ぺ", "PE");
            hiragana = hiragana.Replace("ぽ", "PO");
            switch (romajisetting[0])
            {
                case 0:
                    hiragana = hiragana.Replace("ぁ", "LA");
                    break;
                case 1:
                    hiragana = hiragana.Replace("ぁ", "XA");
                    break;
            }
            switch (romajisetting[1])
            {
                case 0:
                    hiragana = hiragana.Replace("ぃ", "LI");
                    break;
                case 1:
                    hiragana = hiragana.Replace("ぃ", "XI");
                    break;
            }
            switch (romajisetting[2])
            {
                case 0:
                    hiragana = hiragana.Replace("ぅ", "LU");
                    break;
                case 1:
                    hiragana = hiragana.Replace("ぅ", "XU");
                    break;
            }
            switch (romajisetting[3])
            {
                case 0:
                    hiragana = hiragana.Replace("ぇ", "LE");
                    break;
                case 1:
                    hiragana = hiragana.Replace("ぇ", "XE");
                    break;
            }
            switch (romajisetting[4])
            {
                case 0:
                    hiragana = hiragana.Replace("ぉ", "LO");
                    break;
                case 1:
                    hiragana = hiragana.Replace("ぉ", "XO");
                    break;
            }
            switch (romajisetting[24])
            {
                case 0:
                    hiragana = hiragana.Replace("ゃ", "LYA");
                    break;
                case 1:
                    hiragana = hiragana.Replace("ゃ", "XYA");
                    break;
            }
            switch (romajisetting[25])
            {
                case 0:
                    hiragana = hiragana.Replace("ゅ", "LYU");
                    break;
                case 1:
                    hiragana = hiragana.Replace("ゅ", "XYU");
                    break;
            }
            switch (romajisetting[26])
            {
                case 0:
                    hiragana = hiragana.Replace("ょ", "LYO");
                    break;
                case 1:
                    hiragana = hiragana.Replace("ょ", "XYO");
                    break;
            }
            hiragana = hiragana.Replace("ゎ", "LWA");
            switch (romajisetting[22])
            {
                case 0:
                    hiragana = hiragana.Replace("っっ", "LLTU");
                    break;
                case 1:
                    hiragana = hiragana.Replace("っっ", "XXTU");
                    break;
            }

            int n = 0;

            // 「ん」の処理
            while ((n = hiragana.IndexOf('ん')) != -1)
            {
                if (n == hiragana.Length - 1)
                    hiragana = hiragana.Replace("ん", "NN");
                else if (hiragana[n + 1] == 'A' || hiragana[n + 1] == 'I' || hiragana[n + 1] == 'U' || hiragana[n + 1] == 'E' || hiragana[n + 1] == 'O' ||
                    hiragana[n + 1] == 'N' || hiragana[n + 1] == 'Y' || 
                    hiragana[n + 1] == 'ん'/* || "abcdefghijklmnopqrstuvwxyz".IndexOf(hiragana[n + 1]) != -1*/)
                    hiragana = hiragana.Substring(0, n) + "NN" + hiragana.Substring(n + 1);
                else
                    hiragana = hiragana.Substring(0, n) + "N" + hiragana.Substring(n + 1);
            }

            n = 0;

            // 「っ」の処理
            while ((n = hiragana.IndexOf('っ')) != -1)
            {
                if (n == hiragana.Length - 1)
                    switch (romajisetting[22])
                    {
                        case 0:
                            hiragana = hiragana.Replace("っ", "LTU");
                            break;
                        case 1:
                            hiragana = hiragana.Replace("っ", "XTU");
                            break;
                    }
                else if (hiragana[n + 1] == 'A' || hiragana[n + 1] == 'I' || hiragana[n + 1] == 'U' || hiragana[n + 1] == 'E' || hiragana[n + 1] == 'O' ||
                    hiragana[n + 1] == 'N' || hiragana[n + 1].ToString().ToLower() == hiragana[n + 1].ToString())
                    switch (romajisetting[22])
                    {
                        case 0:
                            hiragana = hiragana.Substring(0, n) + "LTU" + hiragana.Substring(n + 1);
                            break;
                        case 1:
                            hiragana = hiragana.Substring(0, n) + "XTU" + hiragana.Substring(n + 1);
                            break;
                    }
                else
                    hiragana = hiragana.Substring(0, n) + hiragana[n + 1] + hiragana.Substring(n + 1);
            }

            return hiragana.ToUpper();
        }

        /// <summary>
        /// 必要kpmを計算する
        /// </summary>
        /// <param name="hiragana">ひらがな文字列</param>
        /// <param name="interval">インターバル長さ</param>
        /// <returns>kpm（double値）</returns>
        public double necessarykpm(string hiragana, int interval)
        {
            int hiraganatoromajilength = hiraganatoromaji(hiragana, true).Length;
            if (hiraganatoromajilength < 1)
                return 0;
            else
                return interval != 0 ? ((hiraganatoromaji(hiragana, true).Length - 1) * 60000 / interval) : double.PositiveInfinity;
        }

        /// <summary>
        /// 時刻を文字列に変換する
        /// </summary>
        /// <param name="second">秒</param>
        /// <returns>0:00の形で返す</returns>
        public string timetostring(int second)
        {
            if (second % 60 < 10)
                return second / 60 + ":0" + second % 60;
            else
                return second / 60 + ":" + second % 60;
        }

        /// <summary>
        /// 時刻を文字列に変換する
        /// </summary>
        /// <param name="millisecond">ミリ秒</param>
        /// <returns>0.0（秒）の形で返す</returns>
        public string timetostring2(int millisecond)
        {
            if (millisecond >= 0)
                return millisecond / 1000 + "." + (millisecond / 100) % 10;
            else
                return millisecond / 1000 + "." + (-millisecond / 100) % 10;
        }

        /// <summary>
        /// nextstring と nextstringa の代入を簡略化するためだけのメソッド
        /// </summary>
        /// <param name="_nextstring"></param>
        /// <param name="_nextstringa"></param>
        public void nextstringset(string _nextstring, string _nextstringa)
        {
            nextstring = _nextstring;
            nextstringa = _nextstringa;
        }

        /// <summary>
        /// ランクの計算
        /// </summary>
        /// <returns></returns>
        public string classcalc(int typecount, int nkpm, int kpm, int misstype, int truetype)
        {
            double dtc = typecount;
            double dnk = nkpm;
            double dmt = misstype;
            double dtt = truetype;

            //if (kpm > normalkpm)
            //    n = n * normalkpm / kpm;

            double c;
            if (nkpm == 0)
                c = 0;
            else
            {
                double Base = Math.Pow(Math.E, Math.Log(limitkpm) / rank.Length);
                if (truetype > typecount)
                    c = dtt / dtc;
                else
                    c = (dtt * dtt) / (dtc * dtc);
                c *= Math.Pow(Math.Abs(dtt - dmt) / dtt, basekpm / dnk) * Math.Log(kpm + 1, Base);
                if (truetype < misstype)
                    c *= -1;
            }

            //int c = (int)(5 * kpm * (truetype - misstype) / (normalkpm * n));

            if (c < -1)
                return "Ｃ:。ミ";
            else if (c < rank.Length && c >= 0)
            {
                return rank[(int)c];
            }
            else if (c >= rank.Length)
                return "undefined";
            else
                return "-";
            
        }

        static string[] rank = { "R", "Q", "P", "O", "N", "M", "L", "K", "J", "I", "H", "G", "F", "E", "D", "C", "B", "A", "AA", "AAA", 
                                    "S", "SS", "SSS", "SSS+", "SSS++", "SSS+++", "Super", "Over" };
        static double normalkpm = 350.0;
        static double basekpm = 800.0;
        static double limitkpm = 1000.0;

        #endregion


        # region romajisetting

        void romajieventset()
        {
            mainWindow.Rb_0_0.Click += Rb_0_0_Click;
            mainWindow.Rb_0_1.Click += Rb_0_1_Click;
            mainWindow.Rb_1_0.Click += Rb_1_0_Click;
            mainWindow.Rb_1_1.Click += Rb_1_1_Click;
            mainWindow.Rb_2_0.Click += Rb_2_0_Click;
            mainWindow.Rb_2_1.Click += Rb_2_1_Click;
            mainWindow.Rb_3_0.Click += Rb_3_0_Click;
            mainWindow.Rb_3_1.Click += Rb_3_1_Click;
            mainWindow.Rb_4_0.Click += Rb_4_0_Click;
            mainWindow.Rb_4_1.Click += Rb_4_1_Click;
            mainWindow.Rb_5_0.Click += Rb_5_0_Click;
            mainWindow.Rb_5_1.Click += Rb_5_1_Click;
            mainWindow.Rb_6_0.Click += Rb_6_0_Click;
            mainWindow.Rb_6_1.Click += Rb_6_1_Click;
            mainWindow.Rb_6_2.Click += Rb_6_2_Click;
            mainWindow.Rb_7_0.Click += Rb_7_0_Click;
            mainWindow.Rb_7_1.Click += Rb_7_1_Click;
            mainWindow.Rb_8_0.Click += Rb_8_0_Click;
            mainWindow.Rb_8_1.Click += Rb_8_1_Click;
            mainWindow.Rb_8_2.Click += Rb_8_2_Click;
            mainWindow.Rb_9_0.Click += Rb_9_0_Click;
            mainWindow.Rb_9_1.Click += Rb_9_1_Click;
            mainWindow.Rb_10_0.Click += Rb_10_0_Click;
            mainWindow.Rb_10_1.Click += Rb_10_1_Click;
            mainWindow.Rb_11_0.Click += Rb_11_0_Click;
            mainWindow.Rb_11_1.Click += Rb_11_1_Click;
            mainWindow.Rb_12_0.Click += Rb_12_0_Click;
            mainWindow.Rb_12_1.Click += Rb_12_1_Click;
            mainWindow.Rb_13_0.Click += Rb_13_0_Click;
            mainWindow.Rb_13_1.Click += Rb_13_1_Click;
            mainWindow.Rb_14_0.Click += Rb_14_0_Click;
            mainWindow.Rb_14_1.Click += Rb_14_1_Click;
            mainWindow.Rb_15_0.Click += Rb_15_0_Click;
            mainWindow.Rb_15_1.Click += Rb_15_1_Click;
            mainWindow.Rb_16_0.Click += Rb_16_0_Click;
            mainWindow.Rb_16_1.Click += Rb_16_1_Click;
            mainWindow.Rb_17_0.Click += Rb_17_0_Click;
            mainWindow.Rb_17_1.Click += Rb_17_1_Click;
            mainWindow.Rb_18_0.Click += Rb_18_0_Click;
            mainWindow.Rb_18_1.Click += Rb_18_1_Click;
            mainWindow.Rb_19_0.Click += Rb_19_0_Click;
            mainWindow.Rb_19_1.Click += Rb_19_1_Click;
            mainWindow.Rb_19_2.Click += Rb_19_2_Click;
            mainWindow.Rb_20_0.Click += Rb_20_0_Click;
            mainWindow.Rb_20_1.Click += Rb_20_1_Click;
            mainWindow.Rb_20_2.Click += Rb_20_2_Click;
            mainWindow.Rb_21_0.Click += Rb_21_0_Click;
            mainWindow.Rb_21_1.Click += Rb_21_1_Click;
            mainWindow.Rb_21_2.Click += Rb_21_2_Click;
            mainWindow.Rb_22_0.Click += Rb_22_0_Click;
            mainWindow.Rb_22_1.Click += Rb_22_1_Click;
            mainWindow.Rb_23_0.Click += Rb_23_0_Click;
            mainWindow.Rb_23_1.Click += Rb_23_1_Click;
            mainWindow.Rb_24_0.Click += Rb_24_0_Click;
            mainWindow.Rb_24_1.Click += Rb_24_1_Click;
            mainWindow.Rb_25_0.Click += Rb_25_0_Click;
            mainWindow.Rb_25_1.Click += Rb_25_1_Click;
            mainWindow.Rb_26_0.Click += Rb_26_0_Click;
            mainWindow.Rb_26_1.Click += Rb_26_1_Click;
            mainWindow.Rb_27_0.Click += Rb_27_0_Click;
            mainWindow.Rb_27_1.Click += Rb_27_1_Click;
            mainWindow.Rb_28_0.Click += Rb_28_0_Click;
            mainWindow.Rb_28_1.Click += Rb_28_1_Click;
            mainWindow.Rb_29_0.Click += Rb_29_0_Click;
            mainWindow.Rb_29_1.Click += Rb_29_1_Click;
            mainWindow.Rb_30_0.Click += Rb_30_0_Click;
            mainWindow.Rb_30_1.Click += Rb_30_1_Click;
            mainWindow.Rb_31_0.Click += Rb_31_0_Click;
            mainWindow.Rb_31_1.Click += Rb_31_1_Click;
            mainWindow.Rb_31_2.Click += Rb_31_2_Click;
        }

        void Rb_0_0_Click(object sender, RoutedEventArgs e) { romajisetting[0] = 0; }
        void Rb_0_1_Click(object sender, RoutedEventArgs e) { romajisetting[0] = 1; }
        void Rb_1_0_Click(object sender, RoutedEventArgs e) { romajisetting[1] = 0; }
        void Rb_1_1_Click(object sender, RoutedEventArgs e) { romajisetting[1] = 1; }
        void Rb_2_0_Click(object sender, RoutedEventArgs e) { romajisetting[2] = 0; }
        void Rb_2_1_Click(object sender, RoutedEventArgs e) { romajisetting[2] = 1; }
        void Rb_3_0_Click(object sender, RoutedEventArgs e) { romajisetting[3] = 0; }
        void Rb_3_1_Click(object sender, RoutedEventArgs e) { romajisetting[3] = 1; }
        void Rb_4_0_Click(object sender, RoutedEventArgs e) { romajisetting[4] = 0; }
        void Rb_4_1_Click(object sender, RoutedEventArgs e) { romajisetting[4] = 1; }
        void Rb_5_0_Click(object sender, RoutedEventArgs e) { romajisetting[5] = 0; }
        void Rb_5_1_Click(object sender, RoutedEventArgs e) { romajisetting[5] = 1; }
        void Rb_6_0_Click(object sender, RoutedEventArgs e) { romajisetting[6] = 0; }
        void Rb_6_1_Click(object sender, RoutedEventArgs e) { romajisetting[6] = 1; }
        void Rb_6_2_Click(object sender, RoutedEventArgs e) { romajisetting[6] = 2; }
        void Rb_7_0_Click(object sender, RoutedEventArgs e) { romajisetting[7] = 0; }
        void Rb_7_1_Click(object sender, RoutedEventArgs e) { romajisetting[7] = 1; }
        void Rb_8_0_Click(object sender, RoutedEventArgs e) { romajisetting[8] = 0; }
        void Rb_8_1_Click(object sender, RoutedEventArgs e) { romajisetting[8] = 1; }
        void Rb_8_2_Click(object sender, RoutedEventArgs e) { romajisetting[8] = 2; }
        void Rb_9_0_Click(object sender, RoutedEventArgs e) { romajisetting[9] = 0; }
        void Rb_9_1_Click(object sender, RoutedEventArgs e) { romajisetting[9] = 1; }
        void Rb_10_0_Click(object sender, RoutedEventArgs e) { romajisetting[10] = 0; }
        void Rb_10_1_Click(object sender, RoutedEventArgs e) { romajisetting[10] = 1; }
        void Rb_11_0_Click(object sender, RoutedEventArgs e) { romajisetting[11] = 0; }
        void Rb_11_1_Click(object sender, RoutedEventArgs e) { romajisetting[11] = 1; }
        void Rb_12_0_Click(object sender, RoutedEventArgs e) { romajisetting[12] = 0; }
        void Rb_12_1_Click(object sender, RoutedEventArgs e) { romajisetting[12] = 1; }
        void Rb_13_0_Click(object sender, RoutedEventArgs e) { romajisetting[13] = 0; }
        void Rb_13_1_Click(object sender, RoutedEventArgs e) { romajisetting[13] = 1; }
        void Rb_14_0_Click(object sender, RoutedEventArgs e) { romajisetting[14] = 0; }
        void Rb_14_1_Click(object sender, RoutedEventArgs e) { romajisetting[14] = 1; }
        void Rb_15_0_Click(object sender, RoutedEventArgs e) { romajisetting[15] = 0; }
        void Rb_15_1_Click(object sender, RoutedEventArgs e) { romajisetting[15] = 1; }
        void Rb_16_0_Click(object sender, RoutedEventArgs e) { romajisetting[16] = 0; }
        void Rb_16_1_Click(object sender, RoutedEventArgs e) { romajisetting[16] = 1; }
        void Rb_17_0_Click(object sender, RoutedEventArgs e) { romajisetting[17] = 0; }
        void Rb_17_1_Click(object sender, RoutedEventArgs e) { romajisetting[17] = 1; }
        void Rb_18_0_Click(object sender, RoutedEventArgs e) { romajisetting[18] = 0; }
        void Rb_18_1_Click(object sender, RoutedEventArgs e) { romajisetting[18] = 1; }
        void Rb_19_0_Click(object sender, RoutedEventArgs e) { romajisetting[19] = 0; }
        void Rb_19_1_Click(object sender, RoutedEventArgs e) { romajisetting[19] = 1; }
        void Rb_19_2_Click(object sender, RoutedEventArgs e) { romajisetting[19] = 2; }
        void Rb_20_0_Click(object sender, RoutedEventArgs e) { romajisetting[20] = 0; }
        void Rb_20_1_Click(object sender, RoutedEventArgs e) { romajisetting[20] = 1; }
        void Rb_20_2_Click(object sender, RoutedEventArgs e) { romajisetting[20] = 2; }
        void Rb_21_0_Click(object sender, RoutedEventArgs e) { romajisetting[21] = 0; }
        void Rb_21_1_Click(object sender, RoutedEventArgs e) { romajisetting[21] = 1; }
        void Rb_21_2_Click(object sender, RoutedEventArgs e) { romajisetting[21] = 2; }
        void Rb_22_0_Click(object sender, RoutedEventArgs e) { romajisetting[22] = 0; }
        void Rb_22_1_Click(object sender, RoutedEventArgs e) { romajisetting[22] = 1; }
        void Rb_23_0_Click(object sender, RoutedEventArgs e) { romajisetting[23] = 0; }
        void Rb_23_1_Click(object sender, RoutedEventArgs e) { romajisetting[23] = 1; }
        void Rb_24_0_Click(object sender, RoutedEventArgs e) { romajisetting[24] = 0; }
        void Rb_24_1_Click(object sender, RoutedEventArgs e) { romajisetting[24] = 1; }
        void Rb_25_0_Click(object sender, RoutedEventArgs e) { romajisetting[25] = 0; }
        void Rb_25_1_Click(object sender, RoutedEventArgs e) { romajisetting[25] = 1; }
        void Rb_26_0_Click(object sender, RoutedEventArgs e) { romajisetting[26] = 0; }
        void Rb_26_1_Click(object sender, RoutedEventArgs e) { romajisetting[26] = 1; }
        void Rb_27_0_Click(object sender, RoutedEventArgs e) { romajisetting[27] = 0; }
        void Rb_27_1_Click(object sender, RoutedEventArgs e) { romajisetting[27] = 1; }
        void Rb_28_0_Click(object sender, RoutedEventArgs e) { romajisetting[28] = 0; }
        void Rb_28_1_Click(object sender, RoutedEventArgs e) { romajisetting[28] = 1; }
        void Rb_29_0_Click(object sender, RoutedEventArgs e) { romajisetting[29] = 0; }
        void Rb_29_1_Click(object sender, RoutedEventArgs e) { romajisetting[29] = 1; }
        void Rb_30_0_Click(object sender, RoutedEventArgs e) { romajisetting[30] = 0; }
        void Rb_30_1_Click(object sender, RoutedEventArgs e) { romajisetting[30] = 1; }
        void Rb_31_0_Click(object sender, RoutedEventArgs e) { romajisetting[31] = 0; }
        void Rb_31_1_Click(object sender, RoutedEventArgs e) { romajisetting[31] = 1; }
        void Rb_31_2_Click(object sender, RoutedEventArgs e) { romajisetting[31] = 2; }

        void reflectromajisetting()
        {
            if (romajisetting[0] == 1)
                mainWindow.Rb_0_1.IsChecked = true;
            if (romajisetting[1] == 1)
                mainWindow.Rb_1_1.IsChecked = true;
            if (romajisetting[2] == 1)
                mainWindow.Rb_2_1.IsChecked = true;
            if (romajisetting[3] == 1)
                mainWindow.Rb_3_1.IsChecked = true;
            if (romajisetting[4] == 1)
                mainWindow.Rb_4_1.IsChecked = true;
            if (romajisetting[5] == 1)
                mainWindow.Rb_5_1.IsChecked = true;
            if (romajisetting[6] == 1)
                mainWindow.Rb_6_1.IsChecked = true;
            else if (romajisetting[6] == 2)
                mainWindow.Rb_6_2.IsChecked = true;
            if (romajisetting[7] == 1)
                mainWindow.Rb_7_1.IsChecked = true;
            if (romajisetting[8] == 1)
                mainWindow.Rb_8_1.IsChecked = true;
            else if (romajisetting[8] == 2)
                mainWindow.Rb_8_2.IsChecked = true;
            if (romajisetting[9] == 1)
                mainWindow.Rb_9_1.IsChecked = true;
            if (romajisetting[10] == 1)
                mainWindow.Rb_10_1.IsChecked = true;
            if (romajisetting[11] == 1)
                mainWindow.Rb_11_1.IsChecked = true;
            if (romajisetting[12] == 1)
                mainWindow.Rb_12_1.IsChecked = true;
            if (romajisetting[13] == 1)
                mainWindow.Rb_13_1.IsChecked = true;
            if (romajisetting[14] == 1)
                mainWindow.Rb_14_1.IsChecked = true;
            if (romajisetting[15] == 1)
                mainWindow.Rb_15_1.IsChecked = true;
            if (romajisetting[16] == 1)
                mainWindow.Rb_16_1.IsChecked = true;
            if (romajisetting[17] == 1)
                mainWindow.Rb_17_1.IsChecked = true;
            if (romajisetting[18] == 1)
                mainWindow.Rb_18_1.IsChecked = true;
            if (romajisetting[19] == 1)
                mainWindow.Rb_19_1.IsChecked = true;
            else if (romajisetting[19] == 2)
                mainWindow.Rb_19_2.IsChecked = true;
            if (romajisetting[20] == 1)
                mainWindow.Rb_20_1.IsChecked = true;
            else if (romajisetting[20] == 2)
                mainWindow.Rb_20_2.IsChecked = true;
            if (romajisetting[21] == 1)
                mainWindow.Rb_21_1.IsChecked = true;
            else if (romajisetting[21] == 2)
                mainWindow.Rb_21_2.IsChecked = true;
            if (romajisetting[22] == 1)
                mainWindow.Rb_22_1.IsChecked = true;
            if (romajisetting[23] == 1)
                mainWindow.Rb_23_1.IsChecked = true;
            if (romajisetting[24] == 1)
                mainWindow.Rb_24_1.IsChecked = true;
            if (romajisetting[25] == 1)
                mainWindow.Rb_25_1.IsChecked = true;
            if (romajisetting[26] == 1)
                mainWindow.Rb_26_1.IsChecked = true;
            if (romajisetting[27] == 1)
                mainWindow.Rb_27_1.IsChecked = true;
            if (romajisetting[28] == 1)
                mainWindow.Rb_28_1.IsChecked = true;
            if (romajisetting[29] == 1)
                mainWindow.Rb_29_1.IsChecked = true;
            if (romajisetting[30] == 1)
                mainWindow.Rb_30_1.IsChecked = true;
            if (romajisetting[31] == 1)
                mainWindow.Rb_31_1.IsChecked = true;
            else if (romajisetting[31] == 2)
                mainWindow.Rb_31_2.IsChecked = true;
        }

        # endregion

    }
}
