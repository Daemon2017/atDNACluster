﻿using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using System.Collections.Generic;
using System.Net;
using System.Text;

using ZedGraph;

using Accord.Statistics.Analysis;
using Accord.MachineLearning;
using Accord.Controls;

namespace atDNACluster
{
    public partial class Form1 : Form
    {
        ColorSequenceCollection colors = new ColorSequenceCollection();

        ZedGraphControl zedGraph;
        KMeans kmeans;
        PrincipalComponentAnalysis pca;
        DescriptiveAnalysis sda;
        AnalysisMethod AnalysisPCA;
        CommonMatchClass[] MyCommonMatches;
        MatchClass[] MyMatches;

        string NoDataMessage = "Вы не загрузили матрицы с данными!";
        string NoDataCaption = "Нехватка данных!";
        DialogResult NoData;

        string LoginErrorMessage = "Неправильный логин и/или пароль!";
        string LoginErrorCaption = "Ошибка!";
        DialogResult LoginErrorResult;

        string ServerOfflineMessage = "Сервер с данными FTDNA временно недоступен!";
        string ServerOfflineCaption = "Ошибка!";
        DialogResult ServerOfflineResult;

        string ServerDeadMessage = "Один из серверов API не работает!";
        string ServerDeadCaption = "Ошибка!";
        DialogResult ServerDeadResult;

        string PCAMmessage = "Сперва Вы должны провести обработку с помощью МГК!";
        string PCACaption = "Нехватка данных!";

        string NumberOfClustersChMessage = "С момента последней обработки с помощью МГК Вы изменили число кластеров - повторите обработку с помощью МГК!";
        string NumberOfClustersChCaption = "Неправильное количество кластеров!";
        DialogResult NumberOfClustersCh;

        string NumberOfClustersErrorMessage = "Для работы этой функции число кластеров должно быть равным 4!";
        string NumberOfClustersErrorCaption = "Неправильное количество кластеров!";
        DialogResult NumberOfClustersError;

        string NumberOfPCAClustersErrorMessage = "Сперва нужно запустить МГК при числе кластеров, равном 4!";
        string NumberOfPCAClustersErrorCaption = "Неправильное количество кластеров!";
        DialogResult NumberOfPCAClustersError;

        string NumberOfClusteringClustersErrorMessage = "Сперва нужно запустить кластеризацию К-средних при числе кластеров, равном 4!";
        string NumberOfClusteringClustersErrorCaption = "Неправильное количество кластеров!";
        DialogResult NumberOfClusteringClustersError;

        double[,] MatrixOfDistances;
        string[] KitNumbers;
        string[] KitNames;
        double[,] matrixOfCoordinates;
        double[][] mixture;
        int[] classificationsOur;

        int LastPCANumberOfClusters;
        int LastClusteringNumberOfClusters;
        int NumberOfClusters = 2;
        bool NumberOfClustersChanged = false;

        bool FTDNA = false;

        public Form1()
        {
            InitializeComponent();

            zedGraph = new ZedGraphControl();
            zedGraph.Location = new Point(0, 24);
            zedGraph.Name = "zedGraph";
            zedGraph.Size = new Size(1366, 768 - 24 - 54 - 22);
            zedGraph.GraphPane.XAxis.IsVisible = false;
            zedGraph.GraphPane.YAxis.IsVisible = false;
            zedGraph.GraphPane.Title.IsVisible = false;
            zedGraph.PointValueEvent += new ZedGraphControl.PointValueHandler(zedGraph_PointValueEvent);
            Controls.Add(zedGraph);
        }

        void CreateGraph(ZedGraphControl zgc)
        {
            GraphPane myPane = zgc.GraphPane;
            myPane.CurveList.Clear();

            myPane.XAxis.Cross = 0.0;
            myPane.YAxis.Cross = 0.0;
            myPane.XAxis.Title.IsVisible = false;
            myPane.YAxis.Title.IsVisible = false;
            myPane.Title.IsVisible = false;
            myPane.Legend.IsVisible = false;

            int start = 1;

            PointPairList list = new PointPairList();
            for (int i = start; i < matrixOfCoordinates.GetLength(0); i++)
            {
                list.Add(matrixOfCoordinates[i, 0],
                         matrixOfCoordinates[i, 1]);
            }

            LineItem myCurve = myPane.AddCurve("Совпаденец", list, Color.Gray, SymbolType.Diamond);
            myCurve.Line.IsVisible = false;
            myCurve.Symbol.Border.IsVisible = false;
            myCurve.Symbol.Fill = new Fill(Color.Gray);

            for (int i = 0; i < NumberOfClusters; i++)
            {
                Color color = colors[i];
                myCurve = myPane.AddCurve("D" + (i + 1), new PointPairList(), color, SymbolType.Diamond);
                myCurve.Line.IsVisible = false;
                myCurve.Symbol.Border.IsVisible = false;
                myCurve.Symbol.Fill = new Fill(color);
            }

            myPane.Fill = new Fill(Color.WhiteSmoke);

            zgc.IsShowPointValues = true;
            zgc.AxisChange();
            zgc.Refresh();
        }

