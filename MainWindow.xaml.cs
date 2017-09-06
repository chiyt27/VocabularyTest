using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Speech.Synthesis;

namespace WordTest
{
    /// <summary>
    /// MainWindow.xaml 的互動邏輯
    /// </summary>
    public partial class MainWindow : Window
    {
        int count = Properties.Settings.Default.Count; /*批次取的數量*/
        string wordFile = Properties.Settings.Default.WordFile; /*檔案位置*/
        int speakRate = Properties.Settings.Default.SpeakRate; /*讀速*/
        int currentIdx = 0;
        List<WordInfo> DataList = new List<WordInfo>();
        List<WordInfo> resultList = new List<WordInfo>();

        Random rnd = new Random();

        int knowCnt = 0,nnKnowCnt = 0;
        string knowBtnName = "知道", unknowBtnName = "忘了";

        public MainWindow()
        {
            InitializeComponent();

            Left = SystemParameters.WorkArea.Width - Width;
            Top = SystemParameters.WorkArea.Height - Height;
            KnowBtn.Content = knowBtnName;
            UnKnowBtn.Content = unknowBtnName;

            DataList = OpenCSV(wordFile);
            GetNextOrNewAry();
        }

        #region  元件 Event or Method
        private void Btn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnGrid.IsEnabled = false;
                WordInfo current = resultList[currentIdx];
                SetValue(current.Word, current.Mean);

                string senderName = ((Button)sender).Name;
                if (senderName == KnowBtn.Name)
                {
                    knowCnt++;
                    KnowBtn.Content = string.Format("{0} ({1})", knowBtnName, knowCnt);
                    if (DataList[current.Key].Weight > 1)
                        DataList[current.Key].Weight--;
                }
                else if (senderName == UnKnowBtn.Name)
                {
                    nnKnowCnt++;
                    UnKnowBtn.Content = string.Format("{0} ({1})", unknowBtnName, nnKnowCnt);
                    DataList[current.Key].Weight++;
                }


                Task.Factory.StartNew(() =>
                {
                    System.Threading.Thread.Sleep(2000);
                    Dispatcher.Invoke((Action)delegate {
                        GetNextOrNewAry();
                        btnGrid.IsEnabled = true;
                    });
                });
            }
            catch (Exception ex)
            {
                AlertMessage(ex.Message);
            }
        }

        private void Speak_Click(object sender, RoutedEventArgs e)
        {
            //ref: https://code.msdn.microsoft.com/windowsdesktop/Text-to-Speech-Converter-0ed77dd5
            try
            {
                SpeechSynthesizer reader = new SpeechSynthesizer();
                reader.Rate = speakRate;
                reader.SpeakAsync(Word.Content.ToString());
            }
            catch (Exception ex)
            {
                AlertMessage(ex.Message);
            }
        }

        private void ExitBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Task.Factory.StartNew(() =>
            {
                WriteCSV(wordFile, DataList);
            });
        }

        private void SetValue(string _word, string _mean)
        {
            Word.Content = _word;
            Mean.Content = _mean;
        }

        #endregion Controls Event or Method

        #region 取值 Method
        private void GetNextOrNewAry()
        {
            try
            {
                if (DataList.Count <= 0) return;

                currentIdx++;
                if (resultList.Count <= 0)
                {
                    /*跑完一批後，取新的下一批 或 do other things*/
                    resultList = PickByWeight(DataList, count);
                    currentIdx = 0;
                }
                else if (currentIdx >= resultList.Count)
                {
                    this.Close();
                    return;
                }
                SetValue(resultList[currentIdx].Word, "");
            }
            catch (Exception ex)
            {
                AlertMessage(ex.Message);
            }
        }

        private int GetTotalWeight(List<WordInfo> _dataList)
        {
            int total_weight = 0;
            foreach (WordInfo item in _dataList)
                total_weight += item.Weight;
            return total_weight;
        }

        private List<WordInfo> PickByWeight(List<WordInfo> _dataList, int _count)
        {
            List<WordInfo> resList = new List<WordInfo>();
            int total_weight = GetTotalWeight(_dataList);


            for (int i = 0; i < _count; i++)
            {
                int idx = rnd.Next(1, total_weight);
                int k = 0;
                foreach (WordInfo item in _dataList)
                {
                    if (idx > k && idx <= k + item.Weight)
                    {
                        resList.Add(item);
                        break;
                    }
                    else
                        k += item.Weight;
                }
            }
            return resList;
        }

        #endregion

        #region 讀寫檔
        private List<WordInfo> OpenCSV(string _filePath)
        {
            List<WordInfo> dataList = new List<WordInfo>();
            FileStream fs = new FileStream(_filePath, System.IO.FileMode.Open, System.IO.FileAccess.Read);
            StreamReader sr = new StreamReader(fs, Encoding.UTF8);
            try
            {
                string strLine = "";
                string[] aryLine;
                int count = -1;
                while ((strLine = sr.ReadLine()) != null)
                {
                    if (count < 0)
                    {
                    }
                    else
                    {
                        aryLine = strLine.Split(',');
                        if(aryLine.Length == 4)
                            dataList.Add(new WordInfo()
                            {
                                Key = count,
                                Idx = string.IsNullOrWhiteSpace(aryLine[0]) ? 0 : Convert.ToInt32(aryLine[0]),
                                Word = aryLine[1],
                                Mean = aryLine[2],
                                Weight = (string.IsNullOrWhiteSpace(aryLine[3]) ? 0 : Convert.ToInt32(aryLine[3])) + 1
                            });
                    }
                    count++;
                }
            }
            catch (Exception ex)
            {
                dataList = new List<WordInfo>();
                AlertMessage(ex.Message);
            }
            finally
            {
                sr.Close();
                fs.Close();
            }
            return dataList;
        }

        private void WriteCSV(string _filePath, List<WordInfo> _dataList)
        {
            FileStream fs = new FileStream(_filePath, System.IO.FileMode.Open, System.IO.FileAccess.Write);
            StreamWriter sw = new StreamWriter(fs, Encoding.UTF8);
            string strLine = "";
            try
            {
                sw.WriteLine("Idx,Word,Mean,Weight");
                foreach (WordInfo item in _dataList)
                {
                    strLine = string.Format("{0},{1},{2},{3}",
                        item.Idx == 0 ? "" : item.Idx.ToString(),
                        item.Word,
                        item.Mean,
                        item.Weight > 1 ? (item.Weight - 1).ToString() : "");
                    sw.WriteLine(strLine);
                }
            }
            catch (Exception ex)
            { }
            finally
            {
                sw.Close();
                fs.Close();
            }
        }
        #endregion 讀寫檔

        private void AlertMessage(string str)
        {
            MessageBox.Show(str);
        }


        class WordInfo
            {
                public int Key { get; set; }
                public int Idx { get; set; }
                public string Word { get; set; }
                public int Weight { get; set; }
                public string Mean { get; set; }

            }

        
    }
}