        void updateGraph(int[] classifications)
        {
            for (int i = 0; i < NumberOfClusters + 1; i++)
            {
                zedGraph.GraphPane.CurveList[i].Clear();
            }

            for (int j = 0; j < mixture.Length; j++)
            {
                int c = classifications[j];

                var curveList = zedGraph.GraphPane.CurveList[c + 1];
                double[] point = mixture[j];
                curveList.AddPoint(point[0], point[1]);
            }

            zedGraph.Invalidate();
        }

        string zedGraph_PointValueEvent(ZedGraphControl sender,
                                        GraphPane pane,
                                        CurveItem curve,
                                        int iPt)
        {
            PointPair point = curve[iPt];

            string kit = 0.ToString();
            string name = 0.ToString();
            string distance = 0.ToString();

            for (int i = 0; i < matrixOfCoordinates.GetLength(0); i++)
            {
                if ((point.X == matrixOfCoordinates[i, 0]) && (point.Y == matrixOfCoordinates[i, 1]))
                {
                    kit = KitNumbers[i];
                    name = KitNames[i];
                    distance = MatrixOfDistances[0, i].ToString();
                }
            }

            string result = string.Format("X: {0:F3}\nY: {1:F3}\nKit: {2:F3}\nName: {3:F3}\nTMRCA: {4:F3}", point.X, point.Y, kit, name, distance);

            return result;
        }

        private void button8_Click(object sender, EventArgs e)
        {
            updateGraph(classificationsOur);
        }

        void replaceZeros()
        {
            for (int i = 0; i < MatrixOfDistances.GetLength(0); i++)
            {
                for (int j = 0; j < MatrixOfDistances.GetLength(0); j++)
                {
                    MatrixOfDistances[i, j] = 99;
                }
            }
        }

        void fillDiagonalByZeros()
        {
            for (int i = 0; i < MatrixOfDistances.GetLength(0); i++)
            {
                MatrixOfDistances[i, i] = 0;
            }
        }

        void transformOfMatrix()
        {
            mixture = new double[matrixOfCoordinates.GetLength(0) - 1][];

            for (int i = 1; i < matrixOfCoordinates.GetLength(0); i++)
            {
                mixture[i - 1] = new double[] { matrixOfCoordinates[i, 0], matrixOfCoordinates[i, 1] };
            }

        }

        private void processToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (FTDNA == true)
            {
                MatrixOfDistances = null;
                KitNames = null;
                KitNumbers = null;

                MatrixOfDistances = new double[MyMatches.Length + 1, MyMatches.Length + 1];
                KitNames = new string[MyMatches.Length + 1];
                KitNumbers = new string[MyMatches.Length + 1];

                replaceZeros();
                fillDiagonalByZeros();

                //-----------------------------------------------------

                for (int i = 1; i < MatrixOfDistances.GetLength(0); i++)
                {
                    MatrixOfDistances[0, i] = convertTotalCMToTMRCA(MyMatches[i - 1].totalCM);
                    MatrixOfDistances[i, 0] = convertTotalCMToTMRCA(MyMatches[i - 1].totalCM);
                    KitNames[i] = MyMatches[i - 1].firstName + " " + MyMatches[i - 1].middleName + " " + MyMatches[i - 1].lastName;
                    KitNumbers[i] = MyMatches[i - 1].eKitNum;
                }

                for (int i = 0; i < MyMatches.Length; i++)
                {
                    for (int j = 0; j < MyCommonMatches.Length; j++)
                    {
                        if (MyMatches[i].matchResultId == MyCommonMatches[j].matchResultId)
                        {
                            for (int n = 0; n < MyCommonMatches[j].commonMatches.Count; n++)
                            {
                                for (int m = 0; m < MyMatches.Length; m++)
                                {
                                    if (MyMatches[m].matchResultId == MyCommonMatches[j].commonMatches[n].resultId2)
                                    {
                                        MatrixOfDistances[i + 1, m + 1] = convertTotalCMToTMRCA(MyCommonMatches[j].commonMatches[n].totalCM);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            //-----------------------------------------------------

            int[] counter = new int[MatrixOfDistances.GetLength(0)];
            int[] orphan = new int[1];
            int d = 0;

            for (int i = 0; i < MatrixOfDistances.GetLength(0); i++)
            {
                for (int j = 0; j < MatrixOfDistances.GetLength(0); j++)
                {
                    if (MatrixOfDistances[i, j] == 99)
                    {
                        counter[i]++;
                    }
                }

                if (counter[i] == MatrixOfDistances.GetLength(0) - 2)
                {
                    orphan[d] = i;

                    d++;

                    Array.Resize(ref orphan, orphan.Length + 1);
                }
            }

            counter = null;

            Array.Resize(ref orphan, orphan.Length - 1);

            //-----------------------------------------------------

            int deleted = 0;
            string[] TempKitNamesMatrix = KitNames;
            string[] TempKitNumbersMatrix = KitNumbers;
            double[,] TempDistancesMatrix = MatrixOfDistances;

            for (int i = 0; i < orphan.Length; i++)
            {
                TempKitNamesMatrix = CutArrayString(orphan[i] - deleted, TempKitNamesMatrix);
                TempKitNumbersMatrix = CutArrayString(orphan[i] - deleted, TempKitNumbersMatrix);
                TempDistancesMatrix = CutArrayDouble(orphan[i] - deleted, orphan[i] - deleted, TempDistancesMatrix);

                deleted++;
            }

            orphan = null;

            KitNames = null;
            KitNames = TempKitNamesMatrix;
            TempKitNamesMatrix = null;

            KitNumbers = null;
            KitNumbers = TempKitNumbersMatrix;
            TempKitNumbersMatrix = null;

            MatrixOfDistances = null;
            MatrixOfDistances = TempDistancesMatrix;
            TempDistancesMatrix = null;

            classificationsOur = null;
            classificationsOur = new int[KitNumbers.Length];


            toolStripStatusLabel1.Text = "Число совпаденцев: " + KitNumbers.Length;

            //-----------------------------------------------------

            if ((MatrixOfDistances != null) && (KitNumbers != null))
            {
                sda = null;

                sda = new DescriptiveAnalysis(MatrixOfDistances);
                sda.Compute();

                AnalysisPCA = new AnalysisMethod();

                if (centerToolStripMenuItem.CheckState == CheckState.Checked)
                {
                    AnalysisPCA = AnalysisMethod.Center;
                }
                else if (standartizeToolStripMenuItem.CheckState == CheckState.Checked)
                {
                    AnalysisPCA = AnalysisMethod.Standardize;
                }

                pca = new PrincipalComponentAnalysis(sda.Source, AnalysisPCA);
                pca.Compute();

                matrixOfCoordinates = pca.Transform(MatrixOfDistances, 2);
                LastPCANumberOfClusters = NumberOfClusters;

                CreateGraph(zedGraph);

                NumberOfClustersChanged = false;
            }
            else
            {
                NoData = MessageBox.Show(NoDataMessage, NoDataCaption, MessageBoxButtons.OK);
            }
        }

        private void processToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (matrixOfCoordinates != null)
            {
                if (NumberOfClustersChanged != true)
                {
                    transformOfMatrix();

                    kmeans = new KMeans(NumberOfClusters);
                    kmeans.Compute(mixture);

                    int[] classifications = kmeans.Clusters.Nearest(mixture);
                    LastClusteringNumberOfClusters = NumberOfClusters;

                    updateGraph(classifications);
                }
                else
                {
                    NumberOfClustersCh = MessageBox.Show(NumberOfClustersChMessage, NumberOfClustersChCaption, MessageBoxButtons.OK);
                }
            }
            else
            {
                MessageBoxButtons PCAButtons = MessageBoxButtons.OK;
                DialogResult PCAResult;

                PCAResult = MessageBox.Show(PCAMmessage, PCACaption, PCAButtons);
            }
        }

        void paintDots(int ColorNumber)
        {
            if (NumberOfClusters != 4)
            {
                NumberOfClustersError = MessageBox.Show(NumberOfClustersErrorMessage, NumberOfClustersErrorCaption, MessageBoxButtons.OK);
            }
            else
            {
                if (LastPCANumberOfClusters != 4)
                {
                    NumberOfPCAClustersError = MessageBox.Show(NumberOfPCAClustersErrorMessage, NumberOfPCAClustersErrorCaption, MessageBoxButtons.OK);
                }
                else
                {
                    if (LastClusteringNumberOfClusters != 4)
                    {
                        NumberOfClusteringClustersError = MessageBox.Show(NumberOfClusteringClustersErrorMessage, NumberOfClusteringClustersErrorCaption, MessageBoxButtons.OK);
                    }
                    else
                    {
                        string[] kitsForPaint;

                        using (OpenFileDialog openFileDialog = new OpenFileDialog())
                        {
                            openFileDialog.Filter = "Csv files (*.csv)|*.csv|All files (*.*)|*.*";

                            if (openFileDialog.ShowDialog() == DialogResult.OK)
                            {
                                kitsForPaint = File.ReadAllLines(openFileDialog.FileName);

                                for (int i = 0; i < kitsForPaint.Length; i++)
                                {
                                    for (int j = 1; j < KitNumbers.Length; j++)
                                    {
                                        if (KitNumbers[j] == kitsForPaint[i])
                                        {
                                            if (ColorNumber == 1)
                                            {
                                                classificationsOur[j - 1] = 1;
                                            }
                                            else if (ColorNumber == 2)
                                            {
                                                classificationsOur[j - 1] = 2;
                                            }
                                            else if (ColorNumber == 3)
                                            {
                                                classificationsOur[j - 1] = 3;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void redToolStripMenuItem_Click(object sender, EventArgs e)
        {
            paintDots(1);
        }

        private void greenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            paintDots(2);
        }

        private void blackToolStripMenuItem_Click(object sender, EventArgs e)
        {
            paintDots(3);
        }

        private void processToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            updateGraph(classificationsOur);
        }

        private void centerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (standartizeToolStripMenuItem.CheckState == CheckState.Checked)
            {
                standartizeToolStripMenuItem.CheckState = CheckState.Unchecked;
            }

            centerToolStripMenuItem.CheckState = CheckState.Checked;
        }

        private void standartizeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (centerToolStripMenuItem.CheckState == CheckState.Checked)
            {
                centerToolStripMenuItem.CheckState = CheckState.Unchecked;
            }

            standartizeToolStripMenuItem.CheckState = CheckState.Checked;
        }

        private void eNGToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        double convertTotalCMToTMRCA(double TotalCM)
        {
            double TMRCA = -0.722 * Math.Log(TotalCM) + 6.8657;

            return TMRCA;
        }

        public class MatchClass
        {
            public int matchResultId { get; set; }
            public string eKitNum { get; set; }
            public string firstName { get; set; }
            public string middleName { get; set; }
            public string lastName { get; set; }
            public double totalCM { get; set; }
            public double longestCM { get; set; }
            public List<Segment> segments { get; set; }
        }

        public class Segment
        {
            public double centimorgans { get; set; }
            public int numberOfSNPs { get; set; }
            public int startPosition { get; set; }
            public int endPosition { get; set; }
            public int chromosome { get; set; }
        }

        public class Common
        {
            public int resultId2 { get; set; }
            public double totalCM { get; set; }
        }

        public class CommonMatchClass
        {
            public int matchResultId { get; set; }
            public List<Common> commonMatches { get; set; }
        }

        public static string[] CutArrayString(int rowToRemove, string[] originalArray)
        {
            string[] result = new string[originalArray.GetLength(0) - 1];

            for (int i = 0, j = 0; i < originalArray.GetLength(0); i++)
            {
                if (i == rowToRemove)
                {
                    continue;
                }

                result[j] = originalArray[i];

                j++;
            }

            return result;
        }

        public static double[,] CutArrayDouble(int rowToRemove, int columnToRemove, double[,] originalArray)
        {
            double[,] result = new double[originalArray.GetLength(0) - 1, originalArray.GetLength(1) - 1];

            for (int i = 0, j = 0; i < originalArray.GetLength(0); i++)
            {
                if (i == rowToRemove)
                    continue;

                for (int k = 0, u = 0; k < originalArray.GetLength(1); k++)
                {
                    if (k == columnToRemove)
                        continue;

                    result[j, u] = originalArray[i, k];
                    u++;
                }
                j++;
            }

            return result;
        }

        private void numberOfClustersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int NumberOfClustersOld = NumberOfClusters;

            using (ClustersRegulator ClustersRegulatorWindow = new ClustersRegulator(NumberOfClusters))
            {
                ClustersRegulatorWindow.ShowDialog();
                NumberOfClusters = ClustersRegulatorWindow.numberOfClusters;

                if (NumberOfClusters != NumberOfClustersOld)
                {
                    NumberOfClustersChanged = true;
                }
            }
        }

        private void saveKitsOfMatchesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog SaveKitNumbersDialog = new SaveFileDialog())
            {
                SaveKitNumbersDialog.Filter = "csv files (*.csv)|*.csv|All files (*.*)|*.*";
                SaveKitNumbersDialog.FilterIndex = 1;
                SaveKitNumbersDialog.RestoreDirectory = true;

                if (SaveKitNumbersDialog.ShowDialog() == DialogResult.OK)
                {
                    if (SaveKitNumbersDialog.FileName != null)
                    {
                        using (StreamWriter str = new StreamWriter(SaveKitNumbersDialog.FileName))
                        {
                            for (int i = 0; i < KitNumbers.GetLength(0); i++)
                            {
                                str.WriteLine(KitNumbers[i]);
                            }
                            str.Close();
                        }
                    }
                }
            }
        }

        private void openGedmatchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MatrixOfDistances = null;
            KitNumbers = null;
            KitNames = null;

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Csv files (*.csv)|*.csv|All files (*.*)|*.*";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string[] allLinesDistances = File.ReadAllLines(openFileDialog.FileName);

                    MatrixOfDistances = new double[allLinesDistances.Length - 1, allLinesDistances.Length - 1];

                    replaceZeros();
                    fillDiagonalByZeros();

                    for (int i = 1; i < allLinesDistances.Length; i++)
                    {
                        string[] rowDistances = allLinesDistances[i].Split(new[] { ';' });

                        for (int j = 2; j < allLinesDistances.Length + 1; j++)
                        {
                            if (double.TryParse(rowDistances[j], out MatrixOfDistances[i - 1, j - 2]))
                            {

                            }
                        }
                    }

                    string[] allLinesKits = File.ReadAllLines(openFileDialog.FileName);

                    KitNumbers = new string[allLinesKits.Length - 1];
                    KitNames = new string[allLinesKits.Length - 1];

                    for (int i = 1; i < allLinesKits.Length; i++)
                    {
                        string[] rowKits = allLinesKits[i].Split(new[] { ';' });

                        for (int j = 0; j < 0 + 1; j++)
                        {
                            KitNumbers[i - 1] = rowKits[j];
                        }

                        for (int j = 1; j < 1 + 1; j++)
                        {
                            KitNames[i - 1] = rowKits[j];
                        }
                    }

                    FTDNA = false;
                }
            }
        }

        private void openFTDNAToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            string KitNumber;
            string PassWord;

            using (Authorization AuthorizationWindow = new Authorization())
            {
                AuthorizationWindow.ShowDialog();
                KitNumber = AuthorizationWindow.KitNumber;
                PassWord = AuthorizationWindow.PassWord;
            }

            if (KitNumber != null & PassWord != null)
            {
                WebClient client = new WebClient();
                string credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(KitNumber + ":" + PassWord));
                client.Headers[HttpRequestHeader.Authorization] = "Basic " + credentials;

                MyMatches = null;
                MyCommonMatches = null;

                bool MCReceived = true;

                try
                {
                    string url2 = "https://www.familytreedna.com/api/family-finder/matches-common";
                    var jsonCommonMatchesRaw = client.DownloadString(url2);
                    JavaScriptSerializer serializerCommonMatches = new JavaScriptSerializer();
                    serializerCommonMatches.MaxJsonLength = int.MaxValue;
                    MyCommonMatches = serializerCommonMatches.Deserialize<CommonMatchClass[]>(jsonCommonMatchesRaw);
                    jsonCommonMatchesRaw = null;
                }
                catch (WebException ex)
                {
                    MCReceived = false;
                }
                catch (ArgumentException ex)
                {
                    ServerOfflineResult = MessageBox.Show(ServerOfflineMessage, ServerOfflineCaption, MessageBoxButtons.OK);
                }

                bool MReceived = true;

                try
                {
                    string url = "https://www.familytreedna.com/api/family-finder/matches";
                    var jsonMatchesRaw = client.DownloadString(url);
                    JavaScriptSerializer serializerMatches = new JavaScriptSerializer();
                    serializerMatches.MaxJsonLength = int.MaxValue;
                    MyMatches = serializerMatches.Deserialize<MatchClass[]>(jsonMatchesRaw);
                    jsonMatchesRaw = null;
                }
                catch (WebException ex)
                {
                    MReceived = false;
                }
                catch (ArgumentException ex)
                {
                    ServerOfflineResult = MessageBox.Show(ServerOfflineMessage, ServerOfflineCaption, MessageBoxButtons.OK);
                }

                if ((MCReceived == false && MReceived == true) || (MCReceived == true && MReceived == false))
                {
                    ServerDeadResult = MessageBox.Show(ServerDeadMessage, ServerDeadCaption, MessageBoxButtons.OK);
                }
                else if (MCReceived == false && MReceived == false)
                {
                    LoginErrorResult = MessageBox.Show(LoginErrorMessage, LoginErrorCaption, MessageBoxButtons.OK);
                }

                FTDNA = true;
            }
        }

        private void SumOfSegmentsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (LongestSegmentToolStripMenuItem.CheckState == CheckState.Checked)
            {
                LongestSegmentToolStripMenuItem.CheckState = CheckState.Unchecked;
            }

            SumOfSegmentsToolStripMenuItem.CheckState = CheckState.Checked;
        }

        private void LongestSegmentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SumOfSegmentsToolStripMenuItem.CheckState == CheckState.Checked)
            {
                SumOfSegmentsToolStripMenuItem.CheckState = CheckState.Unchecked;
            }

            LongestSegmentToolStripMenuItem.CheckState = CheckState.Checked;
        }

        private void fTDNAmcmToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MyMatches = null;
            MyCommonMatches = null;

            FTDNA = true;

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    var jsonMatchesRaw = File.ReadAllText(openFileDialog.FileName);

                    JavaScriptSerializer serializerMatches = new JavaScriptSerializer();
                    serializerMatches.MaxJsonLength = int.MaxValue;
                    MyMatches = serializerMatches.Deserialize<MatchClass[]>(jsonMatchesRaw);
                }

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    var jsonCommonMatchesRaw = File.ReadAllText(openFileDialog.FileName);

                    JavaScriptSerializer serializerCommonMatches = new JavaScriptSerializer();
                    serializerCommonMatches.MaxJsonLength = int.MaxValue;
                    MyCommonMatches = serializerCommonMatches.Deserialize<CommonMatchClass[]>(jsonCommonMatchesRaw);
                }
            }
        }

        private void saveKitsOfXMatchesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string[] xKitNumbers = new string[0];

            foreach (MatchClass match in MyMatches)
            {
                int numberOfXSegments = 0;

                foreach (Segment segment in match.segments)
                {
                    if(segment.chromosome==98)
                    {
                        numberOfXSegments++;
                    }
                }

                if(numberOfXSegments>0)
                {
                    Array.Resize(ref xKitNumbers, xKitNumbers.Length + 1);
                    xKitNumbers[xKitNumbers.Length - 1] = match.eKitNum;
                }
            }

            using (SaveFileDialog SaveKitNumbersDialog = new SaveFileDialog())
            {
                SaveKitNumbersDialog.Filter = "csv files (*.csv)|*.csv|All files (*.*)|*.*";
                SaveKitNumbersDialog.FilterIndex = 1;
                SaveKitNumbersDialog.RestoreDirectory = true;

                if (SaveKitNumbersDialog.ShowDialog() == DialogResult.OK)
                {
                    if (SaveKitNumbersDialog.FileName != null)
                    {
                        using (StreamWriter str = new StreamWriter(SaveKitNumbersDialog.FileName))
                        {
                            for (int i = 0; i < xKitNumbers.GetLength(0); i++)
                            {
                                str.WriteLine(xKitNumbers[i]);
                            }
                            str.Close();
                        }
                    }
                }
            }
        }
    }
}
